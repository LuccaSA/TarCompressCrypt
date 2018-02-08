using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;

namespace TCC
{
    public static class TeminationHelper
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);

        private delegate bool ConsoleEventDelegate(int eventType);

        [ExcludeFromCodeCoverage]
        public static void HookTermination(this CancellationTokenSource cts)
        {
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
        }
    }
}