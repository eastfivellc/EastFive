using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class HtmlHttpResponse : StringHttpResponse
    {
        public HtmlHttpResponse(IHttpRequest request, HttpStatusCode statusCode,
            string html)
            : base(request, statusCode,
                  default, "text/html", default,
                  html, default)
        {
        }

    }

    public class JsonStringHttpResponse : StringHttpResponse
    {
        public JsonStringHttpResponse(IHttpRequest request, HttpStatusCode statusCode,
            string json)
            : base(request, statusCode,
                  default, "application/json", default,
                  json, default)
        {
        }

    }
}
