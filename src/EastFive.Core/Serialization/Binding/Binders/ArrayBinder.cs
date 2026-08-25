using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EastFive.Serialization.Binding.Binders
{
    /// <summary>
    /// Binds arrays of any element type. Requires the source to surface an array
    /// (<c>onArray</c>); each element is bound as a NEW root (KeyPath reset to "")
    /// against its own <see cref="IBindingSource"/>.
    /// </summary>
    public sealed class ArrayBinder : ITypeBinder
    {
        public bool CanBind(Type targetType) =>
            targetType.IsArray && targetType.GetArrayRank() == 1;

        public ValueTask<TResult> Read<TResult>(
            Type targetType,
            IBindingSource source,
            IBindingContext context,
            Func<object, TResult> onBound,
            Func<BindFailure, TResult> onFailure,
            Func<TResult> onNull = null)
        {
            var path = context?.KeyPath;
            var elemType = targetType.GetElementType();

            return source.GetValue<TResult>(
                path: path,
                elementTypeHint: elemType,
                onNull: onNull,
                onArray: en => BindElements(en, elemType, context, onBound, onFailure, path),
                onString: s =>
                {
                    // Single-cell delimited list fallback (?ids=1;2;3 — commonly used in legacy clients).
                    var parts = EastFive.Serialization.StringExtensions.ParseStringToArray(s);
                    var elemSources = new System.Collections.Generic.List<IBindingSource>();
                    foreach (var part in parts)
                        elemSources.Add(new EastFive.Serialization.Binding.Sources.StringBindingSource(part));
                    return BindElements(elemSources, elemType, context, onBound, onFailure, path);
                },
                onFailure: onFailure);
        }

        private static TResult BindElements<TResult>(
            IEnumerable<IBindingSource> elements,
            Type elemType,
            IBindingContext context,
            Func<object, TResult> onBound,
            Func<BindFailure, TResult> onFailure,
            string path)
        {
            var items = new List<object>();
            var elemContext = context.WithKeyPath(string.Empty);
            var idx = 0;
            foreach (var elem in elements)
            {
                object bound = null;
                BindFailure? failure = null;
                var task = context.TypeBindings.Bind<bool>(
                    elemType,
                    elem,
                    elemContext,
                    v => { bound = v; return true; },
                    f => { failure = f; return false; });
                var ok = task.IsCompletedSuccessfully ? task.Result : task.AsTask().GetAwaiter().GetResult();
                if (!ok)
                {
                    var inner = failure ?? new BindFailure(new WrongSourceType("element", "?"), elemType, $"[{idx}]");
                    return onFailure(new BindFailure(inner.Reason, elemType, $"{path}[{idx}]"));
                }
                items.Add(bound);
                idx++;
            }
            var arr = Array.CreateInstance(elemType, items.Count);
            for (var i = 0; i < items.Count; i++) arr.SetValue(items[i], i);
            return onBound(arr);
        }

        public void Write(Type sourceType, object value, IBindingSink sink, IBindingContext context)
        {
            if (value is null) { sink.WriteNull(); return; }
            var elemType = sourceType.GetElementType();
            foreach (var item in (IEnumerable)value)
                context.TypeBindings.Emit(elemType, item, sink.AppendItem(), context);
        }
    }
}
