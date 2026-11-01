#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace EastFive.Configuration
{
    /// <summary>
    /// Implemented by an attribute on a configuration MEMBER: the member's value can be generated
    /// (a random secret, a key pair, an id). Generation is READ-ONLY — the result is a draft an
    /// editor shows and the user saves through the ordinary write paths. A member declares at most
    /// one generator; which members may have one is the configuration type's decision.
    /// </summary>
    /// <remarks>
    /// The context carries draft SECRETS. Never log it and never put any of it in an exception
    /// message.
    /// </remarks>
    public interface IGenerateConfigurationValue
    {
        /// <summary>What the generator produces, for an editor's tooltip (e.g. "Random 32 bytes, base64url").</summary>
        string Label { get; }

        Task<ConfigurationGenerationResult> GenerateAsync(ConfigurationGenerationContext context);
    }

    /// <summary>
    /// What a generator sees: the type and member being generated, the type's current draft values
    /// by member PROPERTY name (secrets included; the caller decides what to pass), the environment,
    /// the account or instance of a keyed type, and injectable random and clock sources.
    /// </summary>
    public sealed record ConfigurationGenerationContext(
        Type ConfigurationType,
        ConfigurationMember Member,
        IReadOnlyDictionary<string, string?> Draft,
        string? Environment = null,
        string? Instance = null)
    {
        private static readonly RandomNumberGenerator SystemRandom = RandomNumberGenerator.Create();

        /// <summary>The random source every built-in draws from; tests substitute a deterministic one.
        /// Key-pair generation uses the platform's own source and is never deterministic.</summary>
        public RandomNumberGenerator Random { get; init; } = SystemRandom;

        /// <summary>The clock dated key ids are stamped from.</summary>
        public TimeProvider Clock { get; init; } = TimeProvider.System;

        /// <summary>The draft value of one member (by property name); null when blank or absent.</summary>
        public string? DraftValue(string member)
            => Draft.FirstOrDefault(entry => string.Equals(entry.Key, member, StringComparison.OrdinalIgnoreCase)).Value
                is { } value && !string.IsNullOrWhiteSpace(value) ? value : null;

        public byte[] RandomBytes(int count)
        {
            var bytes = new byte[count];
            Random.GetBytes(bytes);
            return bytes;
        }

        /// <summary>A dated key id: <c>yyyy-MM-dd-xxxxxxxx</c> (UTC date, eight random hex digits).</summary>
        public string NewKeyId()
            => $"{Clock.GetUtcNow():yyyy-MM-dd}-{Convert.ToHexString(RandomBytes(4)).ToLowerInvariant()}";

        /// <summary>Names the type and member only; never the draft.</summary>
        public override string ToString() => $"{ConfigurationType.Name}.{Member.Property.Name}";
    }

    /// <summary>How an editor renders an output.</summary>
    public enum ConfigurationOutputKind
    {
        Text,
        Pem,
        Json,
    }

    /// <summary>A display-only result (a public key, a JWKS entry, a fingerprint): never stored in configuration.</summary>
    public sealed record ConfigurationGenerationOutput(string Label, string Value, ConfigurationOutputKind Kind = ConfigurationOutputKind.Text)
    {
        public override string ToString() => $"{Label} ({Kind})";
    }

    /// <summary>A step the user takes outside configuration, e.g. "Upload this JWKS at the vendor's portal, then save."</summary>
    public sealed record ConfigurationGenerationInstruction(string Text, string? Link = null);

    /// <summary>
    /// What a generator returns: <see cref="Values"/> for members of the SAME type (by property name;
    /// they fill drafts), <see cref="Outputs"/> to show and copy, and <see cref="Instructions"/>.
    /// </summary>
    public sealed record ConfigurationGenerationResult
    {
        public static ConfigurationGenerationResult Empty { get; } = new();

        public IReadOnlyDictionary<string, string> Values { get; init; } = new Dictionary<string, string>();

        public IReadOnlyList<ConfigurationGenerationOutput> Outputs { get; init; } = Array.Empty<ConfigurationGenerationOutput>();

        public IReadOnlyList<ConfigurationGenerationInstruction> Instructions { get; init; } = Array.Empty<ConfigurationGenerationInstruction>();

        public static ConfigurationGenerationResult Of(string member, string value) => Empty.WithValue(member, value);

        public ConfigurationGenerationResult WithValue(string member, string value)
            => this with { Values = new Dictionary<string, string>(Values, StringComparer.Ordinal) { [member] = value } };

        public ConfigurationGenerationResult WithOutput(string label, string value, ConfigurationOutputKind kind = ConfigurationOutputKind.Text)
            => this with { Outputs = Outputs.Append(new ConfigurationGenerationOutput(label, value, kind)).ToArray() };

        public ConfigurationGenerationResult WithInstruction(string text, string? link = null)
            => this with { Instructions = Instructions.Append(new ConfigurationGenerationInstruction(text, link)).ToArray() };

        /// <summary>Names the members and outputs only; never a value.</summary>
        public override string ToString()
            => $"values [{string.Join(", ", Values.Keys)}], outputs [{string.Join(", ", Outputs.Select(output => output.Label))}]";
    }

    /// <summary>Runs a member's generator and validates its result against the type's declaration.</summary>
    public static class ConfigurationGeneration
    {
        /// <param name="onGenerated">The result, its value keys normalized to property names.</param>
        /// <param name="onNoGenerator">The member declares no generator.</param>
        /// <param name="onRefused">The result names a member the type does not declare, or a value the
        /// member cannot hold. The reason names members, never values.</param>
        public static async Task<TResult> GenerateAsync<TResult>(ConfigurationGenerationContext context,
            Func<ConfigurationGenerationResult, TResult> onGenerated,
            Func<string, TResult> onNoGenerator,
            Func<string, TResult> onRefused)
        {
            var where = $"{context.ConfigurationType.Name}.{context.Member.Property.Name}";
            if (context.Member.Generator is not { } generator)
                return onNoGenerator($"{where} declares no generator.");

            var result = await generator.GenerateAsync(context);
            var members = ConfigurationMembers.For(context.ConfigurationType).Members;
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (name, value) in result.Values)
            {
                var member = members.FirstOrDefault(candidate => string.Equals(candidate.Property.Name, name, StringComparison.OrdinalIgnoreCase));
                if (member == null)
                    return onRefused($"{where}'s generator returned '{name}', which {context.ConfigurationType.Name} does not declare; a generator fills members of its own type only.");
                if (value == null || !ConfigurationValueConverter.TryConvert(member.Property.PropertyType, value.Trim(), out _))
                    return onRefused($"{where}'s generator returned a value {context.ConfigurationType.Name}.{member.Property.Name} cannot hold ({member.Property.PropertyType.Name}).");
                values[member.Property.Name] = value;
            }
            return onGenerated(result with { Values = values });
        }
    }
}
