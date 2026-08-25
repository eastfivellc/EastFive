using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using EastFive;
using EastFive.Extensions;

namespace EastFive.Api
{
    public interface IHttpResponse
    {
        IHttpRequest Request { get; }

        HttpStatusCode StatusCode { get; set; }

        string ReasonPhrase { get; set; }

        IDictionary<string, string[]> Headers { get; }

        void AddCookie(string cookieKey, string cookieValue, TimeSpan? expireTime);

        Task WriteResponseAsync(HttpContext context);

        void WritePreamble(HttpContext context);

        Task WriteResponseAsync(Stream stream);
    }

    
}
