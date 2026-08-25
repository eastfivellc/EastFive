using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using EastFive.Api;

namespace EastFive.Api.Tests.Harness;

/// <summary>
/// Minimal <see cref="IHttpResponse"/> implementation returned by capturing
/// stub delegates. Tests assert on the captured branch name and arguments,
/// not on the response object.
/// </summary>
internal sealed class StubHttpResponse : IHttpResponse
{
    public StubHttpResponse(HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        this.StatusCode = statusCode;
        this.ReasonPhrase = string.Empty;
        this.Headers = new Dictionary<string, string[]>();
    }

    public IHttpRequest Request => null!;

    public HttpStatusCode StatusCode { get; set; }

    public string ReasonPhrase { get; set; }

    public IDictionary<string, string[]> Headers { get; }

    public void AddCookie(string cookieKey, string cookieValue, TimeSpan? expireTime) { }

    public Task WriteResponseAsync(HttpContext context) => Task.CompletedTask;

    public void WritePreamble(HttpContext context) { }

    public Task WriteResponseAsync(Stream stream) => Task.CompletedTask;
}
