using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using TCC.Lib;
using TCC.Lib.Benchmark;
using TCC.Lib.Blocks;
using TCC.Lib.Command;
using TCC.Lib.Dependencies;
using TCC.Lib.Options;
using TCC.Parser;

namespace TCC
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            var cts = new CancellationTokenSource();

            cts.HookTermination();

            TccCommand parsed = args.ParseCommandLine();

            if (parsed.ReturnCode == 1)
            {
                Environment.Exit(1);
            }

            var blockingCollection = new BlockingCollection<(CommandResult Cmd, Block Block, int Total)>();
            int counter = 0;
            ThreadPool.QueueUserWorkItem(_ => OnProgress(ref counter, blockingCollection));

            var e = new ExternalDependecies();
            await e.EnsureAllDependenciesPresent();

            OperationSummary op = await RunTcc(cts, parsed, blockingCollection);

            Environment.Exit(op.IsSuccess ? 0 : 1);
        }

        [ExcludeFromCodeCoverage]
        private static void OnProgress(ref int counter, BlockingCollection<(CommandResult Cmd, Block Block, int Total)> blockingCollection)
        {
            foreach (var r in blockingCollection.GetConsumingEnumerable())
            {
                Interlocked.Increment(ref counter);
                Console.WriteLine(counter + "/" + r.Total + " : " + r.Block.ArchiveName);
                if (r.Cmd.HasError)
                {
                    Console.Error.WriteLine("Error : " + r.Cmd.Errors);
                }

                if (!String.IsNullOrEmpty(r.Cmd.Output))
                {
                    Console.Out.WriteLine("Info : " + r.Cmd.Errors);
                }
            }
        }

        private static Task<OperationSummary> RunTcc(CancellationTokenSource cts, TccCommand command, BlockingCollection<(CommandResult Cmd, Block Block, int Total)> blockingCollection)
        {
            switch (command.Mode)
            {
                case Mode.Compress:
                    return TarCompressCrypt.Compress(command.Option as CompressOption, blockingCollection, cts.Token);
                case Mode.Decompress:
                    return TarCompressCrypt.Decompress(command.Option as DecompressOption, blockingCollection, cts.Token);
                case Mode.Benchmark:
                    return BenchmarkHelper.RunBenchmark(command.BenchmarkOption, cts.Token);
                default:
                    throw new NotImplementedException();
            }
        }

    }
}
