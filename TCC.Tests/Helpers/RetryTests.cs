using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TCC.Lib.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace TCC.Tests.Helpers
{
    [CollectionDefinition("Non-Parallel Collection", DisableParallelization = true)]
    public class NonParallelCollectionDefinitionClass
    {
    }

    [Collection("Non-Parallel Collection")]
    public class RetryTests
    {
        private readonly ITestOutputHelper _output;
        public RetryTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(9)]
        [InlineData(15)]
        public async Task BackOffRetries(int maxSeconds)
        {
            // warmup
            var warmup = new RetryContext(2);
            await Retry.WaitForNextRetryWithDuration(warmup);
            await Retry.WaitForNextRetryWithDuration(warmup);
            await Retry.WaitForNextRetryWithDuration(warmup);

            // test
            var retryResults = new List<RetryResult>();
            _output.WriteLine("s_tickFrequency : " + Retry.s_tickFrequency);
            var sw = Stopwatch.StartNew();
            TimeSpan lastTimeSpan = TimeSpan.Zero;
            var ctx = new RetryContext(maxSeconds);
            for (int i = 0; i <= maxSeconds; i++)
            {
                var result = await Retry.WaitForNextRetryWithDuration(ctx);
                var elapsed = sw.Elapsed;
                var sinceLast = elapsed - lastTimeSpan;
                lastTimeSpan = elapsed;
                _output.WriteLine($"{i} {result.CanRetry} sw:{sinceLast.HumanizedTimeSpan(3)} retry:{result.WaitDuration.HumanizedTimeSpan(3)} ticks:{result.WaitDuration.Ticks}");
                retryResults.Add(result);
            }
            sw.Stop();

            var total = new TimeSpan(retryResults.Sum(i => i.WaitDuration.Ticks));

            Assert.True(IsNear(maxSeconds, total.TotalSeconds, 10), $"incorrect total seconds : {total.TotalSeconds}");
            Assert.True(IsNear(maxSeconds, sw.Elapsed.TotalSeconds, 10), $"incorrect global duration : {sw.Elapsed.TotalSeconds}");
            
            Assert.Contains(retryResults, i => i.CanRetry);
            Assert.Contains(retryResults, i => !i.CanRetry);
        }

        private bool IsNear(int expected, double actual, int marginPercent)
        {
            float min = (100 - marginPercent) / 100f;
            float max = (100 + marginPercent) / 100f;
            return actual <= expected * max && actual >= expected * min;
        }

    }
}
