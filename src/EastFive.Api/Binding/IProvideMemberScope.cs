using System;

using EastFive.Serialization.Binding;

namespace EastFive.Api.Binding
{
    /// <summary>
    /// Optional contract on an <see cref="IBindFromRequest"/> attribute that
    /// declares the member scope active while binding the chosen source. The
    /// V3 dispatcher reads this when present and stamps
    /// <see cref="IBindingContext.MemberScope"/> via
    /// <see cref="IBindingContext.WithMemberScope"/> before invoking
    /// <see cref="ITypeBindings.Bind"/>. <c>PocoBinder</c> requires a scope
    /// at the point it walks members of a complex type.
    ///
    /// <para>Built-in mappings:
    /// <list type="bullet">
    ///   <item><c>[Body]</c>, <c>[Resource]</c> → <c>RequestBody</c>.</item>
    ///   <item><c>[Query]</c>, <c>[Route]</c>, <c>[Header]</c> → <c>QueryString</c>.</item>
    ///   <item><c>[MutateEntity]</c> → <c>PatchBody</c>.</item>
    /// </list>
    /// </para>
    /// </summary>
    public interface IProvideMemberScope
    {
        Type MemberScope { get; }
    }
}
