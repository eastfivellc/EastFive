using System;
using System.Linq;
using System.Reflection;

using EastFive.Linq;

namespace EastFive.Api.Routing
{
    /// <summary>
    /// One <see cref="RouteCandidate"/> resolved against the request envelope:
    /// each parameter has a <see cref="ParameterFulfillment"/> describing
    /// whether the envelope can supply its value. Produced by
    /// <see cref="MethodDispatcher.BuildMatches"/>.
    /// </summary>
    public readonly struct MethodMatch
    {
        public MethodMatch(Type controllerType, IInvokeResource invokeResource,
            MethodInfo method, ParameterFulfillment[] fulfillments)
        {
            this.ControllerType = controllerType;
            this.InvokeResource = invokeResource;
            this.Method = method;
            this.Fulfillments = fulfillments ?? Array.Empty<ParameterFulfillment>();
        }

        public Type ControllerType { get; }
        public IInvokeResource InvokeResource { get; }
        public MethodInfo Method { get; }
        public ParameterFulfillment[] Fulfillments { get; }

        public bool IsValid => this.Fulfillments.All(f => f.IsValid);

        public string ErrorMessage
            => string.Join(", ", this.Fulfillments
                .Where(f => !f.IsValid)
                .Select(f => $"{f.Path}: envelope could not fulfill"));
    }
}
