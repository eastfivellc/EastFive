using System;

namespace EastFive.Api.Meta.Flows.Scripted
{
    /// <summary>
    /// Marker helpers used inside a scripted flow expression. None of these execute at
    /// runtime — each one is recognized by <see cref="FlowScriptReader"/> as a specific node
    /// in the captured expression tree:
    /// <list type="bullet">
    /// <item><see cref="NewId{T}"/> renders a client-generated id as <c>{{$guid}}</c>.</item>
    /// <item><see cref="Input{T}"/> renders an externally supplied environment variable as <c>{{name}}</c>.</item>
    /// <item><see cref="Capture"/> terminates a flow while exporting step outputs as
    /// environment variables.</item>
    /// </list>
    /// </summary>
    public static class Flow
    {
        /// <summary>
        /// A client-generated identifier. In the emitted request body this renders as the
        /// Postman dynamic variable <c>{{$guid}}</c>. Assign it to an <see cref="IRef{T}"/>
        /// member of a resource body to mint that resource's id on the client.
        /// </summary>
        public static IRef<T> NewId<T>()
            where T : IReferenceable
            => default;

        /// <summary>
        /// An externally supplied value referenced by Postman environment variable name. In
        /// the emitted request this renders as <c>{{name}}</c>. Use for flow inputs that are
        /// provided by the environment rather than produced by an earlier step.
        /// </summary>
        public static T Input<T>(string name)
            => default;

        /// <summary>
        /// Terminates a flow while exporting the referenced step outputs as Postman
        /// environment variables. Captures are normally demand-driven — a value is exported
        /// only when a later step references it — so a flow's final outputs (e.g. a session
        /// token) would otherwise never be captured. Each argument must be a member access on
        /// an earlier step's output parameter; the variable takes the member's name.
        /// </summary>
        public static FlowNode Capture(params object[] values)
            => default;
    }
}
