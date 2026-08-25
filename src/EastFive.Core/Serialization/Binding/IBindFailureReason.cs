using System;

namespace EastFive.Serialization.Binding
{
    /// <summary>
    /// Polymorphic reason carried by <see cref="BindFailure"/>. Implemented as an open
    /// sum type so domain-specific reasons (e.g. storage not-found) can be declared
    /// alongside the binders that produce them, without modifying EastFive.Core.
    /// Consumers typically use pattern matching against concrete record types
    /// (with an open <c>_</c> arm as the safety net for unknown reasons).
    /// </summary>
    public interface IBindFailureReason
    {
        string Describe();
    }

    /// <summary>Value is absent from the source (e.g. JSON property missing, query key absent).</summary>
    public sealed record NotPresent : IBindFailureReason
    {
        public string Describe() => "value not present";
    }

    /// <summary>
    /// The source represents an explicit null value, and the caller did not supply
    /// an <c>onNull</c> callback to handle it. Binders that intrinsically model null
    /// (e.g. <c>NullableBinder</c>, <c>RefOptionalBinder</c>) opt in by passing
    /// <c>onNull</c>; binders that do not let this failure propagate, which is the
    /// correct outcome for non-nullable targets.
    /// </summary>
    public sealed record NullValue : IBindFailureReason
    {
        public string Describe() => "value is null";
    }

    /// <summary>Source is the wrong shape for the requested access (e.g. asked for object members on a scalar).</summary>
    public sealed record WrongSourceType(string Expected, string Got) : IBindFailureReason
    {
        public string Describe() => $"expected source kind `{Expected}`, got `{Got}`";
    }

    /// <summary>Value is present but cannot be parsed into the requested canonical form.</summary>
    public sealed record ParseError(string Detail) : IBindFailureReason
    {
        public string Describe() => Detail;
    }

    /// <summary>No binder in the supplied <see cref="ITypeBindings"/> can produce the requested target type.</summary>
    public sealed record UnsupportedTargetType(Type Target) : IBindFailureReason
    {
        public string Describe() => $"no binder registered for `{Target.FullName}`";
    }

    /// <summary>A nested bind (e.g. a POCO member or array element) failed; <see cref="Inner"/> carries the underlying cause.</summary>
    public sealed record NestedFailure(BindFailure Inner) : IBindFailureReason
    {
        public string Describe() => $"nested failure at `{Inner.KeyPath}`: {Inner.Reason.Describe()}";
    }
}
