using System;
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

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddTcc(WorkingPath(parsed));
            serviceCollection.AddSerilog(parsed.Mode != Mode.Benchmark && parsed.Option.Verbose);

            if (parsed.Mode != Mode.Benchmark)
            {
                serviceCollection.AddSingleton<IBlockListener, CommandLineBlockListener>();
            }

            OperationSummary op;
            IServiceProvider provider = serviceCollection.BuildServiceProvider();
            using (var scope = provider.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<CancellationTokenSource>().HookTermination();
                await scope.ServiceProvider.GetRequiredService<ExternalDependencies>().EnsureAllDependenciesPresent();

                op = await RunTcc(scope.ServiceProvider, parsed);
            }

            if(op == null || !op.IsSuccess)
            {
                Environment.Exit(1);
            }
        }

        private static string WorkingPath(TccCommand parsed)
        {
            string workingPath = null;
            switch (parsed.Mode)
            {
                case Mode.Compress:
                    workingPath = parsed.Option.SourceDirOrFile;
                    break;
                case Mode.Decompress:
                    workingPath = parsed.Option.DestinationDir;
                    break;
            }
            return workingPath;
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
