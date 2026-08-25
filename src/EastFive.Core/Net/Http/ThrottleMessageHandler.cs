using EastFive.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EastFive.Net.Http
{
    public abstract class ThrottleMessageHandler : DelegatingHandler
    {
        private static AutoResetEvent rateLock = new AutoResetEvent(true);

        public ThrottleMessageHandler(HttpMessageHandler innerHandler = default(HttpMessageHandler))
            : base()
        {
            if(innerHandler.IsDefaultOrNull())
                innerHandler = new HttpClientHandler();
            this.InnerHandler = innerHandler;
        }

        protected abstract TimeSpan ComputeDelay(HttpRequestMessage request);

        protected abstract void UpdateDelay(HttpResponseMessage response, bool timeout);

        protected virtual bool ShouldRateLock(HttpRequestMessage request)
        {
            return true;
        }

        protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var ct = new CancellationToken();
            return await await Task.Run<Task<HttpResponseMessage>>(
                async () =>
                {
                    var threadName = request.RequestUri.AbsoluteUri;
                    var didRateLock = ShouldRateLock(request);
                    if (didRateLock)
                    {
                        System.Diagnostics.Debug.WriteLine($"Thread [{threadName}] requesting mutex");
                        rateLock.WaitOne();
                    }
                    var delay = ComputeDelay(request);

                    if (delay.TotalSeconds > 0)
                        await Task.Delay(delay);

                    HttpResponseMessage response = default(HttpResponseMessage);
                    bool didTimeout = false;
                    try
                    {
                        response = await base.SendAsync(request, ct);
                        System.Diagnostics.Debug.WriteLine((didRateLock ? "SYNC:" : "PARALLEL") + $"Response [{threadName}]");
                        if (!IsOverage(response))
                        {
                            // not disposing of response because it is the caller's job now
                            return response;
                        }
                    }
                    #region Capture timeout
                    // Timeouts can be initiated either internally or externally. 
                    // Internal timeouts are caused by the Task being cancelled. It is worth noting that the cancellation token
                    // cannot be reused because, well, it has flagged the process as cancelled.
                    catch (TaskCanceledException ex)
                    {
                        if (ex.CancellationToken.IsCancellationRequested)
                        {
                            if (response != default(HttpResponseMessage))
                                response.Dispose();
                            throw;
                        }

                        // This is normally just a timeout
                        didTimeout = true;
                    }
                    // External timouts are connection failures.
                    catch(HttpRequestException ex)
                    {
                        if (!(ex.InnerException is System.Net.WebException))
                        {
                            if (response != default(HttpResponseMessage))
                                response.Dispose();
                            throw;
                        }

                        var webEx = ex.InnerException as System.Net.WebException;
                        if (webEx.InnerException is System.Net.Sockets.SocketException)
                        {
                            var socketEx = webEx.InnerException as System.Net.Sockets.SocketException;
                            didTimeout = (socketEx.SocketErrorCode == System.Net.Sockets.SocketError.TimedOut);
                        }
                        else
                        {
                            if (webEx.Status == System.Net.WebExceptionStatus.Timeout ||
                                webEx.Status == System.Net.WebExceptionStatus.ConnectFailure ||
                                webEx.Status == System.Net.WebExceptionStatus.ConnectionClosed)
                                didTimeout = true;
                        }

                        if (!didTimeout)
                        {
                            if (response != default(HttpResponseMessage))
                                response.Dispose();
                            throw;
                        }
                    }
                    #endregion
                    catch (Exception)
                    {
                        if (response != default(HttpResponseMessage))
                            response.Dispose();

                        throw;
                    }
                    finally
                    {
                        UpdateDelay(response, didTimeout);
                        if (didRateLock)
                        {
                            System.Diagnostics.Debug.WriteLine($"Thread [{threadName}] leaving mutex");
                            rateLock.Set();
                            System.Diagnostics.Debug.WriteLine($"Thread [{threadName}] exited mutex");
                        }
                    }
                    if (response != default(HttpResponseMessage))
                        response.Dispose();
                    return await SendAsync(request, ct);
                }).ConfigureAwait(false);
        }

        protected virtual bool IsOverage(HttpResponseMessage response)
        {
            return (((int)response.StatusCode) == 429);
        }
    }
}
