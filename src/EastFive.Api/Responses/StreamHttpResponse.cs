using EastFive.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class StreamHttpResponse : EastFive.Api.HttpResponse
    {
        private Stream stream;

        public StreamHttpResponse(IHttpRequest request, HttpStatusCode statusCode,
            string fileName, string contentType, bool? inline,
            Stream stream)
            : base(request, statusCode)
        {
            this.SetFileHeaders(fileName, contentType, inline);
            this.stream = stream;
        }

        public override Task WriteResponseAsync(Stream responseStream)
        {
            return stream.CopyToAsync(responseStream);
        }
    }

    public class WriteStreamHttpResponse : EastFive.Api.HttpResponse
    {
        private Action<Stream> streamWriter;

        public WriteStreamHttpResponse(IHttpRequest request, HttpStatusCode statusCode,
            string fileName, string contentType, bool? inline,
            Action<Stream> streamWriter)
            : base(request, statusCode)
        {
            this.SetFileHeaders(fileName, contentType, inline);
            this.streamWriter = streamWriter;
        }

        public override Task WriteResponseAsync(Stream responseStream)
        {
            streamWriter(responseStream);
            return true.AsTask();
        }
    }

    public class WriteStreamAsyncHttpResponse : EastFive.Api.HttpResponse
    {
        private Func<Stream, Task> streamWriterAsync;

        public WriteStreamAsyncHttpResponse(IHttpRequest request, HttpStatusCode statusCode,
            string fileName, string contentType, bool? inline,
            Func<Stream, Task> streamWriterAsync)
            : base(request, statusCode)
        {
            this.SetFileHeaders(fileName, contentType, inline);
            this.streamWriterAsync = streamWriterAsync;
        }

        public override Task WriteResponseAsync(Stream responseStream)
        {
            return streamWriterAsync(responseStream);
        }
    }

    public class WriteStreamSyncAsyncHttpResponse : EastFive.Api.HttpResponse
    {
        private Func<Stream, Task> streamWriterAsync;

        public WriteStreamSyncAsyncHttpResponse(IHttpRequest request, HttpStatusCode statusCode,
            string fileName, string contentType, bool? inline,
            Func<Stream, Task> streamWriterAsync)
            : base(request, statusCode)
        {
            this.SetFileHeaders(fileName, contentType, inline);
            this.streamWriterAsync = streamWriterAsync;
        }

        public override async Task WriteResponseAsync(Stream responseStream)
        {
            using (var wrappedResponseStream = new StreamAsyncWrapper(responseStream))
            {
                await streamWriterAsync(wrappedResponseStream);
                await wrappedResponseStream.CompleteAsync();
            }
        }
    }

    internal class StreamAsyncWrapper : Stream
    {
        private Stream asyncOnlyStream;
        private Queue<Task> asyncOps;

        public StreamAsyncWrapper(Stream responseStream)
        {
            this.asyncOnlyStream = responseStream;
            this.asyncOps = new Queue<Task>();
        }

        public override bool CanRead => asyncOnlyStream.CanRead;

        public override bool CanSeek => asyncOnlyStream.CanSeek;

        public override bool CanWrite => asyncOnlyStream.CanWrite;

        public override long Length => asyncOnlyStream.Length;

        public override long Position
        {
            get => asyncOnlyStream.Position;
            set
            {
                asyncOnlyStream.Position = value;
            }
        }

        public override void Flush()
        {
            var asyncOp = asyncOnlyStream.FlushAsync();
            asyncOps.Enqueue(asyncOp);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return asyncOnlyStream.ReadAsync(buffer, offset, count).Result;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return asyncOnlyStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            asyncOnlyStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var asyncOp = asyncOnlyStream.WriteAsync(buffer, offset, count);
            asyncOps.Enqueue(asyncOp);
        }

        public async Task CompleteAsync()
        {
            while (asyncOps.Any())
            {
                await asyncOps.Dequeue();
            }
        }
    }
}
