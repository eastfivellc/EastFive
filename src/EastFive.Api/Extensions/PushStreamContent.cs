using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class PushStreamContent : HttpContent
    {
        private readonly Func<Stream, HttpContent, TransportContext, Task> onStreamAvailable;

        public PushStreamContent(Action<Stream, HttpContent, TransportContext> onStreamAvailable,
            MediaTypeHeaderValue mediaType)
        {
            this.onStreamAvailable = (stream, content, context) =>
            {
                onStreamAvailable(stream, content, context);
                return Task.CompletedTask;
            };
            Headers.ContentType = mediaType;
        }

        public PushStreamContent(Func<Stream, HttpContent, TransportContext, Task> onStreamAvailable,
            MediaTypeHeaderValue mediaType)
        {
            this.onStreamAvailable = onStreamAvailable;
            Headers.ContentType = mediaType;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            await onStreamAvailable(stream, this, context);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }
    }
}
