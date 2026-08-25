using System;
using System.Threading.Tasks;

namespace EastFive.Serialization.Binding.Binders
{
    /// <summary>Binds <c>IRefOptional&lt;T&gt;</c>. Null/empty/sentinel-Guid all become an empty optional.</summary>
    public sealed class RefOptionalBinder : ITypeBinder
    {
        // Mirrors EastFive.Azure EDMExtensions.NullGuidKey (which lives in a sibling assembly).
        private static readonly Guid NullSentinel = new Guid("a4a347f8-4ef7-444b-b1fa-c010cd475fd2");

        public bool CanBind(Type targetType) =>
            targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(IRefOptional<>);

        public ValueTask<TResult> Read<TResult>(
            Type targetType,
            IBindingSource source,
            IBindingContext context,
            Func<object, TResult> onBound,
            Func<BindFailure, TResult> onFailure,
            Func<TResult> onNull = null)
        {
            var path = context?.KeyPath;
            var referenced = targetType.GetGenericArguments()[0];
            object Empty() => RefOptionalHelper.CreateEmpty(referenced);

            return source.GetValue<TResult>(
                path: path,
                onNull: () => onBound(Empty()),
                onGuid: g => g == Guid.Empty || g == NullSentinel
                    ? onBound(Empty())
                    : onBound(((Guid?)g).BindToRefOptional(referenced)),
                onString: s =>
                {
                    if (string.IsNullOrWhiteSpace(s)) return onBound(Empty());
                    if (Guid.TryParse(s, out var g))
                        return g == Guid.Empty || g == NullSentinel
                            ? onBound(Empty())
                            : onBound(((Guid?)g).BindToRefOptional(referenced));
                    return onFailure(new BindFailure(new ParseError($"'{s}' is not a Guid"), targetType, path));
                },
                onFailure: f =>
                {
                    // NotPresent on an optional → empty.
                    if (f.Reason is NotPresent) return onBound(Empty());
                    return onFailure(f);
                });
        }

        public void Write(Type sourceType, object value, IBindingSink sink, IBindingContext context)
        {
            if (value is null) { sink.WriteNull(); return; }
            var rt = value.GetType();
            var hasValueProp = rt.GetProperty("HasValue");
            if (hasValueProp is not null && !(bool)hasValueProp.GetValue(value))
            {
                sink.WriteNull();
                return;
            }
            var idProp = rt.GetProperty("id");
            if (idProp is not null)
            {
                var raw = idProp.GetValue(value);
                if (raw is Guid g) { sink.WriteGuid(g); return; }
            }
            sink.WriteNull();
        }
    }
}
