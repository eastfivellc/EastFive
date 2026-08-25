using System;
using System.Threading.Tasks;

namespace EastFive.Serialization.Binding
{
    /// <summary>
    /// Knows how to materialize one target type (or family of target types, e.g.
    /// <c>IRef&lt;T&gt;</c> for any T) from an <see cref="IBindingSource"/>.
    /// <para>
    /// Binders MUST consume only the <see cref="IBindingSource"/> surface — never
    /// inspect the underlying format (JSON token, EntityProperty, raw string).
    /// This is what lets the same binder serve every input shape.
    /// </para>
    /// </summary>
    public interface ITypeBinder
    {
        bool CanBind(Type targetType);

        ValueTask<TResult> Read<TResult>(
            Type targetType,
            IBindingSource source,
            IBindingContext context,
            Func<object, TResult> onBound,
            Func<BindFailure, TResult> onFailure,
            Func<TResult> onNull = null);

        /// <summary>
        /// Write <paramref name="value"/> into <paramref name="sink"/>. Mirrors
        /// <see cref="Read"/> in shape: binders only touch <see cref="IBindingSink"/>,
        /// so the same binder serves every output format. Values of <c>null</c>
        /// MUST be encoded via <see cref="IBindingSink.WriteNull"/>.
        /// </summary>
        void Write(
            Type sourceType,
            object value,
            IBindingSink sink,
            IBindingContext context);
    }
}
