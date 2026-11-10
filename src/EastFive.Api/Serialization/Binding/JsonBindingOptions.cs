using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using EastFive.Serialization.Binding;

namespace EastFive.Api.Serialization.Binding
{
    /// <summary>
    /// Options for <see cref="JsonBinding"/>.
    /// </summary>
    public sealed class JsonBindingOptions
    {
        public static JsonBindingOptions Default { get; } = new JsonBindingOptions();

        /// <summary>
        /// Write: drop object members whose value is null instead of writing
        /// JSON <c>null</c> (Newtonsoft's <c>NullValueHandling.Ignore</c>). Array
        /// elements and dictionary values keep their nulls. Ignored on read.
        /// </summary>
        public bool OmitNulls { get; init; }
    }

    /// <summary>
    /// The single failure type of <see cref="JsonBinding"/> reads. The message
    /// names the full key path of the value that could not be bound
    /// (<c>'steps[3].output_variable' must be String: …</c>); <see cref="Failure"/>
    /// carries the structured <see cref="BindFailure"/> chain.
    /// </summary>
    public sealed class JsonBindingException : Exception
    {
        public JsonBindingException(BindFailure failure)
            : base(Describe(failure, out var keyPath))
        {
            Failure = failure;
            KeyPath = keyPath;
        }

        /// <summary>The root failure; nested causes hang off <see cref="NestedFailure"/> reasons.</summary>
        public BindFailure Failure { get; }

        /// <summary>Dotted/bracketed path from the JSON root to the value that failed; empty for the root itself.</summary>
        public string KeyPath { get; }

        private static string Describe(BindFailure failure, out string keyPath)
        {
            var path = new StringBuilder();
            Type expected = null;
            var current = failure;
            while (true)
            {
                if (!string.IsNullOrEmpty(current.KeyPath))
                {
                    if (path.Length > 0 && current.KeyPath[0] != '[')
                        path.Append('.');
                    path.Append(current.KeyPath);
                }
                if (current.ExpectedType is not null && current.ExpectedType != typeof(object))
                    expected = current.ExpectedType;
                if (current.Reason is NestedFailure nested)
                {
                    current = nested.Inner;
                    continue;
                }
                break;
            }
            expected ??= current.ExpectedType;
            keyPath = path.ToString();
            var where = keyPath.Length == 0 ? "$" : keyPath;
            var reason = current.Reason?.Describe() ?? "unknown failure";
            return $"'{where}' must be {TypeName(expected)}: {reason}";
        }

        private static string TypeName(Type type)
        {
            if (type is null) return "a value";
            if (!type.IsGenericType) return type.Name;
            var name = type.Name;
            var tick = name.IndexOf('`');
            if (tick >= 0) name = name.Substring(0, tick);
            return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(TypeName))}>";
        }
    }
}
