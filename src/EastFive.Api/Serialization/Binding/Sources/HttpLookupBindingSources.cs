using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Microsoft.AspNetCore.Http;

using EastFive.Serialization.Binding;

namespace EastFive.Api.Serialization.Binding.Sources
{
    /// <summary>
    /// Convenience factories that adapt the ASP.NET form/query primitives onto
    /// <see cref="LookupBindingSource"/>. Form and query share a single underlying
    /// shape (flat key → multi-valued strings); this class isolates the framework-
    /// dependent surface so binders never see <c>IFormCollection</c>/
    /// <c>IQueryCollection</c> directly.
    /// </summary>
    public static class HttpLookupBindingSources
    {
        /// <summary>Wrap an <see cref="IFormCollection"/>.</summary>
        /// <remarks>
        /// File uploads (<see cref="IFormCollection.Files"/>) are not yet exposed
        /// through this source; consumers needing <c>IFormFile</c> bytes should
        /// pull them directly from the form until a binder lands for that shape.
        /// </remarks>
        public static LookupBindingSource ForForm(IFormCollection form, CultureInfo culture = null)
        {
            var pairs = form.Select(kv => new KeyValuePair<string, string[]>(kv.Key, kv.Value.ToArray()));
            return new LookupBindingSource(pairs, culture);
        }

        /// <summary>Wrap an <see cref="IQueryCollection"/>.</summary>
        public static LookupBindingSource ForQuery(IQueryCollection query, CultureInfo culture = null)
        {
            var pairs = query.Select(kv => new KeyValuePair<string, string[]>(kv.Key, kv.Value.ToArray()));
            return new LookupBindingSource(pairs, culture);
        }

        /// <summary>Wrap a parsed query lookup as returned by EastFive's
        /// <c>RequestUri.ParseQuery</c>.</summary>
        public static LookupBindingSource ForQuery(IDictionary<string, string> query, CultureInfo culture = null)
        {
            var pairs = query.Select(kv => new KeyValuePair<string, string[]>(
                kv.Key, kv.Value is null ? System.Array.Empty<string>() : new[] { kv.Value }));
            return new LookupBindingSource(pairs, culture);
        }
    }
}
