using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Http.Headers;
using Microsoft.Net.Http.Headers;
using MediaTypeHeaderValue = Microsoft.Net.Http.Headers.MediaTypeHeaderValue;
using ContentDispositionHeaderValue = Microsoft.Net.Http.Headers.ContentDispositionHeaderValue;

using Newtonsoft.Json;

using EastFive.Linq;
using EastFive;
using EastFive.Reflection;
using EastFive.Extensions;
using EastFive.Linq.Async;
using EastFive.Api.Controllers;
using EastFive.Api;
using EastFive.Collections.Generic;

namespace EastFive.Api
{
    public static class ResponseExtensions
    {
        public static string GetHeader(this IHttpResponse response, string key)
        {
            if (!response.Headers.ContainsKey(key))
                return string.Empty;

            return response.Headers[key].First(
                (item , next) => item,
                () => string.Empty);
        }

        public static bool TryGetHeader(this IHttpResponse response, string key, out string value)
        {
            if (!response.Headers.ContainsKey(key))
            {
                value = string.Empty;
                return false;
            }

            if(!response.Headers[key].Any())
            {
                value = string.Empty;
                return false;
            }

            value = response.Headers[key].First();
            return true;
        }

        public static IHttpResponse SetHeader(this IHttpResponse response,
            string key, string value)
        {
            if(response.Headers.ContainsKey(key))
            {
                response.Headers[key] = response.Headers[key]
                    .Append(value).ToArray();
                return response;
            }
            response.Headers.Add(key, value.AsArray());
            return response;
        }

        public static IHttpResponse AddReason(this IHttpResponse routeResponse,
            string reasonText)
        {
            routeResponse.ReasonPhrase = reasonText.Replace('\n', ' ');
            return routeResponse;
        }

        #region Location

        private const string HeaderKeyLocation = "Location";

        public static bool TryGetLocation(this IHttpResponse req, out Uri location)
        {
            if(!req.TryGetHeader(HeaderKeyLocation, out string locationString))
            {
                location = default;
                return false;
            }
            return Uri.TryCreate(locationString, UriKind.RelativeOrAbsolute, out location);
        }

        public static void SetLocation(this IHttpResponse req, Uri location)
            => req.SetHeader(HeaderKeyLocation, location.OriginalString);

        #endregion

        #region Content Type

        private const string HeaderKeyContentType = "Content-Type";

        public static bool TryGetContentType(this IHttpResponse req, out string contentType)
            => req.TryGetHeader(HeaderKeyContentType, out contentType);

        public static void SetContentType(this IHttpResponse req, string contentType)
            => req.SetHeader(HeaderKeyContentType, contentType);

        #endregion

        #region File Responses

        public static void SetFileHeaders(this IHttpResponse response,
            string fileName, string contentType, bool? inline)
        {
            if (contentType.HasBlackSpace())
                response.SetContentType(contentType);

            if (inline.HasValue || fileName.HasBlackSpace())
            {
                var dispositionType = GetDispositiontype();
                var dispHeader = new ContentDispositionHeaderValue(dispositionType)
                {
                    FileName = fileName,
                };
                var dispositionHeaderValue = dispHeader.ToString();
                response.SetHeader("Content-Disposition", dispositionHeaderValue);

                string GetDispositiontype()
                {
                    if (inline.HasValue)
                        if(!inline.Value)
                            return "attachment";

                    return "inline";
                }
            }
        }

        public static void SetFileHeaders(this ResponseHeaders headers,
            string fileName, string contentType, bool? inline)
        {
            if (contentType.HasBlackSpace())
                if (MediaTypeHeaderValue.TryParse(contentType, out MediaTypeHeaderValue contentTypeHeader))
                    headers.ContentType = contentTypeHeader;

            if (fileName.HasBlackSpace())
            {
                var dispositionType = GetDispositiontype();
                headers.ContentDisposition = new ContentDispositionHeaderValue(dispositionType)
                {
                    FileName = fileName,
                };

                string GetDispositiontype()
                {
                    if (inline.HasValue)
                        if (inline.Value)
                            return "inline";

                    return "attachment";
                }
            }
        }

        #endregion

    }
}