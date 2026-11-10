using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using EastFive.Api.Serialization.Binding.Sinks;
using EastFive.Serialization.Binding;
using EastFive.Serialization.Binding.Binders;

namespace EastFive.Api.Serialization.Binding.Binders
{
    /// <summary>
    /// String-keyed dictionaries (<c>Dictionary&lt;string, T&gt;</c>,
    /// <c>IDictionary&lt;string, T&gt;</c>, …) as JSON objects. Reads walk the
    /// object's own properties, so keys containing <c>.</c> or <c>[</c> survive;
    /// interface-typed members get a <c>Dictionary&lt;,&gt;</c>. Writes keep null
    /// values even when nulls are omitted, as Newtonsoft does for dictionaries, and
    /// write an empty dictionary as <c>{}</c>.
    /// </summary>
    public sealed class JsonDictionaryBinder : ITypeBinder
    {
        public bool CanBind(Type targetType) =>
            !typeof(JToken).IsAssignableFrom(targetType) &&
            DictionaryInterface(targetType) is { } dictionary &&
            dictionary.GetGenericArguments()[0] == typeof(string);

        public ValueTask<TResult> Read<TResult>(
            Type targetType,
            IBindingSource source,
            IBindingContext context,
            Func<object, TResult> onBound,
            Func<BindFailure, TResult> onFailure,
            Func<TResult> onNull = null)
        {
            var path = context?.KeyPath ?? string.Empty;
            if (!JsonTokens.TryGet(source, path, targetType, out var token, out var failure))
                return new ValueTask<TResult>(onFailure(failure));
            if (JsonTokens.IsNull(token))
                return new ValueTask<TResult>(onNull is not null
                    ? onNull()
                    : onFailure(new BindFailure(new NullValue(), targetType, path)));
            if (token is not JObject obj)
                return new ValueTask<TResult>(onFailure(JsonTokens.WrongShape("object", token, targetType, path)));

            var dictionary = DictionaryInterface(targetType);
            var valueType = dictionary.GetGenericArguments()[1];
            var indexer = dictionary.GetProperty("Item");
            var instance = Create(targetType, dictionary);
            foreach (var property in obj.Properties())
            {
                var (ok, value, valueFailure) = JsonTokens.BindToken(valueType, property.Value, context);
                if (!ok)
                    return new ValueTask<TResult>(onFailure(new BindFailure(
                        new NestedFailure(JsonTokens.Under(property.Name, valueFailure)), targetType, path)));
                indexer.SetValue(instance, value ?? JsonTokens.DefaultOf(valueType), new object[] { property.Name });
            }
            return new ValueTask<TResult>(onBound(instance));
        }

        public void Write(Type sourceType, object value, IBindingSink sink, IBindingContext context)
        {
            if (value is null) { sink.WriteNull(); return; }
            var valueType = DictionaryInterface(sourceType).GetGenericArguments()[1];
            var json = sink as JTokenBindingSink;
            (sink as IShapedBindingSink)?.BeginObject();
            foreach (var entry in (IEnumerable)value)
            {
                var (key, item) = Entry(entry);
                var child = json is not null ? json.ScopeEntry(key) : sink.Scope(key);
                context.TypeBindings.Emit(valueType, item, child, context);
            }
        }

        private static (string key, object value) Entry(object entry)
        {
            if (entry is DictionaryEntry de) return ((string)de.Key, de.Value);
            var type = entry.GetType();
            return ((string)type.GetProperty("Key").GetValue(entry), type.GetProperty("Value").GetValue(entry));
        }

        internal static Type DictionaryInterface(Type type)
        {
            if (type is null) return null;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                return type;
            return type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
        }

        private static object Create(Type targetType, Type dictionary)
        {
            if (!targetType.IsInterface && !targetType.IsAbstract && targetType.GetConstructor(Type.EmptyTypes) is not null)
                return Activator.CreateInstance(targetType);
            return Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(dictionary.GetGenericArguments()));
        }
    }

    /// <summary>
    /// Arrays (<c>T[]</c>, not <c>byte[]</c>) from a JSON array whose elements may
    /// be null: a null element reads as null (the default for a non-nullable value
    /// type), as the sink writes it. Any other shape falls back to the core
    /// <see cref="ArrayBinder"/> (its delimited-string reading included).
    /// </summary>
    public sealed class JsonArrayBinder : ITypeBinder
    {
        private static readonly ArrayBinder fallback = new ArrayBinder();

        public bool CanBind(Type targetType) =>
            targetType is not null && targetType.IsArray && targetType.GetArrayRank() == 1 && targetType != typeof(byte[]);

        public ValueTask<TResult> Read<TResult>(
            Type targetType,
            IBindingSource source,
            IBindingContext context,
            Func<object, TResult> onBound,
            Func<BindFailure, TResult> onFailure,
            Func<TResult> onNull = null)
        {
            var path = context?.KeyPath ?? string.Empty;
            if (!JsonTokens.TryGet(source, path, targetType, out var token, out var failure))
                return new ValueTask<TResult>(onFailure(failure));
            if (token is not JArray array)
                return fallback.Read(targetType, source, context, onBound, onFailure, onNull);

            var element = targetType.GetElementType();
            var result = Array.CreateInstance(element, array.Count);
            for (var i = 0; i < array.Count; i++)
            {
                var (ok, value, elementFailure) = JsonTokens.BindToken(element, array[i], context);
                if (!ok)
                    return new ValueTask<TResult>(onFailure(JsonTokens.Under($"{path}[{i}]", elementFailure)));
                result.SetValue(value ?? JsonTokens.DefaultOf(element), i);
            }
            return new ValueTask<TResult>(onBound(result));
        }

        public void Write(Type sourceType, object value, IBindingSink sink, IBindingContext context) =>
            fallback.Write(sourceType, value, sink, context);
    }

    /// <summary>
    /// Writes a value held by a non-sealed class member, or by a non-collection
    /// interface member, as its runtime type — Newtonsoft resolves the contract
    /// from the value, so a <c>Derived</c> in a <c>Base</c> member keeps its own
    /// members. A class no binder claims (binder selection is read-shaped:
    /// <see cref="PocoBinder"/> needs a parameterless constructor to build one) is
    /// still written, by walking its member plan. Reads (which carry no type
    /// information) and every other write go to the bindings this decorates.
    /// </summary>
    internal sealed class JsonRuntimeTypeBinder : ITypeBinder
    {
        private static readonly PocoBinder memberWriter = new PocoBinder();

        private readonly ITypeBindings declared;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<Type, bool> declaredBinders = new();

        public JsonRuntimeTypeBinder(ITypeBindings declared)
        {
            this.declared = declared;
        }

        public bool CanBind(Type targetType) =>
            targetType is not null &&
            !targetType.IsValueType &&
            targetType != typeof(object) &&
            targetType != typeof(string) &&
            !typeof(IEnumerable).IsAssignableFrom(targetType) &&
            !typeof(JToken).IsAssignableFrom(targetType) &&
            (!targetType.IsSealed || !HasDeclaredBinder(targetType));

        // TypeBindings answers UnsupportedTargetType before touching the source
        // when no binder claims a type; anything else means one does.
        private bool HasDeclaredBinder(Type type) =>
            declaredBinders.GetOrAdd(type, t =>
            {
                try
                {
                    return JsonTokens.Complete(declared.Bind<bool>(
                        t,
                        AbsentSource.Instance,
                        new BindingContext(declared, memberPlanProvider: JsonMemberPlanProvider.Instance, memberScope: typeof(EastFive.Api.Binding.Scopes.ResponseBody)),
                        _ => true,
                        f => f.Reason is not UnsupportedTargetType,
                        onNull: () => true));
                }
                catch (Exception)
                {
                    // A binder claimed the type and then objected to the probe.
                    return true;
                }
            });

        private sealed class AbsentSource : IBindingSource
        {
            public static AbsentSource Instance { get; } = new AbsentSource();

            public ValueTask<TResult> GetValue<TResult>(
                string path = null,
                Func<TResult> onNull = null,
                Func<string, TResult> onString = null,
                Func<Guid, TResult> onGuid = null,
                Func<bool, TResult> onBool = null,
                Func<long, TResult> onInt64 = null,
                Func<double, TResult> onDouble = null,
                Func<DateTime, TResult> onDateTime = null,
                Func<byte[], TResult> onBytes = null,
                Func<IBindingSource, TResult> onObject = null,
                Func<IEnumerableBindingSource, TResult> onArray = null,
                Type elementTypeHint = null,
                Func<BindFailure, TResult> onFailure = null) =>
                BindingSourceDispatch.FailTask(new BindFailure(new NotPresent(), typeof(object), path ?? string.Empty), onFailure);
        }

        public ValueTask<TResult> Read<TResult>(
            Type targetType,
            IBindingSource source,
            IBindingContext context,
            Func<object, TResult> onBound,
            Func<BindFailure, TResult> onFailure,
            Func<TResult> onNull = null) =>
            declared.Bind(targetType, source, context, onBound, onFailure, onNull);

        public void Write(Type sourceType, object value, IBindingSink sink, IBindingContext context)
        {
            if (value is null) { sink.WriteNull(); return; }
            var runtime = value.GetType();
            if (runtime.IsClass && !typeof(IEnumerable).IsAssignableFrom(runtime) && !HasDeclaredBinder(runtime))
                memberWriter.Write(runtime, value, sink, context);
            else if (runtime != sourceType)
                context.TypeBindings.Emit(runtime, value, sink, context);
            else
                declared.Emit(sourceType, value, sink, context);
        }
    }

    /// <summary>
    /// <c>List&lt;T&gt;</c>, <c>IList&lt;T&gt;</c>, <c>ICollection&lt;T&gt;</c>,
    /// <c>IEnumerable&lt;T&gt;</c>, <c>IReadOnlyList&lt;T&gt;</c>,
    /// <c>IReadOnlyCollection&lt;T&gt;</c>, <c>HashSet&lt;T&gt;</c>,
    /// <c>ISet&lt;T&gt;</c> and <c>IReadOnlySet&lt;T&gt;</c> as JSON arrays. Reads
    /// bind a <c>T[]</c> and wrap it in a <c>List&lt;T&gt;</c> (a
    /// <c>HashSet&lt;T&gt;</c> for the set shapes). An empty collection writes
    /// <c>[]</c>; null stays null.
    /// </summary>
    public sealed class JsonCollectionBinder : ITypeBinder
    {
        private static readonly Type[] listShapes =
        {
            typeof(List<>), typeof(IList<>), typeof(ICollection<>), typeof(IEnumerable<>),
            typeof(IReadOnlyList<>), typeof(IReadOnlyCollection<>),
        };

        private static readonly Type[] setShapes =
        {
            typeof(HashSet<>), typeof(ISet<>), typeof(IReadOnlySet<>),
        };

        public bool CanBind(Type targetType)
        {
            if (targetType is null || !targetType.IsGenericType) return false;
            var definition = targetType.GetGenericTypeDefinition();
            return listShapes.Contains(definition) || setShapes.Contains(definition);
        }

        public ValueTask<TResult> Read<TResult>(
            Type targetType,
            IBindingSource source,
            IBindingContext context,
            Func<object, TResult> onBound,
            Func<BindFailure, TResult> onFailure,
            Func<TResult> onNull = null)
        {
            var element = targetType.GetGenericArguments()[0];
            var isSet = setShapes.Contains(targetType.GetGenericTypeDefinition());
            var concrete = (isSet ? typeof(HashSet<>) : typeof(List<>)).MakeGenericType(element);
            return context.TypeBindings.Bind(
                element.MakeArrayType(),
                source,
                context,
                array => onBound(Activator.CreateInstance(concrete, array)),
                onFailure,
                onNull);
        }

        public void Write(Type sourceType, object value, IBindingSink sink, IBindingContext context)
        {
            if (value is null) { sink.WriteNull(); return; }
            var element = sourceType.GetGenericArguments()[0];
            (sink as IShapedBindingSink)?.BeginArray();
            foreach (var item in (IEnumerable)value)
                context.TypeBindings.Emit(element, item, sink.AppendItem(), context);
        }
    }

    /// <summary>
    /// <see cref="object"/> members. Writes go by the value's runtime type. Reads
    /// produce JSON natives whatever token type the JSON carries: strings (dates,
    /// guids, uris and bytes as their text), <see cref="long"/>,
    /// <see cref="double"/>, <see cref="bool"/>, null, a nested
    /// <c>Dictionary&lt;string, object&gt;</c> for an object and an
    /// <c>object[]</c> for an array.
    /// </summary>
    public sealed class JsonObjectBinder : ITypeBinder
    {
        public bool CanBind(Type targetType) => targetType == typeof(object);

        public ValueTask<TResult> Read<TResult>(
            Type targetType,
            IBindingSource source,
            IBindingContext context,
            Func<object, TResult> onBound,
            Func<BindFailure, TResult> onFailure,
            Func<TResult> onNull = null)
        {
            var path = context?.KeyPath ?? string.Empty;
            if (!JsonTokens.TryGet(source, path, targetType, out var token, out var failure))
                return new ValueTask<TResult>(onFailure(failure));
            if (JsonTokens.IsNull(token))
                return new ValueTask<TResult>(onNull is not null ? onNull() : onBound(null));

            switch (token)
            {
                case JObject:
                    return context.TypeBindings.Bind(typeof(IDictionary<string, object>), source, context, onBound, onFailure, onNull);
                case JArray:
                    return context.TypeBindings.Bind(typeof(object[]), source, context, onBound, onFailure, onNull);
                case JValue value when value.Type == JTokenType.Integer:
                    return new ValueTask<TResult>(onBound(value.Value is System.Numerics.BigInteger big ? (object)(double)big : value.Value<long>()));
                case JValue value when value.Type == JTokenType.Float:
                    return new ValueTask<TResult>(onBound(value.Value<double>()));
                case JValue value when value.Type == JTokenType.Boolean:
                    return new ValueTask<TResult>(onBound(value.Value<bool>()));
            }
            var text = JsonTokens.ScalarText(token);
            if (text is null)
                return new ValueTask<TResult>(onFailure(JsonTokens.WrongShape("value", token, targetType, path)));
            return new ValueTask<TResult>(onBound(text));
        }

        public void Write(Type sourceType, object value, IBindingSink sink, IBindingContext context)
        {
            if (value is null) { sink.WriteNull(); return; }
            context.TypeBindings.Emit(value.GetType(), value, sink, context);
        }
    }

    /// <summary>
    /// Classes with no parameterless constructor that a JSON reader can still
    /// build: positional records (bound through their primary constructor) and
    /// classes marking one constructor <c>[JsonConstructor]</c>. Each constructor
    /// parameter takes the member of the same name (else the same wire name),
    /// case-insensitively; a parameter whose member is absent gets its default.
    /// Members not covered by the constructor (e.g. <c>init</c> properties) are
    /// then set. Writes walk the member plan like <see cref="PocoBinder"/>.
    /// </summary>
    public sealed class JsonConstructorBinder : ITypeBinder
    {
        private static readonly PocoBinder writer = new PocoBinder();

        public bool CanBind(Type targetType) =>
            targetType is not null &&
            targetType.IsClass &&
            !targetType.IsAbstract &&
            targetType.GetConstructor(Type.EmptyTypes) is null &&
            Constructor(targetType) is not null;

        internal static ConstructorInfo Constructor(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var marked = type.GetConstructors(flags).FirstOrDefault(c => c.IsDefined(typeof(JsonConstructorAttribute), inherit: false));
            if (marked is not null) return marked;
            var isRecord = type.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.Public) is not null;
            if (!isRecord) return null;
            var candidates = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public)
                .Where(c => !(c.GetParameters() is { Length: 1 } ps && ps[0].ParameterType == type))
                .ToArray();
            return candidates.Length == 1 ? candidates[0] : null;
        }

        public async ValueTask<TResult> Read<TResult>(
            Type targetType,
            IBindingSource source,
            IBindingContext context,
            Func<object, TResult> onBound,
            Func<BindFailure, TResult> onFailure,
            Func<TResult> onNull = null)
        {
            var path = context?.KeyPath ?? string.Empty;
            if (!JsonTokens.TryGet(source, path, targetType, out var token, out var failure))
                return onFailure(failure);
            if (JsonTokens.IsNull(token))
                return onNull is not null ? onNull() : onFailure(new BindFailure(new NullValue(), targetType, path));
            if (token is not JObject obj)
                return onFailure(JsonTokens.WrongShape("object", token, targetType, path));

            var provider = context.MemberPlanProvider
                ?? throw new InvalidOperationException("JsonConstructorBinder requires an IMemberPlanProvider on the context.");
            var scope = context.MemberScope
                ?? throw new InvalidOperationException($"JsonConstructorBinder requires a MemberScope on the context to bind type {targetType.FullName}.");
            var members = provider.Aggregate<List<IMemberPlan>, List<IMemberPlan>>(
                targetType, scope, new List<IMemberPlan>(),
                (list, member, next) => { list.Add(member); return next(list); },
                list => list);

            var objSource = new Sources.JTokenBindingSource(obj, literalKeys: true);
            var memberRoot = context.WithKeyPath(string.Empty);
            var bound = new Dictionary<IMemberPlan, object>();
            foreach (var member in members)
            {
                var memberContext = member.ScopeInto(memberRoot);
                BindFailure memberFailure = default;
                object value = null;
                var ok = await memberContext.TypeBindings.ForSlot(memberContext.Slot).Bind<bool?>(
                    member.MemberType,
                    objSource,
                    memberContext,
                    v => { value = v; return true; },
                    f =>
                    {
                        if (f.Reason is NotPresent || f.Reason is UnsupportedTargetType) return null;
                        memberFailure = f;
                        return false;
                    },
                    onNull: () => { value = null; return true; });
                if (ok == false)
                    return onFailure(new BindFailure(new NestedFailure(member.DecorateFailure(memberFailure)), targetType, path));
                if (ok == true)
                    bound[member] = value;
            }

            var constructor = Constructor(targetType);
            var parameters = constructor.GetParameters();
            var arguments = new object[parameters.Length];
            var consumed = new HashSet<IMemberPlan>();
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                var member = members.FirstOrDefault(m => m is JsonMemberPlan jm &&
                        string.Equals(jm.Member.Name, parameter.Name, StringComparison.OrdinalIgnoreCase))
                    ?? members.FirstOrDefault(m =>
                        string.Equals((m as JsonMemberPlan)?.WireName ?? m.ComposePath(null), parameter.Name, StringComparison.OrdinalIgnoreCase));
                if (member is not null) consumed.Add(member);
                arguments[i] = member is not null && bound.TryGetValue(member, out var value)
                    ? value ?? JsonTokens.DefaultOf(parameter.ParameterType)
                    : parameter.HasDefaultValue ? parameter.DefaultValue : JsonTokens.DefaultOf(parameter.ParameterType);
            }

            var instance = constructor.Invoke(arguments);
            foreach (var pair in bound)
            {
                if (consumed.Contains(pair.Key)) continue;
                instance = pair.Key.WithMember(instance, pair.Value);
            }
            return onBound(instance);
        }

        public void Write(Type sourceType, object value, IBindingSink sink, IBindingContext context) =>
            writer.Write(sourceType, value, sink, context);
    }
}
