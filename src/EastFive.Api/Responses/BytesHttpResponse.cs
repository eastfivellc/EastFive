using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class BytesHttpResponse : HttpResponse
    {
        private byte[] data;

        public BytesHttpResponse(IHttpRequest request, HttpStatusCode statusCode,
            string fileName, string contentType, bool? inline,
            byte [] data)
            : base(request, statusCode)
        {
            this.data = data;
            this.SetFileHeaders(fileName, contentType, inline);
        }

        public override async Task WriteResponseAsync(Stream responseStream)
        {
            try
            {
                await responseStream.WriteAsync(data, 0, data.Length,
                    this.Request.CancellationToken);
            } catch(OperationCanceledException)
            {
            }
        }
    }
}
