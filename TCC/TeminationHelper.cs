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
        private static CancellationTokenSource _staticCancellationTokenSource;

        public static void HookTermination(this CancellationTokenSource cts)
        {
            _staticCancellationTokenSource = cts;
            SetConsoleCtrlHandler(CtrlC, true); 
            Console.CancelKeyPress += Console_CancelKeyPress;
        }

        [ExcludeFromCodeCoverage]
        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Console.Error.WriteLine("Closing process");
            _staticCancellationTokenSource?.Cancel();
        }

        [ExcludeFromCodeCoverage]
        private static bool CtrlC(int eventType)
        {
            Console.Error.WriteLine("Process termination requested");
            _staticCancellationTokenSource?.Cancel();
            return false;
        }
    }
}