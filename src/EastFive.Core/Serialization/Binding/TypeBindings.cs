using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using EastFive.Serialization.Binding.Binders;

namespace EastFive.Serialization.Binding
{
    /// <summary>
    /// Default <see cref="ITypeBindings"/> implementation: an immutable, ordered
    /// list of <see cref="ITypeBinder"/>s with a per-type resolution cache.
    /// </summary>
    public sealed class TypeBindings : ITypeBindings
    {
        private readonly ITypeBinder[] binders;
        private readonly ConcurrentDictionary<Type, ITypeBinder> cache = new();

        public TypeBindings(IEnumerable<ITypeBinder> binders)
        {
            this.binders = (binders ?? Array.Empty<ITypeBinder>()).ToArray();
        }

        /// <summary>Built-in registry: scalars, refs, refs collections, enums, date/time, nullable, arrays.</summary>
        public static TypeBindings Default { get; } = new TypeBindings(BuiltInBinders());

        private static IEnumerable<ITypeBinder> BuiltInBinders()
        {
            // Order matters: more-specific shapes first.
            yield return new NullableBinder();
            yield return new GuidBinder();
            yield return new RefBinder();
            yield return new RefOptionalBinder();
            yield return new RefsBinder();
            yield return new EnumBinder();
            yield return new DateTimeBinder();
            yield return new DateTimeOffsetBinder();
            yield return new TimeSpanBinder();
            yield return new BytesBinder();
            yield return new PrimitiveBinder();
            yield return new ArrayBinder();
            yield return new DictionaryBinder();
            // Catch-all: any class/struct with bindable members. Registered last so
            // every specific binder gets first crack.
            yield return new PocoBinder();
        }

        public ValueTask<TResult> Bind<TResult>(
            Type targetType,
            IBindingSource source,
            IBindingContext context,
            Func<object, TResult> onBound,
            Func<BindFailure, TResult> onFailure,
            Func<TResult> onNull = null)
        {
            if (targetType is null)
                return new ValueTask<TResult>(onFailure(new BindFailure(
                    new UnsupportedTargetType(typeof(object)), typeof(object), context?.KeyPath ?? string.Empty)));

            var binder = cache.GetOrAdd(targetType, ResolveBinder);
            if (binder is null)
                return new ValueTask<TResult>(onFailure(new BindFailure(
                    new UnsupportedTargetType(targetType), targetType, context?.KeyPath ?? string.Empty)));

            return binder.Read(targetType, source, context ?? new BindingContext(this), onBound, onFailure, onNull);
        }

        public void Emit(
            Type sourceType,
            object value,
            IBindingSink sink,
            IBindingContext context)
        {
            if (sourceType is null)
                throw new ArgumentNullException(nameof(sourceType));
            if (sink is null)
                throw new ArgumentNullException(nameof(sink));

            var binder = cache.GetOrAdd(sourceType, ResolveBinder);
            if (binder is null)
                throw new InvalidOperationException(
                    $"No ITypeBinder registered for {sourceType.FullName}.");

            binder.Write(sourceType, value, sink, context ?? new BindingContext(this));
        }

        private ITypeBinder ResolveBinder(Type targetType)
        {
            for (var i = 0; i < binders.Length; i++)
            {
                if (binders[i].CanBind(targetType))
                    return binders[i];
            }
            return null;
        }

        public ITypeBindings ForSlot(IBindingSlot slot)
        {
            // v1: no attribute-driven per-slot overlay. Sub-classes / future
            // attribute-interface dispatch can produce a wrapping ITypeBindings.
            return this;
        }

        public ITypeBindings With(ITypeBinder binder)
        {
            if (binder is null) throw new ArgumentNullException(nameof(binder));
            var combined = new ITypeBinder[binders.Length + 1];
            combined[0] = binder;
            Array.Copy(binders, 0, combined, 1, binders.Length);
            return new TypeBindings(combined);
        }

        public ITypeBindings Without(Func<ITypeBinder, bool> predicate)
        {
            if (predicate is null) throw new ArgumentNullException(nameof(predicate));
            return new TypeBindings(binders.Where(b => !predicate(b)));
        }
    }
}
