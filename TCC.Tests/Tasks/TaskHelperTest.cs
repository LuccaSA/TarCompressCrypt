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
        [InlineData(128)]
        public async Task ParallelTestAsync(int degree)
        {
            var tasks = Enumerable.Range(0, 2000).Select(i => 2).ToList();
            tasks.Add(200);
            tasks.Add(100);
            tasks.Add(300);
            var cts = new CancellationTokenSource();
            int count = 0;
            int max = 0;
            var option = new ParallelizeOption
            {
                FailMode = Fail.Smart,
                MaxDegreeOfParallelism = degree
            };
            await tasks.ParallelizeAsync(async (i, ct) =>
            {
                int loopMax = Interlocked.Increment(ref count);
                max = Math.Max(max, loopMax);
                await Task.Delay(i, ct);
                Interlocked.Decrement(ref count);
                Assert.True(count <= degree);
            }, option, cts.Token);

            Assert.Equal(degree, max);
        }


        [Theory]
        [InlineData(Fail.Default)]
        [InlineData(Fail.Fast)]
        [InlineData(Fail.Smart)]
        public async Task CancellationAsync(Fail failMode)
        {
            var tasks = Enumerable.Range(0, 10).ToList();
            var cts = new CancellationTokenSource();
            int index = 0;
            var option = new ParallelizeOption
            {
                FailMode = failMode,
                MaxDegreeOfParallelism = 8
            };
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
            }, option, cts.Token);

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
                Assert.True(result.IsCanceled);
            }
        }

        [Theory]
        [InlineData(Fail.Default)]
        [InlineData(Fail.Fast)]
        [InlineData(Fail.Smart)]
        public async Task ExceptionAsync(Fail failMode)
        {
            var tasks = Enumerable.Range(0, 10).ToList();
            int index = 0;
            bool badBehavior = false;
            var option = new ParallelizeOption
            {
                FailMode = failMode,
                MaxDegreeOfParallelism = 8
            };
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
            }, option, CancellationToken.None);

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
                Assert.False(result.IsSuccess);
                Assert.False(result.IsCanceled);
                Assert.NotEmpty(result.Exceptions);
            }

            if (badBehavior)
            {
                Assert.True(false);
            }
        }

        public sealed class TestException : Exception { }
    }
}
