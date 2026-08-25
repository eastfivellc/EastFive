using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EastFive.Api
{
    [ContinuationResponse]
    public delegate IHttpResponse ContinuationResponse(
        IHttpResponse response, Func<Task> continuation);

    public class ContinuationResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => 0;

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            ContinuationResponse responseDelegate = (response, continuation) =>
            {
                return new ContinuationResponseResponse(response, continuation, request);
            };
            return onSuccess(responseDelegate);
        }

        private class ContinuationResponseResponse : HttpResponse, IHaveMoreWork
        {
            private Func<Task> continuation;

            public ContinuationResponseResponse(
                IHttpResponse response, Func<Task> continuation,
                IHttpRequest request) : base(request, response.StatusCode, 
                    async stream =>
                    {
                        await response.WriteResponseAsync(stream);
                        await stream.FlushAsync();
                    })
            {
                this.continuation = continuation;
            }

            public Task ProcessWorkAsync(CancellationToken cancellationToken)
            {
                return continuation();
            }
        }
    }
}
