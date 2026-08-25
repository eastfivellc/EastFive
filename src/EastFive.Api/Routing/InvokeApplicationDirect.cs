using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EastFive.Api.Core;
using EastFive.Api.Modules;
using EastFive.Extensions;
using EastFive.Linq;
using Microsoft.AspNetCore.Http;

namespace EastFive.Api
{
    [InvokeApplicationDirect.Instigate]
    public class InvokeApplicationDirect : InvokeApplication
    {
        public InvokeApplicationDirect(IApplication application, Uri serverUrl, string apiRouteName, System.Threading.CancellationToken token) 
            : base(serverUrl, apiRouteName)
        {
            this.application = application;
            this.token = token;
        }

        private readonly IApplication application;
        private readonly System.Threading.CancellationToken token;

        public override IApplication Application => application;

        public override Task<IHttpResponse> SendAsync(IHttpRequest httpRequest)
        {
            return Middleware.InvokeRequestAsync(httpRequest, application,
                () =>
                {
                    throw new Exception();
                });
        }

        public class InstigateAttribute : Attribute, IInstigatable
        {
            public Task<IHttpResponse> Instigate(IApplication httpApp, 
                    IHttpRequest request,
                    ParameterInfo parameterInfo,
                Func<object, Task<IHttpResponse>> onSuccess)
            {
                string GetApiPrefix()
                {
                    return "api";
                }
                var instance = new InvokeApplicationDirect(httpApp,
                    request.RequestUri, GetApiPrefix(), request.CancellationToken);
                return onSuccess(instance);
            }
        }
    }
}
