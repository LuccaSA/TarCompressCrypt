using System;
using System.Reactive.Subjects;
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
            var subject = new Subject<CommandResult>();
            subject.Subscribe(r =>
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
            });

            var cts = new CancellationTokenSource();
            bool ConsoleEventCallback(int eventType)
            {
                Console.Error.WriteLine("!!! Process termination requested");
                cts.Cancel();
                return false;
            }
            SetConsoleCtrlHandler(ConsoleEventCallback, true);

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
                return;
            }

            var resetEvent = new ManualResetEvent(false);

            int returnCode = 1;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                if (mode == Mode.Compress)
                {
                    returnCode = TarCompressCrypt.Compress(option as CompressOption, subject, cts.Token);
                }
                else if (mode == Mode.Decompress)
                {
                    returnCode = TarCompressCrypt.Decompress(option as DecompressOption, subject, cts.Token);
                }
                resetEvent.Set();
            });

            resetEvent.WaitOne();

            Environment.Exit(returnCode);
        }

    }

}
