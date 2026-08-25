using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EastFive.Serialization.Binding.Binders
{
    /// <summary>
    /// Binds <c>IDictionary&lt;string, TValue&gt;</c> (and concrete types like
    /// <c>Dictionary&lt;string, TValue&gt;</c>) from an object-shaped source.
    /// Requires the source's <c>onObject</c> child to additionally implement
    /// <see cref="IKeyedBindingSource"/> so the key set can be enumerated (e.g.
    /// <c>JTokenBindingSource</c> over a JSON object) — sources that don't model
    /// an enumerable key set (form collections, EDM rows, etc.) report
    /// <see cref="UnsupportedTargetType"/>, same as before this binder existed.
    /// Each value is bound independently via the registered binder for
    /// <c>TValue</c>, so <c>IDictionary&lt;string, string&gt;</c>,
    /// <c>IDictionary&lt;string, int&gt;</c>, etc. all work without extra code.
    /// <para>
    /// Only <c>string</c> keys are supported (<see cref="IKeyedBindingSource.Keys"/>
    /// is string-keyed); non-string-keyed dictionary targets fall through to the
    /// next binder (typically <c>PocoBinder</c>, which will itself decline or
    /// mis-bind — no worse than before this binder existed).
    /// </para>
    /// </summary>
    public sealed class DictionaryBinder : ITypeBinder
    {
        public bool CanBind(Type targetType)
        {
            var dictInterface = FindDictionaryInterface(targetType);
            if (dictInterface is null) return false;
            return dictInterface.GetGenericArguments()[0] == typeof(string);
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
            var dictInterface = FindDictionaryInterface(targetType);
            var valueType = dictInterface.GetGenericArguments()[1];

            IBindingSource child = null;
            BindFailure? navFailure = null;
            var isNull = false;
            await source.GetValue<object>(
                path: path,
                onNull: () => { isNull = true; return null; },
                onObject: src => { child = src; return null; },
                onFailure: f => { navFailure = f; return null; });

            if (navFailure is { } nf)
                return onFailure(nf);

            if (isNull)
            {
                if (onNull is not null) return onNull();
                return onFailure(new BindFailure(new NullValue(), targetType, path));
            }

            if (child is not IKeyedBindingSource keyed)
                return onFailure(new BindFailure(new UnsupportedTargetType(targetType), targetType, path));

            var instance = (IDictionary)CreateInstance(targetType);
            var valueContext = context.WithKeyPath(string.Empty);

            foreach (var key in keyed.Keys)
            {
                object boundValue = null;
                BindFailure? valueFailure = null;
                var isValueNull = false;
                var task = context.TypeBindings.Bind<bool>(
                    valueType,
                    keyed,
                    valueContext.WithKeyPath(key),
                    v => { boundValue = v; return true; },
                    f => { valueFailure = f; return false; },
                    onNull: () => { isValueNull = true; return true; });
                var ok = task.IsCompletedSuccessfully ? task.Result : await task;
                if (!ok)
                {
                    var inner = valueFailure ?? new BindFailure(new WrongSourceType("value", "?"), valueType, key);
                    return onFailure(new BindFailure(new NestedFailure(inner), targetType, $"{path}.{key}"));
                }
                instance[key] = isValueNull ? null : boundValue;
            }

            return onBound(instance);
        }

        public void Write(Type sourceType, object value, IBindingSink sink, IBindingContext context)
        {
            if (value is null) { sink.WriteNull(); return; }
            var dictInterface = FindDictionaryInterface(sourceType);
            var valueType = dictInterface.GetGenericArguments()[1];
            foreach (DictionaryEntry entry in (IDictionary)value)
                context.TypeBindings.Emit(valueType, entry.Value, sink.Scope((string)entry.Key), context);
        }

        private static Type FindDictionaryInterface(Type targetType)
        {
            if (targetType is null) return null;
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                return targetType;
            return targetType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
        }

        private static object CreateInstance(Type targetType)
        {
            if (!targetType.IsInterface && !targetType.IsAbstract && targetType.GetConstructor(Type.EmptyTypes) is not null)
                return Activator.CreateInstance(targetType);
            var dictInterface = FindDictionaryInterface(targetType);
            var args = dictInterface.GetGenericArguments();
            var concreteType = typeof(Dictionary<,>).MakeGenericType(args);
            return Activator.CreateInstance(concreteType);
        }
    }
}
