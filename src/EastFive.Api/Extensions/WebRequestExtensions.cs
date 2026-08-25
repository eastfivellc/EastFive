using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace EastFive.Web
{
    public static class WebRequestExtensions
    {
        public static HttpWebRequest AsHttpWebRequest(this WebRequest webRequest, string method)
        {
            if (!(webRequest is HttpWebRequest))
                throw new ArgumentException("webRequest must be of type HttpWebRequest");
            var httpWebRequest = (HttpWebRequest)webRequest;

            httpWebRequest.Method = method;
            return httpWebRequest;
        }

        #region resource safe access to request and response

        public static TResult GetRequestStream<TResult>(this HttpWebRequest httpWebRequest,
            Func<Stream, TResult> success,
            Func<HttpStatusCode, string, TResult> webFailure,
            Func<string, TResult> failure)
        {
            try
            {
                using (var requestStream = httpWebRequest.GetRequestStream())
                {
                    return success(requestStream);
                }
            }
            catch (WebException ex)
            {
                var httpResponse = (HttpWebResponse)ex.Response;
                if (default(HttpWebResponse) == httpResponse)
                    return failure(ex.Message);
                var responseText = new System.IO.StreamReader(httpResponse.GetResponseStream()).ReadToEnd();
                return webFailure(httpResponse.StatusCode, responseText);
            }
        }

        public static async Task<TResult> GetResponseAsync<TResult>(this HttpWebRequest httpWebRequest,
            Func<HttpWebResponse, TResult> onSuccess,
            Func<HttpStatusCode, string, TResult> webFailure,
            Func<string, TResult> failure)
        {
            try
            {
                using (var createAuthResponse = ((HttpWebResponse)(await httpWebRequest.GetResponseAsync())))
                {
                    return onSuccess(createAuthResponse);
                }
            }
            catch (WebException ex)
            {
                var httpResponse = (HttpWebResponse)ex.Response;
                if (default(HttpWebResponse) == httpResponse)
                    return failure(ex.Message);
                var responseText = new System.IO.StreamReader(httpResponse.GetResponseStream()).ReadToEnd();
                return webFailure(httpResponse.StatusCode, responseText);
            }
        }

        #endregion

        #region JSON Parsing

        public static TResult GetRequestJson<TResult, TResource>(this HttpWebRequest httpWebRequest,
            TResource resource,
            Func<TResult> success,
            Func<HttpStatusCode, string, TResult> webFailure,
            Func<string, TResult> failure)
        {
            httpWebRequest.ContentType = "application/json";
            return httpWebRequest.GetRequestStream(
                (requestStream) =>
                {
                    try
                    {
                        using (var streamWriter = new StreamWriter(requestStream))
                        {
                            var resourceJson = Newtonsoft.Json.JsonConvert.SerializeObject(resource);
                            streamWriter.Write(resourceJson);
                            streamWriter.Flush();
                        }
                        return success();
                    }
                    catch (Exception ex)
                    {
                        return failure(ex.Message);
                    }
                },
                webFailure,
                failure);
        }

        public static async Task<TResult> GetResponseJsonAsync<TResource, TResult>(this HttpWebRequest httpWebRequest,
            Func<TResource, TResult> onSuccess,
            Func<HttpStatusCode, string, TResult> webFailure,
            Func<string, TResult> failure)
        {
            return await httpWebRequest.GetResponseAsync(
                (response) =>
                {
                    try
                    {
                        var responseJson = new StreamReader(response.GetResponseStream()).ReadToEnd();
                        var resource = Newtonsoft.Json.JsonConvert.DeserializeObject<TResource>(responseJson);
                        return onSuccess(resource);
                    }
                    catch (Exception ex)
                    {
                        return failure(ex.Message);
                    }
                },
                webFailure, failure);
        }

        #endregion

        #region methods

        public static async Task<TResult> GetAsync<TResource, TResult>(this WebRequest webRequest,
            Func<TResource, TResult> onSuccess,
            Func<HttpStatusCode, string, TResult> webFailure,
            Func<string, TResult> failure)
        {
            var httpWebRequest = webRequest.AsHttpWebRequest("GET");
            httpWebRequest.ContentType = "application/json";
            return await httpWebRequest.GetResponseJsonAsync<TResource, TResult>(
                (response) => onSuccess(response),
                webFailure,
                failure);
        }

        public static Task<TResult> PostAsync<TResource, TResult>(this WebRequest webRequest, TResource resource,
            Func<HttpWebResponse, TResult> onSuccess,
            Func<HttpStatusCode, string, TResult> onWebFailure,
            Func<string, TResult> onFailure)
        {
            var httpWebRequest = webRequest.AsHttpWebRequest("POST");
            return httpWebRequest
                .GetRequestJson(resource,
                    () => httpWebRequest.GetResponseAsync(onSuccess, onWebFailure, onFailure),
                    (code, message) => Task.FromResult(onWebFailure(code, message)),
                    (whyFailed) => Task.FromResult(onFailure(whyFailed)));
        }

        public static Task<TResult> PutAsync<TResource, TResult>(this WebRequest webRequest, TResource resource,
            Func<HttpWebResponse, TResult> onSuccess,
            Func<HttpStatusCode, string, TResult> onWebFailure,
            Func<string, TResult> onFailure)
        {
            var httpWebRequest = webRequest.AsHttpWebRequest("PUT");
            return httpWebRequest
                .GetRequestJson(resource,
                    () => httpWebRequest.GetResponseAsync(onSuccess, onWebFailure, onFailure),
                    (code, message) => Task.FromResult(onWebFailure(code, message)),
                    (whyFailed) => Task.FromResult(onFailure(whyFailed)));
        }

        public static Task<TResult> DeleteAsync<TResource, TResult>(this WebRequest webRequest,
            TResource resource,
            Func<HttpWebResponse, TResult> onSuccess,
            Func<HttpStatusCode, string, TResult> onWebFailure,
            Func<string, TResult> onFailure)
        {
            var httpWebRequest = webRequest.AsHttpWebRequest("DELETE");
            return httpWebRequest
                .GetRequestJson(resource,
                    () => httpWebRequest.GetResponseAsync(onSuccess, onWebFailure, onFailure),
                    (code, message) => Task.FromResult(onWebFailure(code, message)),
                    (whyFailed) => Task.FromResult(onFailure(whyFailed)));
        }

        #endregion

        public static bool IsSuccess(this HttpStatusCode statusCode)
        {
            return ((int)statusCode >= 200) && ((int)statusCode <= 299);
        }
    }
}
