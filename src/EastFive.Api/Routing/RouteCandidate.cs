using System;
using System.Collections.Generic;
using System.Reflection;

namespace EastFive.Api.Routing
{
    /// <summary>
    /// One (controller, method, template, captures) tuple surviving the
    /// per-request verb+path filter. Produced by
    /// <see cref="EastFive.Api.Core.RouteTable.Match"/> and consumed by
    /// <see cref="MethodDispatcher.BuildMatches"/>.
    /// </summary>
    public readonly struct RouteCandidate
    {
        public RouteCandidate(Type controllerType, IInvokeResource invokeResource,
            MethodInfo method, RouteTemplate template,
            IReadOnlyDictionary<string, string> captures)
        {
            this.ControllerType = controllerType;
            this.InvokeResource = invokeResource;
            this.Method = method;
            this.Template = template;
            this.Captures = captures;
        }

        public Type ControllerType { get; }
        public IInvokeResource InvokeResource { get; }
        public MethodInfo Method { get; }
        public RouteTemplate Template { get; }
        public IReadOnlyDictionary<string, string> Captures { get; }
    }
}
