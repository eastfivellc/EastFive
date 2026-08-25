using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using Newtonsoft.Json;

using EastFive.Extensions;
using System.IO;
using EastFive.Api.Bindings.ContentHandlers;
using Microsoft.AspNetCore.Http.Headers;

namespace EastFive.Api
{
    public class JsonHttpResponse : HttpResponse
    {
        private object content;

        public JsonHttpResponse(IHttpRequest request,
            HttpStatusCode statusCode, object content) 
            : base(request, statusCode)
        {
            this.content = content;
            // this.Headers.Add("Content-Type", "application/json".AsArray());
        }

        public override void WriteHeaders(HttpContext context, ResponseHeaders headers)
        {
            base.WriteHeaders(context, headers);
            headers.ContentType = new Microsoft.Net.Http.Headers.MediaTypeHeaderValue(
                "application/json");
        }

        public override Task WriteResponseAsync(System.IO.Stream responseStream)
        {
            return WriteResponseAsync(responseStream, this.content, this.Request);
        }

        public static Task WriteResponseAsync(System.IO.Stream responseStream, object content,
            IHttpRequest request)
        {
            if (request.TryGetAcceptCharset(out Encoding encoding))
                return WriteResponseAsync(responseStream, content, request, encoding);

            return WriteResponseAsync(responseStream, content, request, encoding);
        }

        public static Task WriteResponseAsync(System.IO.Stream responseStream, object content,
            IHttpRequest request,
            Encoding encoding = default)
        {
            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new Serialization.Converter(request));
            settings.DefaultValueHandling = DefaultValueHandling.Include;
            settings.ContractResolver = Serialization.Json.ApiPropertyContractResolver.Instance;
            var contentJsonString = JsonConvert.SerializeObject(content, settings);

            return responseStream.WriteResponseText(contentJsonString, encoding);
        }
    }
}
