using System;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using EastFive.Api.Binding.Scopes;
using EastFive.Api.Serialization.Binding.Binders;
using EastFive.Api.Serialization.Binding.Sinks;
using EastFive.Api.Serialization.Binding.Sources;
using EastFive.Serialization.Binding;
using EastFive.Serialization.Binding.Binders;

namespace EastFive.Api.Serialization.Binding
{
    /// <summary>
    /// Write and read entity JSON through the V3 binding framework, with no
    /// <c>IHttpRequest</c> and no <c>IApplication</c>.
    /// </summary>
    public static class JsonBinding
    {
        /// <summary>The member scope used when a call passes none.</summary>
        public static Type DefaultMemberScope => typeof(ResponseBody);

        /// <summary>The binder table every <see cref="JsonBinding"/> call uses.</summary>
        public static ITypeBindings TypeBindings { get; } = BuildTypeBindings();

        /// <summary>The member selector every <see cref="JsonBinding"/> call uses.</summary>
        public static IMemberPlanProvider MemberPlanProvider { get; } = JsonMemberPlanProvider.Instance;

        private static readonly PocoBinder populator = new PocoBinder();

        private static ITypeBindings BuildTypeBindings()
        {
            // With() prepends, so these JSON-specific binders win over the defaults
            // for their shapes; every other shape keeps its default binder.
            ITypeBindings bindings = EastFive.Serialization.Binding.TypeBindings.Default;
            foreach (var binder in new ITypeBinder[]
            {
                new JsonArrayBinder(),
                new JsonConstructorBinder(),
                new JsonObjectBinder(),
                new JsonCollectionBinder(),
                new JsonDictionaryBinder(),
                new JsonRefBinder(),
                new JsonDateTimeOffsetBinder(),
                new JsonNumberBinder(),
                new JsonStringBinder(),
                new JsonUriBinder(),
                new JsonTokenBinder(),
            })
                bindings = bindings.With(binder);
            return bindings.With(new JsonRuntimeTypeBinder(bindings));
        }

        /// <summary>
        /// Parse JSON text for <see cref="Read{T}"/> without Newtonsoft's lossy
        /// defaults: a string that merely looks like a date stays the exact text
        /// that was written (no date sniffing), and each non-integer number reads as
        /// an exact <see cref="decimal"/> when its text fits one and means the same
        /// <see cref="double"/>; any other number stays a double.
        /// </summary>
        public static JToken Parse(string json)
        {
            if (json is null) throw new ArgumentNullException(nameof(json));
            JToken token;
            using (var reader = new JsonTextReader(new System.IO.StringReader(json))
            {
                DateParseHandling = DateParseHandling.None,
            })
            {
                token = JToken.ReadFrom(reader);
                while (reader.Read())
                {
                    if (reader.TokenType != JsonToken.Comment)
                        throw new JsonReaderException($"Additional text after the JSON value at {reader.Path}.");
                }
            }

            // Newtonsoft keeps no number text, so read the raw text of each number
            // from System.Text.Json and upgrade the doubles a decimal states exactly.
            try
            {
                using var document = System.Text.Json.JsonDocument.Parse(json, new System.Text.Json.JsonDocumentOptions
                {
                    CommentHandling = System.Text.Json.JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                });
                PreferExactDecimals(token, document.RootElement);
            }
            catch (System.Text.Json.JsonException)
            {
                // Lenient JSON Newtonsoft accepts but System.Text.Json does not: keep doubles.
            }
            return token;
        }

        private static void PreferExactDecimals(JToken token, System.Text.Json.JsonElement element)
        {
            switch (token)
            {
                case JObject obj when element.ValueKind == System.Text.Json.JsonValueKind.Object:
                    foreach (var property in obj.Properties())
                    {
                        if (element.TryGetProperty(property.Name, out var child))
                            PreferExactDecimals(property.Value, child);
                    }
                    return;
                case JArray array when element.ValueKind == System.Text.Json.JsonValueKind.Array:
                    var index = 0;
                    foreach (var child in element.EnumerateArray())
                    {
                        if (index >= array.Count) return;
                        PreferExactDecimals(array[index++], child);
                    }
                    return;
                case JValue { Type: JTokenType.Float, Value: double approximate } value
                    when element.ValueKind == System.Text.Json.JsonValueKind.Number:
                    if (element.TryGetDecimal(out var exact) && (double)exact == approximate)
                        value.Value = exact;
                    return;
            }
        }

        /// <summary>
        /// Write <paramref name="value"/> as a token. <paramref name="type"/> is the
        /// declared type to write as (default: the value's runtime type). Null
        /// writes JSON <c>null</c>.
        /// </summary>
        public static JToken Write(
            object value,
            Type type = null,
            Type memberScope = null,
            JsonBindingOptions options = null)
        {
            options ??= JsonBindingOptions.Default;
            if (value is null) return JValue.CreateNull();
            var sink = new JTokenBindingSink(options.OmitNulls);
            TypeBindings.Emit(type ?? value.GetType(), value, sink, CreateContext(memberScope));
            return sink.ToToken() ?? JValue.CreateNull();
        }

        /// <summary>
        /// Read a <typeparamref name="T"/> from <paramref name="json"/>. JSON
        /// <c>null</c> reads as <c>default</c>. Throws
        /// <see cref="JsonBindingException"/> naming the failing key path.
        /// </summary>
        public static T Read<T>(
            JToken json,
            Type memberScope = null,
            JsonBindingOptions options = null)
        {
            var value = Read(json, typeof(T), memberScope, options);
            return value is null ? default : (T)value;
        }

        /// <inheritdoc cref="Read{T}"/>
        public static object Read(
            JToken json,
            Type type,
            Type memberScope = null,
            JsonBindingOptions options = null)
        {
            if (type is null) throw new ArgumentNullException(nameof(type));
            var source = new JTokenBindingSource(json ?? JValue.CreateNull(), literalKeys: true);
            var outcome = Complete(TypeBindings.Bind<Outcome>(
                type,
                source,
                CreateContext(memberScope),
                onBound: value => Outcome.Bound(value),
                onFailure: failure => Outcome.Failed(failure),
                onNull: () => Outcome.Bound(null)));
            return outcome.Unwrap();
        }

        /// <summary>
        /// Overlay <paramref name="json"/> on <paramref name="existing"/>: members
        /// present in the JSON overwrite, members absent keep <paramref name="existing"/>'s
        /// values. Returns the populated value (for a struct, a populated copy).
        /// Values nested inside a present member are replaced, not merged. A null
        /// <paramref name="existing"/> is a plain <see cref="Read{T}"/>; JSON
        /// <c>null</c> returns <paramref name="existing"/> unchanged.
        /// </summary>
        public static T Populate<T>(
            JToken json,
            T existing,
            Type memberScope = null,
            JsonBindingOptions options = null)
        {
            object boxed = existing;
            if (boxed is null) return Read<T>(json, memberScope, options);
            var type = boxed.GetType();
            var source = new JTokenBindingSource(json ?? JValue.CreateNull(), literalKeys: true);
            var outcome = Complete(populator.ReadInto<Outcome>(
                type,
                boxed,
                source,
                CreateContext(memberScope),
                onBound: value => Outcome.Bound(value),
                onFailure: failure => Outcome.Failed(failure),
                onNull: () => Outcome.Bound(boxed)));
            return (T)outcome.Unwrap();
        }

        /// <summary>
        /// A binding context for this façade's <see cref="TypeBindings"/> and
        /// <see cref="MemberPlanProvider"/> under <paramref name="memberScope"/>
        /// (default <see cref="DefaultMemberScope"/>).
        /// </summary>
        public static IBindingContext CreateContext(Type memberScope = null)
        {
            var scope = memberScope ?? DefaultMemberScope;
            if (!typeof(IMemberScope).IsAssignableFrom(scope))
                throw new ArgumentException(
                    $"Member scope {scope.FullName} must implement {nameof(IMemberScope)}.", nameof(memberScope));
            return new BindingContext(
                TypeBindings,
                memberPlanProvider: MemberPlanProvider,
                memberScope: scope);
        }

        // JTokenBindingSource answers synchronously, so every bind completes
        // inline and the façade can stay synchronous like the ToObject<T> call
        // sites it replaces. Blocking is only a fallback for a binder that
        // genuinely awaits.
        private static T Complete<T>(ValueTask<T> task) =>
            task.IsCompletedSuccessfully ? task.Result : task.AsTask().GetAwaiter().GetResult();

        private readonly struct Outcome
        {
            private readonly object value;
            private readonly BindFailure? failure;

            private Outcome(object value, BindFailure? failure)
            {
                this.value = value;
                this.failure = failure;
            }

            public static Outcome Bound(object value) => new Outcome(value, null);

            public static Outcome Failed(BindFailure failure) => new Outcome(null, failure);

            public object Unwrap()
            {
                if (failure is { } f) throw new JsonBindingException(f);
                return value;
            }
        }
    }
}
