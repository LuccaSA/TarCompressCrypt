using System;
using System.Threading;
using System.Threading.Tasks;

namespace TCC.Lib.Helpers
{
    public static class TimeOut
    {
        public static async Task<T> AfterAsync<T>(Func<CancellationToken, Task<T>> actionAsync, TimeSpan timeout)
        {
            if (actionAsync == null)
            {
                throw new ArgumentNullException(nameof(actionAsync));
            }

            using (var ctsDelay = new CancellationTokenSource())
            using (var ctsTask = new CancellationTokenSource())
            {
                var delayTask = Task.Delay(timeout, ctsDelay.Token);
                var task = actionAsync(ctsTask.Token);
                if (task == null)
                {
                    throw new TimeoutArgumentException("actionAsync should return a non null task");
                }
                var result = await Task.WhenAny(task, delayTask);
                if (result == delayTask)
                {
                    ctsTask.Cancel();
                    throw new TimeoutException($"Timeout after {timeout}");
                }
                ctsDelay.Cancel();
                return await task;
            }
        }

        public static async Task<T> AfterAsync<T>(Func<CancellationToken, Task<T>> actionAsync, CancellationToken token, TimeSpan timeout)
        {
            if (actionAsync == null)
            {
                throw new ArgumentNullException(nameof(actionAsync));
            }

            using (var ctsDelay = new CancellationTokenSource())
            using (var linkedTcsDelay = CancellationTokenSource.CreateLinkedTokenSource(ctsDelay.Token, token))
            using (var ctsTask = new CancellationTokenSource())
            using (var linkedTcsTask = CancellationTokenSource.CreateLinkedTokenSource(ctsTask.Token, token))
            {
                Task delayTask = null;
                Task<T> task = null;
                try
                {
                    delayTask = Task.Delay(timeout, linkedTcsDelay.Token);
                    task = actionAsync(linkedTcsTask.Token);
                    if (task == null)
                    {
                        throw new TimeoutArgumentException("actionAsync should return a non null task");
                    }
                    if (delayTask == await Task.WhenAny(task, delayTask))
                    {
                        ctsTask.Cancel();
                        if (delayTask.IsCanceled)
                        {
                            throw new TaskCanceledException(delayTask);
                        }
                        throw new TimeoutException($"Timeout after {timeout}");
                    }
                    ctsDelay.Cancel();
                    return await task;
                }
                catch (TaskCanceledException tce)
                {
                    if (tce.Task == delayTask)
                    {
                        ctsTask.Cancel();
                    }
                    else if (tce.Task == task)
                    {
                        linkedTcsDelay.Cancel();
                    }
                    throw;
                }
            }
        }

        public static async Task AfterAsync(Func<CancellationToken, Task> actionAsync, CancellationToken token, TimeSpan timeout)
        {
            if (actionAsync == null)
            {
                throw new ArgumentNullException(nameof(actionAsync));
            }

            using (var ctsDelay = new CancellationTokenSource())
            using (var linkedTcsDelay = CancellationTokenSource.CreateLinkedTokenSource(ctsDelay.Token, token))
            using (var ctsTask = new CancellationTokenSource())
            using (var linkedTcsTask = CancellationTokenSource.CreateLinkedTokenSource(ctsTask.Token, token))
            {
                Task delayTask = null;
                Task task = null;
                try
                {
                    delayTask = Task.Delay(timeout, linkedTcsDelay.Token);
                    task = actionAsync(linkedTcsTask.Token);
                    if (task == null)
                    {
                        throw new TimeoutArgumentException("actionAsync should return a non null task");
                    }
                    if (delayTask == await Task.WhenAny(task, delayTask))
                    {
                        ctsTask.Cancel();
                        if (delayTask.IsCanceled)
                        {
                            throw new TaskCanceledException(delayTask);
                        }
                        throw new TimeoutException($"Timeout after {timeout}");
                    }
                    ctsDelay.Cancel();
                    await task;
                }
                catch (TaskCanceledException tce)
                {
                    if (tce.Task == delayTask)
                    {
                        ctsTask.Cancel();
                    }
                    else if (tce.Task == task)
                    {
                        linkedTcsDelay.Cancel();
                    }
                    throw;
                }
            }
        }

        public static async Task<T> TimeoutAfterAsync<T>(this Task<T> task, TimeSpan timeout)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            using (var cts = new CancellationTokenSource())
            {
                var delayTask = Task.Delay(timeout, cts.Token);
                var result = await Task.WhenAny(task, delayTask);
                if (result == delayTask)
                {
                    throw new TimeoutException($"Timeout after {timeout}");
                }
                cts.Cancel();
                return await task;
            }
        }
    }

    public sealed class TimeoutArgumentException : Exception
    {
        public TimeoutArgumentException(string message)
            : base(message)
        {
        }
    }
}
