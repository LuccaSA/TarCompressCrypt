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
    static class Program
    {
        static async Task Main(string[] args)
        {
            TccCommand parsed = args.ParseCommandLine();
            if (parsed.ReturnCode == 1)
            {
                Environment.Exit(1);
            }

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddTcc();
            serviceCollection.AddSerilog(parsed.Mode != Mode.Benchmark && parsed.Option.Verbose);

            if (parsed.Mode != Mode.Benchmark)
            {
                serviceCollection.AddSingleton<IBlockListener, CommandLineBlockListener>();
            }

            IServiceProvider provider = serviceCollection.BuildServiceProvider();

            provider.GetRequiredService<CancellationTokenSource>().HookTermination();
            await provider.GetRequiredService<ExternalDependencies>().EnsureAllDependenciesPresent();

            OperationSummary op = await RunTcc(provider, parsed);

            Environment.Exit(op != null && op.IsSuccess ? 0 : 1);
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
                        .GetRequiredService<BenchmarkHelper>()
                        .RunBenchmark(command.BenchmarkOption);
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
