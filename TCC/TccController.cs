using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TCC.Lib;
using TCC.Lib.Benchmark;
using TCC.Lib.Database;
using TCC.Lib.Helpers;
using TCC.Lib.Notification;
using TCC.Lib.Options;

namespace TCC;

public interface ITccController
{
    Task CompressAsync(CompressOption option);
    Task DecompressAsync(DecompressOption option);
    Task BenchmarkAsync(BenchmarkOption option);
}

public class TccController : ITccController
{
    private readonly ILogger<TccController> _logger;
    private readonly TarCompressCrypt _tarCompressCrypt;
    private readonly DatabaseSetup _databaseSetup;
    private readonly BenchmarkRunner _benchmarkRunner;
    private readonly SlackSender _slackSender;

    public TccController(
        ILogger<TccController> loger, TarCompressCrypt tarCompressCrypt,
        DatabaseSetup databaseSetup, BenchmarkRunner benchmarkRunner,
        SlackSender slackSender)
    {
        _logger = loger;
        _tarCompressCrypt = tarCompressCrypt;
        _databaseSetup = databaseSetup;
        _benchmarkRunner = benchmarkRunner;
        _slackSender = slackSender;

        _logger.LogInformation($"Starting ------------------------------------------------- {DateTime.UtcNow}");
    }

    public async Task CompressAsync(CompressOption option)
    {
        var operationResult = await _tarCompressCrypt.CompressAsync(option);
        await LogResultAsync(operationResult, Mode.Compress, option);
    }

    public async Task DecompressAsync(DecompressOption option)
    {
        await _databaseSetup.EnsureDatabaseExistsAsync(Mode.Decompress);
        var operationResult = await _tarCompressCrypt.DecompressAsync(option);
        await _databaseSetup.CleanupDatabaseAsync(Mode.Decompress);
        await LogResultAsync(operationResult, Mode.Decompress, option);
    }

    public async Task BenchmarkAsync(BenchmarkOption option)
    {
        var operationResult = await _benchmarkRunner.RunBenchmarkAsync(option);
        await LogResultAsync(operationResult, Mode.Benchmark, null);
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
                var name = stepResults.FirstOrDefault(sr => string.IsNullOrEmpty(sr.Name))?.Name;
                var logLevel = LogLevel.Information;
                if (stepResults.Any(stepResult => stepResult.HasError))
                {
                    logLevel = LogLevel.Error;
                }
                else if (stepResults.Any(stepResult => stepResult.HasWarning))
                {
                    logLevel = LogLevel.Warning;
                }
                _logger.Log(logLevel, "{Mode} / {Name} / {@results}", mode, name, stepResults.Select(sr => new
                {
                    sr.ArchiveFileSize,
                    sr.Type,
                    sr.ElapsedMilliseconds,
                    sr.HasError,
                    sr.HasWarning,
                    sr.Infos,
                    sr.IsSuccess
                }));
            }
        }
    }

}
