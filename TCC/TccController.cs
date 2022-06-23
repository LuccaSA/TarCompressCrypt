using Google.Apis.Auth.OAuth2;
using Google.Cloud.PubSub.V1;
using Google.Cloud.Storage.V1;
using Grpc.Auth;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TCC.Lib;
using TCC.Lib.Benchmark;
using TCC.Lib.Database;
using TCC.Lib.Dependencies;
using TCC.Lib.Helpers;
using TCC.Lib.Notification;
using TCC.Lib.Options;
using TCC.Parser;

namespace TCC;

public interface ITccController
{
    Task CompressAsync(CompressOption option);
    Task DecompressAsync(DecompressOption option);
    Task BenchmarkAsync(BenchmarkOption option);
    Task AutoDecompressAsync(AutoDecompressOptionBinding option);
}

public class TccController : ITccController
{
    private readonly ILogger<TccController> _logger;
    private readonly TarCompressCrypt _tarCompressCrypt;
    private readonly DatabaseSetup _databaseSetup;
    private readonly BenchmarkRunner _benchmarkRunner;
    private readonly SlackSender _slackSender;
    private readonly ExternalDependencies _externalDependencies;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public TccController(
        ILogger<TccController> loger, TarCompressCrypt tarCompressCrypt,
        DatabaseSetup databaseSetup, BenchmarkRunner benchmarkRunner,
        SlackSender slackSender, ExternalDependencies externalDependencies,
        CancellationTokenSource cancellationTokenSource)
    {
        _logger = loger;
        _tarCompressCrypt = tarCompressCrypt;
        _databaseSetup = databaseSetup;
        _benchmarkRunner = benchmarkRunner;
        _slackSender = slackSender;
        _externalDependencies = externalDependencies;
        _cancellationTokenSource = cancellationTokenSource;
        _logger.LogInformation($"Starting ------------------------------------------------- {DateTime.UtcNow}");
    }

    public async Task CompressAsync(CompressOption option)
    {
        await InitTccAsync();
        var operationResult = await _tarCompressCrypt.CompressAsync(option);
        await LogResultAsync(operationResult, Mode.Compress, option);
    }

    public async Task DecompressAsync(DecompressOption option)
    {
        await InitTccAsync();
        await _databaseSetup.EnsureDatabaseExistsAsync(Mode.Decompress);
        var operationResult = await _tarCompressCrypt.DecompressAsync(option);
        await _databaseSetup.CleanupDatabaseAsync(Mode.Decompress);
        await LogResultAsync(operationResult, Mode.Decompress, option);
    }

    public async Task BenchmarkAsync(BenchmarkOption option)
    {
        await InitTccAsync();
        var operationResult = await _benchmarkRunner.RunBenchmarkAsync(option);
        await LogResultAsync(operationResult, Mode.Benchmark, null);
    }

    public async Task AutoDecompressAsync(AutoDecompressOptionBinding option)
    {
        await InitTccAsync();
        var gcpCredential = await GoogleAuthHelper.GetGoogleClientAsync(option.GoogleStorageCredential, new CancellationToken());
        var subscriber = await GetGoogleClientAsync(gcpCredential, option);
        var storage = await StorageClient.CreateAsync(gcpCredential);

        await _databaseSetup.EnsureDatabaseExistsAsync(Mode.Decompress);

        // Use the client as you'd normally do, to listen for messages in this example.
        await subscriber.StartAsync(async (msg, cancellationToken) =>
        {
            if (!msg.Attributes.Any(kvp => kvp.Key == "eventType" && kvp.Value == "OBJECT_FINALIZE"))
            {
                _logger.LogDebug("EventType not found : {attributes}", msg.Attributes);
                return SubscriberClient.Reply.Ack;
            }
            var msgData = JsonSerializer.Deserialize<ObjectStorageEvent>(msg.Data.ToStringUtf8(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (string.IsNullOrEmpty(msgData.Name))
            {
                _logger.LogDebug("Object name not found");
                return SubscriberClient.Reply.Ack;
            }
            if (string.IsNullOrEmpty(msgData.Bucket))
            {
                _logger.LogDebug("Bucket name not found");
                return SubscriberClient.Reply.Ack;
            }

            var fileName = Path.GetFileName(msgData.Name);
            string tempPath;
            if (!string.IsNullOrEmpty(option.TemporaryDirectory))
            {
                tempPath = Path.Join(option.TemporaryDirectory, fileName);
            }
            else
            {
                tempPath = Path.Join(Path.GetTempPath(), Path.GetRandomFileName() + fileName);
            }
            using (var outputFile = File.OpenWrite(tempPath))
            {
                await storage.DownloadObjectAsync(msgData.Bucket, msgData.Name, outputFile);
            }
            _logger.LogDebug("{fileName} downloaded in {path}", fileName, tempPath);

            option.SourceDirOrFile = tempPath;
            var operationResult = await _tarCompressCrypt.DecompressAsync(option);
            await _databaseSetup.CleanupDatabaseAsync(Mode.Decompress);
            await LogResultAsync(operationResult, Mode.Decompress, option);
            File.Delete(tempPath);

            return SubscriberClient.Reply.Ack;
        });
    }
    private record ObjectStorageEvent(string Bucket, string Name);
    private static async Task<SubscriberClient> GetGoogleClientAsync(GoogleCredential credential, AutoDecompressOptionBinding option)
    {
        var subscriptionName = new SubscriptionName(option.GoogleProjectId, option.GoogleSubscriptionId);
        // Create a google cloud pub/sub client that reads messages one by one
        return await SubscriberClient.CreateAsync(
            subscriptionName,
            new SubscriberClient.ClientCreationSettings(clientCount: 1, credentials: credential.ToChannelCredentials()),
            new SubscriberClient.Settings { FlowControlSettings = new Google.Api.Gax.FlowControlSettings(1, null) }
        );
    }

    private async Task LogResultAsync(OperationSummary operationResult, Mode mode, ISlackOption slackOption)
    {
        var report = ReportOperationStats(operationResult, mode).ToList();
        foreach (var line in report.Where(i => !string.IsNullOrEmpty(i)))
        {
            _logger.LogInformation(line);
        }

        if (slackOption is not null)
        {
            await _slackSender.ReportAsync(operationResult, slackOption, mode);
        }

        WriteAuditFile(mode, operationResult);

    }
    private static IEnumerable<string> ReportOperationStats(OperationSummary op, Mode mode)
    {
        if (op == null)
        {
            yield break;
        }

        if (op.OperationBlocks.Any())
        {
            string report = null;
            if (op.Stopwatch != null)
            {
                report = $"Finished in {op.Stopwatch.Elapsed.HumanizedTimeSpan()}";
            }

            if (op.Statistics != null)
            {
                report += $" Average throughput : {op.Statistics.AverageThroughput.HumanizedBandwidth()}";
            }

            if (!string.IsNullOrEmpty(report))
            {
                yield return string.Empty;
                yield return report;
            }
        }
        else
        {
            yield return $"WARNING : No candidate for {mode}";
            if (op.Stopwatch != null)
            {
                yield return $"Finished in {op.Stopwatch.Elapsed.HumanizedTimeSpan()}";
            }
        }
    }
    private void WriteAuditFile(Mode mode, OperationSummary op)
    {
        using (_logger.BeginScope(LoggerConfigurer.AuditScope))
        {
            foreach (IEnumerable<StepResult> stepResults in op.OperationBlocks.Select(o => o.StepResults).Where(result => result.Any()))
            {
                var name = stepResults.FirstOrDefault(sr => !string.IsNullOrEmpty(sr.Name))?.Name;
                var logLevel = LogLevel.Information;
                if (stepResults.Any(stepResult => stepResult.HasError))
                {
                    logLevel = LogLevel.Error;
                }
                else if (stepResults.Any(stepResult => stepResult.HasWarning))
                {
                    logLevel = LogLevel.Warning;
                }
                _logger.Log(
                    logLevel,
                    "{Mode} / {Name} / {duration} / {@results}",
                    mode,
                    name,
                    stepResults.Select(s => s.ElapsedMilliseconds).Sum() * 1000000,
                    stepResults.Select(sr => new
                    {
                        sr.ArchiveFileSize,
                        sr.Type,
                        sr.ElapsedMilliseconds,
                        sr.HasError,
                        sr.HasWarning,
                        sr.Infos,
                        sr.IsSuccess
                    })
                );
            }
        }
    }

    private Task InitTccAsync()
    {
        _cancellationTokenSource.HookTermination();
        return _externalDependencies.EnsureAllDependenciesPresent();
    }
}