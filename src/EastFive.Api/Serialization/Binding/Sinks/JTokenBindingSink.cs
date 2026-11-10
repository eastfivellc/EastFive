using System;
using System.Collections.Generic;

using Newtonsoft.Json.Linq;

using EastFive.Serialization.Binding;

namespace EastFive.Api.Serialization.Binding.Sinks
{
    /// <summary>
    /// <see cref="IBindingSink"/> that builds a Newtonsoft <see cref="JToken"/> tree —
    /// the write-side twin of <c>JTokenBindingSource</c>. Needs no request and no
    /// application: any binder that writes through <see cref="IBindingSink"/> can
    /// produce JSON with it.
    /// <para>
    /// Each sink is one slot. A scalar write sets the slot's value,
    /// <see cref="Scope"/> makes the slot an object and returns the child slot for
    /// that key (the same child on a repeated key), and <see cref="AppendItem"/>
    /// makes the slot an array and returns a new element slot. Object properties
    /// keep the order in which they were first scoped, so output is deterministic
    /// for a deterministic member plan.
    /// </para>
    /// <para>
    /// A slot that receives nothing is absent from its parent object. An explicit
    /// <see cref="WriteNull"/> writes JSON <c>null</c>, unless the sink was created
    /// with <c>omitNulls</c>, in which case a null object property is dropped (the
    /// same rule as Newtonsoft's <c>NullValueHandling.Ignore</c>: array elements
    /// and dictionary entries opened with <see cref="ScopeEntry"/> keep their nulls).
    /// </para>
    /// <para>
    /// Shape errors are programmer errors and throw
    /// <see cref="InvalidOperationException"/>: a second scalar write on one slot,
    /// <see cref="Scope"/> on a slot that holds a scalar or an array, or
    /// <see cref="AppendItem"/> on a slot that holds a scalar or an object.
    /// </para>
    /// </summary>
    public sealed class JTokenBindingSink : IShapedBindingSink
    {
        private enum Shape { Empty, Scalar, Null, Object, Array }

        private readonly bool omitNulls;
        private readonly bool keepNull;
        private readonly string name;
        private Shape shape;
        private JToken scalar;
        private List<(string key, JTokenBindingSink child)> members;
        private Dictionary<string, JTokenBindingSink> memberIndex;
        private List<JTokenBindingSink> items;

        /// <param name="omitNulls">Drop object properties whose value is null
        /// instead of writing JSON <c>null</c>.</param>
        public JTokenBindingSink(bool omitNulls = false)
            : this(omitNulls, keepNull: false, name: "$")
        { }

        private JTokenBindingSink(bool omitNulls, bool keepNull, string name)
        {
            this.omitNulls = omitNulls;
            this.keepNull = keepNull;
            this.name = name;
        }

        /// <summary>True when object properties holding null are dropped.</summary>
        public bool OmitNulls => omitNulls;

        public void WriteString(string value)
        {
            if (value is null) { WriteNull(); return; }
            SetScalar(new JValue(value));
        }

        public void WriteGuid(Guid value) => SetScalar(new JValue(value));

        public void WriteBool(bool value) => SetScalar(new JValue(value));

        public void WriteInt32(int value) => SetScalar(new JValue((long)value));

        public void WriteInt64(long value) => SetScalar(new JValue(value));

        public void WriteDouble(double value) => SetScalar(new JValue(value));

        public void WriteDateTime(DateTime value) => SetScalar(new JValue(value));

        public void WriteBytes(byte[] value)
        {
            if (value is null) { WriteNull(); return; }
            SetScalar(new JValue(value));
        }

        public void WriteNull()
        {
            GuardEmpty("write null");
            shape = Shape.Null;
        }

        /// <summary>
        /// JSON-only write: put a deep clone of <paramref name="token"/> in this slot
        /// verbatim (a <see cref="JObject"/>, <see cref="JArray"/>, or a
        /// <see cref="JValue"/> whose native type the <see cref="IBindingSink"/>
        /// scalar writes cannot express, such as <see cref="decimal"/> or
        /// <see cref="DateTimeOffset"/>). A null token, or a JSON null, is
        /// <see cref="WriteNull"/>.
        /// </summary>
        public void WriteToken(JToken token)
        {
            if (token is null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
            {
                WriteNull();
                return;
            }
            SetScalar(token.DeepClone());
        }

        public JTokenBindingSink Scope(string key) => OpenMember(key, keepNull: false);

        IBindingSink IBindingSink.Scope(string key) => Scope(key);

        /// <summary>
        /// Like <see cref="Scope"/>, but the child keeps an explicit null even when
        /// the sink omits nulls. Dictionary entries use this: Newtonsoft's
        /// <c>NullValueHandling.Ignore</c> drops null object members, never null
        /// dictionary values.
        /// </summary>
        public JTokenBindingSink ScopeEntry(string key) => OpenMember(key, keepNull: true);

        IBindingSink IBindingSink.AppendItem() => AppendItem();

        public JTokenBindingSink AppendItem()
        {
            if (shape == Shape.Empty) BeginArrayCore();
            if (shape != Shape.Array)
                throw new InvalidOperationException(
                    $"Cannot append an array item to '{name}': it already holds {Describe(shape)}.");
            var child = new JTokenBindingSink(omitNulls, keepNull: true, $"{name}[{items.Count}]");
            items.Add(child);
            return child;
        }

        /// <summary>Make this slot an object; it is written as <c>{}</c> even if no member follows.</summary>
        public void BeginObject()
        {
            if (shape == Shape.Empty) BeginObjectCore();
            if (shape != Shape.Object)
                throw new InvalidOperationException($"Cannot make '{name}' an object: it already holds {Describe(shape)}.");
        }

        /// <summary>Make this slot an array; it is written as <c>[]</c> even if no item follows.</summary>
        public void BeginArray()
        {
            if (shape == Shape.Empty) BeginArrayCore();
            if (shape != Shape.Array)
                throw new InvalidOperationException($"Cannot make '{name}' an array: it already holds {Describe(shape)}.");
        }

        /// <summary>
        /// The token built so far, or <c>null</c> when this slot is absent (nothing
        /// was written, or a null was written to an omitted slot).
        /// </summary>
        public JToken ToToken()
        {
            switch (shape)
            {
                case Shape.Empty:
                    return null;
                case Shape.Null:
                    return omitNulls && !keepNull ? null : JValue.CreateNull();
                case Shape.Scalar:
                    return scalar;
                case Shape.Object:
                    var obj = new JObject();
                    foreach (var (key, child) in members)
                    {
                        var token = child.ToToken();
                        if (token is not null)
                            obj.Add(key, token);
                    }
                    return obj;
                case Shape.Array:
                    var arr = new JArray();
                    foreach (var item in items)
                        arr.Add(item.ToToken() ?? JValue.CreateNull());
                    return arr;
                default:
                    throw new InvalidOperationException($"Unknown sink shape {shape}.");
            }
        }

        private JTokenBindingSink OpenMember(string key, bool keepNull)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            if (shape == Shape.Empty) BeginObjectCore();
            if (shape != Shape.Object)
                throw new InvalidOperationException(
                    $"Cannot open member '{key}' on '{name}': it already holds {Describe(shape)}.");
            if (memberIndex.TryGetValue(key, out var existing))
                return existing;
            var child = new JTokenBindingSink(omitNulls, keepNull, $"{name}.{key}");
            members.Add((key, child));
            memberIndex.Add(key, child);
            return child;
        }

        private void BeginObjectCore()
        {
            shape = Shape.Object;
            members = new List<(string, JTokenBindingSink)>();
            memberIndex = new Dictionary<string, JTokenBindingSink>(StringComparer.Ordinal);
        }

        private void BeginArrayCore()
        {
            shape = Shape.Array;
            items = new List<JTokenBindingSink>();
        }

        private void SetScalar(JToken value)
        {
            GuardEmpty("write a value");
            shape = Shape.Scalar;
            scalar = value;
        }

        private void GuardEmpty(string op)
        {
            if (shape != Shape.Empty)
                throw new InvalidOperationException(
                    $"Cannot {op} to '{name}': it already holds {Describe(shape)}.");
        }

        private static string Describe(Shape shape) => shape switch
        {
            Shape.Scalar => "a value",
            Shape.Null => "null",
            Shape.Object => "an object",
            Shape.Array => "an array",
            _ => "nothing",
        };
    }
}
