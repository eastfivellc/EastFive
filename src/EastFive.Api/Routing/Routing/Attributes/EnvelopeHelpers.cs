using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using Microsoft.Net.Http.Headers;

namespace EastFive.Api.Routing.Envelopes
{
    /// <summary>
    /// Header inspection + URL query parsing helpers shared by the built-in
    /// <see cref="IDeserializeRequestEnvelope"/> attributes.
    /// </summary>
    internal static class EnvelopeHelpers
    {
        /// <summary>True iff the request advertises a body (Content-Length > 0
        /// or a Transfer-Encoding header). Header-only — does not call
        /// <see cref="IHttpRequest.HasBody"/> or read body bytes.</summary>
        public static bool HasBodyByHeaders(IHttpRequest request)
        {
            var len = request.RequestHeaders?.ContentLength;
            if (len.HasValue && len.Value > 0)
                return true;
            var transferEncoding = request.GetHeader("Transfer-Encoding");
            return !string.IsNullOrEmpty(transferEncoding);
        }

        /// <summary>True iff the request has no body advertised in its headers.</summary>
        public static bool LacksBodyByHeaders(IHttpRequest request)
        {
            var len = request.RequestHeaders?.ContentLength;
            if (len.HasValue && len.Value > 0)
                return false;
            var transferEncoding = request.GetHeader("Transfer-Encoding");
            return string.IsNullOrEmpty(transferEncoding);
        }

        /// <summary>True iff the Content-Type media type matches one of
        /// <paramref name="mediaTypes"/> (case-insensitive, exact match).</summary>
        public static bool ContentTypeMatches(IHttpRequest request, params string[] mediaTypes)
        {
            var ct = request.RequestHeaders?.ContentType;
            if (ct == null)
                return false;
            var mediaType = ct.MediaType.Value;
            if (string.IsNullOrEmpty(mediaType))
                return false;
            foreach (var candidate in mediaTypes)
            {
                if (string.Equals(mediaType, candidate, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>True iff the Content-Type media type ends with
        /// <paramref name="suffix"/> (e.g. "+json").</summary>
        public static bool ContentTypeEndsWith(IHttpRequest request, string suffix)
        {
            var ct = request.RequestHeaders?.ContentType;
            if (ct == null)
                return false;
            var mediaType = ct.MediaType.Value;
            return !string.IsNullOrEmpty(mediaType)
                && mediaType.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>True iff the Content-Type media type starts with
        /// <paramref name="prefix"/> (e.g. "multipart/").</summary>
        public static bool ContentTypeStartsWith(IHttpRequest request, string prefix)
        {
            var ct = request.RequestHeaders?.ContentType;
            if (ct == null)
                return false;
            var mediaType = ct.MediaType.Value;
            return !string.IsNullOrEmpty(mediaType)
                && mediaType.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Parse the URL query string into a case-insensitive
        /// dictionary. Last value wins for duplicate keys.</summary>
        public static IReadOnlyDictionary<string, string> ParseQuery(IHttpRequest request)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var query = request.RequestUri?.Query;
            if (string.IsNullOrEmpty(query))
                return result;
            var parsed = HttpUtility.ParseQueryString(query);
            foreach (var key in parsed.AllKeys)
            {
                if (key == null)
                    continue;
                result[key] = parsed[key];
            }
            return result;
        }
    }
}
