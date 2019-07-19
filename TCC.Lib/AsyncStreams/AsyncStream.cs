using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace TCC.Lib.AsyncStreams
{
    public struct AsyncStream<T>
    {
        public AsyncStream(Channel<StreamedValue<T>> channel, CancellationToken cancellationToken, Func<Task> innerTask)
        {
            _channel = channel;
            Task = Task.Run(innerTask);
            CancellationToken = cancellationToken;
        }

        private readonly Channel<StreamedValue<T>> _channel;
        public ChannelReader<StreamedValue<T>> ChannelReader => _channel.Reader;
        public CancellationToken CancellationToken { get; }
        public Task Task { get;}
    }
}