using System;

namespace EastFive.Serialization.Binding
{
    /// <summary>
    /// A single bindable member surfaced by an <see cref="IMemberPlanProvider"/>.
    /// All behaviors are exposed as methods so that consumers (binders, sinks,
    /// failure-decorators) never need to know the underlying wire-name, the
    /// reflection slot, or how the member is read/written. The concrete
    /// implementation is owned by the provider that produced it.
    /// </summary>
    public interface IMemberPlan
    {
        /// <summary>The runtime type of this member. Binders pass this to
        /// <see cref="ITypeBindings.ForSlot"/> on the scoped context to look
        /// up the appropriate <see cref="ITypeBinder"/>.</summary>
        Type MemberType { get; }

        /// <summary>Return a child binding context scoped to this member:
        /// composes the parent's <see cref="IBindingContext.KeyPath"/> with
        /// this member's wire-name and stamps the member's slot onto the
        /// returned context (so <c>context.TypeBindings.ForSlot(context.Slot)</c>
        /// resolves per-slot binder overrides correctly).</summary>
        IBindingContext ScopeInto(IBindingContext parent);

        /// <summary>Return a child sink scoped under this member's
        /// wire-name (<see cref="IBindingSink.Scope(string)"/>).</summary>
        IBindingSink ScopeInto(IBindingSink parent);

        /// <summary>Compose a full dotted path for this member, appended to
        /// <paramref name="root"/>. Returns just the wire-name when
        /// <paramref name="root"/> is null or empty.</summary>
        string ComposePath(string root);

        /// <summary>Wrap <paramref name="inner"/> with this member's wire-name
        /// as the key-path, so callers can re-emit failures without ever
        /// touching the raw wire-name.</summary>
        BindFailure DecorateFailure(BindFailure inner);

        /// <summary>Read this member's current value from
        /// <paramref name="instance"/>.</summary>
        object Read(object instance);

        /// <summary>Functional setter: returns the (possibly new) instance
        /// with this member updated to <paramref name="value"/>. For classes
        /// returns the same reference (mutated in place); for structs returns
        /// the boxed object whose underlying value reflects the assignment.
        /// Call sites assign the result:
        /// <c>instance = member.WithMember(instance, value);</c></summary>
        object WithMember(object instance, object value);
    }

    /// <summary>
    /// <b>Scope-specific member selection and iteration</b> for complex types.
    /// Where <see cref="ITypeBindings"/> answers <em>"which binder for type
    /// X?"</em>, <see cref="IMemberPlanProvider"/> answers <em>"how do I walk
    /// the members of type X under scope S?"</em>.
    ///
    /// <para>
    /// Carried on <see cref="IBindingContext.MemberPlanProvider"/>; the scope
    /// itself is carried on <see cref="IBindingContext.MemberScope"/>.
    /// Membership is strictly opt-in: members declare participation in a scope
    /// through attributes implementing
    /// <see cref="IIncludeInMemberScope{TScope}"/> (e.g. <c>[ApiProperty]</c>
    /// for the HTTP scopes, <c>[StorageProperty]</c> for storage). A type
    /// without any member opted into the requested scope yields no iterations
    /// — <em>no convention fallback</em>.
    /// </para>
    ///
    /// <para>The single iteration verb is a CPS fold mirroring
    /// <c>EastFive.Linq.EnumerableExtensions.Aggregate&lt;TItem,TAccum,TResult&gt;</c>;
    /// async usage is transparent (pass <c>TResult = Task&lt;X&gt;</c> and use an
    /// async <paramref name="aggr"/> lambda). Implementations are responsible
    /// for caching the walked member set by
    /// <c>(targetType, scope)</c>.</para>
    /// </summary>
    public interface IMemberPlanProvider
    {
        /// <summary>
        /// Fold over the scoped members of <paramref name="targetType"/>.
        /// The <paramref name="aggr"/> callback receives the current
        /// accumulator, the current <see cref="IMemberPlan"/>, and a
        /// continuation it must invoke (passing an updated accumulator) to
        /// advance to the next member; returning without calling the
        /// continuation short-circuits the iteration.
        /// <paramref name="onComplete"/> is invoked with the final accumulator
        /// after the last member (or immediately, for empty plans).
        /// </summary>
        TResult Aggregate<TAccum, TResult>(
            Type targetType,
            Type scope,
            TAccum start,
            Func<TAccum, IMemberPlan, Func<TAccum, TResult>, TResult> aggr,
            Func<TAccum, TResult> onComplete);
    }
}
