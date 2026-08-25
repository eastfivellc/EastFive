using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public delegate Task<IHttpResponse> HandleExceptionDelegate(Exception ex,
        MethodInfo method,
        KeyValuePair<ParameterInfo, object>[] queryParameters,
        IApplication httpApp, IHttpRequest routeData);

    public interface IHandleExceptions
    {
        Task<IHttpResponse> HandleExceptionAsync(Exception ex, MethodInfo method,
            KeyValuePair<ParameterInfo, object>[] queryParameters,
            IApplication httpApp, IHttpRequest routeData,
            HandleExceptionDelegate continueExecution);
    }
}
