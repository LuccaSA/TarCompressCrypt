using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TCC.Lib;
using TCC.Lib.Benchmark;
using TCC.Lib.Blocks;
using TCC.Lib.Database;
using TCC.Lib.Dependencies;
using TCC.Lib.Helpers;
using TCC.Lib.Options;
using TCC.Notification;
using TCC.Parser;

namespace TCC
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            TccCommand parsed = args.ParseCommandLine();
            if (parsed.ReturnCode == 1)
            {
                Environment.Exit(1);
            }

            var workingPath = WorkingPath(parsed);
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddTcc(workingPath);
            serviceCollection.AddSerilog(parsed.Mode != Mode.Benchmark && parsed.Option.Verbose, workingPath);

            if (parsed.Mode != Mode.Benchmark)
            {
                serviceCollection.AddSingleton<IBlockListener, CommandLineBlockListener>();
            }

            OperationSummary op = null;
            List<string> report = null;
            try
            {
                IServiceProvider provider = serviceCollection.BuildServiceProvider();
                using (var scope = provider.CreateScope())
                {
                    var logger = scope.ServiceProvider.GetRequiredService<ILogger<TarCompressCrypt>>();
                    scope.ServiceProvider.GetRequiredService<CancellationTokenSource>().HookTermination();
                    await scope.ServiceProvider.GetRequiredService<ExternalDependencies>().EnsureAllDependenciesPresent();
                    logger.LogInformation($"Starting ------------------------------------------------- {DateTime.UtcNow}");

                    op = await RunTcc(scope.ServiceProvider, parsed);

                    report = ReportOperationStats(op).ToList();
                    foreach (var line in report.Where(i => !string.IsNullOrEmpty(i)))
                    {
                        logger.LogInformation(line);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Operation canceled, shutting down");
            }

            if (report != null)
            {
                foreach (var line in report)
                {
                    Console.WriteLine(line);
                }
            }
            
            await SlackSender.ReportAsync(op, parsed.Option, parsed.Mode);

            Serilog.Log.CloseAndFlush();
            if (op == null || !op.IsSuccess)
            {
                Environment.Exit(1);
            }
        }

        private static IEnumerable<string> ReportOperationStats(OperationSummary op)
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
                yield return "WARNING : No archive candidate for extraction";
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
                        return parsed.Option.SourceDirOrFile;
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
            var db = provider.GetRequiredService<DatabaseSetup>();
            await db.EnsureDatabaseExistsAsync(command.Mode);
            OperationSummary op;
            switch (command.Mode)
            {
                case Mode.Compress:
                    op = await provider
                        .GetRequiredService<TarCompressCrypt>()
                        .Compress(command.Option as CompressOption);
                    break;
                case Mode.Decompress:
                    op = await provider
                        .GetRequiredService<TarCompressCrypt>()
                        .Decompress(command.Option as DecompressOption);
                    break;
                case Mode.Benchmark:
                    op = await provider
                        .GetRequiredService<BenchmarkRunner>()
                        .RunBenchmark(command.BenchmarkOption);
                    break;
                default:
                    throw new NotImplementedException();
            }
            await db.CleanupDatabaseAsync(command.Mode);
            return op;
        }
    }
}
