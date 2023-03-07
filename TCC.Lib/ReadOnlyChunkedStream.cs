using System;
using System.IO;

namespace TCC.Lib
{
    public class ReadOnlyChunkedStream : Stream
    {
        private readonly Stream _inputStream;
        private readonly int _chunkSize;

        public ReadOnlyChunkedStream(Stream inputStream, int chunkSize)
        {
            _inputStream = inputStream;
            _chunkSize = chunkSize;
        }

        public override void Flush() => _inputStream.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            var maxRead = (int)Math.Min(count, _chunkSize - Position);
            var nbRead = _inputStream.Read(buffer, offset, maxRead);
            Position += nbRead;
            return nbRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _inputStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override bool CanRead => _inputStream.CanRead;
        public override bool CanSeek => _inputStream.CanSeek;
        public override bool CanWrite => false;
        public override long Length => Math.Min(_chunkSize, _inputStream.Length - _inputStream.Position);
        public override long Position { get; set; }
    }
}