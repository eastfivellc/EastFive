using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using EastFive.Api.Bindings.ContentHandlers;
using EastFive.Extensions;

namespace EastFive.Api
{
    public class StringHttpResponse : HttpResponse
    {
        private string content;
        private Encoding encoding;

        public StringHttpResponse(IHttpRequest request, HttpStatusCode statusCode,
            string fileName, string contentType, bool? inline,
            string content, Encoding encoding)
            : base(request, statusCode)
        {
            this.content = content;
            this.encoding = encoding;
            this.SetFileHeaders(fileName, contentType, inline);
        }

        public override Task WriteResponseAsync(Stream responseStream)
        {
            if (!encoding.IsDefaultOrNull())
                return responseStream.WriteResponseText(content, encoding);
            
            return responseStream.WriteResponseText(content, this.Request);
        }
    }
}
