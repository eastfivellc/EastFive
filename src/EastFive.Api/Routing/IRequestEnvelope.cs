namespace EastFive.Api
{
    /// <summary>
    /// Opaque, deserializer-produced view of an inbound HTTP request used by
    /// the <c>FunctionViewControllerAttribute</c> pipeline.
    ///
    /// The envelope exposes a single non-generic method,
    /// <see cref="TryFulfill"/>. Given a <see cref="BindingRequirement"/>, the
    /// envelope iterates the requirement's converter table, picks any
    /// <c>TRaw</c> it can produce at the requested
    /// <see cref="BindingRequirement.Source"/>/<see cref="BindingRequirement.Path"/>,
    /// and on success returns an <see cref="ExtractAsyncDelegate"/> that
    /// invokes the matching converter.
    ///
    /// The <c>bool</c> answers "do you have this?" — used during method
    /// selection to decide whether a candidate method matches. The
    /// <see cref="ExtractAsyncDelegate"/> body must NOT execute during
    /// selection; binding awaits it after the method has committed.
    ///
    /// The set of producible raw types is intentionally narrow per envelope
    /// (e.g. JSON yields <c>JContainer</c>/<c>JToken</c>/<c>string</c>;
    /// form yields <c>IFormCollection</c>/<c>string</c>; raw yields
    /// <c>byte[]</c>/<c>Stream</c>/<c>string</c>). Binding attributes pick
    /// from that menu via <see cref="BindingRequirement.AddConverter{TRaw}"/>.
    /// </summary>
    public interface IRequestEnvelope
    {
        /// <summary>
        /// Selection + extraction in one call. Returns <c>true</c> if this
        /// envelope can satisfy any of <paramref name="requirement"/>'s
        /// registered converters; in that case <paramref name="extract"/> is
        /// set to a closure that produces the raw value and dispatches it
        /// through the matching converter.
        /// </summary>
        bool TryFulfill(BindingRequirement requirement, out ExtractAsyncDelegate extract);
    }
}
