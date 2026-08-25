using System.Linq;

namespace EastFive.Api.Meta.Flows.Scripted
{
    /// <summary>
    /// Authoring annotations for scripted flows. These are identity functions at runtime —
    /// they return their input unchanged — and exist only so the author can attach Postman
    /// metadata that <see cref="FlowScriptReader"/> reads off the expression tree.
    /// <para><see cref="InScope{T}"/> and <see cref="Named{T}"/> decorate the flow source
    /// (the <c>IQueryable&lt;T&gt;</c>) BEFORE the controller-method call, e.g.
    /// <c>api.SignatureLocations.InScope("Signature Location").Named("Create location").CreateAsync(...)</c>.</para>
    /// </summary>
    public static class FlowAnnotations
    {
        /// <summary>
        /// Places the step in a Postman folder. Chain on the flow source before the call.
        /// </summary>
        public static IQueryable<T> InScope<T>(this IQueryable<T> source, string scope)
            => source;

        /// <summary>
        /// Sets the Postman request (step) name. Chain on the flow source before the call.
        /// </summary>
        public static IQueryable<T> Named<T>(this IQueryable<T> source, string name)
            => source;

        /// <summary>
        /// Attaches a description to a body value (e.g. a cross-step reference). Returns the
        /// value unchanged; the description surfaces in the generated request documentation.
        /// </summary>
        public static T PostmanDescription<T>(this T value, string description)
            => value;
    }
}
