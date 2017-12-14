using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;

namespace TCC
{
    class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);
        private delegate bool ConsoleEventDelegate(int eventType);

        static void Main(string[] args)
        {
            int counter = 0;

            var cts = new CancellationTokenSource();
            bool ConsoleEventCallback(int eventType)
            {
                Console.Error.WriteLine("Process termination requested");
                cts.Cancel();
                return false;
            }
            SetConsoleCtrlHandler(ConsoleEventCallback, true);

            Console.CancelKeyPress += (o, e) =>
            {
                Console.Error.WriteLine("Closing process");
                cts.Cancel();
            };

            var option = args.ParseCommandLine(out Mode mode);

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
