using EastFive.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public class StatusCodeResponseAttribute : HttpFuncDelegateAttribute
    {
        public override Task<IHttpResponse> InstigateInternal(IApplication httpApp,
                IHttpRequest request, ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            Func<IHttpResponse> responseFunc =
                () =>
                {
                    var response = request.CreateResponse(this.StatusCode);
                    return UpdateResponse(parameterInfo, httpApp, request, response);
                };
            var responseDelegate = responseFunc.MakeDelegate(parameterInfo.ParameterType);
            return onSuccess(responseDelegate);
        }
    }
}
