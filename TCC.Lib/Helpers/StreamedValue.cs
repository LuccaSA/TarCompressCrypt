using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace TCC.Lib.Helpers
{
    public class StreamedValue<T>
    {
        public StreamedValue(T item, ExecutionStatus status)
        {
            Item = item;
            Status = status;
            Exception = null;
        }
        public StreamedValue(T item, ExecutionStatus status, Exception exception)
        {
            Item = item;
            Status = status;
            Exception = exception;
        }
        public T Item { get; }
        public ExecutionStatus Status { get; }
        public Exception Exception { get; }
    }

    public class StreamedValue<T, TSource> : StreamedValue<T>
    {
        public StreamedValue(T item, TSource source, ExecutionStatus status)
        : base(item, status)
        {
            ItemSource = source;
        }
        public StreamedValue(T item, TSource source, ExecutionStatus status, Exception exception)
            : base(item, status, exception)
        {
            ItemSource = source;
        }
        public TSource ItemSource { get; } 
    }

    public struct AsyncStream<T>
    {
        public AsyncStream(Channel<StreamedValue<T>> channel, Task innerTask, CancellationToken cancellationToken)
        {
            _channel = channel;
            _innerTask = innerTask;
            CancellationToken = cancellationToken;
        }

        private readonly Channel<StreamedValue<T>> _channel;
        private readonly Task _innerTask;

        public ChannelReader<StreamedValue<T>> ChannelReader => _channel.Reader;
        public CancellationToken CancellationToken { get; }
        internal IReadOnlyCollection<StreamedValue<T>> InternalCollection => _channel.InternalQueue();
        public TaskAwaiter GetAwaiter()
        {
            return _innerTask.GetAwaiter();
        }
    }

    public static class ChannelLinq
    {
        public static AsyncStream<T> AsAsyncStream<T>(this IEnumerable<T> source, CancellationToken cancellationToken)
        {
            var channel = Channel.CreateUnbounded<StreamedValue<T>>(new UnboundedChannelOptions
            {
                SingleWriter = true,
                SingleReader = false,
                AllowSynchronousContinuations = true
            });
            var task = Task.Run(async () =>
            {
                ;
                foreach (var item in source)
                {
                    await channel.Writer.WriteAsync(new StreamedValue<T>(item, ExecutionStatus.Succeeded), cancellationToken);
                }
                channel.Writer.Complete();
                ;
            });
            return new AsyncStream<T>(channel, task, cancellationToken);
        }

        public static AsyncStream<T> CountAsync<T>(this AsyncStream<T> source, out Counter counter)
        {
            var localCounter = new Counter();
            counter = localCounter;
            var channel = Channel.CreateUnbounded<StreamedValue<T>>(new UnboundedChannelOptions
            {
                SingleWriter = false,
                SingleReader = false,
                AllowSynchronousContinuations = true
            });
            var task = Task.Run(async () =>
            {
                while (await source.ChannelReader.WaitToReadAsync())
                {
                    var item = await source.ChannelReader.ReadAsync();
                    localCounter.Increment();
                    await channel.Writer.WriteAsync(item);
                }
                channel.Writer.Complete();
            });
            return new AsyncStream<T>(channel, task, source.CancellationToken);
        }

        public class Counter
        {
            private int _count;
            public int Count => _count;

            public void Increment()
            {
                Interlocked.Increment(ref _count);
            }
        }

        public static AsyncStream<TResult> SelectAsync<TSource, TResult>(this AsyncStream<TSource> source, Func<StreamedValue<TSource>, Task<TResult>> action)
        {
            var channel = Channel.CreateUnbounded<StreamedValue<TResult>>();
            var writer = channel.Writer;
            var task = Task.Run(async () =>
            {
                while (await source.ChannelReader.WaitToReadAsync())
                {
                    var sourceValue = await source.ChannelReader.ReadAsync();
                    await ExecuteStreamedValueAsync(sourceValue, action, writer);
                }
                channel.Writer.Complete();
            });
            return new AsyncStream<TResult>(channel, task, source.CancellationToken);
        }

        private static async Task ExecuteStreamedValueAsync<TSource, TResult>(StreamedValue<TSource> sourceValue, Func<StreamedValue<TSource>, Task<TResult>> action, ChannelWriter<StreamedValue<TResult>> writer)
        {
            TResult result;
            try
            {
                result = await action(sourceValue);
            }
            catch (TaskCanceledException tce)
            {
                await writer.WriteAsync(new StreamedValue<TResult>(default, ExecutionStatus.Canceled, tce));
                return;
            }
            catch (Exception e)
            {
                await writer.WriteAsync(new StreamedValue<TResult>(default, ExecutionStatus.Faulted, e));
                return;
            }
            await writer.WriteAsync(new StreamedValue<TResult>(result, ExecutionStatus.Succeeded));
        }
    }
}