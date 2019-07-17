using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
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
            try
            {
                IServiceProvider provider = serviceCollection.BuildServiceProvider();
                using (var scope = provider.CreateScope())
                {
                    scope.ServiceProvider.GetRequiredService<CancellationTokenSource>().HookTermination();
                    await scope.ServiceProvider.GetRequiredService<ExternalDependencies>().EnsureAllDependenciesPresent();

                    op = await RunTcc(scope.ServiceProvider, parsed);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Operation canceled, shutting down");
            }

            ReportOperationConsole(op);
            await ReportOperationSlackAsync(op, parsed.Option, parsed.Mode);

            Serilog.Log.CloseAndFlush();
            if (op == null || !op.IsSuccess)
            {
                Environment.Exit(1);
            }
        }


        public class SlackReport
        {
            public string BlockName { get; set; }
            public string Message { get; set; }
            public AlertLevel Alert { get; set; }
        }

        private static async Task ReportOperationSlackAsync(OperationSummary op, TccOption parsedOption, Mode mode)
        {
            if (string.IsNullOrWhiteSpace(parsedOption.SlackSecret) ||
                string.IsNullOrWhiteSpace(parsedOption.SlackChannel))
            {
                return;
            }

            var msg = new SlackMessage
            {
                Channel = parsedOption.SlackChannel,
                Text = $"*{mode} report* [{Environment.MachineName}]",
                Attachments = new List<Attachment>()
            };

            var reports = new List<SlackReport>();
            foreach (var o in op.OperationBlocks)
            {
                ExtractSlackReports(o, reports);
            }

            foreach (var ga in reports.GroupBy(i => i.Alert))
            {
                foreach (var grp in ga.GroupBy(g => g.Message))
                {
                    msg.Attachments.Add(new Attachment
                    {
                        Color = ga.Key.ToSlackColor(),
                        Title = grp.Key,
                        Fields = grp.Select(d => new Field
                        {
                            Value = d.BlockName,
                            Short = true
                        }).ToArray()
                    });
                }
            }

            if (op.OperationBlocks.Any())
            {
                msg.Attachments.Add(new Attachment
                {
                    Color = AlertLevel.Info.ToSlackColor(),
                    Title = $"Statistics",
                    Fields = new[]
                    {
                        new Field
                        {
                            Value = $"Input : {parsedOption.SourceDirOrFile}", Short = true
                        },
                        new Field
                        {
                            Value = $"{op.OperationBlocks.Count()} blocks processed in {op.Stopwatch.Elapsed.HumanizedTimeSpan()}", Short = true
                        },
                        new Field
                        {
                            Value = $"Average throughput : {op.Statistics.AverageThroughput.HumanizedBandwidth()}", Short = true
                        },
                        new Field
                        {
                            Value = $"Total job size : {op.OperationBlocks.SelectMany(i => i.BlockResults).Sum(i => i.Block.CompressedSize).HumanizeSize()}", Short = true
                        }
                    }
                });
            }
            else
            {
                msg.Attachments.Add(new Attachment
                {
                    Color = AlertLevel.Error.ToSlackColor(),
                    Title = "Nothing processed !!!"
                });
            }

            await SlackNotifier.SendSlackMessageAsync(msg, parsedOption.SlackSecret);
        }

        private static void ExtractSlackReports(OperationBlock block, List<SlackReport> reports)
        {
            foreach (var v in block.BlockResults.Where(i => i.CommandResult.HasError))
            {
                reports.Add(new SlackReport
                {
                    BlockName = v.Block.BlockName,
                    Message = v.CommandResult.Errors,
                    Alert = AlertLevel.Error
                });
            }

            foreach (var v in block.BlockResults.Where(i => i.CommandResult.Infos.Any()))
            {
                foreach (var inf in v.CommandResult.Infos)
                {
                    if (String.IsNullOrWhiteSpace(inf))
                    {
                        continue;
                    }

                    if (inf.EndsWith("file changed as we read it"))
                    {
                        reports.Add(new SlackReport
                        {
                            BlockName = v.Block.BlockName,
                            Message = "Files created or modified while compressing",
                            Alert = AlertLevel.Warning
                        });
                    }
                    else
                    {
                        reports.Add(new SlackReport
                        {
                            BlockName = v.Block.BlockName,
                            Message = inf,
                            Alert = AlertLevel.Info
                        });
                    }
                }
            }
        }

        private static void ReportOperationConsole(OperationSummary op)
        {
            if (op == null)
            {
                return;
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
                    Console.WriteLine();
                    Console.WriteLine(report);
                }
            }
            else
            {
                Console.WriteLine("WARNING : No archive candidate for extraction");
                if (op.Stopwatch != null)
                {
                    Console.WriteLine($"Finished in {op.Stopwatch.Elapsed.HumanizedTimeSpan()}");
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
            await provider.GetRequiredService<DatabaseSetup>().EnsureDatabaseExistsAsync(command.Mode);
            switch (command.Mode)
            {
                case Mode.Compress:
                    return await provider
                        .GetRequiredService<TarCompressCrypt>()
                        .Compress(command.Option as CompressOption);

                case Mode.Decompress:
                    return await provider
                        .GetRequiredService<TarCompressCrypt>()
                        .Decompress(command.Option as DecompressOption);

                case Mode.Benchmark:
                    return await provider
                        .GetRequiredService<BenchmarkRunner>()
                        .RunBenchmark(command.BenchmarkOption);
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
