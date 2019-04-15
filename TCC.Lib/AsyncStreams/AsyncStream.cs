using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace TCC.Lib.AsyncStreams
{
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
        public TaskAwaiter GetAwaiter() => _innerTask.GetAwaiter();
    }
}