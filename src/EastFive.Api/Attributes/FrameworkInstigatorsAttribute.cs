using EastFive.Api.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Framework
{
    public class FrameworkInstigatorsAttribute : System.Attribute, IInstigate
    {
        public bool CanInstigate(ParameterInfo parameterInfo)
        {
            return parameterInfo.ParameterType.IsAssignableFrom(typeof(IProvideUrl))
                || parameterInfo.ParameterType.IsAssignableFrom(typeof(HttpRequestMessage))
                || parameterInfo.ParameterType.IsAssignableFrom(typeof(Microsoft.Net.Http.Headers.MediaTypeHeaderValue[]));
        }

        public Task<IHttpResponse> Instigate(
            IApplication httpApp, IHttpRequest request,
            ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            if (parameterInfo.ParameterType.IsAssignableFrom(typeof(IProvideUrl)))
            {
                var helper = new Core.CoreUrlProvider(
                    (request as Core.CoreHttpRequest).request.HttpContext);
                return onSuccess(helper);
            }

            if (parameterInfo.ParameterType.IsAssignableFrom(typeof(HttpRequestMessage)))
            {
                var httpRequest = (request as Core.CoreHttpRequest).request.ToHttpRequestMessage();
                return onSuccess(httpRequest);
            }

            if (parameterInfo.ParameterType.IsAssignableFrom(typeof(Microsoft.Net.Http.Headers.MediaTypeHeaderValue[])))
            {
                var acceptHeaders = request.RequestHeaders.Accept.ToArray();
                return onSuccess(acceptHeaders);
            }

            throw new Exception();
        }
    }
}
