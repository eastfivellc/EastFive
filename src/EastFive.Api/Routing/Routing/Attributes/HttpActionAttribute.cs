using EastFive.Api.Bindings;
using EastFive.Api.Meta.Flows;
using EastFive.Api.Meta.Postman.Resources.Collection;
using EastFive.Api.Resources;
using EastFive.Api.Serialization;
using EastFive.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
    public class HttpActionAttribute : HttpVerbAttribute, IProvideWorkflowUrl
    {
        private readonly string httpMethod;

        public HttpActionAttribute(string method)
        {
            this.httpMethod = HttpMethod.Get.Method;
            this.Action = method;
        }

        public HttpActionAttribute(string httpMethod, string action)
        {
            this.httpMethod = httpMethod;
            this.Action = action;
        }

        public string Action { get; set; }

        public override string Method => Action;

        public override Method GetMethod(Route route, MethodInfo methodInfo, HttpApplication httpApp)
        {
            var path = new Uri($"/{route.Namespace}/{route.Name}/{Action}", UriKind.Relative);
            return new Method(this.httpMethod, methodInfo, route, path, httpApp);
        }

        // ---- IMatchRoute ----------------------------------------------------
        // [HttpAction] splices the action name into the path and surfaces its
        // configured HTTP verb instead of the action label that base.Method
        // returns.

        protected override void AppendActionSegment(
            System.Text.StringBuilder pattern, MethodInfo method)
        {
            pattern.Append('/').Append(System.Text.RegularExpressions.Regex.Escape(this.Action ?? string.Empty));
        }

        protected override string[] GetVerbs() => new[] { this.httpMethod };

        public Url GetUrl(Api.Resources.Method method, QueryItem[] queryItems)
        {
            return new Url()
            {
                raw = $"{Url.VariableHostName}/{method.Route.Namespace}/{method.Route.Name}/{this.Action}",
                host = Url.VariableHostName.AsArray(),
                path = new string[] { method.Route.Namespace, method.Route.Name, this.Action },
                query = queryItems,
            };
        }
    }
}
