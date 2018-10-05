using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TCC.Lib.Helpers;
using Xunit;

namespace TCC.Tests.Tasks
{
    public class TaskHelperTest
    {
        [Theory]
        [InlineData(3)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(16)]
        [InlineData(32)]
        public async Task ParallelTestAsync(int degree)
        {
            var tasks = Enumerable.Range(0, 2000).Select(i => 10).ToList();
            tasks.Add(200);
            tasks.Add(100);
            tasks.Add(300);
            var cts = new CancellationTokenSource();
            int count = 0;
            int max = 0;

            await tasks.ParallelizeAsync(async (i, ct) =>
            {
                int loopMax = Interlocked.Increment(ref count);
                max = Math.Max(max, loopMax);
                await Task.Delay(i, ct);
                Interlocked.Decrement(ref count);
                Assert.True(count <= degree);
            }, degree, Fail.Smart, cts.Token);

            Assert.Equal(degree, max);
        }


        [Theory]
        [InlineData(Fail.Default)]
        [InlineData(Fail.Fast)]
        [InlineData(Fail.Smart)]
        public async Task CancellationAsync(Fail failMode)
        {
            var tasks = Enumerable.Range(0, 10);
            var cts = new CancellationTokenSource();

            int index = 0;

            var pTask = tasks.ParallelizeAsync(async (i, ct) =>
            {
                int ix = Interlocked.Increment(ref index);
                if (ix == 4)
                {
                    cts.Cancel();
                }
                else
                {
                    await Task.Delay(1000, ct);
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }
                    Assert.False(true);
                }
            }, 8, failMode, cts.Token);

            if (failMode == Fail.Default)
            {
                await Assert.ThrowsAsync<TaskCanceledException>(async () =>
                {
                    await pTask;
                });
            }
            else
            {
                var result = await pTask; 
                Assert.True(result.IsCancelled);
            }
        }

        [Theory]
        [InlineData(Fail.Default)]
        [InlineData(Fail.Fast)]
        [InlineData(Fail.Smart)]
        public async Task ExceptionAsync(Fail failMode)
        {
            var tasks = Enumerable.Range(0, 10);
            int index = 0;
            bool badBehavior = false;
            var pTask = tasks.ParallelizeAsync(async (i, ct) =>
            {
                int ix = Interlocked.Increment(ref index);
                if (ix == 4)
                {
                    throw new TestException();
                }

                await Task.Delay(1000, ct);
                if (ct.IsCancellationRequested)
                {
                    return;
                }
                if (failMode == Fail.Fast)
                {
                    badBehavior = true;
                }
            }, 8, failMode);

            ParallelizedSummary result;

            if (failMode == Fail.Default)
            {
                await Assert.ThrowsAsync<TestException>(async () =>
                {
                    result = await pTask;
                });
                Assert.Equal(10, index);
            }
            else
            {
                result = await pTask;
                Assert.False(result.IsSucess);
                Assert.False(result.IsCancelled);
                Assert.NotEmpty(result.Exceptions);
            }

            if (badBehavior)
            {
                Assert.True(false);
            }
        }

        public sealed class TestException : Exception {}
    }
}
