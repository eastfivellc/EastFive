using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using EastFive.Api.Binding;
using EastFive.Serialization.Binding;

namespace EastFive.Api.Bindings
{
    /// <summary>
    /// V3 <see cref="ITypeBinder"/> for <see cref="Property{T}"/>. Surfaces
    /// PATCH-style "did the caller supply this field" semantics on top of any
    /// body-path source. Delegates inner-value materialization to the
    /// application's registered binders for <c>T</c>.
    /// <para>
    /// Mapping:
    /// <list type="bullet">
    ///   <item><description>key absent from the source → <c>{ specified = false, value = default }</c> (probed
    ///   directly, so inner binders that model optionality — <c>IRefOptional&lt;T&gt;</c>,
    ///   <c>Nullable&lt;T&gt;</c> — cannot masquerade absence as a specified empty value)</description></item>
    ///   <item><description>inner bind succeeds → <c>{ specified = true, value = T }</c> (an explicit null
    ///   binds through the inner binder, so optional targets receive their empty
    ///   representation as a SPECIFIED value — PATCH null clears the field)</description></item>
    ///   <item><description>explicit null the inner binder does not model → <c>{ specified = false }</c></description></item>
    ///   <item><description>inner bind reports any other <see cref="BindFailure"/> → bubble through <c>onFailure</c></description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Replaces the V2 <c>[PropertyJsonBinder]</c> / <c>[PropertyStringBinder]</c>
    /// stack with a single V3 binder registered globally so any selection
    /// attribute that produces a body-path source (e.g. <c>[Body(Name = "x")]</c>)
    /// composes naturally with <c>Property&lt;T&gt;</c> parameter types.
    /// </para>
    /// </summary>
    public sealed class PropertyBinder : ITypeBinder
    {
        public bool CanBind(Type targetType) =>
            targetType.IsGenericType
            && targetType.GetGenericTypeDefinition() == typeof(Property<>);

        public async ValueTask<TResult> Read<TResult>(
            Type targetType,
            IBindingSource source,
            IBindingContext context,
            Func<object, TResult> onBound,
            Func<BindFailure, TResult> onFailure,
            Func<TResult> onNull = null)
        {
            var innerType = targetType.GetGenericArguments()[0];

            // Probe the source for PRESENCE before delegating: inner binders for
            // intrinsically-optional targets (IRefOptional<T>, Nullable<T>) map an
            // ABSENT value to their empty representation rather than reporting
            // NotPresent — correct for bare parameters, but here it would erase the
            // specified=false signal PATCH handlers rely on (an omitted body key
            // would CLEAR the field instead of leaving it untouched). Absent →
            // unspecified; anything present — including an explicit null, which
            // legitimately clears optional targets — delegates to the inner binder.
            // (TResult = object: CompositeBindingSource, the source the V3 dispatch
            // phase actually supplies, supports no other TResult.)
            var present = (bool)await source.GetValue<object>(
                path: context?.KeyPath,
                onNull: () => true,
                onString: _ => true,
                onGuid: _ => true,
                onBool: _ => true,
                onInt64: _ => true,
                onDouble: _ => true,
                onDateTime: _ => true,
                onBytes: _ => true,
                onObject: _ => true,
                onArray: _ => true,
                onFailure: failure => failure.Reason is not NotPresent);
            if (!present)
                return onBound(MakeUnspecified(targetType));

            return await context.TypeBindings.Bind(
                innerType,
                source,
                context,
                onBound: innerValue => onBound(MakeSpecified(targetType, innerType, innerValue)),
                onFailure: failure => failure.Reason is NotPresent
                    ? onBound(MakeUnspecified(targetType))
                    : onFailure(failure),
                onNull: () => onBound(MakeUnspecified(targetType)));
        }

        public void Write(Type sourceType, object value, IBindingSink sink, IBindingContext context)
        {
            // Mutating an entity from a Property<T> happens in controller bodies;
            // the V3 write side has no first-class concept of "specified" so we
            // intentionally don't emit unspecified properties. Specified values
            // delegate to the inner binder for T.
            if (value is null) { sink.WriteNull(); return; }
            var innerType = sourceType.GetGenericArguments()[0];
            var specified = (bool)sourceType
                .GetField(nameof(Property<int>.specified))!
                .GetValue(value);
            if (!specified) { sink.WriteNull(); return; }
            var inner = sourceType
                .GetField(nameof(Property<int>.value))!
                .GetValue(value);
            context.TypeBindings.Emit(innerType, inner, sink, context);
        }

        private static object MakeSpecified(Type propertyType, Type innerType, object innerValue)
        {
            // Use the `Property<T>(T value)` ctor — it sets specified = true.
            var ctor = propertyType.GetConstructor(new[] { innerType });
            return ctor!.Invoke(new[] { innerValue });
        }

        private static object MakeUnspecified(Type propertyType)
        {
            // Default-init: specified = false, value = default(T).
            return Activator.CreateInstance(propertyType);
        }
    }

    /// <summary>
    /// Module initializer that registers <see cref="PropertyBinder"/> with the
    /// V3 <see cref="TypeBinderRegistry"/>. Runs once per process when the
    /// EastFive.Api assembly is loaded — no application wiring required.
    /// </summary>
    internal static class PropertyBinderModuleInitializer
    {
        #pragma warning disable CA2255 // intentional: framework-level binder registration
        [ModuleInitializer]
        #pragma warning restore CA2255
        internal static void Init()
        {
            TypeBinderRegistry.Register(new PropertyBinder());
        }
    }
}
