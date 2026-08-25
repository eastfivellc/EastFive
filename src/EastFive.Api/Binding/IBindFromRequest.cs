using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace EastFive.Api.Binding
{
    /// <summary>
    /// V3 method-selection contract for parameter attributes. The dispatcher walks
    /// every candidate method's parameters; any parameter carrying an attribute
    /// that implements this interface participates in selection. The attribute
    /// either produces a <see cref="BindCall"/> closure rooted at the parameter's
    /// value (selection match) or returns <c>false</c> (this method doesn't apply).
    /// <para>
    /// Selection is intentionally <b>synchronous</b>: the envelope's data is
    /// already materialized (JToken / form collection / dict in memory), and
    /// selection runs across every candidate so cost must stay low.
    /// </para>
    /// <para>
    /// <b>Closure shape.</b> Attributes return a <see cref="BindCall"/> rather
    /// than an <see cref="EastFive.Serialization.Binding.IBindingSource"/> — the
    /// scalar cases (Query, Route, Header) capture the single value in a closure
    /// instead of allocating a <c>LookupBindingSource</c> + path-prefix wrapper
    /// for each parameter on each request. Body attributes wrap an existing
    /// <see cref="EastFive.Serialization.Binding.IBindingSource"/> via
    /// <see cref="BindCalls.FromSource"/>. Optional-absent contributions return
    /// <see cref="BindCalls.NotPresent"/> rather than <c>false</c>.
    /// </para>
    /// <para>
    /// Surface validation (is it a Guid? a date?) is <b>not</b> the attribute's
    /// job — that belongs to the bind phase via <c>TypeBindings</c>.
    /// </para>
    /// </summary>
    public interface IBindFromRequest
    {
        /// <summary>
        /// Return <c>true</c> with a <paramref name="call"/> closure rooted at the
        /// parameter's value, or <c>false</c> if this envelope cannot supply it.
        /// </summary>
        bool TrySelectSource(IRequestEnvelopeV3 envelope, ParameterInfo parameter,
            out BindCall call);

        /// <summary>
        /// The URL query-string key(s) this parameter <b>claims</b> during V3 method
        /// selection. Keys are matched case-insensitively against the request query.
        /// <para>
        /// The dispatcher rejects a candidate method that leaves any query key
        /// unclaimed (see <see cref="MethodDispatcherV3"/>), restoring the V2
        /// "all query parameters must be matched" rule: a request's query string is
        /// part of method identity, so a data-free list endpoint (whose only bound
        /// parameter is, e.g., <c>[StorageEntities] IQueryable&lt;T&gt;</c>) no
        /// longer shadows a keyed by-id endpoint sharing the same route and verb.
        /// </para>
        /// <para>
        /// This is a property of the parameter's signature, not of any one request:
        /// an <b>optional</b> query parameter claims its key whether or not the key
        /// is present (matching V2, where an optional parameter was a valid way to
        /// "consume" a query parameter that was not required). The default
        /// implementation claims nothing — only attributes that actually read the
        /// query string (Query, QueryOptional, the query-sourced storage loaders)
        /// override it.
        /// </para>
        /// </summary>
        IEnumerable<string> GetConsumedQueryKeys(ParameterInfo parameter)
            => Enumerable.Empty<string>();
    }
}
