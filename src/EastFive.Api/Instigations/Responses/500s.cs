using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;

using EastFive.Linq;
using EastFive.Extensions;
using EastFive.Linq.Async;
using EastFive.Reflection;

namespace EastFive.Api
{
    [GeneralFailureResponse]
    public delegate IHttpResponse GeneralFailureResponse(string why = default);
    public class GeneralFailureResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.InternalServerError;

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            GeneralFailureResponse responseDelegate =
                (why) =>
                {
                    var response = request.CreateResponse(StatusCode);
                    if (why.IsDefaultNullOrEmpty())
                        return UpdateResponse(parameterInfo, httpApp, request, response); 
                    return UpdateResponse(parameterInfo, httpApp, request, response.AddReason(why));
                };
            return onSuccess(responseDelegate);
        }
    }

    [StatusCodeResponse(StatusCode = HttpStatusCode.NotImplemented)]
    public delegate IHttpResponse NotImplementedResponse();

    [StatusCodeResponse(StatusCode = HttpStatusCode.ServiceUnavailable)]
    public delegate IHttpResponse ServiceUnavailableResponse();

    [ConfigurationFailureResponse]
    public delegate IHttpResponse ConfigurationFailureResponse(string configurationValue, string message);
    public class ConfigurationFailureResponseAttribute : HttpFuncDelegateAttribute
    {
        public override HttpStatusCode StatusCode => HttpStatusCode.ServiceUnavailable;

        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            ConfigurationFailureResponse responseDelegate =
                (configurationValue, message) =>
                {
                    var response = request
                        .CreateResponse(System.Net.HttpStatusCode.ServiceUnavailable)
                        .AddReason($"`{configurationValue}` not specified in config:{message}");

                    return UpdateResponse(parameterInfo, httpApp, request, response);
                };
            return onSuccess(responseDelegate);
        }
    }
}
