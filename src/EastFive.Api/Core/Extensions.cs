using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Microsoft.AspNetCore.Mvc.Razor;

using EastFive.Extensions;
using EastFive.Linq;

namespace EastFive.Api.Core
{
    public static class Extensions
    {
        public static IApplicationBuilder UseFVCRouting(
            this IApplicationBuilder builder, IApplication app, IConfiguration configuration,
            IRazorViewEngine razorViewEngine)
        {
            EastFive.Web.Configuration.ConfigurationExtensions.Initialize(configuration);
            return builder.UseMiddleware<Middleware>(app, razorViewEngine);
        }

        #region Get Request Message

        public static HttpRequestMessage GetHttpRequestMessage(this Microsoft.AspNetCore.Http.HttpContext context)
        {
            return context.Request.ToHttpRequestMessage();
        }

        public static HttpRequestMessage ToHttpRequestMessage(this Microsoft.AspNetCore.Http.HttpRequest req)
        => new HttpRequestMessage()
            .SetMethod(req)
            .SetAbsoluteUri(req)
            .SetHeaders(req)
            .SetContent(req)
            .SetContentType(req);

        private static HttpRequestMessage SetAbsoluteUri(this HttpRequestMessage msg, Microsoft.AspNetCore.Http.HttpRequest req)
            => msg.Set(m => m.RequestUri = req.GetAbsoluteUri());

        public static Uri GetAbsoluteUri(this Microsoft.AspNetCore.Http.HttpRequest req)
        {
            var qStr = req.QueryString;

            // This goop is necessary to prevent the spaces in query parameters from becoming "+"'s.
            // URL encoding should occur when the URL is submitted.
            var query = req.Query
                .NullToEmpty()
                .Select(
                    (kvp) =>
                    {
                        return $"{kvp.Key}={kvp.Value}";
                    })
                .Join("&");
            var uriBuilder = new UriBuilder()
               {
                   Scheme = req.Scheme,
                   Host = req.Host.Host,
                   Port = req.Host.Port.HasValue ? req.Host.Port.Value : default,
                   Path = req.PathBase.Add(req.Path),
                   Query = query,
               };
            if (req.Host.Port.HasValue)
                return uriBuilder.Uri;

            var cleanString = uriBuilder.Uri.GetComponents(UriComponents.AbsoluteUri & ~UriComponents.Port, UriFormat.Unescaped);
            return new Uri(cleanString, UriKind.Absolute);
        }

        private static HttpRequestMessage SetMethod(this HttpRequestMessage msg, Microsoft.AspNetCore.Http.HttpRequest req)
            => msg.Set(m => m.Method = new HttpMethod(req.Method));

        private static HttpRequestMessage SetHeaders(this HttpRequestMessage msg, Microsoft.AspNetCore.Http.HttpRequest req)
            => req.Headers
                .Aggregate(msg,
                    (acc, h) => acc.Set(
                        m => m.Headers.TryAddWithoutValidation(h.Key, h.Value.Select(s => s)).AsEnumerable()));

        private static HttpRequestMessage SetContent(this HttpRequestMessage msg, Microsoft.AspNetCore.Http.HttpRequest req)
            => msg.Set(m => m.Content = new StreamContent(req.Body));

        private static HttpRequestMessage SetContentType(this HttpRequestMessage msg, Microsoft.AspNetCore.Http.HttpRequest req)
            => msg.Set(m => m.Content.Headers.Add("Content-Type", req.ContentType), applyIf: req.Headers.ContainsKey("Content-Type"));

        private static HttpRequestMessage Set(this HttpRequestMessage msg, Action<HttpRequestMessage> config, bool applyIf = true)
        {
            if (applyIf)
            {
                config.Invoke(msg);
            }

            return msg;
        }

        #endregion

        #region Response

        public static Task WriteToContextAsync(this HttpResponseMessage msg, HttpContext context)
        {
            return context.Response.FromHttpResponseMessage(msg);
        }

        public static async Task FromHttpResponseMessage(this Microsoft.AspNetCore.Http.HttpResponse resp, HttpResponseMessage msg)
        {
            resp.SetStatusCode(msg)
                .SetHeaders(msg)
                .SetContentType(msg);

            await resp.SetBodyAsync(msg);
        }

        public static Microsoft.AspNetCore.Http.HttpResponse SetStatusCode(this Microsoft.AspNetCore.Http.HttpResponse resp, HttpResponseMessage msg)
            => resp.Set(r => r.StatusCode = (int)msg.StatusCode);

        private static Microsoft.AspNetCore.Http.HttpResponse SetHeaders(this Microsoft.AspNetCore.Http.HttpResponse resp, HttpResponseMessage msg)
            => msg.Headers.Aggregate(resp, (acc, h) => acc.Set(r => r.Headers[h.Key] = new StringValues(h.Value.ToArray())));

        private static async Task<Microsoft.AspNetCore.Http.HttpResponse> SetBodyAsync(this Microsoft.AspNetCore.Http.HttpResponse resp, HttpResponseMessage msg)
        {
            if (msg.Content.IsDefaultOrNull())
                return resp;
            using (var stream = await msg.Content.ReadAsStreamAsync())
            using (var reader = new StreamReader(stream))
            {
                var content = await reader.ReadToEndAsync();

                return resp.Set(async r => await r.WriteAsync(content));
            }
        }

        private static Microsoft.AspNetCore.Http.HttpResponse SetContentType(this Microsoft.AspNetCore.Http.HttpResponse resp, HttpResponseMessage msg)
            => resp.Set(
                r => r.ContentType = msg.Content.Headers.GetValues("Content-Type").Single(),
                applyIf: (!msg.Content.IsDefaultOrNull()) && msg.Content.Headers.Contains("Content-Type"));

        public static Microsoft.AspNetCore.Http.HttpResponse Redirect(this Microsoft.AspNetCore.Http.HttpResponse resp,
            Uri locationHeader)
        {
            resp.StatusCode = (int)System.Net.HttpStatusCode.Redirect;
            resp.GetTypedHeaders().Location = locationHeader;
            return resp;
        }

        private static Microsoft.AspNetCore.Http.HttpResponse Set(this Microsoft.AspNetCore.Http.HttpResponse msg, Action<Microsoft.AspNetCore.Http.HttpResponse> config, bool applyIf = true)
        {
            if (applyIf)
            {
                config.Invoke(msg);
            }

            return msg;
        }

        #endregion
    }
}
