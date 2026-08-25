using System;

namespace EastFive.Api.Meta.Flows.Scripted
{
    /// <summary>
    /// Marks a static member (property or parameterless method) whose value is the flow
    /// expression — a <c>System.Linq.Expressions.Expression&lt;System.Func&lt;TApi, FlowNode&gt;&gt;</c>.
    /// <see cref="FlowScriptReader"/> discovers flows by this attribute and emits one Postman
    /// collection per flow.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ScriptedFlowAttribute : Attribute
    {
        /// <summary>The Postman collection / flow name.</summary>
        public string Name { get; }

        /// <summary>The flow version, surfaced in the collection description.</summary>
        public string Version { get; }

        public ScriptedFlowAttribute(string name, string version)
        {
            this.Name = name;
            this.Version = version;
        }
    }
}
