using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public interface IHttpResponseMessageException
    {
        IHttpResponse CreateResponseAsync(IApplication httpApp,
            IHttpRequest routeData, Dictionary<string, object> queryParameterOptions, 
            MethodInfo method, object[] methodParameters);
    }
}
