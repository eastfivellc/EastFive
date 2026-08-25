using System.Collections.Generic;

using EastFive.Api;

namespace EastFive.Api.Binding
{
    /// <summary>
    /// V3 request envelope. Holds raw, format-specific data; does NOT expose
    /// <see cref="EastFive.Serialization.Binding.IBindingSource"/> properties. Method
    /// selection is performed by parameter-attribute <see cref="IBindFromRequest"/>
    /// implementations inspecting whatever raw shapes they understand.
    /// <para>
    /// Each deserializer (JSON, form, multipart, query-only, raw) implements this
    /// alongside the legacy <see cref="EastFive.Api.IRequestEnvelope"/> for
    /// coexistence; V3 endpoints opt-in via attributes implementing
    /// <see cref="IBindFromRequest"/>.
    /// </para>
    /// </summary>
    public interface IRequestEnvelopeV3
    {
        /// <summary>
        /// Hand back the body in the requested shape if this envelope holds it.
        /// Typical shapes: <c>Newtonsoft.Json.Linq.JToken</c> (JSON envelopes),
        /// <c>Microsoft.AspNetCore.Http.IFormCollection</c> (form / multipart),
        /// <c>byte[]</c> or <c>Stream</c> (raw). Envelopes only answer for the
        /// shapes they actually produce; everything else returns false.
        /// </summary>
        bool TryGetBody<TBody>(out TBody body);

        /// <summary>Materialized query string. Empty (not null) when absent.</summary>
        IReadOnlyDictionary<string, string[]> Query { get; }

        /// <summary>
        /// Route-template captures (e.g. <c>{id}</c> regex groups). Empty when
        /// absent; never null.
        /// </summary>
        IReadOnlyDictionary<string, string> Route { get; }

        /// <summary>
        /// The originating request — exposed verbatim so attributes that look at
        /// headers, raw bytes, the URL, etc. have a single well-known path.
        /// </summary>
        IHttpRequest Request { get; }
    }
}
