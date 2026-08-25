using System.Net;
using System.Net.Http;

namespace EastFive.Api
{
    public static class HttpRequestMessageCompatExtensions
    {
        public static HttpResponseMessage CreateResponse(this HttpRequestMessage request, HttpStatusCode statusCode)
        {
            return new HttpResponseMessage(statusCode)
            {
                RequestMessage = request,
            };
        }
    }
}
