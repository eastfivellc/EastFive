using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public delegate IHttpResponse ResponseHandlingDelegate(ParameterInfo parameterInfo,
            IApplication httpApp, IHttpRequest request,
            IHttpResponse response);

    public interface IHandleResponses
    {
        IHttpResponse HandleResponse(ParameterInfo parameterInfo,
            IApplication httpApp, IHttpRequest request,
            IHttpResponse response,
            ResponseHandlingDelegate continueExecution);
    }
}
