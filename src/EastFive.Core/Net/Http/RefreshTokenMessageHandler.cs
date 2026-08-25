using EastFive.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EastFive.Net.Http
{
    public abstract class RefreshTokenMessageHandler : DelegatingHandler
    {
        public RefreshTokenMessageHandler(HttpMessageHandler innerHandler = default(HttpMessageHandler))
            : base()
        {
            this.InnerHandler = innerHandler.IsDefaultOrNull() ?
                new HttpClientHandler()
                :
                innerHandler;
        }

        protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(ApplyToken(request, AccessToken), cancellationToken);
            if (!await NeedsRefreshAsync(response))
                return response;

            return await await RefreshTokenAsync(
                (token) =>
                {
                    response.Dispose();
                    return SendAsync(request, cancellationToken);
                },
                (why) => response.AsTask());
        }

        protected virtual HttpRequestMessage ApplyToken(HttpRequestMessage request, string accessToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return request;
        }

        protected virtual Task<bool> NeedsRefreshAsync(HttpResponseMessage response)
        {
            return (response.StatusCode == System.Net.HttpStatusCode.Unauthorized).AsTask();
        }

        protected abstract string AccessToken { get; }

        protected abstract Task<TResult> RefreshTokenAsync<TResult>(
            Func<string, TResult> onSuccess,
            Func<string, TResult> onFailure);
    }
}
