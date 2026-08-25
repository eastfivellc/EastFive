using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EastFive;
using EastFive.Api.Core;
using EastFive.Extensions;
using EastFive.Linq;
using Microsoft.AspNetCore.Http;

namespace EastFive.Api
{
    [IInvokeApplication]
    public interface IInvokeApplication : IProvideServerLocation
    {
        string ApiRouteName { get; }

        IDictionary<string, string> Headers { get; }

        IApplication Application { get; }

        RequestMessage<TResource> GetRequest<TResource>();

        Task<IHttpResponse> SendAsync(IHttpRequest httpRequest);

        IHttpRequest GetHttpRequest();
    }

    public class IInvokeApplicationAttribute : Attribute, IInstigatable
    {
        public virtual Task<IHttpResponse> Instigate(IApplication httpApp,
                IHttpRequest request,
                ParameterInfo parameterInfo,
            Func<object, Task<IHttpResponse>> onSuccess)
        {
            var instance = Instigate(httpApp, request);
            return onSuccess(instance);
        }

        public static IInvokeApplication Instigate(IApplication httpApp, IHttpRequest request)
        {
            var apiPrefix = GetApiPrefix(request);
            var serverLocation = GetServerLocation(request);
            var instance = new InvokeApplicationFromRequest(httpApp as HttpApplication, request, serverLocation, apiPrefix);
            return instance;
        }

        static protected Uri GetServerLocation(IHttpRequest request)
        {
            var url = request.GetAbsoluteUri();
            if (url.IsDefaultOrNull())
                return new Uri("http://example.com");

            var hostUrlString = url.GetLeftPart(UriPartial.Authority);
            return new Uri(hostUrlString);
        }

        static protected string GetApiPrefix(IHttpRequest request)
        {
            try
            {
                return request.GetAbsoluteUri().AbsolutePath.Trim('/'.AsArray()).Split('/'.AsArray()).First();
            }
            catch (Exception)
            {

            }
            return "api";
        }

        protected class InvokeApplicationFromRequest : InvokeApplication, IProvideHttpRequest
        {
            private IApplication httpApp;
            private IHttpRequest routeData;

            //private string[] apiRoute;

            public override IApplication Application => httpApp;

            public IHttpRequest HttpRequest => routeData;

            public InvokeApplicationFromRequest(IApplication httpApp, IHttpRequest routeData,
                    Uri serverLocation, string apiPrefix) : base(serverLocation, apiPrefix)
            {
                this.httpApp = httpApp;
                this.routeData = routeData;

                //var routes = request.GetConfiguration().Routes;

                //this.apiRoute = routes
                //    .Where(route => route.RouteTemplate.Contains("{action}"))
                //    .Select(
                //        route =>
                //        {
                //            return route.RouteTemplate;
                //        })
                //    .ToArray();
            }

            //protected override HttpConfiguration ConfigureRoutes(HttpRequestMessage httpRequest, HttpConfiguration config)
            //{
            //    config.Routes.MapHttpRoute(
            //        name: "DefaultApi",
            //        routeTemplate: "api/{controller}/{id}",
            //        defaults: new { id = RouteParameter.Optional }
            //    );
            //    return config;
            //}

            protected override RequestMessage<TResource> BuildRequest<TResource>(IApplication application)
            {
                return new RequestMessage<TResource>(this);
            }

            public override Task<IHttpResponse> SendAsync(IHttpRequest httpRequest)
            {
                throw new NotImplementedException();
                //using (var client = new HttpClient())
                //{
                //    var response = await client.SendAsync(httpRequest);
                //    return response;
                //}
            }

            public override IHttpRequest GetHttpRequest()
            {
                var requestMsg = base.GetHttpRequest();
                requestMsg.SetReferer(this.routeData.RequestUri);
                return requestMsg;
            }
        }
    }
}
