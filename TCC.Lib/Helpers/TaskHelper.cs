using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TCC.Lib.Helpers
{
    public enum Fail
    {
        /// <summary>
        /// Don't fail loop on exception, return a fault task
        /// </summary>
        Default,
        /// <summary>
        /// Fail loop as soon as an exception happends, return a sucessfull task, with exceptions in ParallelizedSummary
        /// </summary>
        Fast,
        /// <summary>
        /// Don't fail loop on exception, return a sucessfull task, with exceptions in ParallelizedSummary
        /// </summary>
        Smart
    }

    public class ParallelizedSummary
    {
        public IEnumerable<Exception> Exceptions { get; }

        public bool IsCancelled { get; }

        public bool IsSucess => !IsCancelled && (Exceptions == null || !Exceptions.Any());

        public ParallelizedSummary(IEnumerable<Exception> exceptions, bool isCancelled)
        {
            Exceptions = exceptions;
            IsCancelled = isCancelled;
        }
    }

    public static class TaskHelper
    {
        private sealed class ParallelizeCore : IDisposable
        {
            private readonly Fail _failMode;
            private readonly ConcurrentBag<Exception> _exceptions = new ConcurrentBag<Exception>();
            private readonly CancellationTokenRegistration _cancellationTokenRegistration;
            private readonly CancellationTokenSource _cancellationTokenPropagation = new CancellationTokenSource();

            public ParallelizeCore(CancellationToken ct, Fail failMode)
            {
                _failMode = failMode;
                if (ct.CanBeCanceled)
                    _cancellationTokenRegistration = ct.Register(OnCancelRequested, false);
            }

            public volatile bool IsLoopBreakRequested;
            public volatile bool IsCancelled;
            public bool IsFaulted => _exceptions.Count != 0;
            public IEnumerable<Exception> Exceptions => _exceptions;

            private void OnCancelRequested()
            {
                IsLoopBreakRequested = true;
                IsCancelled = true;
                _cancellationTokenPropagation.Cancel();
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
                    IsLoopBreakRequested = true;
                    _cancellationTokenPropagation.Cancel();
                }
            }

            public CancellationToken CancellationToken => _cancellationTokenPropagation.Token;

            public void Dispose()
            {
                _cancellationTokenRegistration.Dispose();
                _cancellationTokenPropagation?.Dispose();
            }
        }

      
        public static Task<ParallelizedSummary> ParallelizeAsync<T>(this IEnumerable<T> source,
            Func<T, CancellationToken, Task> funk, int maxDegreeOfParallelism, Fail mode, CancellationToken ct = default(CancellationToken))
        {
            var cs = new ConcurrentQueue<T>(source);

            var globalCompletionSource = new TaskCompletionSource<ParallelizedSummary>();
            var core = new ParallelizeCore(ct, mode);
            Task.Run(async () =>
            {
                var parallelTasks =
                    Enumerable.Range(0, maxDegreeOfParallelism)
                        .Select(i => ParallelizeCoreAsync(core, funk, cs))
                        .ToArray();

                await Task.WhenAll(parallelTasks);

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
                core.Dispose();
            });

            return globalCompletionSource.Task;
        }

        private static Task ParallelizeCoreAsync<T>(ParallelizeCore core, Func<T, CancellationToken, Task> funk, ConcurrentQueue<T> cs)
        {
            return Task.Run(async () =>
            {
                while (cs.TryDequeue(out var item))
                {
                    if (core.IsLoopBreakRequested)
                    {
                        break;
                    }

                    try
                    {
                        await funk(item, core.CancellationToken);
                    }
                    catch (Exception e)
                    {
                        core.OnException(e);
                    }
                }
            });
        }
    }
}