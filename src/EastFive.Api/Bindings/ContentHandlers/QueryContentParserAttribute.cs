using EastFive.Api.Core;
using EastFive.Api.Serialization;
using EastFive.Extensions;
using EastFive.Web;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class QueryContentParserAttribute : Attribute, IParseContent
    {
        public bool DoesParse(IHttpRequest request)
        {
            return false;
        }

        public async Task<IHttpResponse> ParseContentValuesAsync(
            IApplication httpApp, IHttpRequest request,
            Func<
                CastDelegate, 
                string[],
                Task<IHttpResponse>> onParsedContentValues)
        {
            if (!request.RequestUri.IsDefaultOrNull())
                return await UrlMissingAsync("URL was not provided");

            var contentLookup = request.RequestUri.ParseQuery();

            if (contentLookup.IsDefaultOrNull())
                return await UrlMissingAsync("Query is empty");

            CastDelegate parser =
                    (paramInfo, onParsed, onFailure) =>
                    {
                        return paramInfo
                            .GetAttributeInterface<IBindQueryApiValue>()
                            .ParseContentDelegate(contentLookup,
                                    paramInfo, httpApp, request,
                                onParsed,
                                onFailure);
                    };
            var keys = contentLookup
                .Keys
                .ToArray();

            return await onParsedContentValues(parser, keys);

            Task<IHttpResponse> UrlMissingAsync(string failureMessage)
            {
                CastDelegate emptyParser =
                    (paramInfo, onParsed, onFailure) =>
                    {
                        var key = paramInfo
                            .GetAttributeInterface<IBindApiValue>()
                            .GetKey(paramInfo)
                            .ToLowerNullSafe();
                        var type = paramInfo.ParameterType;
                        return onFailure($"[{key}] could not be parsed ({failureMessage}).");
                    };
                var exceptionKeys = new string[] { };
                return onParsedContentValues(emptyParser, exceptionKeys);
            }
        }
    }
}
