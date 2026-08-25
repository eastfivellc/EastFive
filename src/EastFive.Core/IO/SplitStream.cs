using EastFive.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EastFive.IO
{
    public static class SplitStreamExtensions
    {
        private class SharedStream : Stream
        {
            #region Types

            public delegate bool TryReadMoreDelegate(out Data nextBlock);

            public class Data
            {
                public byte[] bytes;
                public Data next;
                public long index;
            }

            #endregion

            #region State

            private Stream subStream;
            private TryReadMoreDelegate tryReadMore;

            private Data first;
            private Data current;
            private int currentIndex;
            private long position;

            #endregion

            public SharedStream(Stream subStream, TryReadMoreDelegate tryReadMore)
            {
                this.subStream = subStream;
                this.tryReadMore = tryReadMore;
                this.currentIndex = 0;
                this.position = 0;
            }

            #region Capabilities
            public override bool CanRead => true;

            public override bool CanSeek => true;

            public override bool CanWrite => false;
            #endregion

            #region Reading

            public override long Length => subStream.Length;

            public override long Position
            {
                get => position;
                set
                {
                    if (first.IsDefaultOrNull())
                    {
                        if (!tryReadMore(out Data nextBlock))
                            throw new IndexOutOfRangeException($"Cannot set position to {value} for empty stream");
                        current = nextBlock;
                        first = nextBlock;
                    }
                    var consider = first;
                    while (consider.index < value)
                    {
                        current = consider;
                        consider = consider.next;
                        if (consider.IsDefaultOrNull())
                            throw new IndexOutOfRangeException($"{value} > {current.index + current.bytes.Length}");
                    }
                    currentIndex = (int)(value - current.index);
                    position = value;
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                var bytesRead = 0;
                if(first.IsDefaultOrNull())
                {
                    if (!tryReadMore(out Data nextBlock))
                        return 0;
                    current = nextBlock;
                    first = nextBlock;
                }
                while (bytesRead < count)
                {
                    var bytesRemaining = current.bytes.Length - currentIndex;
                    if(bytesRemaining <= 0)
                    {
                        if (current.next.IsDefaultOrNull())
                        {
                            if (!tryReadMore(out Data nextBlock))
                                return bytesRead;
                            currentIndex = 0;
                            current = nextBlock;
                            continue;
                        }
                        currentIndex = 0;
                        current = current.next;
                        continue;
                    }

                    var readFromCurrent = Math.Min(bytesRemaining, count);
                    Array.Copy(current.bytes, currentIndex, buffer, offset, readFromCurrent);
                    count -= readFromCurrent;
                    currentIndex += readFromCurrent;
                    bytesRead += readFromCurrent;
                    continue;
                }
                return bytesRead;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                var targetOffset = GetTargetOffset(out Data consider);
                while (consider.index < targetOffset)
                {
                    current = consider;
                    consider = consider.next;
                    if (consider.IsDefaultOrNull())
                    {
                        position = current.index + current.bytes.Length;
                        return position;
                    }
                }
                currentIndex = (int)(targetOffset - current.index);
                position = targetOffset;
                return position;

                long GetTargetOffset(out Data firstConsideration)
                {
                    if (origin == SeekOrigin.Begin)
                    {
                        firstConsideration = first;
                        return offset;
                    }
                    if (origin == SeekOrigin.Current)
                    {
                        var computedOffset = offset + current.index + currentIndex;
                        firstConsideration = offset < 0 ? first : current;
                        return computedOffset;
                    }
                    throw new NotSupportedException();
                }
            }

            #endregion

            #region Writing

            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override void Flush()
            {
                throw new NotImplementedException();
            }

            #endregion
        }

        public static (Stream, Stream) Split(this Stream stream, int blockSize = 0x1000)
        {
            var lockObj = new object();
            var last = default(SharedStream.Data);
            SharedStream.TryReadMoreDelegate tryReadMore = TryReadMoreLocked;

            var stream1 = new SharedStream(stream, tryReadMore);
            var stream2 = new SharedStream(stream, tryReadMore);
            return (stream1, stream2);

            bool TryReadMoreLocked(out SharedStream.Data nextBlock)
            {
                lock (lockObj)
                {
                    nextBlock = new SharedStream.Data
                    {
                        bytes = new byte[blockSize],
                        index = 0,
                    };
                    var read = stream.Read(nextBlock.bytes, 0, blockSize);
                    if (read == 0)
                        return false;

                    if (read < blockSize)
                        nextBlock.bytes = nextBlock.bytes.Take(read).ToArray();
                    last.next = nextBlock;
                    last = nextBlock;
                    return true;
                }
            }
        }

        //public static (Stream, Func<Task<byte[]>>) Cache(this Stream stream, int blockSize = 0x1000)
        //{
        //    var blocks = new List<CachedStream.Data>();
        //    CachedStream.TryReadMoreDelegate tryReadMore = TryReadMoreLocked;

        //    var stream1 = new SharedStream(stream, tryReadMore);
        //    var stream2 = new SharedStream(stream, tryReadMore);
        //    return (stream1, ReadBlocks);

        //    bool TryReadMoreLocked(out CachedStream.Data nextBlock)
        //    {
        //        lock (lockObj)
        //        {
        //            nextBlock = new CachedStream.Data
        //            {
        //                bytes = new byte[blockSize],
        //                index = 0,
        //                position = stream.Position,
        //            };
        //            var read = stream.Read(nextBlock.bytes, 0, blockSize);
        //            if (nextBlock.length == 0)
        //                return false;

        //            if (read < blockSize)
        //                nextBlock.bytes = nextBlock.bytes.Take(read).ToArray();
        //            last.next = nextBlock;
        //            last = nextBlock;
        //            return true;

        //        }
        //    }

        //    Task<byte[]> ReadBlocks()
        //    {
                
        //    }
        //}

        //private class CachedStream : Stream
        //{

        //    #region State

        //    IDictionary<long, byte[]> blocks = new Dictionary<long, byte[]>();
        //    private Stream subStream;

        //    #endregion

        //    public CachedStream(Stream subStream, TryReadMoreDelegate tryReadMore)
        //    {
        //        this.subStream = subStream;
        //        this.currentIndex = 0;
        //        this.position = 0;
        //    }

        //    #region Capabilities
        //    public override bool CanRead => subStream.CanRead;

        //    public override bool CanSeek => subStream.CanSeek;

        //    public override bool CanWrite => subStream.CanWrite;
        //    #endregion

        //    #region Reading

        //    public override long Length => subStream.Length;

        //    public override long Position
        //    {
        //        get => subStream.Position;
        //        set
        //        {
        //            subStream.Position = value;
        //        }
        //    }

        //    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        //    {
        //        return base.ReadAsync(buffer, offset, count, cancellationToken);
        //    }

        //    public override int Read(byte[] buffer, int offset, int count)
        //    {
        //        var position = subStream.Position;
        //        var read = subStream.Read(buffer, offset, count);
        //        if (read == 0)
        //            return 0;

        //        var cacheBytes = new byte[read];
        //        Array.Copy(buffer, cacheBytes, read);

        //        blocks.TryAdd(position, cacheBytes);

        //        return read;
        //    }

        //    public override long Seek(long offset, SeekOrigin origin)
        //    {
        //        return subStream.Seek(offset, origin);
        //    }

        //    #endregion

        //    #region Writing

        //    public override void SetLength(long value)
        //    {
        //        throw new NotImplementedException();
        //    }

        //    public override void Write(byte[] buffer, int offset, int count)
        //    {
        //        throw new NotImplementedException();
        //    }

        //    public override void Flush()
        //    {
        //        throw new NotImplementedException();
        //    }

        //    #endregion
        //}
    }
}
