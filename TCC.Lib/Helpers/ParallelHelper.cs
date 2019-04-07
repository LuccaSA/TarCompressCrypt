using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace TCC.Lib.Helpers
{
    public static class ParallelHelper
    {
        public static async Task<ParallelizedSummary> ParallelizeAsync<T>(this IEnumerable<T> source,
            Func<T, CancellationToken, Task> actionAsync, ParallelizeOption option, CancellationToken cancellationToken)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (actionAsync == null)
            {
                throw new ArgumentNullException(nameof(actionAsync));
            }

            var channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions
            {
                SingleWriter = true,
                SingleReader = false,
                AllowSynchronousContinuations = true
            });

            var feederTask = Task.Run(async () =>
            {
                foreach (var item in source)
                {
                    await channel.Writer.WriteAsync(item);
                }
                channel.Writer.Complete();
            });

            var processingTask = channel.Reader.ParallelizeStreamAsync(null, actionAsync, option, cancellationToken);
            await Task.WhenAll(feederTask, processingTask);
            return await processingTask;
        }

        public static Task<ParallelizedSummary> ParallelizeStreamAsync<T>(this ChannelReader<T> source,
            ChannelWriter<ParallelResult<T>> resultsChannel,
            Func<T, CancellationToken, Task> actionAsync, ParallelizeOption option, CancellationToken cancellationToken)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (actionAsync == null)
            {
                throw new ArgumentNullException(nameof(actionAsync));
            }

            var core = new ParallelizeCore(cancellationToken, option);
            var monitor = new ParallelMonitor<T>(option.MaxDegreeOfParallelism);

            return Task.Run(async () =>
            {
                try
                {
                    using (core)
                    {
                        var parallelTasks =
                            Enumerable.Range(0, option.MaxDegreeOfParallelism)
                                .Select(i => ParallelizeCoreStreamAsync(core, actionAsync, source, resultsChannel,i, monitor))
                                .ToArray();

                        await Task.WhenAll(parallelTasks);
                    }
                }
                catch (Exception e)
                {
                    resultsChannel?.TryComplete(e);
                    throw;
                }
                resultsChannel?.TryComplete();
                if (option.FailMode == Fail.Default)
                {
                    if (core.IsFaulted)
                    {
                        if (core.Exceptions.Count() == 1)
                        {
                            throw core.Exceptions.First();
                        }
                        throw new AggregateException(core.Exceptions);
                    }
                    if (core.IsCanceled)
                    {
                        throw new TaskCanceledException();
                    }
                }
                return new ParallelizedSummary(core.Exceptions, core.IsCanceled);
            });
        }

        private static Task ParallelizeCoreStreamAsync<T>(ParallelizeCore core,
            Func<T, CancellationToken, Task> actionAsync,
            ChannelReader<T> channelReader,
            ChannelWriter<ParallelResult<T>> resultsChannel,
            int index,
            ParallelMonitor<T> monitor)
        {
            return Task.Run(async () =>
            {
                while (await channelReader.WaitToReadAsync()) //returns false when the channel is completed
                {
                    while (channelReader.TryRead(out T item))
                    {
                        monitor.ActiveItem[index] = item;
                        if (core.IsLoopBreakRequested)
                        {
                            await YieldNotExecutedAsync(resultsChannel, item);
                            monitor.ActiveItem[index] = default;
                            if (core.FailMode == Fail.Fast)
                            {
                                return;
                            }
                            break;
                        }
                        try
                        {
                            await actionAsync(item, core.GlobalCancellationToken);
                            await YieldExecutedAsync(resultsChannel, item);
                        }
                        catch (TaskCanceledException tce)
                        {
                            await YieldCanceledAsync(resultsChannel, item, tce);
                        }
                        catch (Exception e)
                        {
                            await YieldFailedAsync(resultsChannel, item, e);
                            core.OnException(e);
                        }
                        monitor.ActiveItem[index] = default;
                    }
                }
            });
        }

        private static async Task YieldNotExecutedAsync<T>(ChannelWriter<ParallelResult<T>> resultsChannel, T item)
        {
            if (resultsChannel != null)
            {
                await resultsChannel.WriteAsync(new ParallelResult<T>(item, ExecutionState.NotExecuted));
            }
        }

        private static async Task YieldExecutedAsync<T>(ChannelWriter<ParallelResult<T>> resultsChannel, T item)
        {
            if (resultsChannel != null)
            {
                await resultsChannel.WriteAsync(new ParallelResult<T>(item, ExecutionState.Executed));
            }
        }

        private static async Task YieldFailedAsync<T>(ChannelWriter<ParallelResult<T>> resultsChannel, T item, Exception e)
        {
            if (resultsChannel != null)
            {
                await resultsChannel.WriteAsync(new ParallelResult<T>(item, ExecutionState.Failed, e));
            }
        }

        private static async Task YieldCanceledAsync<T>(ChannelWriter<ParallelResult<T>> resultsChannel, T item, Exception tce)
        {
            if (resultsChannel != null)
            {
                await resultsChannel.WriteAsync(new ParallelResult<T>(item, ExecutionState.Canceled, tce));
            }
        }
    }
}
