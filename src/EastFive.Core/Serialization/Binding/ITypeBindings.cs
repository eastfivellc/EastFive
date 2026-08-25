using System;
using System.Threading.Tasks;

namespace EastFive.Serialization.Binding
{
    /// <summary>
    /// Ordered registry of <see cref="ITypeBinder"/>s. Resolves a target type by
    /// scanning binders in priority order and returns a <see cref="UnsupportedTargetType"/>
    /// failure when no binder matches.
    /// <para>
    /// <b>Always a parameter, never a global.</b> Tests, alternate strategies, and
    /// per-slot overlays all rely on flowing distinct instances through the call
    /// chain. The static <see cref="TypeBindings.Default"/> exists only as a
    /// convenience entry point for shim call-sites (e.g. <c>StringExtensions.BindTo</c>).
    /// </para>
    /// </summary>
    public interface ITypeBindings
    {
        ValueTask<TResult> Bind<TResult>(
            Type targetType,
            IBindingSource source,
            IBindingContext context,
            Func<object, TResult> onBound,
            Func<BindFailure, TResult> onFailure,
            Func<TResult> onNull = null);

        /// <summary>
        /// Symmetric write entry-point: resolves the binder for <paramref name="sourceType"/>
        /// and delegates to <see cref="ITypeBinder.Write"/>. Throws
        /// <see cref="System.InvalidOperationException"/> if no binder matches —
        /// writes are programmer-driven, so this is a programmer error.
        /// </summary>
        void Emit(
            Type sourceType,
            object value,
            IBindingSink sink,
            IBindingContext context);

        /// <summary>Returns a (possibly identical) ITypeBindings overlaid for the given slot, honoring any per-slot attributes/overrides.</summary>
        ITypeBindings ForSlot(IBindingSlot slot);

        /// <summary>Returns a new ITypeBindings with the given binder prepended (higher priority).</summary>
        ITypeBindings With(ITypeBinder binder);

        /// <summary>Returns a new ITypeBindings with binders matching the predicate removed.</summary>
        ITypeBindings Without(Func<ITypeBinder, bool> predicate);
    }
}
