using System;
using System.Threading.Tasks;
using EastFive.Extensions;

namespace EastFive.Serialization.Binding.Binders
{
    /// <summary>
    /// Catch-all binder for complex types: probes the source for object shape,
    /// unwraps it, then folds the scoped <see cref="IMemberPlan"/> set via
    /// <see cref="IMemberPlanProvider.Aggregate{TAccum,TResult}"/>. Each member
    /// scopes its own child context, looks up its own binder, and either
    /// applies the bound value to the accumulating instance or skips on
    /// <see cref="NotPresent"/> / <see cref="UnsupportedTargetType"/>.
    /// </summary>
    public sealed class PocoBinder : ITypeBinder
    {
        public bool CanBind(Type targetType) =>
            targetType is not null &&
            targetType != typeof(string) &&
            !targetType.IsArray &&
            !targetType.IsPrimitive &&
            !targetType.IsEnum &&
            (targetType.IsClass
                ? targetType.GetConstructor(Type.EmptyTypes) is not null
                : targetType.IsValueType && !targetType.IsGenericTypeDefinition);

        public async ValueTask<TResult> Read<TResult>(
            Type targetType,
            IBindingSource source,
            IBindingContext context,
            Func<object, TResult> onBound,
            Func<BindFailure, TResult> onFailure,
            Func<TResult> onNull = null)
        {
            var path = context?.KeyPath ?? string.Empty;

            // Probe shape and unwrap to the inner source at this path. The probe
            // uses TResult=object so it is compatible with CompositeBindingSource
            // (which only supports TResult=object); the returned IBindingSource
            // is the source rooted AT the parameter's path, so member iteration
            // walks it with member-relative paths (no parameter-name prefix).
            IBindingSource child = null;
            BindFailure? navFailure = null;
            var isNull = false;
            await source.GetValue<object>(
                path: path,
                onNull: () => { isNull = true; return null; },
                onObject: src => { child = src; return null; },
                onFailure: f => { navFailure = f; return null; });

            if (navFailure is { } nf)
            {
                if (nf.Reason is NotPresent && onNull is not null) return onNull();
                return onFailure(nf);
            }

            if (isNull)
            {
                if (onNull is not null) return onNull();
                return onFailure(new BindFailure(new NullValue(), targetType, path));
            }

            // child is the source rooted at this path; subsequent member
            // navigation uses each member's wire-name as a path on that source.
            var childSource = child ?? source;

            var provider = context.MemberPlanProvider
                ?? throw new InvalidOperationException("PocoBinder requires an IMemberPlanProvider on the context.");
            var scope = context.MemberScope
                ?? throw new InvalidOperationException(
                    $"PocoBinder requires a MemberScope on the context to bind type {targetType.FullName}. " +
                    "The binding driver must call IBindingContext.WithMemberScope before invoking a complex-type bind.");

            var instance = Activator.CreateInstance(targetType);

            // Reset the key-path on the context we hand to ScopeInto: childSource
            // is already rooted at the parent path, and ScopeInto appends — we
            // want member-relative paths, not parent.member paths.
            var memberRoot = context.WithKeyPath(string.Empty);

            return await provider.Aggregate<object, Task<TResult>>(
                targetType,
                scope,
                start: instance,
                aggr: async (inst, member, next) =>
                {
                    var memberContext = member.ScopeInto(memberRoot);
                    var memberBindings = memberContext.TypeBindings.ForSlot(memberContext.Slot);
                    return await await memberBindings.Bind(
                        member.MemberType,
                        childSource,
                        memberContext,
                        value => next(member.WithMember(inst, value)),
                        failure =>
                        {
                            if (failure.Reason is NotPresent)
                                return next(inst); // tolerate missing
                            if (failure.Reason is UnsupportedTargetType)
                                return next(inst); // V2 parity: Newtonsoft silently left unbindable members defaulted
                            var outer = new BindFailure(
                                new NestedFailure(member.DecorateFailure(failure)),
                                targetType,
                                path);
                            return onFailure(outer).AsTask();
                        },
                        onNull: () => next(member.WithMember(inst, null)));
                },
                onComplete: inst => onBound(inst).AsTask());
        }

        public void Write(Type sourceType, object value, IBindingSink sink, IBindingContext context)
        {
            if (value is null) { sink.WriteNull(); return; }
            var provider = context.MemberPlanProvider
                ?? throw new InvalidOperationException("PocoBinder requires an IMemberPlanProvider on the context.");
            var scope = context.MemberScope
                ?? throw new InvalidOperationException(
                    $"PocoBinder requires a MemberScope on the context to write type {sourceType.FullName}.");

            provider.Aggregate<int, int>(
                sourceType,
                scope,
                start: 0,
                aggr: (state, member, next) =>
                {
                    var childSink = member.ScopeInto(sink);
                    context.TypeBindings.Emit(member.MemberType, member.Read(value), childSink, context);
                    return next(state);
                },
                onComplete: _ => 0);
        }
    }
}
