using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TCC.Lib.Helpers
{
    public static class Retry
    {
        private const long TicksPerMillisecond = 10000;
        private const long TicksPerSecond = TicksPerMillisecond * 1000;
        internal static readonly double s_tickFrequency = (double)TicksPerSecond / Stopwatch.Frequency;

        private static readonly Random RandomJitter = new Random();

        public static async Task<bool> WaitForNextRetry(this RetryContext retryContext)
        {
            var result = await WaitForNextRetryWithDuration(retryContext);
            return result.CanRetry;
        }

        internal static async Task<RetryResult> WaitForNextRetryWithDuration(RetryContext retryContext)
        {
            retryContext.Increment();
            var elapsedSinceStart = (long)((Stopwatch.GetTimestamp() - retryContext.TimeStamp) * s_tickFrequency);
            var remaining = retryContext.MaxDurationSecondsTicks - elapsedSinceStart;
            if (remaining <= TimeSpan.TicksPerMillisecond) // Task.Delay() with a TimeSpan < 1ms returns a CompletedTask
            {
                return new RetryResult(false, TimeSpan.Zero);
            }
            if (elapsedSinceStart >= retryContext.MaxDurationSecondsTicks)
            {
                return new RetryResult(false, TimeSpan.Zero);
            }
            var candidate = retryContext.Retries * TimeSpan.TicksPerSecond + RandomJitter.Next(0, 1000000); // adds 0-100ms;
            var min = Math.Min(remaining, candidate);
            var nextRetry = new TimeSpan(min);
            await Task.Delay(nextRetry);
            return new RetryResult(min > 0, nextRetry);
        }
    }

    public class RetryResult
    {
        public RetryResult(bool canRetry, TimeSpan waitDuration)
        {
            CanRetry = canRetry;
            WaitDuration = waitDuration;
        }

        public bool CanRetry { get; set; }
        public TimeSpan WaitDuration { get; set; }
    }

    public class RetryContext
    {
        private readonly long _timeStamp;
        private int _retries = 0;

        public long TimeStamp => _timeStamp;
        public int Retries => _retries;
        public long MaxDurationSecondsTicks { get; }

        public RetryContext(int maxDurationSeconds)
        {
            if (maxDurationSeconds == 0)
                throw new ArgumentException("Duration can't be 0", nameof(maxDurationSeconds));
            MaxDurationSecondsTicks = maxDurationSeconds * TimeSpan.TicksPerSecond;
            _timeStamp = Stopwatch.GetTimestamp();
        }

        internal void Increment()
        {
            Interlocked.Increment(ref _retries);
        }
    }
}
