using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;

namespace EastFive.Api
{
    public static class HttpHeaderExtensions
    {
        public static bool IsSuccess(this HttpStatusCode statusCode)
        {
            return ((int)statusCode >= 200) && ((int)statusCode <= 299);
        }

        public static MediaTypeHeaderValue GetContentMediaTypeHeaderNullSafe(this HttpResponseMessage httpResponse)
        {
            if (httpResponse.IsDefaultOrNull())
                return null;
            return httpResponse.Content.GetContentMediaTypeHeaderNullSafe();
        }

        public static MediaTypeHeaderValue GetContentMediaTypeHeaderNullSafe(this HttpContent httpContent)
        {
            if (httpContent.IsDefaultOrNull())
                return null;
            return httpContent.Headers.GetContentMediaTypeHeaderNullSafe();
        }

        public static MediaTypeHeaderValue GetContentMediaTypeHeaderNullSafe(this HttpContentHeaders headers)
        {
            if (headers.IsDefaultNullOrEmpty())
                return null;
            return headers.ContentType;
        }

        public static string GetContentMediaTypeNullSafe(this HttpResponseMessage httpResponse)
        {
            if (httpResponse.IsDefaultOrNull())
                return null;
            return httpResponse.Content.GetContentMediaTypeNullSafe();
        }

        public static string GetContentMediaTypeNullSafe(this HttpContent httpContent)
        {
            if (httpContent.IsDefaultOrNull())
                return null;
            return httpContent.Headers.GetContentMediaTypeNullSafe();
        }

        public static string GetContentMediaTypeNullSafe(this HttpContentHeaders headers)
        {
            if (headers.IsDefaultNullOrEmpty())
                return null;
            if (headers.ContentType.IsDefaultOrNull())
                return null;
            return headers.ContentType.MediaType;
        }

        public static string GetFileNameNullSafe(this HttpResponseMessage httpResponse)
        {
            if (httpResponse.IsDefaultOrNull())
                return null;
            return httpResponse.Content.GetFileNameNullSafe();
        }

        public static string GetFileNameNullSafe(this HttpContent httpContent)
        {
            if (httpContent.IsDefaultOrNull())
                return null;
            return httpContent.Headers.GetFileNameNullSafe();
        }

        public static string GetFileNameNullSafe(this HttpContentHeaders headers)
        {
            if (headers.IsDefaultNullOrEmpty())
                return null;
            return headers.ContentDisposition.GetFileNameNullSafe();
        }

        public static string GetFileNameNullSafe(this ContentDispositionHeaderValue contentDisposition)
        {
            if (contentDisposition.IsDefaultOrNull())
                return null;
            return contentDisposition.FileName;
        }

        public static ContentDispositionHeaderValue GetContentDispositionNullSafe(this HttpResponseMessage httpResponse)
        {
            if (httpResponse.IsDefaultOrNull())
                return null;
            return httpResponse.Content.GetContentDispositionNullSafe();
        }

        public static ContentDispositionHeaderValue GetContentDispositionNullSafe(this HttpContent httpContent)
        {
            if (httpContent.IsDefaultOrNull())
                return null;
            return httpContent.Headers.GetContentDispositionNullSafe();
        }

        public static ContentDispositionHeaderValue GetContentDispositionNullSafe(this HttpContentHeaders headers)
        {
            if (headers.IsDefaultNullOrEmpty())
                return null;
            if (headers.ContentDisposition.IsDefaultOrNull())
                return null;
            return headers.ContentDisposition;
        }

        public static bool TryConvertToMimeDisposition(this ContentDispositionHeaderValue dispositionHeaderValue,
            out System.Net.Mime.ContentDisposition dispositionMime)
        {
            if(dispositionHeaderValue.IsDefaultOrNull())
            {
                dispositionMime = default;
                return false;
            }

            return dispositionHeaderValue
                .ToString()
                .TryParseMimeDisposition(out dispositionMime);
        }

        public static bool TryParseMimeDisposition(this string dispositionStringValue,
            out System.Net.Mime.ContentDisposition dispositionMime)
        {
            try
            {
                dispositionMime = new System.Net.Mime.ContentDisposition(dispositionStringValue);
                return true;
            }
            catch (Exception)
            {
                dispositionMime = default;
                return false;
            }
        }
    }
}
