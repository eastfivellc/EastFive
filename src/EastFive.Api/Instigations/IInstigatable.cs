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
    public interface IInstigatable
    {
        Task<IHttpResponse> Instigate(
                IApplication httpApp, IHttpRequest routeData,
                ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess);
    }

    public interface IInstigate : IInstigatable
    {
        bool CanInstigate(ParameterInfo parameterInfo);

        //Task<IHttpResponse> Instigate(
        //        IApplication httpApp, IHttpRequest request,
        //        ParameterInfo parameterInfo,
        //    Func<object, Task<IHttpResponse>> onSuccess);
    }
}
