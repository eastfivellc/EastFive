using System;

namespace EastFive.Api.Meta.Flows.Scripted
{
    /// <summary>
    /// Stamped by the flow generator on each generated <c>IQueryable&lt;T&gt;</c> extension
    /// method so <see cref="FlowScriptReader"/> can map a call in the flow expression back to
    /// the originating controller method (for HTTP verb, route, namespace, and response
    /// metadata). Hand-applied in the Phase-1 spike; emitted by the generator thereafter.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class FlowMethodAttribute : Attribute
    {
        /// <summary>The controller resource type that declares the originating method.</summary>
        public Type ResourceType { get; }

        /// <summary>The originating controller method name.</summary>
        public string MethodName { get; }

        /// <summary>
        /// Ordered parameter names of the originating controller method, used to disambiguate
        /// overloads. May be empty when the method name is unique on the resource.
        /// </summary>
        public string[] ParameterNames { get; }

        public FlowMethodAttribute(Type resourceType, string methodName, params string[] parameterNames)
        {
            this.ResourceType = resourceType;
            this.MethodName = methodName;
            this.ParameterNames = parameterNames ?? Array.Empty<string>();
        }
    }
}
