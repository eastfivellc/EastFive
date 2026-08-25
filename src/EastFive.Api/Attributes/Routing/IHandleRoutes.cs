using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public delegate Task<IHttpResponse> RouteHandlingDelegate(Type controllerType,
            IApplication httpApp, IHttpRequest routeData);

    public interface IHandleRoutes
    {
        Task<IHttpResponse> HandleRouteAsync(Type controllerType, IInvokeResource resourceInvoker,
            IApplication httpApp, IHttpRequest request,
            RouteHandlingDelegate continueExecution);
    }
}
