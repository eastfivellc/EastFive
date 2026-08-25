using System;
using System.Globalization;

namespace EastFive.Serialization.Binding
{
    /// <summary>
    /// Ambient information carried alongside a bind call: the active type-binding
    /// table, the slot being filled (if any), a dotted key path for diagnostics,
    /// and culture. Capabilities (e.g. <c>IApplication</c>) are deliberately not
    /// part of this interface — they're injected when constructing the
    /// <see cref="ITypeBindings"/> via factory methods like
    /// <see cref="ITypeBindings.ForSlot"/> or assembly-specific entry points.
    /// </summary>
    public interface IBindingContext
    {
        ITypeBindings TypeBindings { get; }

        /// <summary>The slot currently being filled, if any (POCO member, method parameter).</summary>
        IBindingSlot Slot { get; }

        string KeyPath { get; }

        CultureInfo Culture { get; }

        /// <summary>
        /// Scope-specific member selector consulted by <c>PocoBinder</c>. Determines
        /// <em>which</em> members of a complex type are bindable and under what
        /// wire-names. Orthogonal to <see cref="ITypeBindings"/>, which determines
        /// <em>how</em> a single value is bound.
        /// </summary>
        IMemberPlanProvider MemberPlanProvider { get; }

        /// <summary>
        /// Active member-scope marker passed to
        /// <see cref="IMemberPlanProvider.GetPlan(System.Type, System.Type)"/>.
        /// Identifies the binding purpose (e.g. request body, PATCH body,
        /// storage row) so the provider can decide which members of a complex
        /// type participate. Must be set by the binding driver (the API
        /// dispatcher, a storage column reader, etc.) before any complex-type
        /// bind that walks members; <c>PocoBinder</c> rejects null.
        /// </summary>
        Type MemberScope { get; }

        IBindingContext WithSlot(IBindingSlot slot);

        IBindingContext WithKeyPath(string keyPath);

        IBindingContext WithTypeBindings(ITypeBindings typeBindings);

        IBindingContext WithMemberPlanProvider(IMemberPlanProvider memberPlanProvider);

        IBindingContext WithMemberScope(Type memberScope);
    }
}
