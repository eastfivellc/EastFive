using System;
using System.Linq;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using EastFive.Serialization.Binding;

namespace EastFive.Api.Serialization.Binding.Binders
{
    /// <summary>
    /// Refs on the JSON wire are ids, never ref objects. Declared as the interface
    /// or the concrete struct alike:
    /// <list type="bullet">
    /// <item><c>IRef&lt;T&gt;</c> / <c>Ref&lt;T&gt;</c>: a guid; null reads as null.</item>
    /// <item><c>IRefOptional&lt;T&gt;</c> / <c>RefOptional&lt;T&gt;</c>: a guid or null;
    /// null, absent, empty string and <see cref="Guid.Empty"/> read as an empty optional,
    /// which writes as null.</item>
    /// <item><c>IRefs&lt;T&gt;</c> / <c>Refs&lt;T&gt;</c>: an array of guids; null reads
    /// as empty.</item>
    /// </list>
    /// An object where an id belongs (<c>{"id": …}</c>, <c>{"HasValue": false}</c>) is
    /// refused with its key path, not repaired: it is the shape stock Newtonsoft
    /// writes for a ref, which no reader can rebuild faithfully.
    /// </summary>
    public sealed class JsonRefBinder : ITypeBinder
    {
        // Mirrors EastFive.Azure EDMExtensions.NullGuidKey, as RefOptionalBinder does.
        private static readonly Guid NullSentinel = new Guid("a4a347f8-4ef7-444b-b1fa-c010cd475fd2");

        private enum RefKind { None, Single, Optional, Many }

        public bool CanBind(Type targetType) => KindOf(targetType) != RefKind.None;

        private static RefKind KindOf(Type type)
        {
            if (type is null || !type.IsGenericType) return RefKind.None;
            var definition = type.GetGenericTypeDefinition();
            if (definition == typeof(IRef<>) || definition == typeof(Ref<>)) return RefKind.Single;
            if (definition == typeof(IRefOptional<>) || definition == typeof(RefOptional<>)) return RefKind.Optional;
            if (definition == typeof(IRefs<>) || definition == typeof(Refs<>)) return RefKind.Many;
            return RefKind.None;
        }

        public ValueTask<TResult> Read<TResult>(
            Type targetType,
            IBindingSource source,
            IBindingContext context,
            Func<object, TResult> onBound,
            Func<BindFailure, TResult> onFailure,
            Func<TResult> onNull = null)
        {
            var path = context?.KeyPath ?? string.Empty;
            var kind = KindOf(targetType);
            var referenced = targetType.GetGenericArguments()[0];

            if (!JsonTokens.TryGet(source, path, targetType, out var token, out var failure))
            {
                if (kind == RefKind.Optional && failure.Reason is NotPresent)
                    return new ValueTask<TResult>(onBound(RefOptionalHelper.CreateEmpty(referenced)));
                return new ValueTask<TResult>(onFailure(failure));
            }

            switch (kind)
            {
                case RefKind.Single:
                    if (JsonTokens.IsNull(token))
                        return new ValueTask<TResult>(onNull is not null ? onNull() : onBound(null));
                    return new ValueTask<TResult>(TryId(token, targetType, path, out var id, out var idFailure)
                        ? onBound(id.BindToRef(referenced))
                        : onFailure(idFailure));

                case RefKind.Optional:
                    if (JsonTokens.IsNull(token) || (token.Type == JTokenType.String && string.IsNullOrWhiteSpace((string)token)))
                        return new ValueTask<TResult>(onBound(RefOptionalHelper.CreateEmpty(referenced)));
                    if (!TryId(token, targetType, path, out var optionalId, out var optionalFailure))
                        return new ValueTask<TResult>(onFailure(optionalFailure));
                    return new ValueTask<TResult>(onBound(optionalId == Guid.Empty || optionalId == NullSentinel
                        ? RefOptionalHelper.CreateEmpty(referenced)
                        : ((Guid?)optionalId).BindToRefOptional(referenced)));

                default:
                    if (JsonTokens.IsNull(token))
                        return new ValueTask<TResult>(onBound(Refs(referenced, Array.Empty<Guid>())));
                    if (token is JArray array)
                    {
                        var ids = new Guid[array.Count];
                        for (var i = 0; i < array.Count; i++)
                        {
                            if (!TryId(array[i], typeof(Guid), $"{path}[{i}]", out ids[i], out var elementFailure))
                                return new ValueTask<TResult>(onFailure(elementFailure));
                        }
                        return new ValueTask<TResult>(onBound(Refs(referenced, ids)));
                    }
                    if (token.Type == JTokenType.String)
                    {
                        // Delimited-list fallback, as RefsBinder accepts on every other source.
                        var parts = EastFive.Serialization.StringExtensions.ParseStringToArray((string)token);
                        var ids = new Guid[parts.Length];
                        for (var i = 0; i < parts.Length; i++)
                        {
                            if (!Guid.TryParse(parts[i], out ids[i]))
                                return new ValueTask<TResult>(onFailure(new BindFailure(
                                    new ParseError($"'{parts[i]}' is not a Guid"), typeof(Guid), path)));
                        }
                        return new ValueTask<TResult>(onBound(Refs(referenced, ids)));
                    }
                    return new ValueTask<TResult>(onFailure(JsonTokens.WrongShape("array", token, targetType, path)));
            }
        }

        private static bool TryId(JToken token, Type expected, string path, out Guid id, out BindFailure failure)
        {
            failure = default;
            if (token is JValue { Value: Guid g })
            {
                id = g;
                return true;
            }
            if (token.Type == JTokenType.String)
            {
                if (Guid.TryParse((string)token, out id))
                    return true;
                failure = new BindFailure(new ParseError($"'{(string)token}' is not a Guid"), expected, path);
                return false;
            }
            id = default;
            failure = JsonTokens.WrongShape("guid", token, expected, path);
            return false;
        }

        private static object Refs(Type referenced, Guid[] ids) =>
            Activator.CreateInstance(typeof(Refs<>).MakeGenericType(referenced), new object[] { ids });

        public void Write(Type sourceType, object value, IBindingSink sink, IBindingContext context)
        {
            if (value is null) { sink.WriteNull(); return; }
            switch (value)
            {
                case IReferenceableOptional optional:
                    if (optional.HasValue && optional.id is Guid optionalId)
                        sink.WriteGuid(optionalId);
                    else
                        sink.WriteNull();
                    return;
                case IReferences many:
                    (sink as IShapedBindingSink)?.BeginArray();
                    foreach (var id in many.ids ?? Enumerable.Empty<Guid>())
                        sink.AppendItem().WriteGuid(id);
                    return;
                case IReferenceable single:
                    sink.WriteGuid(single.id);
                    return;
                default:
                    throw new InvalidOperationException($"JsonRefBinder cannot write a {value.GetType().FullName} as {sourceType.FullName}.");
            }
        }
    }
}
