using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using EastFive.Extensions;

namespace EastFive.Net.Http;

public class BearerTokenHandler : DelegatingHandler
{
    private readonly string token;

    public BearerTokenHandler(string token, HttpMessageHandler innerHandler = default(HttpMessageHandler))
        : base()
    {
        this.token = token;
        this.InnerHandler = innerHandler.IsDefaultOrNull() 
            ? new HttpClientHandler()
            : innerHandler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return base.SendAsync(ApplyToken(request), cancellationToken);
    }

    protected virtual HttpRequestMessage ApplyToken(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
