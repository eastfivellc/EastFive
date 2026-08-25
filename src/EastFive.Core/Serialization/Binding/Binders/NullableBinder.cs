using System;
using System.Threading.Tasks;

namespace EastFive.Serialization.Binding.Binders
{
    /// <summary>
    /// Binds <c>Nullable&lt;T&gt;</c>. Supplies <c>onNull → onBound(null)</c> and
    /// otherwise delegates to the inner binder for <c>T</c> against the SAME source.
    /// </summary>
    public sealed class NullableBinder : ITypeBinder
    {
        public bool CanBind(Type targetType) => Nullable.GetUnderlyingType(targetType) is not null;

        public ValueTask<TResult> Read<TResult>(
            Type targetType,
            IBindingSource source,
            IBindingContext context,
            Func<object, TResult> onBound,
            Func<BindFailure, TResult> onFailure,
            Func<TResult> onNull = null)
        {
            var inner = Nullable.GetUnderlyingType(targetType);
            return context.TypeBindings.Bind(
                inner,
                source,
                context,
                onBound,
                onFailure,
                onNull: () => onBound(null));
        }

        public void Write(Type sourceType, object value, IBindingSink sink, IBindingContext context)
        {
            if (value is null) { sink.WriteNull(); return; }
            var inner = Nullable.GetUnderlyingType(sourceType);
            context.TypeBindings.Emit(inner, value, sink, context);
        }
    }
}
