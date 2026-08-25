using System;

using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;

using EastFive.Api.Serialization.Binding.Sources;
using EastFive.Serialization.Binding;

namespace EastFive.Api.Binding
{
    /// <summary>
    /// Shared helpers for selection-time attribute implementations that draw
    /// values from the request body. Centralizes the small raw-shape ladder so
    /// each attribute does not re-implement <c>TryGetBody&lt;JToken&gt;</c> /
    /// <c>TryGetBody&lt;IFormCollection&gt;</c> arms.
    /// <para>
    /// Current shapes: <see cref="JToken"/> → <see cref="JTokenBindingSource"/>,
    /// <see cref="IFormCollection"/> → <see cref="LookupBindingSource"/>. New raw
    /// shapes (e.g. multipart streams) extend this single ladder rather than
    /// every attribute.
    /// </para>
    /// </summary>
    public static class EnvelopeBodyAccessor
    {
        /// <summary>
        /// Hand back an <see cref="IBindingSource"/> rooted at the request body,
        /// if this envelope carries one. Caller wraps it via
        /// <see cref="BindCalls.FromSource"/> with the desired path prefix.
        /// </summary>
        public static bool TryGetBodyRoot(IRequestEnvelopeV3 envelope, 
            out IBindingSource source, out object rawBody)
        {
            if (envelope is null) throw new ArgumentNullException(nameof(envelope));

            if (envelope.TryGetBody<JToken>(out var jtoken) && jtoken is not null)
            {
                source = new JTokenBindingSource(jtoken);
                rawBody = jtoken;
                return true;
            }

            if (envelope.TryGetBody<IFormCollection>(out var form) && form is not null)
            {
                source = HttpLookupBindingSources.ForForm(form);
                rawBody = form;
                return true;
            }

            source = null;
            rawBody = null;
            return false;
        }

        /// <summary>
        /// Compose a path prefix with a relative path segment using dotted /
        /// bracketed conventions shared by all sources: empty relative → prefix
        /// as-is; bracketed relative stays flush against the parent key; dotted
        /// everywhere else.
        /// </summary>
        public static string ComposePath(string prefix, string relative)
        {
            if (string.IsNullOrEmpty(relative)) return prefix ?? string.Empty;
            if (string.IsNullOrEmpty(prefix)) return relative;
            if (relative[0] == '[') return prefix + relative;
            return prefix + "." + relative;
        }
    }
}
