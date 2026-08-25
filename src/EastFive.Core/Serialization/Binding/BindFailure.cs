using System;

namespace EastFive.Serialization.Binding
{
    /// <summary>
    /// A structured deserialization failure. Carried through every <c>onFailure</c>
    /// callback in the binding pipeline; never thrown as an exception.
    /// </summary>
    public readonly struct BindFailure
    {
        public BindFailure(IBindFailureReason reason, Type expectedType, string keyPath = "")
        {
            Reason = reason;
            ExpectedType = expectedType;
            KeyPath = keyPath ?? string.Empty;
        }

        public IBindFailureReason Reason { get; }

        public Type ExpectedType { get; }

        /// <summary>Dotted path from the root source to the slot that failed (e.g. <c>patient.address.zip</c>).</summary>
        public string KeyPath { get; }

        public BindFailure WithKeyPath(string path) => new BindFailure(Reason, ExpectedType, path);

        public BindFailure Nest(string segment)
        {
            var path = string.IsNullOrEmpty(KeyPath) ? segment : $"{segment}.{KeyPath}";
            return new BindFailure(Reason, ExpectedType, path);
        }

        public override string ToString() =>
            $"BindFailure[{ExpectedType?.Name ?? "?"} @ '{KeyPath}']: {Reason?.Describe() ?? "<no reason>"}";
    }
}
