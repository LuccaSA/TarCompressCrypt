using System;
using System.Collections.Concurrent;
using System.Threading;
using TCC.Lib;

namespace TCC
{
    class Program
    {

        static void Main(string[] args)
        {
            var cts = new CancellationTokenSource();

            int counter = 0;

            cts.HookTermination();

            var option = args.ProcessCommandLine(out Mode mode);

            if (option == null)
            {
                switch (mode)
                {
                    case Mode.Unknown:
                        CommandLineHelper.PrintHelp();
                        break;
                    case Mode.Compress:
                        CommandLineHelper.PrintCompressHelp();
                        break;
                    case Mode.Decompress:
                        CommandLineHelper.PrintDecompressHelp();
                        break;
                }
                Environment.Exit(1);
            }

            var blockingCollection = new BlockingCollection<CommandResult>();
            ThreadPool.QueueUserWorkItem(_ =>
            {
                foreach (var r in blockingCollection.GetConsumingEnumerable())
                {
                    Interlocked.Increment(ref counter);
                    Console.WriteLine(counter + "/" + r.BatchTotal + " : " + r.Block.ArchiveName);
                    if (r.HasError)
                    {
                        Console.Error.WriteLine("Error : " + r.Errors);
                    }

                    if (!String.IsNullOrEmpty(r.Output))
                    {
                        Console.Out.WriteLine("Info : " + r.Errors);
                    }
                }
            });

            var resetEvent = new ManualResetEvent(false);

            int returnCode = 1;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                if (mode == Mode.Compress)
                {
                    returnCode = TarCompressCrypt.Compress(option as CompressOption, blockingCollection, cts.Token);
                }
                else if (mode == Mode.Decompress)
                {
                    returnCode = TarCompressCrypt.Decompress(option as DecompressOption, blockingCollection, cts.Token);
                }
                resetEvent.Set();
            });

            resetEvent.WaitOne();
            Environment.Exit(returnCode);
        }

    }
}
