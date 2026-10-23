#nullable enable
using System;

namespace EastFive.Configuration
{
    /// <summary>
    /// WHICH secret serves a member, distinct from the secret itself so a reference can never be
    /// passed where a credential is expected. <see cref="Name"/> is the secret's name in the
    /// secret store (a vault secret name, <c>[A-Za-z0-9-]</c>), read directly by a secret client
    /// rather than back through <c>IConfiguration</c>, so no dot/dash key mapping ever applies.
    /// </summary>
    public sealed record SecretReference(string Name, string Purpose)
    {
        public override string ToString() => $"{Purpose} -> [{Name}]";
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
