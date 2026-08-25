using System;

namespace EastFive.Api.Meta.Flows.Scripted
{
    /// <summary>
    /// Opt-in marker for the flow-extension source generator. Apply at assembly scope on a
    /// project that declares <c>[FunctionViewController]</c> endpoints to have the generator
    /// emit a single <c>{Assembly}Api</c> struct (the whole API surface, one
    /// <c>IQueryable&lt;T&gt;</c> member per controller) plus a delegates-last
    /// <c>IQueryable&lt;T&gt;</c> extension per controller method for authoring scripted flows.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class GenerateFlowApiAttribute : Attribute
    {
        /// <summary>
        /// Optional override for the generated API struct's type name. Defaults to the
        /// sanitized assembly name suffixed with <c>Api</c> (e.g. <c>RosemaryApi</c>).
        /// </summary>
        public string ApiTypeName { get; set; }
    }
}
