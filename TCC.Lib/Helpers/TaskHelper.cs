using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace TCC.Lib.Helpers
{
    public static class TaskHelper
    {
        private sealed class ParallelizeCore
        {
            private readonly Fail _failMode;
            private readonly ConcurrentBag<Exception> _exceptions = new ConcurrentBag<Exception>();
            private int _concurrencyEmergencyStop;
            private bool _isLoopBreakRequested;
            private readonly CancellationToken _cancellationRequestToken;
            private readonly CancellationTokenSource _exceptionCancellationTokenSource = new CancellationTokenSource();

            public ParallelizeCore(CancellationToken cancellationToken, Fail failMode)
            {
                _cancellationRequestToken = cancellationToken;
                _failMode = failMode;
                GlobalCancellationToken =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _exceptionCancellationTokenSource.Token).Token;
            }

            public bool IsLoopBreakRequested => _isLoopBreakRequested || GlobalCancellationToken.IsCancellationRequested;
            public CancellationToken GlobalCancellationToken { get; }
            public bool IsCancelled => _cancellationRequestToken.IsCancellationRequested;
            public bool IsFaulted => _exceptions.Count != 0;
            public IEnumerable<Exception> Exceptions => _exceptions;

            internal void ConcurrencyIncrement(int maxDegreeOfParallelism)
            {
                var updated = Interlocked.Increment(ref _concurrencyEmergencyStop);
                if (updated > maxDegreeOfParallelism)
                {
                    throw new MaximumParallelismReachedException();
                }
            }

            internal void ConcurrencyDecrement()
            {
                var updated = Interlocked.Decrement(ref _concurrencyEmergencyStop);
                if (updated < 0)
                {
                    throw new MaximumParallelismReachedException();
                }
            }

            public void OnException(Exception e)
            {
                if (e is TaskCanceledException)
                {
                    return;
                }
                _exceptions.Add(e);
                if (_failMode == Fail.Fast)
                {
                    _exceptionCancellationTokenSource.Cancel();
                    _isLoopBreakRequested = true;
                }
            }
        }


        public static Task<ParallelizedSummary> ParallelizeAsync<T>(this IEnumerable<T> source,
            Func<T, CancellationToken, Task> funk, int maxDegreeOfParallelism, Fail mode, CancellationToken ct)
        {
            var cs = new ConcurrentQueue<T>(source);

            var globalCompletionSource = new TaskCompletionSource<ParallelizedSummary>();
            var core = new ParallelizeCore(ct, mode);

            Task.Factory.StartNew(async () =>
            {
                try
                {
                    var parallelTasks =
                        Enumerable.Range(0, maxDegreeOfParallelism)
                            .Select(i => ParallelizeCoreAsync(core, funk, cs, maxDegreeOfParallelism))
                            .ToArray();

                    await Task.WhenAll(parallelTasks);
                }
                catch (Exception e)
                {
                    globalCompletionSource.SetException(e);
                    return;
                }

                if (core.IsFaulted && mode == Fail.Default)
                {
                    globalCompletionSource.SetException(core.Exceptions);
                }
                else if (core.IsCancelled && mode == Fail.Default)
                {
                    globalCompletionSource.SetCanceled();
                }
                else
                {
                    globalCompletionSource.SetResult(new ParallelizedSummary(core.Exceptions, core.IsCancelled));
                }
            }, TaskCreationOptions.LongRunning);

            return globalCompletionSource.Task;
        }

        private static Task ParallelizeCoreAsync<T>(ParallelizeCore core, Func<T, CancellationToken, Task> funk,
            ConcurrentQueue<T> cs, int maxDegreeOfParallelism)
        {
            return Task.Run(async () =>
            {
                while (cs.TryDequeue(out var item))
                {
                    core.ConcurrencyIncrement(maxDegreeOfParallelism);
                    if (core.IsLoopBreakRequested)
                    {
                        core.ConcurrencyDecrement();
                        break;
                    }
                    try
                    {
                        await funk(item, core.GlobalCancellationToken);
                    }
                    catch (Exception e)
                    {
                        core.OnException(e);
                    }
                    core.ConcurrencyDecrement();
                }
            });
        }
    }

    [Serializable]
    public class MaximumParallelismReachedException : Exception
    {
        public MaximumParallelismReachedException()
        {
        }

        protected MaximumParallelismReachedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}