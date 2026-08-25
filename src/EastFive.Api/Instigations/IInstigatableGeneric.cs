using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EastFive.Api
{ 
    public interface IInstigatableGeneric
    {
        Task<IHttpResponse> InstigatorDelegateGeneric(Type type,
                IApplication httpApp, IHttpRequest routeData,
            ParameterInfo parameterInfo,
        Func<object, Task<IHttpResponse>> onSuccess);
    }

    public interface IInstigateGeneric
    {
        bool CanInstigate(ParameterInfo parameterInfo);

        Task<IHttpResponse> InstigatorDelegateGeneric(
            Type type,
                IApplication httpApp, IHttpRequest routeData,
            ParameterInfo parameterInfo,
        Func<object, Task<IHttpResponse>> onSuccess);
    }
}
