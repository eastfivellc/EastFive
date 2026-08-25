using System.Collections.Generic;
using System.Reflection;

namespace EastFive.Api
{
    /// <summary>
    /// Closure produced by <see cref="IProvideBindingRequirements.GetParameterBinding"/>.
    /// Receives the bound sub-values (in the same order as the requirements
    /// list the closure was paired with) and returns the final
    /// <c>value</c> handed to the controller method, plus an optional
    /// <c>context</c> object that rides in the orchestrator's per-parameter
    /// binding-context dictionary for downstream validators.
    /// </summary>
    /// <remarks>
    /// The closure is the single place an attribute publishes the results of
    /// its bind step. Per-parameter precomputation (key-member discovery,
    /// wire-name resolution, entity-type extraction) lives in the body of
    /// <see cref="IProvideBindingRequirements.GetParameterBinding"/> and is
    /// captured by the closure rather than recomputed per call.
    /// Single-requirement attributes typically return
    /// <c>values =&gt; (values[0], null)</c>.
    /// </remarks>
    public delegate (object value, object context) AssembleParameter(object[] boundValues);

    /// <summary>
    /// Marker + provider for parameter attributes whose value comes from the
    /// inbound request (URL, query string, body, form, multipart, etc.).
    /// Implementing this interface is what makes an attribute "binding-class"
    /// in routing: the selector iterates each binding-class parameter,
    /// calls <see cref="GetParameterBinding"/>, and asks the chosen
    /// <see cref="IRequestEnvelope"/> whether it can fulfil each
    /// <see cref="BindingRequirement"/> in the returned list. After every
    /// requirement is fulfilled the <see cref="AssembleParameter"/> closure
    /// produces the final parameter value and optional binding context.
    ///
    /// Parameters whose attributes do not implement this interface are
    /// instigator parameters resolved via <c>httpApp.Instigate(...)</c>.
    ///
    /// Implementing attributes: <c>[QueryId]</c>, <c>[Property]</c>,
    /// <c>[PropertyOptional]</c>, <c>[QueryParameter]</c>,
    /// <c>[OptionalQueryParameter]</c>, <c>[Resource]</c>, <c>[UpdateId]</c>,
    /// <c>[Header]</c>, <c>[Accepts]</c>, <c>[HashedFile]</c>,
    /// <c>[StorageEntityFromQueryId]</c>, <c>[StorageEntities]</c>, etc.
    ///
    /// v2 routing is unaffected — it continues to use the attributes' existing
    /// <c>TryCast</c> logic.
    /// </summary>
    /// <remarks>
    /// Single-value attributes (e.g. <c>[QueryId]</c>) return a one-element
    /// requirements array and an assemble closure that projects the only
    /// bound value: <c>values =&gt; (values[0], null)</c>.
    ///
    /// Multi-value attributes (e.g. <c>[StorageEntityFromQueryId]</c>)
    /// return one requirement per sub-value and an assemble closure that
    /// combines them into a single parameter value plus an optional
    /// per-parameter context (used by the storage loader to ferry wire-level
    /// bindings to its post-bind validator for diagnostic 404 messages).
    /// </remarks>
    public interface IProvideBindingRequirements
    {
        /// <summary>
        /// Build the requirements this parameter wants from the request
        /// envelope and an <see cref="AssembleParameter"/> closure that turns
        /// the bound values into the final <c>(value, context)</c> pair.
        ///
        /// The closure is invoked once, after every requirement in the
        /// returned list has been fulfilled, in the same order. The
        /// <c>context</c> output is carried in a side-channel dictionary
        /// keyed by <see cref="ParameterInfo"/> from bind to validate;
        /// validators read it via
        /// <see cref="IValidateHttpRequestForBoundParameters"/>. Return
        /// <c>null</c> for <c>context</c> when the attribute has nothing
        /// extra to publish.
        /// </summary>
        (IReadOnlyList<BindingRequirement> requirements, AssembleParameter assemble)
            GetParameterBinding(ParameterInfo parameter);
    }
}
