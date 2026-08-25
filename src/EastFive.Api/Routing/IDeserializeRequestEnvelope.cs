using System.Threading.Tasks;

namespace EastFive.Api
{
    /// <summary>
    /// Attribute interface that produces an <see cref="IRequestEnvelope"/> for
    /// the <c>FunctionViewControllerAttribute</c> pipeline. Discovered via
    /// the Attribute Interface pattern (<c>GetAttributesInterface</c>) on
    /// <c>httpApp.GetType()</c> only — application-level scope.
    ///
    /// Per-controller / per-method deserializers are intentionally NOT
    /// supported: a candidate method's deserializer choice could otherwise
    /// leak into requests where that candidate ultimately loses Stage 3
    /// (requirement-based disambiguation), forcing the winning method onto
    /// an unintended wire format. Wire format is a property of the request,
    /// not of any one method. If a route needs a bespoke parse, it has two
    /// well-typed options:
    /// <list type="bullet">
    ///   <item>Tighten an app-level deserializer's <see cref="CanClassify"/>
    ///   so it claims only the requests it understands.</item>
    ///   <item>Register a <c>BindCallback&lt;byte[]&gt;</c> /
    ///   <c>BindCallback&lt;Stream&gt;</c> on the parameter's
    ///   <see cref="BindingRequirement"/> and parse inline — the
    ///   <c>RawRequestEnvelope</c> already supplies raw bytes.</item>
    /// </list>
    ///
    /// Selection rules:
    /// <list type="number">
    ///   <item>Filter app-level attributes by <see cref="CanClassify"/>.
    ///   <see cref="CanClassify"/> is header-only — implementations must NOT
    ///   read the body, form, or call <c>HasBody</c>.</item>
    ///   <item>Pick the candidate with the highest <see cref="Priority"/>.
    ///   Priority ties are logged via <c>Trace.TraceWarning</c> — they are
    ///   a configuration error, not a request-shape concern.</item>
    ///   <item>If the filtered set is empty, the request returns 501.</item>
    /// </list>
    /// </summary>
    public interface IDeserializeRequestEnvelope
    {
        /// <summary>
        /// Higher wins among <see cref="CanClassify"/> matches. Built-in
        /// defaults: query-only 5, json/form/multipart 10, raw -100.
        /// Implementations expose this as a settable property so the value
        /// can be overridden in attribute usage.
        /// </summary>
        double Priority { get; }

        /// <summary>
        /// Header-only test: can this deserializer handle the inbound request?
        /// MUST examine only headers/URL — never read body bytes, form data,
        /// or invoke <see cref="IHttpRequest.HasBody"/>.
        /// </summary>
        bool CanClassify(IHttpRequest request);

        /// <summary>
        /// Build the envelope. Called exactly once per request after
        /// classification commits to this deserializer. May read the body
        /// (and parse it) if needed; all parsing is amortized into the
        /// envelope's cached state, then exposed through
        /// <see cref="IRequestEnvelope.TryFulfill{T}"/> closures.
        /// </summary>
        Task<IRequestEnvelope> CreateEnvelopeAsync(IHttpRequest request);
    }
}
