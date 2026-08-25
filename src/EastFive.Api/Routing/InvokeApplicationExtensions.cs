using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using EastFive;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Linq.Async;
using EastFive.Collections.Generic;
using EastFive.Reflection;
using EastFive.Api.Serialization;
using EastFive.Api.Bindings.ContentHandlers;
using Microsoft.AspNetCore.Http;
using EastFive.Web.Configuration;

namespace EastFive.Api
{
    public static class InvokeApplicationExtensions
    {
        #region GET

        [HttpMethodRequestBuilder(Method = "Get")]
        public static IQueryable<TResource> HttpGet<TResource>(this IQueryable<TResource> requestQuery)
        {
            if (!typeof(RequestMessage<TResource>).IsAssignableFrom(requestQuery.GetType()))
                throw new ArgumentException($"query must be of type `{typeof(RequestMessage<TResource>).FullName}` not `{requestQuery.GetType().FullName}`", "query");
            var requestMessageQuery = requestQuery as RequestMessage<TResource>;

            var methodInfo = typeof(InvokeApplicationExtensions)
                .GetMethod(nameof(InvokeApplicationExtensions.HttpGet), BindingFlags.Static | BindingFlags.Public)
                .MakeGenericMethod(typeof(TResource));
            var condition = Expression.Call(methodInfo, requestQuery.Expression);

            var requestMessageNewQuery = requestMessageQuery.FromExpression(condition);
            return requestMessageNewQuery;
        }
        public class HttpMethodRequestBuilderAttribute : Attribute, IBuildHttpRequests
        {
            public string Method { get; set; }

            public IHttpRequest MutateRequest(IHttpRequest request,
                MethodInfo method, Expression[] arguments)
            {
                request.Method = new HttpMethod(Method);
                return request;
            }
        }

        #endregion

        private static IHttpRequest WriteResource(IHttpRequest request, 
            HttpMethod httpMethod, object resource, KeyValuePair<string, string>[] headers,
            DefaultValueHandling defaultValueHandling)
        {
            request.Method = httpMethod;
            request.SetMediaType("application/json");
            foreach (var header in headers.NullToEmpty())
            {
                request.UpdateHeader(header.Key, x => x.Append(header.Value).ToArray());
            }
            request.WriteBodyAsync =
                (stream) =>
                {
                    var settings = new JsonSerializerSettings();
                    settings.Converters.Add(new Serialization.Converter(request));
                    settings.DefaultValueHandling = DefaultValueHandling.Include;
                    settings.ContractResolver = Serialization.Json.ApiPropertyContractResolver.Instance;
                    var contentJsonString = JsonConvert.SerializeObject(resource, settings);
                    return stream.WriteResponseText(contentJsonString, request);
                    //var writer = request.TryGetAcceptEncoding(out Encoding encoding) ?
                    //    new StreamWriter(stream, encoding)
                    //    :
                    //    new StreamWriter(stream);
                    //await writer.WriteAsync(contentJsonString);
                };

            return request;
        }

        #region POST

        [HttpPostRequestBuilder]
        public static IQueryable<TResource> HttpPost<TResource>(this IQueryable<TResource> requestQuery,
            TResource resource,
            KeyValuePair<string, string>[] headers = default)
        {
            if (!typeof(RequestMessage<TResource>).IsAssignableFrom(requestQuery.GetType()))
                throw new ArgumentException($"query must be of type `{typeof(RequestMessage<TResource>).FullName}` not `{requestQuery.GetType().FullName}`", "query");
            var requestMessageQuery = requestQuery as RequestMessage<TResource>;

            var methodInfo = typeof(InvokeApplicationExtensions)
                .GetMethod(nameof(InvokeApplicationExtensions.HttpPost), BindingFlags.Static | BindingFlags.Public)
                .MakeGenericMethod(typeof(TResource));
            var resourceExpr = Expression.Constant(resource, typeof(TResource));
            var headersExpr = Expression.Constant(headers, typeof(KeyValuePair<string, string>[]));
            var condition = Expression.Call(methodInfo, requestQuery.Expression, resourceExpr, headersExpr);

            var requestMessageNewQuery = requestMessageQuery.FromExpression(condition);
            return requestMessageNewQuery;
        }

        public class HttpPostRequestBuilderAttribute : Attribute, IBuildHttpRequests
        {
            public IHttpRequest MutateRequest(IHttpRequest request, MethodInfo method, Expression[] arguments)
            {
                var resource = arguments[0].Resolve();
                var headers = (KeyValuePair<string, string>[])arguments[1].Resolve();

                return WriteResource(request, HttpMethod.Post, resource, headers, DefaultValueHandling.Include);
            }
        }

        #endregion

        #region PATCH

        [HttpPatchRequestBuilder]
        public static IQueryable<TResource> HttpPatch<TResource>(this IQueryable<TResource> requestQuery,
            TResource resource,
            KeyValuePair<string, string>[] headers = default)
        {
            if (!typeof(RequestMessage<TResource>).IsAssignableFrom(requestQuery.GetType()))
                throw new ArgumentException($"query must be of type `{typeof(RequestMessage<TResource>).FullName}` not `{requestQuery.GetType().FullName}`", "query");
            var requestMessageQuery = requestQuery as RequestMessage<TResource>;

            var methodInfo = typeof(InvokeApplicationExtensions)
                .GetMethod(nameof(InvokeApplicationExtensions.HttpPatch), BindingFlags.Static | BindingFlags.Public)
                .MakeGenericMethod(typeof(TResource));
            var resourceExpr = Expression.Constant(resource, typeof(TResource));
            var headersExpr = Expression.Constant(headers, typeof(KeyValuePair<string, string>[]));
            var condition = Expression.Call(methodInfo, requestQuery.Expression, resourceExpr, headersExpr);

            var requestMessageNewQuery = requestMessageQuery.FromExpression(condition);
            return requestMessageNewQuery;
        }

        public class HttpPatchRequestBuilderAttribute : Attribute, IBuildHttpRequests
        {
            public IHttpRequest MutateRequest(IHttpRequest request, MethodInfo method, Expression[] arguments)
            {
                var httpMethod = new HttpMethod("Patch");
                var resource = arguments[0].Resolve();
                var headers = (KeyValuePair<string, string>[])arguments[1].Resolve();
                return WriteResource(request, httpMethod, resource, headers.NullToEmpty().ToArray(), DefaultValueHandling.Ignore);
            }
        }

        #endregion

        #region PUT

        [HttpPutRequestBuilder]
        public static IQueryable<TResource> HttpPut<TResource>(this IQueryable<TResource> requestQuery,
            TResource resource,
            KeyValuePair<string, string>[] headers = default)
        {
            if (!typeof(RequestMessage<TResource>).IsAssignableFrom(requestQuery.GetType()))
                throw new ArgumentException($"query must be of type `{typeof(RequestMessage<TResource>).FullName}` not `{requestQuery.GetType().FullName}`", "query");
            var requestMessageQuery = requestQuery as RequestMessage<TResource>;

            var methodInfo = typeof(InvokeApplicationExtensions)
                .GetMethod(nameof(InvokeApplicationExtensions.HttpPatch), BindingFlags.Static | BindingFlags.Public)
                .MakeGenericMethod(typeof(TResource));
            var resourceExpr = Expression.Constant(resource, typeof(TResource));
            var headersExpr = Expression.Constant(headers, typeof(System.Net.Http.Headers.HttpRequestHeaders));
            var condition = Expression.Call(methodInfo, requestQuery.Expression, resourceExpr, headersExpr);

            var requestMessageNewQuery = requestMessageQuery.FromExpression(condition);
            return requestMessageNewQuery;
        }

        public class HttpPutRequestBuilderAttribute : Attribute, IBuildHttpRequests
        {
            public IHttpRequest MutateRequest(IHttpRequest request, MethodInfo method, Expression[] arguments)
            {
                var resource = arguments[0].Resolve();
                var headers = (KeyValuePair<string, string>[])arguments[1].Resolve();
                return WriteResource(request, HttpMethod.Put, resource, headers, DefaultValueHandling.Include);
            }
        }

        #endregion

        #region DELETE

        [HttpMethodRequestBuilder(Method = "Delete")]
        public static IQueryable<TResource> HttpDelete<TResource>(this IQueryable<TResource> requestQuery)
        {
            if (!typeof(RequestMessage<TResource>).IsAssignableFrom(requestQuery.GetType()))
                throw new ArgumentException($"query must be of type `{typeof(RequestMessage<TResource>).FullName}` not `{requestQuery.GetType().FullName}`", "query");
            var requestMessageQuery = requestQuery as RequestMessage<TResource>;

            var methodInfo = typeof(InvokeApplicationExtensions)
                .GetMethod(nameof(InvokeApplicationExtensions.HttpDelete), BindingFlags.Static | BindingFlags.Public)
                .MakeGenericMethod(typeof(TResource));
            var condition = Expression.Call(methodInfo, requestQuery.Expression);

            var requestMessageNewQuery = requestMessageQuery.FromExpression(condition);
            return requestMessageNewQuery;
        }

        #endregion

        #region OPTIONS

        [HttpMethodRequestBuilder(Method = "Options")]
        public static IQueryable<TResource> HttpOptions<TResource>(this IQueryable<TResource> requestQuery)
        {
            return requestQuery;
        }

        #endregion

        #region Action

        [HttpActionRequestBuilder]
        public static IQueryable<TResource> HttpAction<TResource>(this IQueryable<TResource> requestQuery,
            string actionName,
            System.Net.Http.Headers.HttpRequestHeaders headers = default)
        {
            if(requestQuery.IsDefaultOrNull())
                throw new ArgumentException($"Null query for {typeof(IQueryable<TResource>).FullName}", "query");
            if (!typeof(IProvideRequestExpression<TResource>).IsAssignableFrom(requestQuery.GetType()))
                throw new ArgumentException($"query must be of type `{nameof(IProvideRequestExpression<TResource>)}` not `{requestQuery.GetType().FullName}`", "query");
            var requestMessageQuery = requestQuery as IProvideRequestExpression<TResource>;

            var methodInfo = typeof(InvokeApplicationExtensions)
                .GetMethod(nameof(HttpAction), BindingFlags.Static | BindingFlags.Public)
                .MakeGenericMethod(typeof(TResource));
            var resourceExpr = Expression.Constant(actionName, typeof(string));
            var headersExpr = Expression.Constant(headers, typeof(System.Net.Http.Headers.HttpRequestHeaders));
            var condition = Expression.Call(methodInfo, requestQuery.Expression, resourceExpr, headersExpr);

            var requestMessageNewQuery = requestMessageQuery.FromExpression(condition);
            return requestMessageNewQuery;
        }

        public class HttpActionRequestBuilder : Attribute, IBuildHttpRequests, IBuildUrls
        {
            public Uri BindUrlQueryValue(Uri url, MethodInfo method, Expression[] arguments)
            {
                var actionName = (string)arguments[0].Resolve();
                var postPendFile = url.Segments.Length > 3;
                url = url.AppendToPath(actionName, postPendFile: postPendFile);

                return url;
            }

            public IHttpRequest MutateRequest(IHttpRequest request, MethodInfo method, Expression[] arguments)
            {
                var actionName = (string)arguments[0].Resolve();
                var headers = (System.Net.Http.Headers.HttpRequestHeaders)arguments[1].Resolve();
                var url = request.RequestUri;
                var postPendFile = url.Segments.Length > 3;
                request.RequestUri = url.AppendToPath(actionName, postPendFile: postPendFile);

                foreach (var header in headers.NullToEmpty())
                {
                    request.UpdateHeader(header.Key, x => x.Concat(header.Value).ToArray());
                }
                return request;
            }
        }

        #endregion

        #region Security

        public static IQueryable<TResource> SignWithAccessToken<TResource>(this IQueryable<TResource> requestQuery)
        {
            var requestProvider = requestQuery as IProvideHttpRequest;
            var request = requestProvider.HttpRequest;

            return request.GetClaims(
                (claimsEnumerable) =>
                {
                    var claims = claimsEnumerable.ToArray();

                    var sessionId = claims.GetSessionId(
                        (sessionId) => sessionId,
                        () => throw new Exception());

                    var actorId = claims.GetActorId(
                        (accountId) => accountId,
                        () => throw new Exception());

                    var duration = EastFive.Api.AppSettings.AccessTokenExpirationInMinutes.ConfigurationDouble(
                        (minutes) => TimeSpan.FromMinutes(minutes));
                    return SignWithSpecificAccessToken(requestQuery, sessionId, actorId, duration);
                },
                () => throw new Exception(),
                (why) => throw new Exception());
        }

        [AccessTokenRequestBuilder]
        public static IQueryable<TResource> SignWithSpecificAccessToken<TResource>(this IQueryable<TResource> requestQuery,
            Guid sessionId, Guid accountId, TimeSpan duration)
        {
            if (requestQuery.IsDefaultOrNull())
                throw new ArgumentException($"Null query for {typeof(IQueryable<TResource>).FullName}", "query");
            if (!typeof(IProvideRequestExpression<TResource>).IsAssignableFrom(requestQuery.GetType()))
                throw new ArgumentException($"query must be of type `{nameof(IProvideRequestExpression<TResource>)}` not `{requestQuery.GetType().FullName}`", "query");
            var requestMessageQuery = requestQuery as IProvideRequestExpression<TResource>;

            var methodInfo = typeof(InvokeApplicationExtensions)
                .GetMethod(nameof(SignWithSpecificAccessToken), BindingFlags.Static | BindingFlags.Public)
                .MakeGenericMethod(typeof(TResource));
            var sessionExpr = Expression.Constant(sessionId, typeof(Guid));
            var accountExpr = Expression.Constant(accountId, typeof(Guid));
            var durationExpr = Expression.Constant(duration, typeof(TimeSpan));
            var condition = Expression.Call(methodInfo, requestQuery.Expression, sessionExpr, accountExpr, durationExpr);

            var requestMessageNewQuery = requestMessageQuery.FromExpression(condition);
            return requestMessageNewQuery;
        }

        public class AccessTokenRequestBuilder : Attribute, IBuildHttpRequests, IBuildUrls
        {
            public Uri BindUrlQueryValue(Uri url, MethodInfo method, Expression[] arguments)
            {
                var sessionId = (Guid)arguments[0].Resolve();
                var accountId = (Guid)arguments[1].Resolve();
                var expiration = (TimeSpan)arguments[2].Resolve();
                var expirationUtc = DateTime.UtcNow + expiration;
                var updatedUrl = url.SignWithAccessTokenAccount(sessionId, accountId, expirationUtc, urlNew => urlNew);
                
                return updatedUrl;
            }

            public IHttpRequest MutateRequest(IHttpRequest request, MethodInfo method, Expression[] arguments)
            {
                request.RequestUri = BindUrlQueryValue(request.RequestUri, method, arguments);
                return request;
            }
        }

        #endregion

        public static async Task<IHttpResponse> SendAsync<TResource>(this IQueryable<TResource> requestQuery)
        {
            var request = (requestQuery as RequestMessage<TResource>);
            var httpRequest = request.CompileRequest();
            var response = await request.InvokeApplication.SendAsync(httpRequest);
            return response;
        }

        public static async Task<TResult> MethodAsync<TResource, TResult>(this IQueryable<TResource> requestQuery,
            Func<TResource, TResult> onContent = default,
            Func<TResource[], TResult> onContents = default,
            Func<object[], TResult> onContentObjects = default,
            Func<string, TResult> onHtml = default,
            Func<byte[], string, TResult> onXls = default,
            Func<TResult> onCreated = default,
            Func<TResource, string, TResult> onCreatedBody = default,
            Func<TResult> onNoContent = default,
            Func<TResult> onUpdated = default,

            Func<Uri, TResult> onRedirect = default,

            Func<TResult> onBadRequest = default(Func<TResult>),
            Func<TResult> onUnauthorized = default(Func<TResult>),
            Func<TResult> onExists = default(Func<TResult>),
            Func<TResult> onNotFound = default(Func<TResult>),
            Func<Type, TResult> onRefDoesNotExistsType = default,
            Func<string, TResult> onFailure = default,

            Func<TResult> onNotImplemented = default,
            Func<IExecuteAsync, Task<TResult>> onExecuteBackground = default,
            Func<IHttpResponse, TResult> onResponse = default,
            Func<HttpStatusCode, TResult> onNotOverriddenResponse = default)
        {
            var httpRequest = requestQuery.CompileRequest();
            var request = (requestQuery as RequestMessage<TResource>);
            var response = await request.InvokeApplication.SendAsync(httpRequest);

            var application = request.InvokeApplication.Application as HttpApplication;

            if (response.StatusCode == HttpStatusCode.OK)
            {
                if (!onContent.IsDefaultOrNull())

                    return onContent(
                        await response.ParseContentAsync<TResource>(application));

                    if (!onContents.IsDefaultOrNull())
                        return onContents(
                            await response.ParseContentAsync<TResource[]>(application));

                    if (!onContentObjects.IsDefaultOrNull())
                        return onContentObjects(
                            await response.ParseContentAsync<object[]>(application));
                
            }

            if (response.StatusCode == HttpStatusCode.Created)
            {
                var memoryStream = new MemoryStream();
                await response.WriteResponseAsync(memoryStream);
                if (memoryStream.Length > 0)
                    if (!onCreatedBody.IsDefaultOrNull())
                    {
                        response.TryGetContentType(out string contentType);
                        return onCreatedBody(
                            await response.ParseContentAsync<TResource>(application),
                            contentType);
                    }

                if (!onCreated.IsDefaultOrNull())
                    return onCreated();
            }

            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                if (!onNoContent.IsDefaultOrNull())
                    return onNoContent();

                if (!onUpdated.IsDefaultOrNull())
                    return onUpdated();
            }

            if (response.StatusCode == HttpStatusCode.Redirect)
            {
                if (!onRedirect.IsDefaultOrNull())
                {
                    response.TryGetLocation(out Uri location);
                    return onRedirect(location);
                }
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                if (!onBadRequest.IsDefaultOrNull())
                    return onBadRequest();
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                if (!onExists.IsDefaultOrNull())
                {
                    if(response.Headers.Contains(kvp => kvp.Key == "X-Something"))
                        application.AlreadyExistsResponse<TResource, TResult>(onExists);
                }

                if (!onFailure.IsDefaultOrNull())
                    return onFailure(response.ReasonPhrase);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                if(!onNotFound.IsDefaultOrNull())
                    return onNotFound();
            }

            if (response.StatusCode == HttpStatusCode.NotImplemented)
            {
                if (!onNotImplemented.IsDefaultOrNull())
                    return onNotImplemented();
            }

            if (!onResponse.IsDefaultOrNull())
                return onResponse(response);
            
            application.RefNotFoundTypeResponse(onRefDoesNotExistsType);

            application.HtmlResponse<TResource, TResult>(onHtml);
            //application.XlsResponse<TResource, TResult>(onXls);

            application.UnauthorizedResponse<TResource, TResult>(onUnauthorized);
            application.GeneralConflictResponse<TResource, TResult>(onFailure);
            application.ExecuteBackgroundResponse<TResource, TResult>(onExecuteBackground);


            if (response is IDidNotOverride)
                (response as IDidNotOverride).OnFailure();

            if (response is IReturnResult)
            {
                var attachedResponse = response as IReturnResult;
                var result = attachedResponse.GetResultCasted<TResult>();
                return result;
            }

            if (!onNotOverriddenResponse.IsDefaultOrNull())
            {
                return onNotOverriddenResponse(response.StatusCode);
            }
            var msg = $"Failed to override response with status code `{response.StatusCode}` for {typeof(TResource).FullName}" +
                $"\nResponse:{response.ReasonPhrase}";
            throw new Exception(msg);
        }

        public static async Task<TResource> ParseContentAsync<TResource>(this IHttpResponse response, HttpApplication httpApp)
        {
            var stream = new MemoryStream();
            await response.WriteResponseAsync(stream);
            var json = await stream.ReadAsStringAsync();
            var converter = new BindConvert(response.Request, httpApp);
            return JsonConvert.DeserializeObject<TResource>(json, converter);
        }

        public static Task<TResult> GetAsync<TResource, TResult>(this IQueryable<TResource> requestQuery,
            Func<TResource, TResult> onContent = default(Func<TResource, TResult>),
            Func<TResource[], TResult> onContents = default(Func<TResource[], TResult>),
            Func<object[], TResult> onContentObjects = default(Func<object[], TResult>),
            Func<TResult> onBadRequest = default(Func<TResult>),
            Func<TResult> onNotFound = default(Func<TResult>),
            Func<TResult> onUnauthorized = default,
            Func<Type, TResult> onRefDoesNotExistsType = default(Func<Type, TResult>),
            Func<Uri, TResult> onRedirect = default(Func<Uri, TResult>),
            Func<TResult> onCreated = default(Func<TResult>),
            Func<string, TResult> onHtml = default(Func<string, TResult>),
            Func<byte[], string, TResult> onXls = default,
            Func<IExecuteAsync, Task<TResult>> onExecuteBackground = default,
            Func<HttpStatusCode, TResult> onNotOverriddenResponse = default)
        {
            return requestQuery
                .HttpGet()
                .MethodAsync<TResource, TResult>(
                    onContent: onContent,
                    onContents: onContents,
                    onContentObjects: onContentObjects,
                    onBadRequest: onBadRequest,
                    onNotFound: onNotFound,
                    onUnauthorized: onUnauthorized,
                    onRefDoesNotExistsType: onRefDoesNotExistsType,
                    onRedirect: onRedirect,
                    onCreated: onCreated,
                    onHtml: onHtml,
                    onXls: onXls,
                    onExecuteBackground: onExecuteBackground,
                    onNotOverriddenResponse: onNotOverriddenResponse);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TResource"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="application"></param>
        /// <param name="resource"></param>
        /// <param name="onCreated"></param>
        /// <param name="onBadRequest"></param>
        /// <param name="onExists"></param>
        /// <param name="onRefDoesNotExistsType"></param>
        /// <param name="onNotImplemented"></param>
        /// <returns></returns>
        /// <remarks>Response hooks are only called if the method is actually invoked. Responses from the framework are not trapped.</remarks>
        public static Task<TResult> PostAsync<TResource, TResult>(this IQueryable<TResource> requestQuery,
                TResource resource, KeyValuePair<string, string>[] headers = default,
            Func<TResult> onCreated = default,
            Func<TResource, string, TResult> onCreatedBody = default,
            Func<TResult> onBadRequest = default,
            Func<TResult> onExists = default, 
            Func<string, TResult> onFailure = default,
            Func<Type, TResult> onRefDoesNotExistsType = default,
            Func<Uri, TResult> onRedirect = default,
            Func<TResult> onNotImplemented = default,
            Func<IExecuteAsync, Task<TResult>> onExecuteBackground = default,
            Func<HttpStatusCode, TResult> onNotOverriddenResponse = default)
        {
            return requestQuery
                .HttpPost(resource, headers:headers)
                .MethodAsync<TResource, TResult>(
                    onCreated: onCreated,
                    onCreatedBody: onCreatedBody,
                    onBadRequest: onBadRequest,
                    onExists: onExists,
                    onRefDoesNotExistsType: onRefDoesNotExistsType,
                    onRedirect: onRedirect,
                    onFailure:onFailure,
                    onNotImplemented: onNotImplemented,
                    onExecuteBackground: onExecuteBackground,
                    onNotOverriddenResponse: onNotOverriddenResponse);
        }

        public static Task<TResult> PatchAsync<TResource, TResult>(this IQueryable<TResource> requestQuery,
                TResource resource,
            Func<TResult> onUpdated = default,
            Func<TResource, TResult> onUpdatedBody = default,
            Func<TResult> onNotFound = default,
            Func<TResult> onUnauthorized = default,
            Func<string, TResult> onFailure = default,
            Func<HttpStatusCode, TResult> onNotOverriddenResponse = default,
            Func<IHttpResponse, TResult> onResponse = default)
        {
            return requestQuery
                .HttpPatch(resource)
                .MethodAsync<TResource, TResult>(
                    onUpdated: onUpdated,
                    onContent: onUpdatedBody,
                    onNotFound: onNotFound,
                    onUnauthorized: onUnauthorized,
                    onFailure: onFailure,
                    onNotOverriddenResponse: onNotOverriddenResponse,
                    onResponse: onResponse);
        }

        public static Task<TResult> DeleteAsync<TResource, TResult>(this IQueryable<TResource> request,
            Func<TResult> onNoContent = default(Func<TResult>),
            Func<TResource, TResult> onContent = default(Func<TResource, TResult>),
            Func<TResource[], TResult> onContents = default(Func<TResource[], TResult>),
            Func<TResult> onBadRequest = default(Func<TResult>),
            Func<TResult> onNotFound = default(Func<TResult>),
            Func<Type, TResult> onRefDoesNotExistsType = default(Func<Type, TResult>),
            Func<Uri, TResult> onRedirect = default(Func<Uri, TResult>),
            Func<string, TResult> onHtml = default(Func<string, TResult>))
        {
            return request
                .HttpDelete()
                .MethodAsync<TResource, TResult>(
                    onUpdated: onNoContent,
                    onContent: onContent,
                    onContents: onContents,
                    onNoContent: onNoContent,
                    onBadRequest: onBadRequest,
                    onNotFound: onNotFound,
                    onRefDoesNotExistsType: onRefDoesNotExistsType,
                    onRedirect: onRedirect,
                    onHtml: onHtml);
        }

        public static Task<TResult> ActionAsync<TResource, TResult>(this IQueryable<TResource> requestQuery,
                string actionName,
            Func<TResult> onUpdated = default,
            Func<TResource, TResult> onUpdatedBody = default,
            Func<TResource[], TResult> onContents = default(Func<TResource[], TResult>),
            Func<object[], TResult> onContentObjects = default(Func<object[], TResult>),
            Func<TResult> onNotFound = default,
            Func<TResult> onUnauthorized = default,
            Func<string, TResult> onFailure = default,
            Func<HttpStatusCode, TResult> onNotOverriddenResponse = default,
            Func<IHttpResponse, TResult> onResponse = default)
        {
            return requestQuery
                .HttpAction(actionName)
                .MethodAsync<TResource, TResult>(
                    onUpdated: onUpdated,
                    onContent: onUpdatedBody,
                    onContents: onContents,
                    onContentObjects: onContentObjects,
                    onNotFound: onNotFound,
                    onUnauthorized: onUnauthorized,
                    onFailure: onFailure,
                    onNotOverriddenResponse: onNotOverriddenResponse,
                    onResponse: onResponse);
        }

        #region Response types

        public interface IReturnResult
        {
            TResult GetResultCasted<TResult>();
        }

        private class AttachedHttpResponseMessage<TResult> : IHttpResponse, IReturnResult
        {
            public TResult Result { get; }

            public AttachedHttpResponseMessage(TResult result)
            {
                this.Result = result;
            }

            public IHttpResponse Inner { get; }

            public IHttpRequest Request => Inner.Request;

            public HttpStatusCode StatusCode
            {
                get => Inner.StatusCode;
                set
                {
                    Inner.StatusCode = value;
                }
            }

            public string ReasonPhrase { get => Inner.ReasonPhrase; set => Inner.ReasonPhrase = value; }

            public IDictionary<string, string[]> Headers => Inner.Headers;

            public AttachedHttpResponseMessage(TResult result, IHttpResponse inner)
            {
                this.Result = result;
                this.Inner = inner;
            }

            public TResult1 GetResultCasted<TResult1>()
            {
                return (TResult1)(this.Result as object);
            }

            public void AddCookie(string cookieKey, string cookieValue, TimeSpan? expireTime)
            {
                Inner.AddCookie(cookieKey, cookieValue, expireTime);
            }

            public Task WriteResponseAsync(HttpContext context)
            {
                return Inner.WriteResponseAsync(context);
            }

            public void WritePreamble(HttpContext context)
            {
                Inner.WritePreamble(context);
            }

            public Task WriteResponseAsync(Stream stream)
            {
                return Inner.WriteResponseAsync(stream);
            }
        }

        private interface IDidNotOverride
        {
            void OnFailure();
        }

        private class NoOverrideHttpResponseMessage<TResource> : IHttpResponse, IDidNotOverride
        {
            private Type typeOfResponse;
            private IHttpRequest request;
            public NoOverrideHttpResponseMessage(Type typeOfResponse, IHttpRequest request)
            {
                this.typeOfResponse = typeOfResponse;
                this.request = request;
            }

            public IHttpRequest Request => throw new NotImplementedException();

            public HttpStatusCode StatusCode { get; set; }

            public string ReasonPhrase { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public IDictionary<string, string[]> Headers => throw new NotImplementedException();

            public void OnFailure()
            {
                var message = $"Failed to override response for: [{request.Method.Method}] `{typeof(TResource).FullName}`.`{typeOfResponse.Name}`";
                throw new Exception(message);
            }

            public void AddCookie(string cookieKey, string cookieValue, TimeSpan? expireTime)
            {
                throw new NotImplementedException();
            }

            public Task WriteResponseAsync(HttpContext context)
            {
                throw new NotImplementedException();
            }

            public void WritePreamble(HttpContext context)
            {
                throw new NotImplementedException();
            }

            public Task WriteResponseAsync(Stream stream)
            {
                throw new NotImplementedException();
            }
        }

        private static void ContentResponse<TResource, TResult>(this IApplication application,
            Func<TResource, TResult> onContent)
        {
            application.SetInstigator(
                typeof(ContentResponse),
                (thisAgain, requestAgain, paramInfo, onSuccess) =>
                {
                    ContentResponse created =
                        (content, mimeType) =>
                        {
                            if (onContent.IsDefaultOrNull())
                                return FailureToOverride<TResource>(typeof(ContentResponse), thisAgain, requestAgain, paramInfo, onSuccess);
                            if (!(content is TResource))
                                throw new Exception($"Could not cast {content.GetType().FullName} to {typeof(TResource).FullName}.");
                            var resource = (TResource)content;
                            var result = onContent(resource);
                            return new AttachedHttpResponseMessage<TResult>(result);
                        };
                    return onSuccess(created);
                },
                onContent.IsDefaultOrNull());
        }

        private static void HtmlResponse<TResource, TResult>(this IApplication application,
            Func<string, TResult> onHtml)
        {
            application.SetInstigator(
                typeof(HtmlResponse),
                (thisAgain, requestAgain, paramInfo, onSuccess) =>
                {
                    HtmlResponse created =
                        (content) =>
                        {
                            if (onHtml.IsDefaultOrNull())
                                return FailureToOverride<TResource>(typeof(EastFive.Api.HtmlResponse), thisAgain, requestAgain, paramInfo, onSuccess);
                            var result = onHtml(content);
                            return new AttachedHttpResponseMessage<TResult>(result);
                        };
                    return onSuccess(created);
                });
        }

        //private static void XlsResponse<TResource, TResult>(this IApplication application,
        //    Func<byte[], string, TResult> onXls)
        //{
        //    application.SetInstigator(
        //        typeof(XlsxResponse),
        //        (thisAgain, requestAgain, paramInfo, onSuccess) =>
        //        {
        //            XlsxResponse created =
        //                (content, name) =>
        //                {
        //                    if (onXls.IsDefaultOrNull())
        //                        return FailureToOverride<TResource>(typeof(EastFive.Api.XlsxResponse), thisAgain, requestAgain, paramInfo, onSuccess);
        //                    var result = onXls(content, name);
        //                    return new AttachedHttpResponseMessage<TResult>(result);
        //                };
        //            return onSuccess(created);
        //        });
        //}

        private static void BadRequestResponse<TResource, TResult>(this IApplication application,
            Func<TResult> onBadRequest)
        {
            application.SetInstigator(
                typeof(BadRequestResponse),
                (thisAgain, requestAgain, paramInfo, onSuccess) =>
                {
                    BadRequestResponse badRequest =
                        () =>
                        {
                            if (onBadRequest.IsDefaultOrNull())
                                return FailureToOverride<TResource>(typeof(BadRequestResponse), thisAgain, requestAgain, paramInfo, onSuccess);
                            var result = onBadRequest();
                            return new AttachedHttpResponseMessage<TResult>(result);
                        };
                    return onSuccess(badRequest);
                });
        }
        
        private static void RefNotFoundTypeResponse<TResult>(this IApplication application,
            Func<Type, TResult> referencedDocDoesNotExists)
        {
            application.SetInstigatorGeneric(
                typeof(ReferencedDocumentDoesNotExistsResponse<>),
                (type, thisAgain, requestAgain, paramInfo, onSuccess) =>
                {
                    var scope = new CallbackWrapperReferencedDocumentDoesNotExistsResponse<TResult>(referencedDocDoesNotExists,
                        thisAgain, requestAgain, paramInfo, onSuccess);
                    var multipartResponseMethodInfoGeneric = typeof(CallbackWrapperReferencedDocumentDoesNotExistsResponse<TResult>)
                        .GetMethod(nameof(CallbackWrapperReferencedDocumentDoesNotExistsResponse<TResult>.RefNotFoundTypeResponseGeneric), BindingFlags.Public | BindingFlags.Instance);
                    var multipartResponseMethodInfoBound = multipartResponseMethodInfoGeneric
                        .MakeGenericMethod(type.GenericTypeArguments);
                    var dele = Delegate.CreateDelegate(type, scope, multipartResponseMethodInfoBound);
                    return onSuccess((object)dele);
                },
                referencedDocDoesNotExists.IsDefaultOrNull());
        }

        public class CallbackWrapperReferencedDocumentDoesNotExistsResponse<TResult>
        {
            private Func<Type, TResult> referencedDocDoesNotExists;
            private HttpApplication thisAgain;
            private IHttpRequest requestAgain;
            private ParameterInfo paramInfo;
            private Func<object, Task<IHttpResponse>> onSuccess;
            
            public CallbackWrapperReferencedDocumentDoesNotExistsResponse(Func<Type, TResult> referencedDocDoesNotExists,
                HttpApplication thisAgain, IHttpRequest requestAgain, ParameterInfo paramInfo, Func<object, Task<IHttpResponse>> onSuccess)
            {
                this.referencedDocDoesNotExists = referencedDocDoesNotExists;
                this.thisAgain = thisAgain;
                this.requestAgain = requestAgain;
                this.paramInfo = paramInfo;
                this.onSuccess = onSuccess;
            }

            public IHttpResponse RefNotFoundTypeResponseGeneric<TResource>()
            {
                if (referencedDocDoesNotExists.IsDefaultOrNull())
                    return FailureToOverride<TResource>(typeof(ReferencedDocumentDoesNotExistsResponse<>), thisAgain, requestAgain, paramInfo, onSuccess);

                var result = referencedDocDoesNotExists(typeof(TResource));
                return new AttachedHttpResponseMessage<TResult>(result);
            }
        }

        private class InstigatorGenericWrapper1<TCallback, TResult, TResource>
        {
            private Type type;
            private HttpApplication httpApp;
            private IHttpRequest request;
            private ParameterInfo paramInfo;
            private TCallback callback;
            private Func<object, Task<IHttpResponse>> onSuccess;

            public InstigatorGenericWrapper1(Type type,
                HttpApplication httpApp, IHttpRequest request, ParameterInfo paramInfo,
                TCallback callback, Func<object, Task<IHttpResponse>> onSuccess)
            {
                this.type = type;
                this.httpApp = httpApp;
                this.request = request;
                this.paramInfo = paramInfo;
                this.callback = callback;
                this.onSuccess = onSuccess;
            }

            IHttpResponse ContentTypeResponse(object content, string mediaType = default(string))
            {
                if (callback.IsDefault())
                    return FailureToOverride<TResource>(
                        type, this.httpApp, this.request, this.paramInfo, onSuccess);
                var contentObj = (object)content;
                var contentType = (TResource)contentObj;
                var callbackObj = (object)callback;
                var callbackDelegate = (Delegate)callbackObj;
                var resultObj = callbackDelegate.DynamicInvoke(contentType, mediaType);
                var result = (TResult)resultObj;
                return new AttachedHttpResponseMessage<TResult>(result);
            }

            IHttpResponse CreatedBodyResponse(object content, string mediaType = default(string))
            {
                if (callback.IsDefault())
                    return FailureToOverride<TResource>(
                        type, this.httpApp, this.request, this.paramInfo, onSuccess);
                var contentObj = (object)content;
                var contentType = (TResource)contentObj;
                var callbackObj = (object)callback;
                var callbackDelegate = (Delegate)callbackObj;
                var resultObj = callbackDelegate.DynamicInvoke(contentType, mediaType);
                var result = (TResult)resultObj;
                return new AttachedHttpResponseMessage<TResult>(result);
            }
        }

        private static void CreatedBodyResponse<TResource, TResult>(this IApplication application,
            Func<TResource, string, TResult> onCreated)
        {
            application.SetInstigatorGeneric(
                typeof(CreatedBodyResponse<>),
                (type, httpApp, request, paramInfo, onSuccess) =>
                {
                    type = typeof(CreatedBodyResponse<>).MakeGenericType(typeof(TResource));
                    var wrapperConcreteType = typeof(InstigatorGenericWrapper1<,,>).MakeGenericType(
                        //type.GenericTypeArguments
                        //    .Append(typeof(Func<TResource, string, TResult>))
                        typeof(Func<TResource, string, TResult>)
                            .AsArray()
                            .Append(typeof(TResult))
                            .Append(typeof(TResource))
                            .ToArray());
                    var wrapperInstance = Activator.CreateInstance(wrapperConcreteType,
                        new object[] { type, httpApp, request, paramInfo, onCreated, onSuccess });
                    var dele = Delegate.CreateDelegate(type, wrapperInstance, "CreatedBodyResponse", false);
                    return onSuccess(dele);
                },
                onCreated.IsDefaultOrNull());
        }

        private static void ContentTypeResponse<TResource, TResult>(this IApplication application,
            Func<TResource, string, TResult> onCreated)
        {
            application.SetInstigatorGeneric(
                typeof(EastFive.Api.ContentTypeResponse<>),
                (type, httpApp, request, paramInfo, onSuccess) =>
                {
                    //type = typeof(ContentTypeResponse<>).MakeGenericType(typeof(TResource));
                    //var wrapperConcreteType = typeof(InstigatorGenericWrapper1<,,>).MakeGenericType(
                    //    typeof(Func<TResource, string, TResult>)
                    //        .AsArray()
                    //        .Append(typeof(TResult))
                    //        .Append(typeof(TResource))
                    //        .ToArray());
                    //var wrapperInstance = Activator.CreateInstance(wrapperConcreteType,
                    //    new object[] { type, httpApp, request, paramInfo, onCreated, onSuccess });

                    var resourceType = type.GenericTypeArguments.First();
                    var funcOfRes = typeof(Func<,,>).MakeGenericType(new[] { resourceType, typeof(string), typeof(TResult) });
                    var wrapperConcreteType = typeof(InstigatorGenericWrapper1<,,>).MakeGenericType(
                        funcOfRes
                            .AsArray()
                            .Append(typeof(TResult))
                            .Append(resourceType)
                            .ToArray());
                    var wrapperInstance = Activator.CreateInstance(wrapperConcreteType,
                        new object[] { type, httpApp, request, paramInfo, onCreated, onSuccess });

                    var dele = Delegate.CreateDelegate(type, wrapperInstance, "ContentTypeResponse", false);
                    return onSuccess(dele);
                },
                onCreated.IsDefaultOrNull());
        }

        private static void CreatedResponse<TResource, TResult>(this IApplication application,
            Func<TResult> onCreated)
        {
            application.SetInstigator(
                typeof(CreatedResponse),
                (thisAgain, requestAgain, paramInfo, onSuccess) =>
                {
                    CreatedResponse created =
                        () =>
                        {
                            if (onCreated.IsDefaultOrNull())
                                return FailureToOverride<TResource>(
                                    typeof(CreatedResponse),
                                    thisAgain, requestAgain, paramInfo, onSuccess);
                            return new AttachedHttpResponseMessage<TResult>(onCreated());
                        };
                    return onSuccess(created);
                },
                onCreated.IsDefaultOrNull());
        }

        private static void RedirectResponse<TResource, TResult>(this IApplication application,
            Func<Uri, TResult> onRedirect)
        {
            application.SetInstigator(
                typeof(RedirectResponse),
                (thisAgain, requestAgain, paramInfo, onSuccess) =>
                {
                    RedirectResponse redirect =
                        (where) =>
                        {
                            if (onRedirect.IsDefaultOrNull())
                                return FailureToOverride<TResource>(
                                    typeof(RedirectResponse),
                                    thisAgain, requestAgain, paramInfo, onSuccess);
                            return new AttachedHttpResponseMessage<TResult>(onRedirect(where));
                        };
                    return onSuccess(redirect);
                });
        }

        private static void AlreadyExistsResponse<TResource, TResult>(this IApplication application,
            Func<TResult> onAlreadyExists)
        {
            if (!onAlreadyExists.IsDefaultOrNull())
                application.SetInstigator(
                    typeof(AlreadyExistsResponse),
                    (thisAgain, requestAgain, paramInfo, onSuccess) =>
                    {
                        AlreadyExistsResponse exists =
                            () =>
                            {
                                if (onAlreadyExists.IsDefaultOrNull())
                                    return FailureToOverride<TResource>(
                                        typeof(AlreadyExistsResponse),
                                        thisAgain, requestAgain, paramInfo, onSuccess);
                                return new AttachedHttpResponseMessage<TResult>(onAlreadyExists());
                            };
                        return onSuccess(exists);
                    });
        }


        private static void NotImplementedResponse<TResource, TResult>(this IApplication application,
            Func<TResult> onNotImplemented)
        {
            application.SetInstigator(
                typeof(NotImplementedResponse),
                (thisAgain, requestAgain, paramInfo, onSuccess) =>
                {
                    NotImplementedResponse notImplemented =
                        () =>
                        {
                            if (onNotImplemented.IsDefaultOrNull())
                                return FailureToOverride<TResource>(
                                    typeof(NotImplementedResponse),
                                    thisAgain, requestAgain, paramInfo, onSuccess);
                            return new AttachedHttpResponseMessage<TResult>(onNotImplemented());
                        };
                    return onSuccess(notImplemented);
                });
        }

        private static void MultipartContentResponse<TResource, TResult>(this IApplication application,
            Func<TResource[], TResult> onContents)
        {

            //application.SetInstigatorGeneric(
            //    typeof(MultipartResponseAsync<>),
            //    (type, thisAgain, requestAgain, paramInfo, onSuccess) =>
            //    {
            //        var callbackWrapperType = typeof(CallbackWrapper<,>).MakeGenericType(
            //            paramInfo.ParameterType.GenericTypeArguments.Append(typeof(TResult)).ToArray());

            //        //  new CallbackWrapper<TResource, TResult>(onContents, null, thisAgain, requestAgain, paramInfo, onSuccess);
            //        var instantiationParams = new object[]
            //            {
            //                onContents,
            //                null,
            //                thisAgain,
            //                requestAgain,
            //                paramInfo,
            //                onSuccess,
            //            };
            //        var scope = Activator.CreateInstance(callbackWrapperType, instantiationParams);

            //        var multipartResponseMethodInfoGeneric = callbackWrapperType.GetMethod("MultipartResponseAsyncGeneric", BindingFlags.Public | BindingFlags.Instance);
            //        var multipartResponseMethodInfoBound = multipartResponseMethodInfoGeneric;
            //        var dele = Delegate.CreateDelegate(type, scope, multipartResponseMethodInfoBound);
            //        return onSuccess((object)dele);
            //    });
        }

        private static void MultipartContentObjectResponse<TResource, TResult>(this IApplication application,
            Func<object[], TResult> onContents)
        {
            //application.SetInstigator(
            //    typeof(EastFive.Api.MultipartAcceptArrayResponseAsync),
            //    (thisAgain, requestAgain, paramInfo, onSuccess) =>
            //    {
            //        EastFive.Api.MultipartAcceptArrayResponseAsync created =
            //            (contents) =>
            //            {
            //                var resources = contents.ToArray();
            //                // TODO: try catch
            //                //if (!(content is TResource))
            //                //    Assert.Fail($"Could not cast {content.GetType().FullName} to {typeof(TResource).FullName}.");

            //                if (onContents.IsDefaultOrNull())
            //                    return FailureToOverride<TResource>(
            //                        typeof(MultipartAcceptArrayResponseAsync),
            //                        thisAgain, requestAgain, paramInfo, onSuccess).AsTask();
            //                var result = onContents(resources);
            //                return new AttachedHttpResponseMessage<TResult>(result).ToTask<IHttpResponse>();
            //            };
            //        return onSuccess(created);
            //    });

            //application.SetInstigatorGeneric(
            //    typeof(MultipartResponseAsync<>),
            //    (type, thisAgain, requestAgain, paramInfo, onSuccess) =>
            //    {
            //        var callbackWrapperInstance = typeof(CallbackWrapper<,>).MakeGenericType(
            //            new Type[] { type.GenericTypeArguments.First(), typeof(TResult) });
            //        //var scope = new CallbackWrapper<TResource, TResult>(null, onContents, thisAgain, requestAgain, paramInfo, onSuccess);
            //        var scope = Activator.CreateInstance(callbackWrapperInstance, 
            //            new object[] { null, onContents, thisAgain, requestAgain, paramInfo, onSuccess });
            //        var multipartResponseMethodInfoGeneric = callbackWrapperInstance.GetMethod("MultipartResponseAsyncGeneric", BindingFlags.Public | BindingFlags.Instance);
            //        var multipartResponseMethodInfoBound = multipartResponseMethodInfoGeneric; // multipartResponseMethodInfoGeneric.MakeGenericMethod(type.GenericTypeArguments);
            //        var dele = Delegate.CreateDelegate(type, scope, multipartResponseMethodInfoBound);
            //        return onSuccess((object)dele);
            //    });
        }

        public class CallbackWrapper<TResource, TResult>
        {
            private Func<TResource[], TResult> callback;
            private Func<object[], TResult> callbackObjs;
            private HttpApplication thisAgain;
            private IHttpRequest requestAgain;
            private ParameterInfo paramInfo;
            private Func<object, Task<IHttpResponse>> onSuccess;
            
            public CallbackWrapper(Func<TResource[], TResult> onContents, Func<object[], TResult> callbackObjs,
                HttpApplication thisAgain, IHttpRequest requestAgain, ParameterInfo paramInfo,
                Func<object, Task<IHttpResponse>> onSuccess)
            {
                this.callback = onContents;
                this.callbackObjs = callbackObjs;
                this.thisAgain = thisAgain;
                this.requestAgain = requestAgain;
                this.paramInfo = paramInfo;
                this.onSuccess = onSuccess;
            }

            public async Task<IHttpResponse> MultipartResponseAsyncGeneric(IEnumerableAsync<TResource> resources)
            {
                if (!callback.IsDefaultOrNull())
                {
                    var resourcesArray = await resources.ToArrayAsync();
                    var result = callback(resourcesArray);
                    return new AttachedHttpResponseMessage<TResult>(result);
                }
                if (!callbackObjs.IsDefaultOrNull())
                {
                    var resourcesArray = await resources.ToArrayAsync();
                    var result = callbackObjs(resourcesArray.Cast<object>().ToArray());
                    return new AttachedHttpResponseMessage<TResult>(result);
                }
                return FailureToOverride<TResource>(typeof(EastFive.Api.MultipartAsyncResponse<>), 
                    thisAgain, requestAgain, paramInfo, onSuccess);
            }
            
        }

        private static void NoContentResponse<TResource, TResult>(this IApplication application,
            Func<TResult> onNoContent)
        {
            application.SetInstigator(
                typeof(NoContentResponse),
                (thisAgain, requestAgain, paramInfo, onSuccess) =>
                {
                    NoContentResponse created =
                        () =>
                        {
                            if (onNoContent.IsDefaultOrNull())
                                return FailureToOverride<TResource>(typeof(NoContentResponse), thisAgain, requestAgain, paramInfo, onSuccess);
                            var result = onNoContent();
                            return new AttachedHttpResponseMessage<TResult>(result);
                        };
                    return onSuccess(created);
                });
        }

        private static void NotFoundResponse<TResource, TResult>(this IApplication application,
            Func<TResult> onNotFound)
        {
            application.SetInstigator(
                typeof(NotFoundResponse),
                (thisAgain, requestAgain, paramInfo, onSuccess) =>
                {
                    NotFoundResponse notFound =
                        () =>
                        {
                            if (onNotFound.IsDefaultOrNull())
                                return FailureToOverride<TResource>(typeof(NotFoundResponse), thisAgain, requestAgain, paramInfo, onSuccess);
                            var result = onNotFound();
                            return new AttachedHttpResponseMessage<TResult>(result);
                        };
                    return onSuccess(notFound);
                });
        }

        private static void UnauthorizedResponse<TResource, TResult>(this IApplication application,
            Func<TResult> onUnauthorized)
        {
            application.SetInstigator(
                typeof(UnauthorizedResponse),
                (thisAgain, requestAgain, paramInfo, onSuccess) =>
                {
                    UnauthorizedResponse created =
                        () =>
                        {
                            if (onUnauthorized.IsDefaultOrNull())
                                return FailureToOverride<TResource>(typeof(UnauthorizedResponse), thisAgain, requestAgain, paramInfo, onSuccess);
                            var result = onUnauthorized();
                            return new AttachedHttpResponseMessage<TResult>(result);
                        };
                    return onSuccess(created);
                });
        }

        private static void GeneralConflictResponse<TResource, TResult>(this IApplication application,
            Func<string, TResult> onGeneralConflictResponse)
        {
            application.SetInstigator(
                typeof(GeneralConflictResponse),
                (thisAgain, requestAgain, paramInfo, onSuccess) =>
                {
                    GeneralConflictResponse created =
                        (reason) =>
                        {
                            if (onGeneralConflictResponse.IsDefaultOrNull())
                                return FailureToOverride<TResource>(typeof(GeneralConflictResponse), thisAgain, requestAgain, paramInfo, onSuccess);
                            var result = onGeneralConflictResponse(reason);
                            return new AttachedHttpResponseMessage<TResult>(result);
                        };
                    return onSuccess(created);
               });
        }

        private static void ExecuteBackgroundResponse<TResource, TResult>(this IApplication application,
            Func<IExecuteAsync, Task<TResult>> onExecuteBackgroundResponse)
        {
            application.SetInstigator(
                typeof(ExecuteBackgroundResponseAsync),
                (thisAgain, requestAgain, paramInfo, onSuccess) =>
                {
                    ExecuteBackgroundResponseAsync created =
                        async (executionContent) =>
                        {
                            if (onExecuteBackgroundResponse.IsDefaultOrNull())
                                return FailureToOverride<TResource>(typeof(ExecuteBackgroundResponseAsync), thisAgain, requestAgain, paramInfo, onSuccess);
                            var result = await onExecuteBackgroundResponse(executionContent);
                            return new AttachedHttpResponseMessage<TResult>(result);
                        };
                    return onSuccess(created);
                });
        }


        private static IHttpResponse FailureToOverride<TResource>(Type typeOfResponse,
            HttpApplication application,
            IHttpRequest request, ParameterInfo paramInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            return new NoOverrideHttpResponseMessage<TResource>(paramInfo.ParameterType, request);
        }

        #endregion


        //public static async Task<TResult> UrlAsync<TResource, TResultInner, TResult>(this IApplication application,
        //        IInvokeApplication invokeApplication,
        //        HttpMethod method, Uri location,
        //    Func<TResultInner, TResult> onExecuted,

        //    Func<TResource, TResult> onContent = default(Func<TResource, TResult>),
        //    Func<TResource[], TResult> onContents = default(Func<TResource[], TResult>),
        //    Func<object[], TResult> onContentObjects = default(Func<object[], TResult>),
        //    Func<string, TResult> onHtml = default(Func<string, TResult>),
        //    Func<TResult> onCreated = default(Func<TResult>),
        //    Func<TResource, string, TResult> onCreatedBody = default(Func<TResource, string, TResult>),
        //    Func<TResult> onUpdated = default(Func<TResult>),

        //    Func<Uri, TResult> onRedirect = default(Func<Uri, TResult>),

        //    Func<TResult> onBadRequest = default(Func<TResult>),
        //    Func<TResult> onUnauthorized = default(Func<TResult>),
        //    Func<TResult> onExists = default(Func<TResult>),
        //    Func<TResult> onNotFound = default(Func<TResult>),
        //    Func<Type, TResult> onRefDoesNotExistsType = default(Func<Type, TResult>),
        //    Func<string, TResult> onFailure = default(Func<string, TResult>),

        //    Func<TResult> onNotImplemented = default(Func<TResult>),
        //    Func<IExecuteAsync, Task<TResult>> onExecuteBackground = default(Func<IExecuteAsync, Task<TResult>>))
        //{
        //    throw new NotImplementedException();
        //    //var request = invokeApplication.GetRequest<TResource>();
        //    ////request.Method = method;
        //    ////request.RequestUri = location;

        //    //application.CreatedResponse<TResource, TResult>(onCreated);
        //    //application.CreatedBodyResponse<TResource, TResult>(onCreatedBody);
        //    //application.BadRequestResponse<TResource, TResult>(onBadRequest);
        //    //application.AlreadyExistsResponse<TResource, TResult>(onExists);
        //    //application.RefNotFoundTypeResponse(onRefDoesNotExistsType);
        //    //application.RedirectResponse<TResource, TResult>(onRedirect);
        //    //application.NotImplementedResponse<TResource, TResult>(onNotImplemented);

        //    //application.ContentResponse(onContent);
        //    //application.ContentTypeResponse<TResource, TResult>((body, contentType) => onContent(body));
        //    //application.MultipartContentResponse(onContents);
        //    //if (!onContentObjects.IsDefaultOrNull())
        //    //    application.MultipartContentObjectResponse<TResource, TResult>(onContentObjects);
        //    //application.NotFoundResponse<TResource, TResult>(onNotFound);
        //    //application.HtmlResponse<TResource, TResult>(onHtml);

        //    //application.NoContentResponse<TResource, TResult>(onUpdated);
        //    //application.UnauthorizedResponse<TResource, TResult>(onUnauthorized);
        //    //application.GeneralConflictResponse<TResource, TResult>(onFailure);
        //    //application.ExecuteBackgroundResponse<TResource, TResult>(onExecuteBackground);

        //    //var response = await application.SendAsync(request.Request);

        //    //if (response is IDidNotOverride)
        //    //    (response as IDidNotOverride).OnFailure();

        //    //if (!(response is IReturnResult))
        //    //    throw new Exception($"Failed to override response with status code `{response.StatusCode}` for {typeof(TResource).FullName}\nResponse:{response.ReasonPhrase}");

        //    //var attachedResponse = response as IReturnResult;
        //    //var result = attachedResponse.GetResultCasted<TResultInner>();
        //    //return onExecuted(result);
        //}
    }
}
