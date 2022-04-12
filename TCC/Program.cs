using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;
using TCC.Lib;
using TCC.Lib.Benchmark;
using TCC.Lib.Blocks;
using TCC.Lib.Database;
using TCC.Lib.Dependencies;
using TCC.Lib.Helpers;
using TCC.Lib.Notification;
using TCC.Lib.Options;
using TCC.Parser;
using TCC.SerilogAsync;

namespace TCC
{
    public static class Program
    {
        private const string _tccMutex = "Global\\FD70BFC5-79C8-44DF-9629-65512A1CD0FC";
        private const string AuditScope = "audit";

        public static async Task<int> Main(string[] args)
        {
            using Mutex mutex = new Mutex(false, _tccMutex);

            TccCommand parsed = args.ParseCommandLine();
            if (parsed.ReturnCode == 1)
            {
                return 1;
            }

            var workingPath = WorkingPath(parsed);
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddTcc(workingPath);
            serviceCollection.AddLogging(lb =>
            {
                var logger = CreateLogger(parsed.Mode, parsed.Option?.Verbose ?? false, parsed.Option?.LogPaths ?? workingPath, parsed.Option?.AuditFilePath);
                lb.AddSerilog(logger, true);
            });

            OperationSummary op = null;
            List<string> report = null;
            try
            {
                IServiceProvider provider = serviceCollection.BuildServiceProvider();
                using (var scope = provider.CreateScope())
                {
                    var sp = scope.ServiceProvider;
                    var logger = sp.GetRequiredService<ILogger<TarCompressCrypt>>();

                    if (!mutex.WaitOne(0, false))
                    {
                        Console.Error.WriteLine("Tcc is already running");
                        logger.LogError("Tcc is already running");
                        return 0;
                    }

                    sp.GetRequiredService<CancellationTokenSource>().HookTermination();
                    await sp.GetRequiredService<ExternalDependencies>().EnsureAllDependenciesPresent();

                    logger.LogInformation($"Starting ------------------------------------------------- {DateTime.UtcNow}");

                    op = await RunTcc(sp, parsed);

                    report = ReportOperationStats(op, parsed.Mode).ToList();
                    foreach (var line in report.Where(i => !string.IsNullOrEmpty(i)))
                    {
                        logger.LogInformation(line);
                    }

                    var notifier = sp.GetRequiredService<SlackSender>();
                    await notifier.ReportAsync(op, parsed.Option, parsed.Mode);

                    WriteAuditFile(parsed.Mode, op, logger);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Operation canceled, shutting down");
            }
            catch (Exception e)
            {
                Console.WriteLine("Critical : " + e.Message);
                Console.WriteLine(e.StackTrace);
            }

            if (report != null)
            {
                foreach (var line in report)
                {
                    Console.WriteLine(line);
                }
            }

            Serilog.Log.CloseAndFlush();
            if (op == null || !op.IsSuccess)
            {
                return 1;
            }
            return 0;
        }

        private static void WriteAuditFile(Mode mode, OperationSummary op, ILogger<TarCompressCrypt> logger)
        {
            using (logger.BeginScope(AuditScope))
            {
                foreach (IEnumerable<StepResult> r in op.OperationBlocks.Select(o => o.StepResults).Where(result => result.Any()))
                {
                    var name = r.First().Name;
                    logger.Log(LogLevel.Information, "{Mode} / {Name} / {@results}", mode, name, r);
                }
            }
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

        private static string WorkingPath(TccCommand parsed)
        {
            switch (parsed.Mode)
            {
                case Mode.Compress:
                    if (Directory.Exists(parsed.Option.SourceDirOrFile))
                    {
                        var co = parsed.Option as CompressOption;
                        if (co.BlockMode == BlockMode.Individual)
                        {
                            return parsed.Option.SourceDirOrFile;
                        }
                        else
                        {
                            var fod = new FileOrDirectoryInfo(parsed.Option.SourceDirOrFile);
                            var rootDir = fod.Kind == SourceKind.Directory
                                ? fod.DirectoryInfo?.Parent
                                : fod.FileInfo?.Directory?.Parent;
                            if (rootDir == null)
                            {
                                throw new ArgumentOutOfRangeException(nameof(parsed), "Invalid path provided");
                            }
                            return rootDir.FullName;
                        }
                    }
                    else if (File.Exists(parsed.Option.SourceDirOrFile))
                    {
                        return new FileInfo(parsed.Option.SourceDirOrFile).Directory?.FullName;
                    }
                    break;
                case Mode.Decompress:
                    if (Directory.Exists(parsed.Option.DestinationDir))
                    {
                        return parsed.Option.DestinationDir;
                    }
                    break;
            }
            return null;
        }

        private static async Task<OperationSummary> RunTcc(IServiceProvider provider, TccCommand command)
        {
            OperationSummary op;
            switch (command.Mode)
            {
                case Mode.Compress:
                    op = await provider
                        .GetRequiredService<TarCompressCrypt>()
                        .Compress(command.Option as CompressOption);
                    break;
                case Mode.Decompress:
                    var db = provider.GetRequiredService<DatabaseSetup>();
                    await db.EnsureDatabaseExistsAsync(command.Mode);

                    op = await provider
                        .GetRequiredService<TarCompressCrypt>()
                        .Decompress(command.Option as DecompressOption);

                    await db.CleanupDatabaseAsync(command.Mode);
                    break;
                case Mode.Benchmark:
                    op = await provider
                        .GetRequiredService<BenchmarkRunner>()
                        .RunBenchmark(command.BenchmarkOption);
                    break;
                default:
                    throw new NotImplementedException();
            }
            return op;
        }

        private static Logger CreateLogger(Mode mode, bool verbose, string logDirectoryPath, string auditFile)
        {
            string logFileName = mode switch
            {
                Mode.Compress => "tcc.log",
                Mode.Decompress => "tcc.log",
                _ => null
            };

            string path = string.Empty;
            if (logDirectoryPath != null && Directory.Exists(logDirectoryPath))
            {
                path = logDirectoryPath;
            }
            else
            {
                try
                {
                    Directory.CreateDirectory(logDirectoryPath);
                }
                catch (Exception)
                {
                    path = "logs";
                }
            }

            var level = verbose ? LogEventLevel.Debug : LogEventLevel.Information;
            var loggerConf = new LoggerConfiguration()
                .WriteTo.Logger(logger => logger
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                    .WriteTo.Async(conf =>
                    {
                        if (logFileName != null)
                        {
                            conf.File(Path.Combine(path, logFileName), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 31);
                        }
                        conf.Console();
                    })
                   .Filter.ByExcluding(IsAuditLog)
                   .MinimumLevel.Is(level)
                );
            if (!string.IsNullOrEmpty(auditFile))
            {
                if (!Path.IsPathRooted(auditFile))
                {
                    auditFile = Path.Combine(path, auditFile);
                }
                loggerConf
                    .WriteTo.Logger(auditLogger =>
                    {
                        auditLogger
                            .MinimumLevel.Information()
                            .WriteTo.Async(conf =>
                            {
                                conf.File(new JsonFormatter(renderMessage: true), auditFile, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 31);
                            })
                            .Filter.ByIncludingOnly(IsAuditLog);
                    });
            }

            return loggerConf.CreateLogger();
        }

        private static bool IsAuditLog(LogEvent logEvent)
        {
            LogEventPropertyValue scope = logEvent.Properties.GetValueOrDefault("Scope");
            if (scope is null)
            {
                return false;
            }
            return
                scope is SequenceValue sequenceValue &&
                sequenceValue
                    .Elements
                    .OfType<ScalarValue>()
                    .Any(scalarValue => string.Equals(scalarValue.Value as string, AuditScope));
        }
    }
}
