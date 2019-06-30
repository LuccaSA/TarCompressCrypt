using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TCC.Lib;
using TCC.Lib.Benchmark;
using TCC.Lib.Blocks;
using TCC.Lib.Dependencies;
using TCC.Lib.Helpers;
using TCC.Lib.Options;
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
                    await scope.ServiceProvider.GetRequiredService<ExternalDependencies>()
                        .EnsureAllDependenciesPresent();

                    op = await RunTcc(scope.ServiceProvider, parsed);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Operation canceled, shutting down");
            }

            if (op != null)
            {
                Console.WriteLine($"Finished in {op.Stopwatch.Elapsed.HumanizedTimeSpan()}, average throughput : {op.Statistics.AverageThroughput.HumanizedBandwidth()}");
            }

            if (op == null || !op.IsSuccess)
            {
                Environment.Exit(1);
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
                    if (Directory.Exists(parsed.Option.SourceDirOrFile))
                    {
                        return parsed.Option.SourceDirOrFile;
                    }
                    break;
            }
            return null;
        }

        private static Task<OperationSummary> RunTcc(IServiceProvider provider, TccCommand command)
        {
            switch (command.Mode)
            {
                case Mode.Compress:
                    return provider
                        .GetRequiredService<TarCompressCrypt>()
                        .Compress(command.Option as CompressOption);

                case Mode.Decompress:
                    return provider
                        .GetRequiredService<TarCompressCrypt>()
                        .Decompress(command.Option as DecompressOption);

                case Mode.Benchmark:
                    return provider
                        .GetRequiredService<BenchmarkRunner>()
                        .RunBenchmark(command.BenchmarkOption);
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
