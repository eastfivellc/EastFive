using EastFive;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Web;

namespace EastFive
{
    public static class UriExtensions
    {
        public static Uri ToUri(this string uriString)
        {
            return new Uri(uriString);
        }

        public static bool AreEqual(this Uri uri1, Uri uri2,
            UriComponents partsToCompare = UriComponents.AbsoluteUri,
            UriFormat compareFormat = UriFormat.SafeUnescaped,
            StringComparison comparisonType = StringComparison.OrdinalIgnoreCase)
        {
            return Uri.Compare(uri1, uri2, partsToCompare, compareFormat, comparisonType) == 0;
        }

        public static bool AreEqual(this Uri uri1, string uri2String,
            UriComponents partsToCompare = UriComponents.AbsoluteUri,
            UriFormat compareFormat = UriFormat.SafeUnescaped,
            StringComparison comparisonType = StringComparison.OrdinalIgnoreCase)
        {
            var uri2 = new Uri(uri2String);
            return uri1.AreEqual(uri2);
        }

        public static string ToQueryString(this IDictionary<string, string> source)
        {
            // From: http://stackoverflow.com/questions/829080/how-to-build-a-query-string-for-a-url-in-c#5158497
            return String.Join("&", source.Select(kvp => String.Format("{0}={1}", HttpUtility.UrlEncode(kvp.Key), HttpUtility.UrlEncode(kvp.Value))).ToArray());
        }

        public static Uri SetQuery(this Uri uri, string query)
        {
            var uriWithQueryParam = new UriBuilder(uri) { Query = query };
            return uriWithQueryParam.Uri;
        }

        public static Uri SetQuery(this Uri uri, IDictionary<string, string> query)
        {
            var queryString = query.ToQueryString();
            var uriWithQueryParam = new UriBuilder(uri) { Query = queryString };
            return uriWithQueryParam.Uri;
        }

        public static Uri SetQueryParam(this Uri uri, string name, string value)
        {
            // From: http://stackoverflow.com/questions/829080/how-to-build-a-query-string-for-a-url-in-c#20492373

            // this actually returns HttpValueCollection : NameValueCollection
            // which uses unicode compliant encoding on ToString()
            var queryParams = uri.Query.IsDefaultOrNull() ?
                new System.Collections.Specialized.NameValueCollection()
                :
                HttpUtility.ParseQueryString(uri.Query);
            if (queryParams.AllKeys.Contains(name))
                queryParams[name] = value;
            else
                queryParams.Add(name, value);
            var uriBuilder = new UriBuilder(uri)
            {
                Query = queryParams.ToString()
            };

            return uriBuilder.Uri;
        }

        public static bool TryGetQueryParam(this Uri uri, string name, out string param)
        {
            var queryParams = System.Web.HttpUtility.ParseQueryString(
               uri.Query);
            if (queryParams.AllKeys.Contains(name))
            {
                param = queryParams[name];
                return true;
            }
            param = default(string);
            return false;
        }

        public static string GetQueryParam(this Uri uri, string name)
        {
            return System.Web.HttpUtility.ParseQueryString(
               uri.Query)[name];
        }


        public static Uri AddQuery(this Uri uri, string name, string value)
        {
            var ub = new UriBuilder(uri);

            // decodes urlencoded pairs from uri.Query to HttpValueCollection
            var httpValueCollection = HttpUtility.ParseQueryString(uri.Query);

            httpValueCollection.Add(name, value);

            // urlencodes the whole HttpValueCollection
            ub.Query = httpValueCollection.ToString();

            return ub.Uri;
        }

        public static IDictionary<string, string> ParseQuery(this Uri uri)
        {
            if (uri.IsDefault() || uri.Query.IsNullOrWhiteSpace())
                return new Dictionary<string, string>();

            // https://stackoverflow.com/questions/56706759/correctly-access-query-parameters-from-httprequestmessage
            var plusFixedQuery = uri.Query.Replace("+", "%2b"); 
            var queryNameCollection = HttpUtility.ParseQueryString(plusFixedQuery);
            return queryNameCollection.AsDictionary();
        }

        public static Uri AddQueryParameter(this Uri uri, string parameter, string value)
        {
            if(!uri.IsAbsoluteUri)
            {
                var uriStr = uri.OriginalString.Contains('?')?
                    $"{uri.OriginalString}&{parameter}={HttpUtility.UrlEncode(value)}"
                    :
                    $"{uri.OriginalString}?{parameter}={HttpUtility.UrlEncode(value)}";
                return new Uri(uriStr, UriKind.Relative);
            }
            var uriBuilder = new UriBuilder(uri);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query[parameter] = value;
            uriBuilder.Query = query.CompileQueryParameters();
            return uriBuilder.Uri;
        }

        public static Uri AddQueryParameter<QueryType, TValue>(this Uri uri,
            Expression<Func<QueryType, TValue>> parameterExpr,
            string value)
        {
            return parameterExpr.PropertyName(
                (parameter) => uri.AddQueryParameter(parameter, value),
                () => { throw new ArgumentException("Not a property expression", "parameterExpr"); });
        }

        public static Uri AddQueryParameter<QueryType, TValue>(this Uri uri,
            Expression<Func<QueryType, TValue>> parameterExpr,
            IDictionary<string, string> values)
        {
            return parameterExpr.PropertyName(
                (parameter) =>
                {
                    return values.Aggregate(
                        new
                        {
                            src = uri,
                            index = 0,
                        },
                        (aggr, value) =>
                        {
                            return new
                            {
                                src = uri
                                    .AddQueryParameter($"{parameter}[{aggr.index}].Key", value.Key)
                                    .AddQueryParameter($"{parameter}[{aggr.index}].Value", value.Value),
                                index = aggr.index + 1,
                            };
                        },
                        (aggr) => aggr.src);
                },
                () => { throw new ArgumentException("Not a property expression", "parameterExpr"); });
        }

        public static Uri RemoveQueryParameter(this Uri uri, string parameter)
        {
            var uriBuilder = new UriBuilder(uri);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query.Remove(parameter);
            uriBuilder.Query = query.CompileQueryParameters();
            return uriBuilder.Uri;
        }

        /// <summary>
        /// Compile the query parameters into a string without URL encoding the parameter values.
        /// </summary>
        /// <param name="queryParameters"></param>
        /// <returns></returns>
        public static string CompileQueryParameters(this System.Collections.Specialized.NameValueCollection queryParameters)
        {
            return queryParameters
                .ToEnumerable()
                .NullToEmpty()
                .Select(
                    (kvp) =>
                    {
                        return $"{kvp.key}={kvp.value}";
                    })
                .Join("&");
        }

        public static Uri CopyPathAndQuery(this Uri uri, Uri uriToCopyFrom)
        {
            var uriBuilderTo = new UriBuilder(uri);
            var uriBuilderFrom = new UriBuilder(uriToCopyFrom);
            uriBuilderTo.Path = uriBuilderFrom.Path;
            uriBuilderTo.Query = uriBuilderFrom.Query;
            return uriBuilderTo.Uri;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="fileOrDirectory"></param>
        /// <param name="postPendFile">
        ///     Add <paramref name="fileOrDirectory"/> before the filename. For example:
        ///     if true, /foo/bar becomes /foo/<paramref name="fileOrDirectory"/>/bar
        ///     if false /foo/bar/<paramref name="fileOrDirectory"/>
        /// </param>
        /// <returns></returns>
        public static Uri AppendToPath(this Uri uri, string fileOrDirectory,
            bool postPendFile = false)
        {
            var uriBuilder = new UriBuilder(uri);
            if(postPendFile)
            {
                var pathComponents = uriBuilder.Path
                    .Split('/'.AsArray())
                    .ToArray();
                if (!pathComponents.Any())
                {
                    uriBuilder.Path = $"{fileOrDirectory}/{uriBuilder.Path}";
                    return uriBuilder.Uri;
                }

                uriBuilder.Path = pathComponents.SkipLast(1)
                    .Append(fileOrDirectory)
                    .Append(pathComponents.Last())
                    .Join('/');
                return uriBuilder.Uri;
            }

            uriBuilder.Path = uriBuilder.Path.EndsWith("/") ?
                uriBuilder.Path + fileOrDirectory
                :
                $"{uriBuilder.Path}/{fileOrDirectory}";
            return uriBuilder.Uri;
        }

        public static string[] ParsePath(this Uri uri)
        {
            var uriBuilder = new UriBuilder(uri);
            var pathComponents = uriBuilder.Path
                    .Split('/'.AsArray())
                    .Where(pathComponent => pathComponent.HasBlackSpace())
                    .ToArray();
            return pathComponents;
        }

        public static Uri BaseUri(this Uri uri)
        {
            var uriBuilder = new UriBuilder(uri);
            uriBuilder.Path = String.Empty;
            uriBuilder.Query = String.Empty;
            return uriBuilder.Uri;
        }

        public static string GetFile(this Uri uri)
        {
            return uri.Segments.LastOrDefault();
        }

        public static Uri SetFile(this Uri uri, string fileName)
        {
            var uriBuilder = new UriBuilder(uri);
            uriBuilder.Path = GetPath();
            return uriBuilder.Uri;

            string GetPath()
            {
                if (uriBuilder.Path.EndsWith("/"))
                    return $"{uriBuilder.Path}/{fileName}";

                var currentFileName = uri.GetFile();
                return uriBuilder.Path.Substring(0,
                    uriBuilder.Path.Length - currentFileName.Length) + fileName;
            }
        }

        public static Uri SetSchema(this Uri uri, string schema)
        {
            var uriBuilder = new UriBuilder(uri);
            uriBuilder.Scheme = schema;
            return uriBuilder.Uri;
        }

        public static Dictionary<Guid, object> ParseQueryParameter<QueryType>(this Uri uri,
            Expression<Func<QueryType, Dictionary<Guid, object>>> parameterExpr)
        {
            return uri.ParseQueryParameter(parameterExpr,
                    guidKey => Guid.Parse(guidKey),
                    objValue => (object)objValue,
                (dict) => dict,
                () => default(Dictionary<Guid, object>));
        }

        public static TResult ParseQueryParameter<QueryType, TKey, TValue, TResult>(this Uri uri,
            Expression<Func<QueryType, Dictionary<TKey, TValue>>> parameterExpr,
            Func<string, TKey> parseKey,
            Func<string, TValue> parseValue,
            Func<Dictionary<TKey, TValue>, TResult> onFound,
            Func<TResult> onNotInQueryString)
        {
            return parameterExpr.PropertyName(
                (parameter) =>
                {
                    var value = ParseDictionary(uri, parameter, 0, parseKey, parseValue);
                    if (!value.Any())
                        return onNotInQueryString();
                    return onFound(value);
                },
                () => { throw new ArgumentException("Not a property expression", "parameterExpr"); });
        }

        private static Dictionary<TKey, TValue> ParseDictionary<TKey, TValue>(Uri uri, string parameter, int index,
            Func<string, TKey> parseKey,
            Func<string, TValue> parseValue)
        {
            var parameterKey = $"{parameter}[{index}].Key";
            var parameterValue = $"{parameter}[{index}].Value";
            if (!uri.TryGetQueryParam(parameterKey, out string valueStringKey))
                return new Dictionary<TKey, TValue>();
            if (!uri.TryGetQueryParam(parameterValue, out string valueStringValue))
                return new Dictionary<TKey, TValue>();

            var next = ParseDictionary(uri, parameter, index + 1,
                parseKey, parseValue);
            next.Add(
                parseKey(valueStringKey),
                parseValue(valueStringValue));
            return next;
        }

        public static TResult ParseQueryParameter<QueryType, TValue, TResult>(this Uri uri,
            Expression<Func<QueryType, TValue>> parameterExpr,
            Func<string, TValue> parse,
            Func<TValue, TResult> onFound,
            Func<TResult> onNotInQueryString)
        {
            return parameterExpr.PropertyName(
                (parameter) =>
                {
                    if (!uri.TryGetQueryParam(parameter, out string valueString))
                        return onNotInQueryString();

                    var value = parse(valueString);
                    return onFound(value);
                },
                () => { throw new ArgumentException("Not a property expression", "parameterExpr"); });
        }

        public static int GetNextParameterIndex(this Uri uri, string parameter)
        {
            var linkUriBuilder = new UriBuilder(uri);
            var linkQuery = HttpUtility.ParseQueryString(linkUriBuilder.Query);
            var parameters = linkQuery.AllKeys.Where(key => key.ToLower().Contains(parameter + "[")).ToList();
            var index = 0;
            if (parameters.Any())
                index = parameters.Select(param => Convert.ToInt32(param.Substring(parameter.Length + 1, 1))).Max() + 1;
            return index;
        }

        public static Uri ReplaceQueryParameterValue(this Uri uri, string parameterName, string newValue)
        {
            var uriBuilder = new UriBuilder(uri);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query.Set(parameterName, newValue);
            uriBuilder.Query = query.ToString();
            return uriBuilder.Uri;
        }

        public static Uri ReplaceBase(this Uri startingUrl, Uri baseUri)
        {
            var builder = new UriBuilder(startingUrl);
            builder.Host = baseUri.Host;
            builder.Scheme = baseUri.Scheme;
            if (baseUri.Port > 0)
                builder.Port = baseUri.Port;
            return builder.Uri;
        }
    }
}
