using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using EastFive.Api.Resources;
using EastFive.Linq;
using EastFive.Extensions;
using EastFive.Linq.Async;

namespace EastFive.Api
{
    [StatusCodeResponse(StatusCode = HttpStatusCode.Accepted)]
    public delegate IHttpResponse AcceptedResponse();
    
    [AcceptedBodyResponse]
    public delegate IHttpResponse AcceptedBodyResponse(object content, string contentType = default(string));
    public class AcceptedBodyResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.Accepted;

        public override string Example => "serialized object";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            AcceptedBodyResponse responseDelegate =
                (obj, contentType) =>
                {
                    var response = request.CreateResponse(HttpStatusCode.Accepted, obj);
                    if (!contentType.IsNullOrWhiteSpace())
                        response.SetContentType(contentType);
                    return UpdateResponse(parameterInfo, httpApp, request, response);
                };
            return onSuccess(responseDelegate);
        }
    }

    public interface IExecuteAsync
    {
        bool ForceBackground { get; }

        Task<IHttpResponse> InvokeAsync(Action<double> updateCallback);
    }

    [ExecuteBackgroundResponse]
    public delegate Task<IHttpResponse> ExecuteBackgroundResponseAsync(IExecuteAsync executeAsync);
    public class ExecuteBackgroundResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.Accepted;

        public override string Example => "serialized object";

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            ExecuteBackgroundResponseAsync responseDelegate =
                async (executionContext) =>
                {
                    var responseInvoke = await executionContext.InvokeAsync(v => { });
                    return UpdateResponse(parameterInfo, httpApp, request, responseInvoke);
                };
            return onSuccess(responseDelegate);
        }
    }
}
