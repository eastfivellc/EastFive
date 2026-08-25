using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace EastFive.Serialization.Binding.Binders
{
    /// <summary>Binds <c>IRefs&lt;T&gt;</c> — collection of refs. Accepts <c>onArray</c> or a single comma/semicolon string.</summary>
    public sealed class RefsBinder : ITypeBinder
    {
        public bool CanBind(Type targetType) =>
            targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(IRefs<>);

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

            return source.GetValue<TResult>(
                path: path,
                elementTypeHint: typeof(Guid),
                onNull: onNull,
                onArray: en =>
                {
                    var guids = new List<Guid>();
                    foreach (var elem in en)
                    {
                        BindFailure? elemFailure = null;
                        Guid? elemGuid = null;
                        var task = elem.GetValue<bool>(
                            onGuid: g => { elemGuid = g; return true; },
                            onString: s =>
                            {
                                if (Guid.TryParse(s, out var g)) { elemGuid = g; return true; }
                                elemFailure = new BindFailure(new ParseError($"'{s}' is not a Guid"), typeof(Guid), path);
                                return false;
                            },
                            onFailure: f => { elemFailure = f; return false; });
                        // Sources are synchronous in current impls.
                        var ok = task.IsCompletedSuccessfully ? task.Result : task.AsTask().GetAwaiter().GetResult();
                        if (!ok) return onFailure(elemFailure ?? new BindFailure(new WrongSourceType("guid", "element"), typeof(Guid), path));
                        guids.Add(elemGuid.Value);
                    }
                    return onBound(BuildRefs(referenced, guids));
                },
                onString: s =>
                {
                    var parts = EastFive.Serialization.StringExtensions.ParseStringToArray(s);
                    var guids = new List<Guid>();
                    foreach (var part in parts)
                    {
                        if (!Guid.TryParse(part, out var g))
                            return onFailure(new BindFailure(new ParseError($"'{part}' is not a Guid"), typeof(Guid), path));
                        guids.Add(g);
                    }
                    return onBound(BuildRefs(referenced, guids));
                },
                onFailure: onFailure);
        }

        public void Write(Type sourceType, object value, IBindingSink sink, IBindingContext context)
        {
            if (value is null) { sink.WriteNull(); return; }
            var idsProp = value.GetType().GetProperty("ids");
            var ids = (IEnumerable<Guid>)idsProp.GetValue(value);
            foreach (var g in ids)
                sink.AppendItem().WriteGuid(g);
        }

        private static object BuildRefs(Type referenced, IReadOnlyList<Guid> guids) =>
            guids.Bind(typeof(Guid)).BindToRefs(referenced);
    }

    internal static class _BindingArrayHelpers
    {
        public static Array Bind(this IReadOnlyList<Guid> guids, Type elemType)
        {
            var arr = Array.CreateInstance(elemType, guids.Count);
            for (var i = 0; i < guids.Count; i++) arr.SetValue(guids[i], i);
            return arr;
        }

        public static object BindToRefs(this Array guidArray, Type referenced)
        {
            var refsType = typeof(EastFive.Refs<>).MakeGenericType(referenced);
            return Activator.CreateInstance(refsType, new object[] { (Guid[])guidArray });
        }
    }
}
