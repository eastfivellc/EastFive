using System;
using System.Reflection;

using EastFive.Api.Binding.Scopes;

namespace EastFive.Api.Binding
{
    /// <summary>
    /// V3 selection attribute for <c>MutateResource&lt;T&gt;</c> parameters.
    /// Selects the request body as the source (same accessor as
    /// <see cref="ResourceAttribute"/>), then <see cref="IProvideMemberScope"/>
    /// declares <see cref="PatchBody"/> as the active scope. The actual
    /// body→mutator construction is performed by
    /// <see cref="EastFive.Api.Bindings.MutateResourceBinder"/>.
    ///
    /// <para>Usage:
    /// <code>
    /// public static Task&lt;IHttpResponse&gt; UpdateAsync(
    ///     [StorageEntityFromQueryId(Name = "id")] StorageEntity&lt;ChatAgent&gt; agent,
    ///     [MutateEntity] MutateResource&lt;ChatAgent&gt; mutate,
    ///     ContentTypeResponse&lt;ChatAgent&gt; onUpdated) { ... }
    /// </code>
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class MutateEntityAttribute : Attribute, IBindFromRequest, IProvideMemberScope
    {
        Type IProvideMemberScope.MemberScope => typeof(PatchBody);

        public bool TrySelectSource(IRequestEnvelopeV3 envelope, ParameterInfo parameter,
            out BindCall call)
        {
            if (EnvelopeBodyAccessor.TryGetBodyRoot(envelope, out var root, out var _))
            {
                call = BindCalls.FromSource(root, string.Empty);
                return true;
            }
            if (parameter.HasDefaultValue) { call = BindCalls.NotPresent; return true; }
            call = null;
            return false;
        }
    }
}
