using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PictureSpider.Pawchive
{
    // 只在读取时请求对应范围；Seek 跳过的压缩数据不下载。
    internal sealed class HttpRangeStream : Stream
    {
        private const int MaxReadBytes = 8 * 1024 * 1024;
        private const int MaxRequests = 4096;
        private readonly Func<long, long, CancellationToken, Task<byte[]>> readRange;
        private readonly CancellationToken cancellationToken;
        private readonly long readLimit;
        private long position;
        private long readBytes;
        private int requests;
        private byte[] cache = Array.Empty<byte>();
        private long cacheStart;

        public HttpRangeStream(long length, Func<long, long, CancellationToken, Task<byte[]>> readRange,
            CancellationToken cancellationToken)
        {
            if (length <= 1)
                throw new InvalidDataException("Remote archive is too small.");
            Length = length;
            this.readRange = readRange;
            this.cancellationToken = cancellationToken;
            // 累计读取量必须小于整个文件，不能退化为完整下载。
            readLimit = Math.Min(MaxReadBytes, length - 1);
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length { get; }
        public override long Position
        {
            get => position;
            set => Seek(value, SeekOrigin.Begin);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => checked(position + offset),
                SeekOrigin.End => checked(Length + offset),
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };
            if (target < 0 || target > Length)
                throw new IOException("Remote archive seek is outside the file.");
            return position = target;
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (buffer.Length == 0 || position == Length)
                return 0;
            if (position < cacheStart || position >= cacheStart + cache.Length)
            {
                if (requests >= MaxRequests || readBytes >= readLimit)
                    throw new IOException("Remote archive directory exceeds the inspection limits.");
                // 缓存相邻的小字段，避免 RAR 每读取几个字节就发起一次请求。
                var readAhead = Math.Max(1, Math.Min(4096, Length / 128));
                var count = (int)Math.Min(Math.Min(Math.Max(buffer.Length, readAhead), 64 * 1024),
                    Math.Min(Length - position, readLimit - readBytes));
                cache = readRange(position, position + count - 1, cancellationToken).GetAwaiter().GetResult();
                if (cache.Length != count)
                    throw new IOException("Remote archive returned an unexpected range length.");
                cacheStart = position;
                readBytes += count;
                requests++;
            }
            var cacheOffset = (int)(position - cacheStart);
            var copied = Math.Min(buffer.Length, cache.Length - cacheOffset);
            cache.AsSpan(cacheOffset, copied).CopyTo(buffer);
            position += copied;
            return copied;
        }

        public override int ReadByte()
        {
            Span<byte> value = stackalloc byte[1];
            return Read(value) == 0 ? -1 : value[0];
        }

        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
