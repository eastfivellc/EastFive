using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class FileHttpResponse : EastFive.Api.HttpResponse
    {
        private Func<Stream, Task> writeResponseAsync;

        public FileHttpResponse(IHttpRequest request, HttpStatusCode statusCode,
            string fileName, string contentType, bool? inline,
            Func<Stream, Task> writeResponseAsync)
            : base(request, statusCode)
        {
            this.SetFileHeaders(fileName, contentType, inline);
            this.writeResponseAsync = writeResponseAsync;
        }

        public override Task WriteResponseAsync(Stream responseStream)
        {
            return writeResponseAsync(responseStream);
        }
    }
}
