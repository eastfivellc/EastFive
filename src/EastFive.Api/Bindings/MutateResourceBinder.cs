using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using EastFive.Api.Binding;
using EastFive.Api.Binding.Scopes;
using EastFive.Extensions;
using EastFive.Serialization.Binding;

namespace EastFive.Api.Bindings
{
    /// <summary>
    /// V3 <see cref="ITypeBinder"/> for <see cref="MutateResource{T}"/>.
    /// Folds <c>T</c>'s <see cref="PatchBody"/> member plan into a single
    /// composed mutator: each bound member contributes one
    /// <c>WithMember</c> application, accumulated as a
    /// <c>Func&lt;object,object&gt;</c> that maps the boxed entity to its
    /// updated copy. The final <see cref="MutateResource{T}"/> delegate
    /// unboxes the result.
    ///
    /// <para>Properties:
    /// <list type="bullet">
    ///   <item>No special-case for <see cref="Property{T}"/> — member-type
    ///   binding is delegated to whichever binder is registered for the
    ///   member's slot type (the application's <see cref="PropertyBinder"/>
    ///   handles <c>Property&lt;X&gt;</c> members transparently).</item>
    ///   <item>Per-member <see cref="NotPresent"/> failures are tolerated and
    ///   skip the member; any other failure is propagated through
    ///   <c>onFailure</c>.</item>
    ///   <item>Empty body → identity mutator (<c>t => t</c>).</item>
    ///   <item><see cref="CanBind"/> additionally refuses types whose
    ///   <see cref="PatchBody"/> plan is empty, so misconfiguration (entity
    ///   not annotated with <c>[ApiProperty]</c>) fails loudly at the
    ///   selection ladder rather than silently producing a no-op mutator.</item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class MutateResourceBinder : ITypeBinder
    {
        public bool CanBind(Type targetType)
        {
            if (!targetType.IsGenericType) return false;
            if (targetType.GetGenericTypeDefinition() != typeof(MutateResource<>)) return false;
            return true;
        }

        public ValueTask<TResult> Read<TResult>(
            Type targetType,
            IBindingSource source,
            IBindingContext context,
            Func<object, TResult> onBound,
            Func<BindFailure, TResult> onFailure,
            Func<TResult> onNull = null)
        {
            var entityType = targetType.GetGenericArguments()[0];
            // Dispatch into a generic helper closed over T so we can construct
            // a strongly-typed MutateResource<T> at the end.
            var dispatchMethod = typeof(MutateResourceBinder)
                .GetMethod(nameof(ReadGenericAsync), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(entityType, typeof(TResult));
            return (ValueTask<TResult>)dispatchMethod.Invoke(
                null,
                new object[] { source, context, onBound, onFailure, onNull })!;
        }

        public void Write(Type sourceType, object value, IBindingSink sink, IBindingContext context)
        {
            // MutateResource<T> is a body→delegate transformer; serialising a
            // mutator delegate back out the wire is not a defined operation.
            sink.WriteNull();
        }

        private static async ValueTask<TResult> ReadGenericAsync<T, TResult>(
            IBindingSource source,
            IBindingContext context,
            Func<object, TResult> onBound,
            Func<BindFailure, TResult> onFailure,
            Func<TResult> onNull)
        {
            var rootPath = context?.KeyPath ?? string.Empty;
            var provider = context.MemberPlanProvider
                ?? throw new InvalidOperationException(
                    "MutateResourceBinder requires an IMemberPlanProvider on the context.");

            // Empty-plan probe: short-circuit Aggregate by returning from aggr
            // on the first member without invoking the continuation.
            var hasAny = provider.Aggregate<bool, bool>(
                typeof(T),
                typeof(PatchBody),
                start: false,
                aggr: (_, _, _) => true,
                onComplete: _ => false);
            if (!hasAny)
                return onFailure(new BindFailure(
                    new WrongSourceType("members annotated for PatchBody", "none"),
                    typeof(MutateResource<T>),
                    rootPath));

            // Main fold: accumulate a Func<T,T> that, when applied, walks
            // each bound member's WithMember in declaration order. The
            // starting mutator is identity; each member composes onto it.
            // Per-member (T) casts are no-ops for reference T (the typical
            // MutateResource<T> usage) and box/unbox for value T.
            return await provider.Aggregate<Func<T, T>, Task<TResult>>(
                typeof(T),
                typeof(PatchBody),
                start: t => t,
                aggr: async (mutator, member, next) =>
                {
                    var memberContext = member.ScopeInto(context);
                    var memberBindings = memberContext.TypeBindings.ForSlot(memberContext.Slot);
                    return await await memberBindings.Bind(
                        member.MemberType,
                        source,
                        memberContext,
                        value => next(t => (T)member.WithMember(mutator(t), value)),
                        failure =>
                        {
                            if (failure.Reason is NotPresent)
                                return next(mutator); // tolerate missing
                            var outer = new BindFailure(
                                new NestedFailure(member.DecorateFailure(failure)),
                                typeof(MutateResource<T>),
                                rootPath);
                            return onFailure(outer).AsTask();
                        },
                        onNull: () => next(t => (T)member.WithMember(mutator(t), null)));
                },
                onComplete: mutator => onBound(new MutateResource<T>(mutator)).AsTask());
        }
    }

    /// <summary>
    /// Module initializer that registers <see cref="MutateResourceBinder"/>
    /// with the V3 <see cref="TypeBinderRegistry"/>.
    /// </summary>
    internal static class MutateResourceBinderModuleInitializer
    {
        #pragma warning disable CA2255 // intentional: framework-level binder registration
        [ModuleInitializer]
        #pragma warning restore CA2255
        internal static void Init()
        {
            TypeBinderRegistry.Register(new MutateResourceBinder());
        }
    }
}
