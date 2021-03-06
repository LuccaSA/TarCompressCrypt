﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;

namespace TCC
{
    public static class TeminationHelper
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);

        private delegate bool ConsoleEventDelegate(CtrlType eventType);
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
        private static bool CtrlC(CtrlType eventType)
        {
            switch (eventType)
            {
                case CtrlType.CTRL_BREAK_EVENT:
                case CtrlType.CTRL_C_EVENT:
                case CtrlType.CTRL_LOGOFF_EVENT:
                case CtrlType.CTRL_SHUTDOWN_EVENT:
                case CtrlType.CTRL_CLOSE_EVENT:
                    Console.Error.WriteLine("Process termination requested");
                    _staticCancellationTokenSource?.Cancel();
                    return false;
                default:
                    return false;
            }
        }

        private enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }
    }
}