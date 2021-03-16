using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace TCC.Lib.Helpers
{
    public static class Retry
    {
        private static readonly int[] RetryDurations = new[] { 0, 1, 3, 14, 42 };
        private static readonly int MaxRetries = 4;
        private static readonly Random RandomJitter = new Random();

        public static bool CanRetryIn(out TimeSpan nextRetry, ref int retries, int maxRetries)
        {
            int i = Interlocked.Increment(ref retries);
            if (i > maxRetries || i > MaxRetries)
            {
                nextRetry = TimeSpan.Zero;
                return false;
            }

            double ms = RetryDurations[i] * 1000 + RandomJitter.Next(0, 1000);
            nextRetry = TimeSpan.FromMilliseconds(ms);
            return true;
        }
    }
}
