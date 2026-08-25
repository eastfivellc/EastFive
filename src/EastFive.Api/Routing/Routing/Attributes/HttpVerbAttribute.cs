using EastFive.Api.Bindings;
using EastFive.Api.Resources;
using EastFive.Api.Serialization;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Linq.Async;
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
    public abstract class HttpVerbAttribute : Attribute, IMatchRoute, IDocumentMethod
    {
        private bool matchAllParameters = true;
        public bool MatchAllParameters
        {
            get
            {
                return matchAllParameters;
            }
            set
            {
                matchAllParameters = value;
                matchAllBodyParameters = value;
                matchAllQueryParameters = value;
            }
        }

        private bool matchAllBodyParameters = false;
        public bool MatchAllBodyParameters
        {
            get
            {
                return matchAllBodyParameters;
            }
            set
            {
                if (!value)
                    matchAllParameters = false;
                matchAllBodyParameters = value;
            }
        }

        private bool matchAllQueryParameters = true;
        public bool MatchAllQueryParameters
        {
            get
            {
                return matchAllQueryParameters;
            }
            set
            {
                if (!value)
                    matchAllQueryParameters = false;
                matchAllQueryParameters = value;
            }
        }

        private bool matchFileParameter = true;
        public bool MatchFileParameter
        {
            get
            {
                return matchFileParameter;
            }
            set
            {
                if (!value)
                    matchFileParameter = false;
                matchFileParameter = value;
            }
        }

        public abstract string Method { get; }

        public virtual Method GetMethod(Route route, MethodInfo methodInfo, HttpApplication httpApp)
        {
            var path = new Uri($"/{route.Namespace}/{route.Name}", UriKind.Relative);
            return new Method(this.Method, methodInfo, route, path, httpApp);
        }

        // ---- IMatchRoute ----------------------------------------------------
        // Default route-template synthesis: path is the controller's
        // namespace/route (route falls back to the controller's class name),
        // optionally followed by an open-ended path segment when the method
        // has a [QueryId] / [QueryParameter(CheckFileName=true)] parameter.
        // [HttpAction] subclasses append the action name and may override.

        public virtual RouteTemplate GetRouteTemplate(MethodInfo method, IInvokeResource controller)
        {
            var nsSegment = string.IsNullOrWhiteSpace(controller.Namespace) ? "api" : controller.Namespace;
            var routeSegment = !string.IsNullOrWhiteSpace(controller.Route)
                ? controller.Route
                : method.DeclaringType?.Name;
            if (string.IsNullOrWhiteSpace(routeSegment))
                routeSegment = string.Empty;

            var pattern = new System.Text.StringBuilder("^/?")
                .Append(System.Text.RegularExpressions.Regex.Escape(nsSegment))
                .Append('/')
                .Append(System.Text.RegularExpressions.Regex.Escape(routeSegment));

            AppendActionSegment(pattern, method);

            // Let each parameter-level IModifyRoutePattern attribute splice
            // whatever regex fragment it needs (trailing capture, multiple
            // path captures, alternations, ...). Every modifier runs in
            // parameter / attribute order; each sees the result of the
            // previous one.
            var finalPattern = ApplyRoutePatternModifiers(method, pattern.ToString());
            pattern.Clear();
            pattern.Append(finalPattern);

            pattern.Append("/?$");

            var regex = new System.Text.RegularExpressions.Regex(pattern.ToString(),
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
                | System.Text.RegularExpressions.RegexOptions.Compiled);

            var verbs = GetVerbs();
            return new RouteTemplate(verbs, regex);
        }

        /// <summary>
        /// Hook for <see cref="HttpActionAttribute"/> to splice <c>/Action</c>
        /// into the path. Default: no-op.
        /// </summary>
        protected virtual void AppendActionSegment(
            System.Text.StringBuilder pattern, MethodInfo method)
        {
        }

        /// <summary>
        /// HTTP verbs (case-insensitive) this attribute accepts. Default
        /// returns <see cref="Method"/>; <see cref="HttpActionAttribute"/>
        /// overrides to surface its real HTTP verb.
        /// </summary>
        protected virtual string[] GetVerbs() => new[] { this.Method };

        /// <summary>
        /// Walk every parameter attribute that implements
        /// <see cref="IModifyRoutePattern"/> and let each mutate the regex.
        /// All modifiers run, in parameter / attribute declaration order;
        /// each sees the output of the previous one. A modifier that wants
        /// to opt out simply returns <paramref name="pattern"/> unchanged.
        /// </summary>
        private static string ApplyRoutePatternModifiers(MethodInfo method, string pattern)
        {
            foreach (var p in method.GetParameters())
            {
                foreach (var modifier in p.GetAttributesInterface<IModifyRoutePattern>())
                {
                    var next = modifier.ModifyRoutePattern(method, p, pattern);
                    if (next != null)
                        pattern = next;
                }
            }
            return pattern;
        }
    }
}
