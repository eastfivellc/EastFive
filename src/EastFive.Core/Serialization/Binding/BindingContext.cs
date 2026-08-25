using System;
using System.Globalization;

namespace EastFive.Serialization.Binding
{
    /// <summary>Default immutable <see cref="IBindingContext"/>.</summary>
    public sealed class BindingContext : IBindingContext
    {
        public BindingContext(
            ITypeBindings typeBindings,
            IBindingSlot slot = null,
            string keyPath = "",
            CultureInfo culture = null,
            IMemberPlanProvider memberPlanProvider = null,
            Type memberScope = null)
        {
            TypeBindings = typeBindings;
            Slot = slot;
            KeyPath = keyPath ?? string.Empty;
            Culture = culture ?? CultureInfo.InvariantCulture;
            MemberPlanProvider = memberPlanProvider ?? ScopedMemberPlanProvider.Instance;
            MemberScope = memberScope;
        }

        public ITypeBindings TypeBindings { get; }

        public IBindingSlot Slot { get; }

        public string KeyPath { get; }

        public CultureInfo Culture { get; }

        public IMemberPlanProvider MemberPlanProvider { get; }

        public Type MemberScope { get; }

        public IBindingContext WithSlot(IBindingSlot slot) =>
            new BindingContext(TypeBindings, slot, KeyPath, Culture, MemberPlanProvider, MemberScope);

        public IBindingContext WithKeyPath(string keyPath) =>
            new BindingContext(TypeBindings, Slot, keyPath, Culture, MemberPlanProvider, MemberScope);

        public IBindingContext WithTypeBindings(ITypeBindings typeBindings) =>
            new BindingContext(typeBindings, Slot, KeyPath, Culture, MemberPlanProvider, MemberScope);

        public IBindingContext WithMemberPlanProvider(IMemberPlanProvider memberPlanProvider) =>
            new BindingContext(TypeBindings, Slot, KeyPath, Culture, memberPlanProvider, MemberScope);

        public IBindingContext WithMemberScope(Type memberScope) =>
            new BindingContext(TypeBindings, Slot, KeyPath, Culture, MemberPlanProvider, memberScope);
    }
}
