using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using EastFive;
using EastFive.Extensions;
using EastFive.Serialization.Json;
using Newtonsoft.Json;

namespace EastFive.Net
{
    public static class HttpClientExtensions
    {
        public static async Task<TResult> HttpClientGetResponseAsync<TResult>(this Uri location,
            Func<HttpResponseMessage, TResult> onSuccess,
            Func<string, TResult> onFailure = default,
            Func<HttpStatusCode, string, TResult> onFailureWithBody = default,
            Func<HttpStatusCode, Func<Task<string>>, TResult> onResponseFailure = default,
            Func<TResult> onTimeout = default,
            Func<HttpRequestMessage, HttpRequestMessage> mutateRequest = default)
        {
            using (var httpClient = new HttpClient())
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, location))
                {
                    var mutatedRequest = MutateRequest();
                    try
                    {
                        return await httpClient
                            .SendAsync(mutatedRequest)
                            .IsSuccessStatusCodeAsync(
                                responseSuccess =>
                                {
                                    return onSuccess(responseSuccess);
                                },
                                onFailureWithBody: onFailureWithBody,
                                onResponseFailure: onResponseFailure,
                                onFailure: onFailure,
                                onTimeout:onTimeout);
                    }
                    catch (System.Net.Http.HttpRequestException ex)
                    {
                        if (onFailure.IsNotDefaultOrNull())
                            return onFailure($"Connection Failure: {ex.GetType().FullName}:{ex.Message}");

                        throw;
                    }
                    catch (Exception exGeneral)
                    {
                        if (onFailure.IsNotDefaultOrNull())
                            return onFailure(exGeneral.Message);

                        throw;
                    }

                    HttpRequestMessage MutateRequest()
                    {
                        if (mutateRequest.IsDefaultOrNull())
                            return request;
                        return mutateRequest(request);
                    }
                }
            }
        }

        public static async Task<TResult> HttpClientGetResourceAsync<TResource, TResult>(this Uri location,
            Func<TResource, TResult> onSuccess,
            Func<string, TResult> onFailure = default,
            Func<HttpStatusCode, string, TResult> onFailureWithBody = default,
            Func<HttpStatusCode, Func<Task<string>>, TResult> onResponseFailure = default,
            Func<string, string, TResult> onFailureToParse = default,
            Func<TResult> onTimeout = default,
            Func<HttpRequestMessage, HttpRequestMessage> mutateRequest = default)
        {
            return await await location.HttpClientGetResponseAsync(
                responseSuccess => ParseResourceResponse(responseSuccess,
                    onSuccess: onSuccess,
                    onFailure: onFailure,
                    onFailureWithBody: onFailureWithBody,
                    onResponseFailure: onResponseFailure,
                    onFailureToParse: onFailureToParse),
                onFailureWithBody: onFailureWithBody.AsAsyncFunc(),
                onResponseFailure: onResponseFailure.AsAsyncFunc(),
                onFailure: onFailure.AsAsyncFunc(),
                onTimeout: onTimeout.AsAsyncFunc(),
                    mutateRequest: mutateRequest);
        }

        public static async Task<TResult> HttpClientGetResourceAsync<TResource, TResult>(this Uri location,
            Func<TResource, HttpResponseMessage, TResult> onSuccess,
            Func<string, TResult> onFailure = default,
            Func<HttpStatusCode, string, TResult> onFailureWithBody = default,
            Func<HttpStatusCode, Func<Task<string>>, TResult> onResponseFailure = default,
            Func<string, string, TResult> onFailureToParse = default,
            Func<HttpRequestMessage, HttpRequestMessage> mutateRequest = default)
        {
            return await await location.HttpClientGetResponseAsync(
                responseSuccess => ParseResourceResponse(responseSuccess,
                    onSuccess:(TResource resource) => onSuccess(resource, responseSuccess),
                    onFailure: onFailure,
                    onFailureWithBody: onFailureWithBody,
                    onResponseFailure: onResponseFailure,
                    onFailureToParse: onFailureToParse),
                onFailureWithBody: onFailureWithBody.AsAsyncFunc(),
                onResponseFailure: onResponseFailure.AsAsyncFunc(),
                onFailure: onFailure.AsAsyncFunc(),
                    mutateRequest:mutateRequest);
        }

        public static async Task<TResult> HttpClientGetResourceAuthenticatedAsync<TResponse, TResult>(this Uri location,
                string authToken, string tokenType,
            Func<TResponse, HttpResponseMessage, TResult> onSuccess,
            Func<string, TResult> onFailure = default,
            Func<HttpStatusCode, string, TResult> onFailureWithBody = default,
            Func<HttpStatusCode, Func<Task<string>>, TResult> onResponseFailure = default,
            Func<string, string, TResult> onFailureToParse = default,
                Func<HttpRequestMessage, HttpRequestMessage> mutateRequest = default,
                Func<HttpStatusCode, string, Task<(bool, string)>> didTokenGetRefreshed = default)
        {
            return await await location.HttpClientGetResourceAsync(
                onSuccess: onSuccess.AsAsyncFunc(),
                onFailure: onFailure.AsAsyncFunc(),
                onFailureWithBody: (statusCode, body) =>
                {
                    return TryRefreshAsync(statusCode, body,
                        rerun: (newToken) =>
                        {
                            return location.HttpClientGetResourceAuthenticatedAsync(
                                     newToken, tokenType,
                                     didTokenGetRefreshed: (s, b) => (false, string.Empty).AsTask(),
                                 onSuccess: onSuccess,
                                 onFailure: onFailure,
                                 onFailureWithBody: onFailureWithBody,
                                 onResponseFailure: onResponseFailure,
                                 onFailureToParse: onFailureToParse,
                                 mutateRequest: mutateRequest);
                        },
                        onFailureWithBody: onFailureWithBody,
                        onFailure: onFailure,
                        didTokenGetRefreshed: didTokenGetRefreshed);
                },
                onResponseFailure: onResponseFailure.AsAsyncFunc(),
                onFailureToParse: onFailureToParse.AsAsyncFunc(),
                    mutateRequest: (HttpRequestMessage request) =>
                    {
                        request.Headers.Add("Authorization", $"{tokenType} {authToken}");
                        return request;
                    });
        }

        public static async Task<TResult> HttpClientGetResponseAuthenticatedAsync<TResult>(this Uri location,
                string authToken, string tokenType,
            Func<HttpResponseMessage, TResult> onSuccess,
            Func<string, TResult> onFailure = default,
            Func<HttpStatusCode, string, TResult> onFailureWithBody = default,
            Func<HttpStatusCode, Func<Task<string>>, TResult> onResponseFailure = default,
            Func<string, string, TResult> onFailureToParse = default,
                Func<HttpRequestMessage, HttpRequestMessage> mutateRequest = default,
                Func<HttpStatusCode, string, Task<(bool, string)>> didTokenGetRefreshed = default)
        {
            return await await location.HttpClientGetResponseAsync(
                onSuccess: onSuccess.AsAsyncFunc(),
                onFailure: onFailure.AsAsyncFunc(),
                onFailureWithBody: (statusCode, body) =>
                {
                    return TryRefreshAsync(statusCode, body,
                        rerun: (newToken) => location.HttpClientGetResponseAuthenticatedAsync(
                                     newToken, tokenType,
                                     didTokenGetRefreshed: (s, b) => (false, string.Empty).AsTask(),
                                 onSuccess: onSuccess,
                                 onFailure: onFailure,
                                 onFailureWithBody: onFailureWithBody,
                                 onResponseFailure: onResponseFailure,
                                 onFailureToParse: onFailureToParse,
                                 mutateRequest: mutateRequest),
                        onFailureWithBody: onFailureWithBody,
                        onFailure: onFailure,
                        didTokenGetRefreshed: didTokenGetRefreshed);
                },
                onResponseFailure: onResponseFailure.AsAsyncFunc(),
                    mutateRequest: (HttpRequestMessage request) =>
                    {
                        request.Headers.Add("Authorization", $"{tokenType} {authToken}");
                        return request;
                    });
        }

        #region POST

        public static async Task<TResult> HttpClientPostResourceAsync<TResource, TResponse, TResult>(this Uri location,
                TResource resource,
                string authToken, string tokenType,
            Func<TResponse, HttpResponseMessage, TResult> onSuccess,
            Func<string, TResult> onFailure = default,
            Func<HttpStatusCode, string, TResult> onFailureWithBody = default,
            Func<HttpStatusCode, Func<Task<string>>, TResult> onResponseFailure = default,
            Func<string, string, TResult> onFailureToParse = default,
                Func<HttpRequestMessage, HttpRequestMessage> mutateRequest = default,
                Func<HttpStatusCode, string, Task<(bool, string)>> didTokenGetRefreshed = default)
        {
            return await await location.HttpClientPostResourceAsync(resource,
                onSuccess: onSuccess.AsAsyncFunc(),
                onFailure: onFailure.AsAsyncFunc(),
                onFailureWithBody: (statusCode, body) => TryRefreshAsync(statusCode, body,
                    rerun: (newToken) => location.HttpClientPostResourceAsync(resource,
                                 newToken, tokenType,
                                 didTokenGetRefreshed: (s, b) => (false, string.Empty).AsTask(),
                             onSuccess: onSuccess,
                             onFailure: onFailure,
                             onFailureWithBody: onFailureWithBody,
                             onResponseFailure: onResponseFailure,
                             onFailureToParse: onFailureToParse,
                             mutateRequest: mutateRequest),
                    onFailureWithBody: onFailureWithBody,
                    onFailure: onFailure,
                    didTokenGetRefreshed: didTokenGetRefreshed),
                onResponseFailure: onResponseFailure.AsAsyncFunc(),
                onFailureToParse: onFailureToParse.AsAsyncFunc(),
                    mutateRequest:(HttpRequestMessage request) =>
                    {
                        request.Headers.Add("Authorization", $"{tokenType} {authToken}");
                        return request;
                    });
        }

        public static async Task<TResult> HttpClientPostDynamicRequestAsync<TResponse, TResult>(this Uri location,
                Func<HttpRequestMessage, (HttpRequestMessage, Action)> populateRequest,
                string authToken, string tokenType,
            Func<TResponse, HttpResponseMessage, TResult> onSuccess,
            Func<string, TResult> onFailure = default,
            Func<HttpStatusCode, string, TResult> onFailureWithBody = default,
            Func<HttpStatusCode, Func<Task<string>>, TResult> onResponseFailure = default,
            Func<string, string, TResult> onFailureToParse = default,
                Func<HttpRequestMessage, HttpRequestMessage> mutateRequest = default,
                Func<HttpStatusCode, string, Task<(bool, string)>> didTokenGetRefreshed = default)
        {
            return await await location.HttpClientPostDynamicRequestAsync(
                populateRequest:(request) =>
                {
                    request.Headers.Add("Authorization", $"{tokenType} {authToken}");
                    return populateRequest(request);
                },
                onSuccess: onSuccess.AsAsyncFunc(),
                onFailure: onFailure.AsAsyncFunc(),
                onFailureWithBody: (statusCode, body) => TryRefreshAsync(statusCode, body,
                    rerun:(newToken) => location.HttpClientPostResourceAsync(populateRequest,
                                newToken, tokenType,
                                didTokenGetRefreshed: (s, b) => (false, string.Empty).AsTask(),
                            onSuccess: onSuccess,
                            onFailure: onFailure,
                            onFailureWithBody: onFailureWithBody,
                            onResponseFailure: onResponseFailure,
                            onFailureToParse: onFailureToParse,
                            mutateRequest: mutateRequest),
                    onFailureWithBody:onFailureWithBody,
                    onFailure: onFailure,
                    didTokenGetRefreshed: didTokenGetRefreshed),
                onResponseFailure: onResponseFailure.AsAsyncFunc(),
                onFailureToParse: onFailureToParse.AsAsyncFunc());
        }

        public static Task<TResult> HttpClientPostResourceAsync<TResource, TResponse, TResult>(this Uri location, TResource resource,
            Func<TResponse, HttpResponseMessage, TResult> onSuccess,
            Func<string, TResult> onFailure = default,
            Func<HttpStatusCode, string, TResult> onFailureWithBody = default,
            Func<HttpStatusCode, Func<Task<string>>, TResult> onResponseFailure = default,
            Func<string, string, TResult> onFailureToParse = default,
            Func<HttpRequestMessage, HttpRequestMessage> mutateRequest = default)
        {
            return location.HttpClientPostDynamicRequestAsync(
                populateRequest: httpRequest =>
                {
                     var json = JsonConvert.SerializeObject(resource,
                         Formatting.Indented,
                         new JsonSerializerSettings
                         {
                             MissingMemberHandling = MissingMemberHandling.Ignore,
                             DefaultValueHandling = DefaultValueHandling.Ignore,
                             NullValueHandling = NullValueHandling.Ignore,
                         });
                    var content = new StringContent(json,
                        encoding: System.Text.Encoding.UTF8, "application/json");
                    httpRequest.Content = content;
                    if (mutateRequest.IsDefaultOrNull())
                        return (httpRequest, Cleanup);

                    var mutatedRequest = mutateRequest(httpRequest);
                    return (mutatedRequest, Cleanup);

                    void Cleanup() => content.Dispose();
                },
                onSuccess: onSuccess,
                onFailure: onFailure,
                onFailureWithBody: onFailureWithBody,
                onResponseFailure: onResponseFailure,
                onFailureToParse: onFailureToParse);
        }

        public static async Task<TResult> HttpClientPostDynamicRequestAsync<TResponse, TResult>(this Uri location,
                Func<HttpRequestMessage, (HttpRequestMessage, Action)> populateRequest,
            Func<TResponse, HttpResponseMessage, TResult> onSuccess,
            Func<string, TResult> onFailure = default,
            Func<HttpStatusCode, string, TResult> onFailureWithBody = default,
            Func<HttpStatusCode, Func<Task<string>>, TResult> onResponseFailure = default,
            Func<string, string, TResult> onFailureToParse = default)
        {
            using (var httpClient = new HttpClient())
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, location))
                {
                    var (requestToSend, cleanup) = populateRequest(request);
                    try
                    {
                        return await await httpClient
                            .SendAsync(requestToSend)
                            .IsSuccessStatusCodeAsync(
                                responseSuccess => ParseResourceResponse<TResponse, TResult>(responseSuccess,
                                    onSuccess:(resource) => onSuccess(resource, responseSuccess),
                                    onFailure: onFailure,
                                    onFailureWithBody: onFailureWithBody,
                                    onResponseFailure: onResponseFailure,
                                    onFailureToParse: onFailureToParse),
                                onFailureWithBody: (code, body) =>
                                {
                                    if (onFailureWithBody.IsNotDefaultOrNull())
                                        return onFailureWithBody(code, body).AsTask();

                                    throw new ArgumentException($"{nameof(HttpClientPatchDynamicAsync)}->{nameof(onFailureWithBody)} is NULL. Failure {code} with body `{body}`");
                                },
                                onResponseFailure: onResponseFailure.AsAsyncFunc(),
                                onFailure: onFailure.AsAsyncFunc());
                    }
                    catch (System.Net.Http.HttpRequestException ex)
                    {
                        if (onFailure.IsNotDefaultOrNull())
                            return onFailure($"Connection Failure: {ex.GetType().FullName}:{ex.Message}");

                        throw;
                    }
                    catch (Exception exGeneral)
                    {
                        if (onFailure.IsNotDefaultOrNull())
                            return onFailure(exGeneral.Message);

                        throw;
                    }
                    finally
                    {
                        cleanup();
                    }
                }
            }
        }

        public static async Task<TResult> HttpPostFormUrlEncodedContentAsync<TResource, TResult>(this Uri location,
                IDictionary<string, string> postValues,
            Func<TResource, TResult> onSuccess,
            Func<string, TResult> onFailure = default,
            Func<HttpStatusCode, string, TResult> onFailureWithBody = default,
            Func<HttpStatusCode, Func<Task<string>>, TResult> onResponseFailure = default,
            Func<string, string, TResult> onFailureToParse = default)
        {
            using (var httpClient = new HttpClient())
            {
                using (var body = new FormUrlEncodedContent(postValues))
                {
                    try
                    {
                        return await await httpClient.PostAsync(location, body)
                            .IsSuccessStatusCodeAsync(
                                responseSuccess =>
                                {
                                    return responseSuccess.ParseResourceResponse(
                                        onSuccess: onSuccess,
                                        onFailure: onFailure,
                                        onFailureWithBody: onFailureWithBody,
                                        onResponseFailure: onResponseFailure,
                                        onFailureToParse: onFailureToParse);
                                },
                                onFailureWithBody: (code, body) =>
                                {
                                    if (onFailureWithBody.IsNotDefaultOrNull())
                                        return onFailureWithBody(code, body).AsTask();

                                    throw new ArgumentException($"{nameof(HttpClientPatchDynamicAsync)}->{nameof(onFailureWithBody)} is NULL. Failure {code} with body `{body}`");
                                },
                                onResponseFailure: onResponseFailure.AsAsyncFunc(),
                                onFailure: onFailure.AsAsyncFunc());
                    }
                    catch (System.Net.Http.HttpRequestException ex)
                    {
                        if (onFailure.IsNotDefaultOrNull())
                            return onFailure($"Connection Failure: {ex.GetType().FullName}:{ex.Message}");

                        throw;
                    }
                    catch (Exception exGeneral)
                    {
                        if (onFailure.IsNotDefaultOrNull())
                            return onFailure(exGeneral.Message);

                        throw;
                    }
                }
            }
        }

        public static async Task<TResult> HttpPostFormDataContentAsync<TResource, TResult>(this Uri location,
                IDictionary<string, string> postValues,
            Func<TResource, TResult> onSuccess,
            Func<string, TResult> onFailure = default,
            Func<HttpStatusCode, string, TResult> onFailureWithBody = default,
            Func<HttpStatusCode, Func<Task<string>>, TResult> onResponseFailure = default,
            Func<string, string, TResult> onFailureToParse = default)
        {
            using (var httpClient = new HttpClient())
            {
                using (var body = new MultipartFormDataContent())
                {
                    var populatedBody = postValues.Aggregate(body,
                        (aggr, kvp) =>
                        {
                            aggr.Add(new StringContent(kvp.Value), kvp.Key);
                            return aggr;
                        });

                    try
                    {
                        return await await httpClient
                            .PostAsync(location, populatedBody)
                            .IsSuccessStatusCodeAsync(
                                responseSuccess =>
                                {
                                    return responseSuccess.ParseResourceResponse(
                                        onSuccess: onSuccess,
                                        onFailure: onFailure,
                                        onFailureWithBody: onFailureWithBody,
                                        onResponseFailure: onResponseFailure,
                                        onFailureToParse: onFailureToParse);
                                },
                                onFailureWithBody: (code, body) =>
                                {
                                    if (onFailureWithBody.IsNotDefaultOrNull())
                                        return onFailureWithBody(code, body).AsTask();

                                    if (onResponseFailure.IsNotDefaultOrNull())
                                        return onResponseFailure(code, () => body.AsTask()).AsTask();

                                    if (onFailure.IsNotDefaultOrNull())
                                        return onFailure($"{code}:{body}").AsTask();

                                    throw new ArgumentException($"{nameof(HttpClientPatchDynamicAsync)}->{nameof(onFailureWithBody)} is NULL. Failure {code} with body `{body}`");
                                },
                                onResponseFailure: onResponseFailure.AsAsyncFunc(),
                                onFailure: onFailure.AsAsyncFunc());
                    }
                    catch (System.Net.Http.HttpRequestException ex)
                    {
                        if (onFailure.IsNotDefaultOrNull())
                            return onFailure($"Connection Failure: {ex.GetType().FullName}:{ex.Message}");

                        throw;
                    }
                    catch (Exception exGeneral)
                    {
                        if (onFailure.IsNotDefaultOrNull())
                            return onFailure(exGeneral.Message);

                        throw;
                    }
                }
            }
        }

        #endregion

        #region Patch/Put

        public static async Task<TResult> HttpClientPatchDynamicAsync<TResponse, TResult>(this Uri location,
                Func<HttpRequestMessage, (HttpRequestMessage, Action)> populateRequest,
            Func<TResponse, HttpResponseMessage, TResult> onSuccess,
            Func<string, TResult> onFailure = default,
            Func<HttpStatusCode, string, TResult> onFailureWithBody = default,
            Func<HttpStatusCode, Func<Task<string>>, TResult> onResponseFailure = default,
            Func<string, string, TResult> onFailureToParse = default)
        {
            using (var httpClient = new HttpClient())
            {
                using (var request = new HttpRequestMessage(HttpMethod.Patch, location))
                {
                    var (requestToSend, cleanup) = populateRequest(request);
                    try
                    {
                        return await await httpClient
                            .SendAsync(requestToSend)
                            .IsSuccessStatusCodeAsync(
                                responseSuccess => ParseResourceResponse<TResponse, TResult>(responseSuccess,
                                    onSuccess:(resource) => onSuccess(resource, responseSuccess),
                                    onFailure: onFailure,
                                    onFailureWithBody: onFailureWithBody,
                                    onResponseFailure: onResponseFailure,
                                    onFailureToParse: onFailureToParse),
                                onFailureWithBody:(code, body) =>
                                {
                                    if(onFailureWithBody.IsNotDefaultOrNull())
                                        return onFailureWithBody(code, body).AsTask();

                                    throw new ArgumentException($"{nameof(HttpClientPatchDynamicAsync)}->{nameof(onFailureWithBody)} is NULL. Failure {code} with body `{body}`");
                                },
                                onResponseFailure: onResponseFailure.AsAsyncFunc(),
                                onFailure: onFailure.AsAsyncFunc());
                    }
                    catch (System.Net.Http.HttpRequestException ex)
                    {
                        if (onFailure.IsNotDefaultOrNull())
                            return onFailure($"Connection Failure: {ex.GetType().FullName}:{ex.Message}");

                        throw;
                    }
                    catch (Exception exGeneral)
                    {
                        if (onFailure.IsNotDefaultOrNull())
                            return onFailure(exGeneral.Message);

                        throw;
                    }
                    finally
                    {
                        cleanup();
                    }
                }
            }
        }

        public static async Task<TResult> HttpClientPatchDynamicAuthenticatedAsync<TResponse, TResult>(this Uri location,
                Func<HttpRequestMessage, (HttpRequestMessage, Action)> populateRequest,
                string authToken, string tokenType,
            Func<TResponse, HttpResponseMessage, TResult> onSuccess,
            Func<string, TResult> onFailure = default,
            Func<HttpStatusCode, string, TResult> onFailureWithBody = default,
            Func<HttpStatusCode, Func<Task<string>>, TResult> onResponseFailure = default,
            Func<string, string, TResult> onFailureToParse = default,
                Func<HttpRequestMessage, HttpRequestMessage> mutateRequest = default,
                Func<HttpStatusCode, string, Task<(bool, string)>> didTokenGetRefreshed = default)
        {
            return await await location.HttpClientPatchDynamicAsync(
                populateRequest: (request) =>
                {
                    request.Headers.Add("Authorization", $"{tokenType} {authToken}");
                    return populateRequest(request);
                },
                onSuccess: onSuccess.AsAsyncFunc(),
                onFailure: onFailure.AsAsyncFunc(),
                onFailureWithBody: (statusCode, body) => TryRefreshAsync(statusCode, body,
                    rerun: (newToken) => location.HttpClientPostResourceAsync(populateRequest,
                            newToken, tokenType,
                            didTokenGetRefreshed: (s, b) => (false, string.Empty).AsTask(),
                        onSuccess: onSuccess,
                        onFailure: onFailure,
                        onFailureWithBody: onFailureWithBody,
                        onResponseFailure: onResponseFailure,
                        onFailureToParse: onFailureToParse,
                        mutateRequest: mutateRequest),
                    onFailureWithBody: onFailureWithBody,
                    onFailure: onFailure,
                    didTokenGetRefreshed: didTokenGetRefreshed),
                onResponseFailure: onResponseFailure.AsAsyncFunc(),
                onFailureToParse: onFailureToParse.AsAsyncFunc());
        }

        public static async Task<TResult> HttpClientPutResourceAsync<TResource, TResponseResource, TResult>(this Uri location, TResource resource,
            Func<TResponseResource, TResult> onSuccess,
            Func<string, TResult> onFailure = default,
            Func<HttpStatusCode, string, TResult> onFailureWithBody = default,
            Func<HttpStatusCode, Func<Task<string>>, TResult> onResponseFailure = default,
            Func<string, string, TResult> onFailureToParse = default,
            Func<TResult> onTimeout = default,
            Func<HttpRequestMessage, HttpRequestMessage> mutateRequest = default)
        {
            using (var httpClient = new HttpClient())
            {
                using (var request = new HttpRequestMessage(HttpMethod.Put, location))
                {
                    var json = JsonConvert.SerializeObject(resource,
                        Formatting.Indented,
                        new JsonSerializerSettings
                        {
                            MissingMemberHandling = MissingMemberHandling.Ignore,
                            DefaultValueHandling = DefaultValueHandling.Ignore,
                            NullValueHandling = NullValueHandling.Ignore,
                        });
                    using (var content = new StringContent(json,
                        encoding: System.Text.Encoding.UTF8, "application/json"))
                    {
                        var requestToSend = request;
                        requestToSend.Content = content;

                        if (mutateRequest.IsNotDefaultOrNull())
                            requestToSend = mutateRequest(requestToSend);
                        try
                        {
                            return await await httpClient
                                .SendAsync(request)
                                .IsSuccessStatusCodeAsync(
                                    responseSuccess => ParseResourceResponse(responseSuccess,
                                        onSuccess: onSuccess,
                                        onFailure: onFailure,
                                        onFailureWithBody: onFailureWithBody,
                                        onResponseFailure: onResponseFailure,
                                        onFailureToParse: onFailureToParse),
                                    async responseFailure =>
                                    {
                                        if (onFailureWithBody.IsNotDefaultOrNull())
                                        {
                                            var content = await responseFailure.Content.ReadAsStringAsync();
                                            return onFailureWithBody(responseFailure.StatusCode, content);
                                        }

                                        if (onResponseFailure.IsNotDefaultOrNull())
                                            return onResponseFailure(responseFailure.StatusCode,
                                                () => responseFailure.Content.ReadAsStringAsync());

                                        if (onFailure.IsNotDefaultOrNull())
                                            return onFailure($"Server returned arror code:{responseFailure.StatusCode}");

                                        throw new ArgumentException($"Server returned arror code:{responseFailure.StatusCode}");
                                    },
                                    onTimeout:() =>
                                    {
                                        if (onTimeout.IsNotDefaultOrNull())
                                            return onTimeout().AsTask();

                                        if (onFailure.IsNotDefaultOrNull())
                                            return onFailure($"Timeout").AsTask();

                                        throw new Exception($"Timeout");
                                    });
                        }
                        catch (System.Net.Http.HttpRequestException ex)
                        {
                            if (onFailure.IsNotDefaultOrNull())
                                return onFailure($"Connection Failure: {ex.GetType().FullName}:{ex.Message}");

                            throw;
                        }
                        catch (Exception exGeneral)
                        {
                            if (onFailure.IsNotDefaultOrNull())
                                return onFailure(exGeneral.Message);

                            throw;
                        }
                    }
                }
            }
        }

        #endregion

        private static async Task<TResult> TryRefreshAsync<TResult>(HttpStatusCode statusCode, string body,
            Func<string, Task<TResult>> rerun,
            Func<HttpStatusCode, string, Task<(bool, string)>> didTokenGetRefreshed = default,
            Func<HttpStatusCode, string, TResult> onFailureWithBody = default,
            Func<string, TResult> onFailure = default)
        {
            if(didTokenGetRefreshed.IsNotDefaultOrNull())
            {
                var (didGetRefreshToken, newToken) = await didTokenGetRefreshed(statusCode, body);
                if (didGetRefreshToken)
                    return await rerun(newToken);
            }

            if (onFailureWithBody.IsNotDefaultOrNull())
                return onFailureWithBody(statusCode, body);

            var errorMsg = $"Server returned error [{statusCode}]:{body}";
            if (onFailure.IsNotDefaultOrNull())
                return onFailure(errorMsg);

            throw new Exception(errorMsg);
        }

        public static async Task<TResult> ParseResourceResponse<TResource, TResult>(
                this HttpResponseMessage responseSuccess,
            Func<TResource, TResult> onSuccess,
            Func<string, TResult> onFailure = default,
            Func<HttpStatusCode, string, TResult> onFailureWithBody = default,
            Func<HttpStatusCode, Func<Task<string>>, TResult> onResponseFailure = default,
            Func<string, string, TResult> onFailureToParse = default)
        {
            var content = await responseSuccess.Content.ReadAsStringAsync();
            return content.JsonParse(
                (TResource res) => onSuccess(res),
                (jsonFailure) =>
                {
                    if (onFailureToParse.IsNotDefaultOrNull())
                        return onFailureToParse(jsonFailure, content);

                    if (onFailureWithBody.IsNotDefaultOrNull())
                        return onFailureWithBody(responseSuccess.StatusCode, content);

                    if (onResponseFailure.IsNotDefaultOrNull())
                        return onResponseFailure(responseSuccess.StatusCode, () => content.AsTask());

                    if (onFailure.IsNotDefaultOrNull())
                        return onFailure($"Server returned arror code:{responseSuccess.StatusCode}");

                    throw new ArgumentException($"Failed to parse a `{typeof(TResource).FullName}` from the response.");
                });
        }

        public static async Task<TResult> IsSuccessStatusCodeAsync<TResult>(this Task<HttpResponseMessage> responseFetching,
            Func<HttpResponseMessage, TResult> onSuccessCode,
            Func<HttpStatusCode, string, TResult> onFailureWithBody,
            Func<HttpStatusCode, Func<Task<string>>, TResult> onResponseFailure,
            Func<HttpResponseMessage, TResult> onFailureRequestMessage = default,
            Func<string, TResult> onFailure = default,
            Func<TResult> onTimeout = default)
        {
            return await await responseFetching.IsSuccessStatusCodeAsync(
                onSuccessCode: onSuccessCode.AsAsyncFunc(),
                onFailureCode:
                    async (HttpResponseMessage responseFailure) =>
                    {
                        if (responseFailure.IsDefaultOrNull())
                        {
                            if (onFailure.IsNotDefaultOrNull())
                                return onFailure($"Server request timed out");

                            throw new Exception("Server request timed out");
                        }

                        if (onFailureWithBody.IsNotDefaultOrNull())
                        {
                            var content = await responseFailure.Content.ReadAsStringAsync();
                            return onFailureWithBody(responseFailure.StatusCode, content);
                        }

                        if (onResponseFailure.IsNotDefaultOrNull())
                            return onResponseFailure(responseFailure.StatusCode,
                                () => responseFailure.Content.ReadAsStringAsync());

                        if (onFailureRequestMessage.IsNotDefaultOrNull())
                            return onFailureRequestMessage(responseFailure);

                        if (onFailure.IsNotDefaultOrNull())
                            return onFailure($"Server returned error code:{responseFailure.StatusCode}");

                        throw new ArgumentException($"Server returned error code:{responseFailure.StatusCode}");
                    },
                onTimeout: () =>
                {
                    if (onTimeout.IsNotDefaultOrNull())
                        return onTimeout().AsTask();

                    if (onFailure.IsNotDefaultOrNull())
                        return onFailure($"Timeout").AsTask();

                    throw new Exception($"Timeout");
                });
        }

        public static async Task<TResult> IsSuccessStatusCodeAsync<TResult>(this Task<HttpResponseMessage> responseFetching,
            Func<HttpResponseMessage, TResult> onSuccessCode,
            Func<HttpResponseMessage, TResult> onFailureCode,
            Func<TResult> onTimeout)
        {
            try
            {
                var response = await responseFetching;
                return IsSuccessStatusResponse(response,
                    onSuccessCode:onSuccessCode,
                    onFailureCode:onFailureCode);
            } catch(System.Threading.Tasks.TaskCanceledException ex)
            {
                if(ex.InnerException.GetType().IsAssignableTo(typeof(System.TimeoutException)))
                    return onTimeout();

                return onTimeout();
            }
        }

        public static TResult IsSuccessStatusResponse<TResult>(this HttpResponseMessage response,
            Func<HttpResponseMessage, TResult> onSuccessCode,
            Func<HttpResponseMessage, TResult> onFailureCode)
        {
            return response.IsSuccessStatusCode(
                    onSuccessCode: (status) => onSuccessCode(response),
                    onFailureCode: (status) => onFailureCode(response));
        }

        public static TResult IsSuccessStatusCode<TResult>(this HttpResponseMessage response,
            Func<HttpStatusCode, TResult> onSuccessCode,
            Func<HttpStatusCode, TResult> onFailureCode) => response.IsSuccessStatusCode ?
                onSuccessCode(response.StatusCode)
                :
                onFailureCode(response.StatusCode);
    }
}

