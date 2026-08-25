using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using EastFive;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Collections.Generic;
using Microsoft.AspNetCore.Http.Features;
using System.IO;

namespace EastFive.Api
{
    public class HttpResponse : IHttpResponse
    {
        public HttpResponse(IHttpRequest request, HttpStatusCode statusCode)
        {
            this.Request = request;
            this.StatusCode = statusCode;
            this.ReasonPhrase = string.Empty;
            this.Headers = new Dictionary<string, string[]>();
        }

        public IHttpRequest Request { get; private set; }

        public virtual HttpStatusCode StatusCode { get; set; }

        public string ReasonPhrase { get; set; }

        public IDictionary<string, string[]> Headers { get; private set; }

        private (string, string, TimeSpan?)[] cookies;

        public void AddCookie(string cookieKey, string cookieValue, TimeSpan? expireTime)
        {
            this.cookies = cookies
                .NullToEmpty()
                .Append((cookieKey, cookieValue, expireTime))
                .ToArray();
        }

        public virtual Task WriteResponseAsync(HttpContext context)
        {
            WritePreamble(context);
            return WriteResponseAsync(context.Response.Body);
        }

        #region Preamble

        public virtual void WritePreamble(HttpContext context)
        {
            WriteStatusCode(context);
            WriteReason(context);
            var typedHeaders = context.Response.GetTypedHeaders();
            WriteHeaders(context, typedHeaders);
            WriteCookies(context);
        }

        public virtual void WriteStatusCode(HttpContext context)
        {
            context.Response.StatusCode = (int)this.StatusCode;
        }

        public virtual void WriteReason(HttpContext context)
        {
            WriteReason(context, this.ReasonPhrase);

        }

        public void WriteReason(HttpContext context, string reason)
        {
            if (string.IsNullOrEmpty(reason))
                return;

            var reasonPhrase = SanitizeReasonForHeader(reason);
            if (reasonPhrase.Length > 510)
                reasonPhrase = new string(reasonPhrase.Take(510).ToArray());

            context.Response.Headers["X-Reason"] = reasonPhrase;

            var responseFeature = context.Features.Get<IHttpResponseFeature>();
            if (!responseFeature.IsDefaultOrNull())
                responseFeature.ReasonPhrase = reasonPhrase;

        }

        /// <summary>
        /// HTTP headers must be ASCII: Kestrel throws (turning the intended status into a 500)
        /// on any non-ASCII or control character. Reasons are prose, so transliterate the
        /// common typographic characters to ASCII equivalents and strip anything else rather
        /// than failing the response that carries the explanation.
        /// </summary>
        public static string SanitizeReasonForHeader(string reason)
        {
            return string.Concat(reason
                .Select(c => c switch
                {
                    '\n' => ";",
                    '\r' => "",
                    '\u00A0' => " ",           // non-breaking space
                    '\u2013' or '\u2014' => "-", // en / em dash
                    '\u2018' or '\u2019' => "'", // curly single quotes
                    '\u201C' or '\u201D' => "\"", // curly double quotes
                    '\u2026' => "...",           // ellipsis
                    '\u2192' => "->",            // right arrow
                    _ when c < ' ' || c > '~' => "?",
                    _ => c.ToString(),
                }));
        }

        public virtual void WriteHeaders(HttpContext context,
            Microsoft.AspNetCore.Http.Headers.ResponseHeaders headers)
        {
            foreach (var header in this.Headers)
                context.Response.Headers.Append(header.Key, header.Value);
        }

        public virtual void WriteCookies(HttpContext context)
        {
            if (cookies.IsDefaultNullOrEmpty())
                return;
            foreach (var (cookieKey, cookieValue, expireTime) in cookies)
            {
                CookieOptions option = new CookieOptions()
                { 
                    Secure = true,
                    HttpOnly = true,
                };

                if (expireTime.HasValue)
                    option.Expires = DateTime.Now + expireTime.Value;
                else
                    option.Expires = DateTime.Now.AddMilliseconds(10);

                context.Response.Cookies.Append(cookieKey, cookieValue, option);
            }
        }

        #endregion

        public virtual Task WriteResponseAsync(System.IO.Stream target)
        {
            // A reason-only response (status code + X-Reason, no content-bearing subclass) has
            // no body. This used to echo the REQUEST content back as the response body — a
            // debugging relic that surfaced on every error response to a POST/PATCH: clients saw
            // an unexplained (content-type-less, often base64-rendered) copy of their own
            // payload, and request bodies (PHI, credentials) were duplicated into proxies,
            // browser tooling, and log aggregators that capture response bodies.
            return Task.CompletedTask;
        }
    }
}
