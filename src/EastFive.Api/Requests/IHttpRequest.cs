using System;
using System.IO;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Razor;

namespace EastFive.Api
{
    public interface IHttpRequest : IProvideServerLocation
    {
        Uri RequestUri { get; set; }

        IRequestHeaders RequestHeaders { get; }

        string GetHeader(string headerKey);

        IEnumerable<string> GetHeaders(string headerKey);

        CancellationToken CancellationToken { get; }

        HttpMethod Method { get; set; }

        Func<Stream, Task> WriteBodyAsync { get; set; }

        void UpdateHeader(string headerKey, Func<string[], string[]> callback);

        Task<string> ReadContentAsStringAsync();

        Task<byte[]> ReadContentAsync();

        bool HasBody { get; }

        bool HasFormContentType { get; }

        IFormCollection Form { get; }

        IDictionary<string, object> Properties { get; }

        IDictionary<string, string[]> Headers { get; }

        IRazorViewEngine RazorViewEngine { get; }

        TResult ReadCookie<TResult>(string cookieKey,
            Func<string, TResult> onCookie,
            Func<TResult> onNotAvailable);
    }

    public interface IRequestHeaders
    {
        public DateTimeOffset? LastModified { get; set; }
        public DateTimeOffset? IfUnmodifiedSince { get; set; }
        public Microsoft.Net.Http.Headers.RangeConditionHeaderValue IfRange { get; set; }
        public IList<Microsoft.Net.Http.Headers.EntityTagHeaderValue> IfNoneMatch { get; set; }
        public DateTimeOffset? IfModifiedSince { get; set; }
        public IList<Microsoft.Net.Http.Headers.EntityTagHeaderValue> IfMatch { get; set; }
        public HostString Host { get; set; }
        public DateTimeOffset? Expires { get; set; }
        public DateTimeOffset? Date { get; set; }
        public IList<Microsoft.Net.Http.Headers.CookieHeaderValue> Cookie { get; set; }
        public Microsoft.Net.Http.Headers.MediaTypeHeaderValue ContentType { get; set; }
        public Microsoft.Net.Http.Headers.ContentRangeHeaderValue ContentRange { get; set; }
        public Microsoft.Net.Http.Headers.RangeHeaderValue Range { get; set; }
        public long? ContentLength { get; set; }
        public Microsoft.Net.Http.Headers.CacheControlHeaderValue CacheControl { get; set; }
        public IList<Microsoft.Net.Http.Headers.StringWithQualityHeaderValue> AcceptLanguage { get; set; }
        public IList<Microsoft.Net.Http.Headers.StringWithQualityHeaderValue> AcceptEncoding { get; set; }
        public IList<Microsoft.Net.Http.Headers.StringWithQualityHeaderValue> AcceptCharset { get; set; }
        public IList<Microsoft.Net.Http.Headers.MediaTypeHeaderValue> Accept { get; set; }
        public IHeaderDictionary Headers { get; }
        public Microsoft.Net.Http.Headers.ContentDispositionHeaderValue ContentDisposition { get; set; }
        public Uri Referer { get; set; }

        public void Append(string name, object value);
        public void AppendList<T>(string name, IList<T> values);
        public T Get<T>(string name);
        public IList<T> GetList<T>(string name);
        public void Set(string name, object value);
        public void SetList<T>(string name, IList<T> values);
    }

}
