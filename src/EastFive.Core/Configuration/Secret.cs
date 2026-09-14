#nullable enable
using System;

namespace EastFive.Configuration
{
    /// <summary>
    /// WHERE a secret lives, distinct from the secret itself so a reference can never be passed
    /// where a credential is expected. Today it is a config key the whole-vault
    /// <c>IConfiguration</c> provider serves synchronously; a vault alias + name + version is the
    /// intended later shape, resolved through a direct secret client rather than back through
    /// <c>IConfiguration</c> (so the dot/dash key mapping never round-trips).
    /// </summary>
    public sealed record SecretReference(string Key, string Purpose)
    {
        public override string ToString() => $"{Purpose} -> [{Key}]";
    }

    /// <summary>
    /// Resolved secret material. Deliberately has no public property or field: serializers
    /// (Newtonsoft, System.Text.Json) see an empty object, interpolation and logging see
    /// <c>[secret]</c>, and the only way to the value is an explicit <see cref="Reveal"/> at the
    /// point of use (e.g. constructing an SDK client).
    /// </summary>
    public sealed class Secret
    {
        private readonly string value;

        public Secret(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A secret cannot be empty; absence is a missing member, never an empty credential.", nameof(value));
            this.value = value;
        }

        /// <summary>Hands over the material. Call at the point of use only; never store the result.</summary>
        public string Reveal() => value;

        public override string ToString() => "[secret]";
    }
}
