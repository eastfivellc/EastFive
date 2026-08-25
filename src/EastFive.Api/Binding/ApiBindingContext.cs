using System;
using System.Globalization;

using EastFive.Serialization.Binding;

namespace EastFive.Api.Binding
{
    /// <summary>
    /// V3 dispatcher's <see cref="IBindingContext"/> — extends the core
    /// <see cref="BindingContext"/> shape with the per-request capabilities
    /// (<see cref="Request"/>, <see cref="Application"/>) that storage and
    /// other platform-aware <see cref="ITypeBinder"/>s need to do their work.
    /// <para>
    /// The core <see cref="IBindingContext"/> interface deliberately excludes
    /// capabilities (its docs note that capabilities are injected at the
    /// <see cref="ITypeBindings"/> layer). We honour that by adding the
    /// capabilities on a derived concrete type rather than the interface —
    /// binders that need them downcast; binders that don't are unaffected.
    /// </para>
    /// </summary>
    public sealed class ApiBindingContext : IBindingContext
    {
        public ApiBindingContext(
            ITypeBindings typeBindings,
            IHttpRequest request,
            IApplication application,
            IBindingSlot slot = null,
            string keyPath = "",
            CultureInfo culture = null,
            IMemberPlanProvider memberPlanProvider = null,
            Type memberScope = null)
        {
            TypeBindings = typeBindings;
            Request = request;
            Application = application;
            Slot = slot;
            KeyPath = keyPath ?? string.Empty;
            Culture = culture ?? CultureInfo.InvariantCulture;
            MemberPlanProvider = memberPlanProvider ?? ScopedMemberPlanProvider.Instance;
            MemberScope = memberScope;
        }

        /// <summary>Originating request. Never null in dispatcher-built contexts.</summary>
        public IHttpRequest Request { get; }

        /// <summary>Hosting application. Never null in dispatcher-built contexts.</summary>
        public IApplication Application { get; }

        public ITypeBindings TypeBindings { get; }

        public IBindingSlot Slot { get; }

        public string KeyPath { get; }

        public CultureInfo Culture { get; }

        public IMemberPlanProvider MemberPlanProvider { get; }

        public Type MemberScope { get; }

        public IBindingContext WithSlot(IBindingSlot slot) =>
            new ApiBindingContext(TypeBindings, Request, Application, slot, KeyPath, Culture, MemberPlanProvider, MemberScope);

        public IBindingContext WithKeyPath(string keyPath) =>
            new ApiBindingContext(TypeBindings, Request, Application, Slot, keyPath, Culture, MemberPlanProvider, MemberScope);

        public IBindingContext WithTypeBindings(ITypeBindings typeBindings) =>
            new ApiBindingContext(typeBindings, Request, Application, Slot, KeyPath, Culture, MemberPlanProvider, MemberScope);

        public IBindingContext WithMemberPlanProvider(IMemberPlanProvider memberPlanProvider) =>
            new ApiBindingContext(TypeBindings, Request, Application, Slot, KeyPath, Culture, memberPlanProvider, MemberScope);

        public IBindingContext WithMemberScope(Type memberScope) =>
            new ApiBindingContext(TypeBindings, Request, Application, Slot, KeyPath, Culture, MemberPlanProvider, memberScope);
    }
}
