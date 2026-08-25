using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using EastFive.Extensions;

namespace EastFive.Net.Http
{
    public class MessageHandlerCacheMemory : MessageHandlerCache
    {
        private static ConcurrentDictionary<string, KeyValuePair<DateTime, byte[]>> cache =
            new ConcurrentDictionary<string, KeyValuePair<DateTime, byte[]>>();

        public MessageHandlerCacheMemory(HttpMessageHandler innerHandler = default(HttpMessageHandler))
            : base(innerHandler)
        {
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestUri = request.RequestUri.AbsoluteUri;
            var isNoCache = IsNoCache(request);
            if (isNoCache || (!cache.ContainsKey(requestUri)))
            {
                using (var response = await base.SendAsync(request, cancellationToken))
                {
                    // stream is passed to GenerateResponse and disposed there
                    var memStream = new MemoryStream();
                    await response.Content.CopyToAsync(memStream);
                    if (!isNoCache)
                    {
                        var data = memStream.ToArray();
                        cache.AddOrUpdate(requestUri,
                                whenKey => DateTime.UtcNow.PairWithValue(data),
                                (k, v) => v);
                    }
                    memStream.Seek(0, SeekOrigin.Begin);
                    return GenerateResponse(memStream, response.StatusCode);
                }
            }

            var index = cache[requestUri];
            return GenerateResponse(index.Value);
        }
    }
}
