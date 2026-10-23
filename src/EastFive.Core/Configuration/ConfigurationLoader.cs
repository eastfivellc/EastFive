#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

using EastFive.Web.Configuration;

namespace EastFive.Configuration
{
    /// <summary>How one member stood when a configuration was inspected (never its value).</summary>
    public enum ConfigurationMemberPresence
    {
        /// <summary>Answered by its source.</summary>
        Present,
        /// <summary>Absent and Required or Secret.</summary>
        Missing,
        /// <summary>Absent and Optional: a legitimate partial mode.</summary>
        Absent,
        /// <summary>Absent and Defaulted: the declared default applies.</summary>
        Defaulted,
        /// <summary>Present but not convertible, or (for a secret) not nameable because the
        /// account it is scoped by is invalid or missing.</summary>
        Invalid,
        /// <summary>The source could not answer.</summary>
        Failed,
        /// <summary>A secret that cannot be named yet because the account member it is scoped by
        /// is missing or invalid (that member is the gap, not this one).</summary>
        Unnamed,
    }

    /// <summary>
    /// One row of a doctor's view: the member, the key (values) or generated name (secrets) it is
    /// read by, how it stood, and — for a secret that was found or resolved — where from.
    /// Carries no values.
    /// </summary>
    public sealed record ConfigurationMemberStatus(
        ConfigurationMember Member,
        string Name,
        ConfigurationMemberPresence Presence,
        string? Source)
    {
        public bool IsGap => Presence is ConfigurationMemberPresence.Missing
            or ConfigurationMemberPresence.Invalid or ConfigurationMemberPresence.Failed;
    }

    /// <summary>
    /// Loads one coherent, typed configuration and hands the caller the <c>TResult</c> pattern for
    /// every outcome. VALUES come from <c>readValue</c> (default: the ambient EastFive configuration,
    /// i.e. files + environment). SECRET members come from an <see cref="ISecretStore"/> (default:
    /// <see cref="SecretStore.Current"/>) by the name <see cref="IProvideConfiguration.SecretReference"/>
    /// answers — never from the values store, which is why loading a type with secret members is
    /// asynchronous.
    /// </summary>
    /// <remarks>
    /// Missing semantics, per member kind:
    /// <list type="bullet">
    /// <item><c>Required</c> / <c>Secret</c> absent: <c>onMissing(issue, supply)</c>. The caller
    /// returns its own result (no silent continue) OR calls <c>supply(text)</c>, and the load
    /// resumes with that text converted and validated like a read value. A further missing
    /// member reaches <c>onMissing</c> again, in declaration order.</item>
    /// <item><c>Optional</c> absent: the member is <c>null</c> in the typed config.</item>
    /// <item><c>Defaulted</c> absent: the declared default is applied, converted and validated.</item>
    /// <item>A source THROWS or the secret store reports a failure (vault denied, throttled, timed
    /// out, configuration never initialized): <c>onSourceFailure</c>, never <c>onMissing</c>.</item>
    /// <item>A read, defaulted or supplied value fails conversion, an account cannot name a secret,
    /// or <see cref="IProvideConfiguration.Validate"/> names a member: <c>onInvalid</c>.</item>
    /// </list>
    /// A type implementing <see cref="IResolveConfigurationMember"/> answers its own members first.
    /// </remarks>
    public static class ConfigurationLoader
    {
        /// <summary>The default VALUES source: the ambient EastFive static configuration.
        /// Unset reads as <c>null</c>; an uninitialized configuration throws, which the loader
        /// reports as <see cref="ConfigIssueCategory.SourceFailure"/>.</summary>
        public static string? ReadAmbient(string key)
            => key.ConfigurationString(value => value, _ => (string?)null);

        #region Asynchronous: values + secret store

        public static Task<TResult> LoadAsync<TConfig, TResult>(
            Func<TConfig, TResult> onConfigured,
            Func<ConfigIssue, TResult> onMissing,
            Func<ConfigIssue, TResult> onInvalid,
            Func<ConfigIssue, TResult> onSourceFailure)
            where TConfig : IProvideConfiguration, new()
            => LoadAsync(ReadAmbient, SecretStore.Current, onConfigured, onMissing, onInvalid, onSourceFailure);

        public static Task<TResult> LoadAsync<TConfig, TResult>(
            Func<string, string?> readValue,
            ISecretStore secrets,
            Func<TConfig, TResult> onConfigured,
            Func<ConfigIssue, TResult> onMissing,
            Func<ConfigIssue, TResult> onInvalid,
            Func<ConfigIssue, TResult> onSourceFailure)
            where TConfig : IProvideConfiguration, new()
            => LoadAsync(() => new TConfig(), readValue, secrets, onConfigured,
                (issue, _) => Task.FromResult(onMissing(issue)), onInvalid, onSourceFailure);

        /// <summary>The full form: a constructor (account or tenant inputs), explicit sources and a
        /// resumable <c>onMissing</c>. <paramref name="construct"/> runs once per attempt, so a
        /// resumed load starts from a fresh instance.</summary>
        public static Task<TResult> LoadAsync<TConfig, TResult>(
            Func<TConfig> construct,
            Func<string, string?> readValue,
            ISecretStore secrets,
            Func<TConfig, TResult> onConfigured,
            Func<ConfigIssue, Func<string, Task<TResult>>, Task<TResult>> onMissing,
            Func<ConfigIssue, TResult> onInvalid,
            Func<ConfigIssue, TResult> onSourceFailure)
            where TConfig : IProvideConfiguration
            => Attempt(construct, readValue, secrets, ImmutableDictionary<string, string>.Empty.WithComparers(StringComparer.Ordinal),
                onConfigured, onMissing, onInvalid, onSourceFailure);

        /// <summary>
        /// The doctor's view: every member with the key or generated secret name it is read by
        /// and how it stood, in declaration order, without running validation. Unlike a load it
        /// does not stop at the first gap. Members that ARE present are populated as it goes, so
        /// a secret name derived from the account member is the one a load would read.
        /// </summary>
        public static async Task<ConfigurationMemberStatus[]> InspectAsync(
            Func<IProvideConfiguration> construct, Func<string, string?> readValue, ISecretStore secrets)
        {
            var config = construct();
            var declared = ConfigurationMembers.For(config.GetType());
            var statuses = new List<ConfigurationMemberStatus>();
            foreach (var member in declared.Members)
            {
                var status = await InspectMember(config, declared, member, readValue, secrets).ConfigureAwait(false);
                // An account that reads fine but cannot be a secret-name segment is invalid HERE.
                if (member.IsAccount && status.Presence == ConfigurationMemberPresence.Present && declared.HasInvalidAccount(config))
                    status = status with { Presence = ConfigurationMemberPresence.Invalid };
                statuses.Add(status);
            }
            return statuses.ToArray();
        }

        public static Task<ConfigurationMemberStatus[]> InspectAsync<TConfig>(Func<string, string?> readValue, ISecretStore secrets)
            where TConfig : IProvideConfiguration, new()
            => InspectAsync(() => new TConfig(), readValue, secrets);

        /// <summary>The names (values keys and secret names) of every gap <see cref="InspectAsync{TConfig}"/>
        /// finds: Required or Secret members that are missing, invalid or unreadable.</summary>
        public static async Task<string[]> MissingAsync<TConfig>(Func<string, string?> readValue, ISecretStore secrets)
            where TConfig : IProvideConfiguration, new()
            => (await InspectAsync<TConfig>(readValue, secrets).ConfigureAwait(false))
                .Where(status => status.IsGap)
                .Select(status => status.Name)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

        #endregion

        #region Synchronous: values-only types, and explicit in-memory drafts

        /// <summary>
        /// Values-only types. A type with secret members THROWS here: its secrets live in the
        /// secret store, which is asynchronous — use <c>LoadAsync</c>, or <c>LoadDraft</c> for an
        /// explicit in-memory draft.
        /// </summary>
        public static TResult Load<TConfig, TResult>(
            Func<string, string?> readValue,
            Func<TConfig, TResult> onConfigured,
            Func<ConfigIssue, Func<string, TResult>, TResult> onMissing,
            Func<ConfigIssue, TResult> onInvalid,
            Func<ConfigIssue, TResult> onSourceFailure)
            where TConfig : IProvideConfiguration, new()
            => Load(() => new TConfig(), readValue, onConfigured, onMissing, onInvalid, onSourceFailure);

        public static TResult Load<TConfig, TResult>(
            Func<TConfig> construct,
            Func<string, string?> readValue,
            Func<TConfig, TResult> onConfigured,
            Func<ConfigIssue, Func<string, TResult>, TResult> onMissing,
            Func<ConfigIssue, TResult> onInvalid,
            Func<ConfigIssue, TResult> onSourceFailure)
            where TConfig : IProvideConfiguration
        {
            var instance = construct();
            if (ConfigurationMembers.For(instance.GetType()).HasSecrets)
                throw new InvalidOperationException(
                    $"{instance.GetType().Name} has secret members; they are read from the secret store, so load it with {nameof(LoadAsync)}.");
            return LoadDraft(construct, readValue, _ => null, onConfigured, onMissing, onInvalid, onSourceFailure);
        }

        public static TResult Load<TConfig, TResult>(
            Func<TConfig, TResult> onConfigured,
            Func<ConfigIssue, Func<string, TResult>, TResult> onMissing,
            Func<ConfigIssue, TResult> onInvalid,
            Func<ConfigIssue, TResult> onSourceFailure)
            where TConfig : IProvideConfiguration, new()
            => Load(ReadAmbient, onConfigured, onMissing, onInvalid, onSourceFailure);

        /// <summary>
        /// Loads from an explicit, IN-MEMORY draft: values by key and secrets by name, both handed
        /// in by the caller (an editor previewing what it is about to write, a probe, a test).
        /// Never the ambient store and never a vault, which is the only reason it may be
        /// synchronous.
        /// </summary>
        public static TResult LoadDraft<TConfig, TResult>(
            Func<TConfig> construct,
            Func<string, string?> readValue,
            Func<string, string?> readSecret,
            Func<TConfig, TResult> onConfigured,
            Func<ConfigIssue, Func<string, TResult>, TResult> onMissing,
            Func<ConfigIssue, TResult> onInvalid,
            Func<ConfigIssue, TResult> onSourceFailure)
            where TConfig : IProvideConfiguration
            => Completed(LoadAsync(construct, readValue, new DraftSecrets(readSecret),
                onConfigured,
                (issue, supply) => Task.FromResult(onMissing(issue, text => Completed(supply(text)))),
                onInvalid, onSourceFailure));

        public static TResult LoadDraft<TConfig, TResult>(
            Func<string, string?> readValue,
            Func<string, string?> readSecret,
            Func<TConfig, TResult> onConfigured,
            Func<ConfigIssue, Func<string, TResult>, TResult> onMissing,
            Func<ConfigIssue, TResult> onInvalid,
            Func<ConfigIssue, TResult> onSourceFailure)
            where TConfig : IProvideConfiguration, new()
            => LoadDraft(() => new TConfig(), readValue, readSecret, onConfigured, onMissing, onInvalid, onSourceFailure);

        /// <summary><see cref="MissingAsync{TConfig}"/> over an in-memory draft.</summary>
        public static string[] MissingDraft<TConfig>(Func<string, string?> readValue, Func<string, string?> readSecret)
            where TConfig : IProvideConfiguration, new()
            => Completed(MissingAsync<TConfig>(readValue, new DraftSecrets(readSecret)));

        /// <summary>A draft's secrets: synchronous by construction, so every task it returns is
        /// already complete and <see cref="Completed{T}"/> never blocks.</summary>
        private sealed class DraftSecrets : ISecretStore
        {
            private readonly Func<string, string?> readSecret;
            public DraftSecrets(Func<string, string?> readSecret) => this.readSecret = readSecret;

            public IReadOnlyList<SecretSource> Sources { get; } = new[] { new SecretSource(SecretSource.Draft, "draft") };

            public Task<TResult> GetSecretAsync<TResult>(string name, Func<Secret, SecretSource, TResult> onFound,
                Func<TResult> onMissing, Func<string, TResult> onFailure)
            {
                string? text;
                try
                {
                    text = readSecret(name);
                }
                catch (Exception)
                {
                    return Task.FromResult(onFailure($"The draft could not be read for [{name}]."));
                }
                return Task.FromResult(string.IsNullOrWhiteSpace(text) ? onMissing() : onFound(new Secret(text.Trim()), Sources[0]));
            }
        }

        private static T Completed<T>(Task<T> task)
            => task.IsCompleted
                ? task.GetAwaiter().GetResult()
                : throw new InvalidOperationException("A draft load must complete synchronously; a draft source returned a pending task.");

        #endregion

        private static async Task<TResult> Attempt<TConfig, TResult>(
            Func<TConfig> construct,
            Func<string, string?> readValue,
            ISecretStore secrets,
            ImmutableDictionary<string, string> supplied,
            Func<TConfig, TResult> onConfigured,
            Func<ConfigIssue, Func<string, Task<TResult>>, Task<TResult>> onMissing,
            Func<ConfigIssue, TResult> onInvalid,
            Func<ConfigIssue, TResult> onSourceFailure)
            where TConfig : IProvideConfiguration
        {
            var config = construct();
            var configurationType = config.GetType();
            var declared = ConfigurationMembers.For(configurationType);

            foreach (var member in declared.Members)
            {
                // A secret's name embeds the account; an account that cannot be a name segment
                // is the ACCOUNT member's problem, reported before any secret read.
                if (member.IsSecret && declared.HasInvalidAccount(config))
                    return onInvalid(new ConfigIssue(configurationType, declared.AccountMember!.Key!,
                        declared.AccountMember.Kind, ConfigIssueCategory.Invalid));

                var key = NameOf(config, member);
                ConfigIssue Issue(ConfigIssueCategory category) => new(configurationType, key, member.Kind, category);

                var read = supplied.TryGetValue(key, out var suppliedText)
                    // A caller that supplies blank has not mitigated anything; report it rather than loop.
                    ? string.IsNullOrWhiteSpace(suppliedText)
                        ? Read.Invalid(Issue(ConfigIssueCategory.Invalid))
                        : Read.Answered(suppliedText)
                    : await ReadMember(config, member, key, readValue, secrets, Issue).ConfigureAwait(false);

                if (read.Issue is { } issue)
                    return issue.Category == ConfigIssueCategory.SourceFailure ? onSourceFailure(issue) : onInvalid(issue);

                var text = read.Text;
                if (string.IsNullOrWhiteSpace(text))
                {
                    switch (member.Kind)
                    {
                        case ConfigurationMemberKind.Optional:
                            continue;
                        case ConfigurationMemberKind.Defaulted:
                            text = member.Default;
                            break;
                        default:
                            return await onMissing(Issue(ConfigIssueCategory.Missing),
                                value => Attempt(construct, readValue, secrets, supplied.SetItem(key, value),
                                    onConfigured, onMissing, onInvalid, onSourceFailure)).ConfigureAwait(false);
                    }
                }

                if (!ConfigurationValueConverter.TryConvert(member.Property.PropertyType, text!.Trim(), out var converted))
                    return onInvalid(Issue(ConfigIssueCategory.Invalid));
                member.Property.SetValue(config, converted);
            }

            var invalidKey = config.Validate();
            if (invalidKey == null)
                return onConfigured(config);

            var invalidKind = declared.Members
                .Where(member => string.Equals(NameOf(config, member), invalidKey, StringComparison.Ordinal))
                .Select(member => member.Kind)
                .DefaultIfEmpty(ConfigurationMemberKind.Required)
                .First();
            return onInvalid(new ConfigIssue(configurationType, invalidKey, invalidKind, ConfigIssueCategory.Invalid));
        }

        /// <summary>The key a values member is read by; the secret NAME a secret member is read by.</summary>
        private static string NameOf(IProvideConfiguration config, ConfigurationMember member)
            => member.IsSecret ? config.SecretReference(member.Purpose!).Name : member.KeyFor(config)!;

        private readonly record struct Read(string? Text, string? Source, ConfigIssue? Issue)
        {
            public static Read Answered(string? text, string? source = null) => new(text, source, null);
            public static Read Invalid(ConfigIssue issue) => new(null, null, issue);
        }

        /// <summary>One member from its sources: the type's own resolution first, then the values
        /// store (values) or the secret store (secrets). Never throws.</summary>
        private static async Task<Read> ReadMember(IProvideConfiguration config, ConfigurationMember member, string name,
            Func<string, string?> readValue, ISecretStore secrets, Func<ConfigIssueCategory, ConfigIssue> issue)
        {
            if (config is IResolveConfigurationMember resolver)
            {
                var resolved = resolver.ResolveMember(member, readValue,
                    (text, source) => (Read?)Read.Answered(text, source),
                    () => null,
                    problem => Read.Invalid(problem));
                if (resolved is { } answer)
                    return answer;
            }

            if (!member.IsSecret)
            {
                // The values source is the only thing that may throw here (configuration never
                // initialized, a provider failing); that is a SourceFailure, not a missing member.
                try
                {
                    var text = readValue(name);
                    if (string.IsNullOrWhiteSpace(text) && member.FallbackKeyFor(config) is { } fallback)
                    {
                        text = readValue(fallback);
                        return Read.Answered(text, string.IsNullOrWhiteSpace(text) ? "values" : $"values ({fallback})");
                    }
                    return Read.Answered(text, "values");
                }
                catch (Exception)
                {
                    return Read.Invalid(issue(ConfigIssueCategory.SourceFailure));
                }
            }

            return await secrets.GetSecretAsync(name,
                (secret, source) => Read.Answered(secret.Reveal(), source.ToString()),
                () => Read.Answered(null),
                _ => Read.Invalid(issue(ConfigIssueCategory.SourceFailure))).ConfigureAwait(false);
        }

        private static async Task<ConfigurationMemberStatus> InspectMember(IProvideConfiguration config, ConfigurationMembers declared,
            ConfigurationMember member, Func<string, string?> readValue, ISecretStore secrets)
        {
            if (member.IsSecret && declared.HasInvalidAccount(config))
                return new ConfigurationMemberStatus(member, $"{declared.TypeName}-(account)-{member.Property.Name}",
                    ConfigurationMemberPresence.Unnamed, null);

            var name = NameOf(config, member);
            var read = await ReadMember(config, member, name, readValue, secrets,
                category => new ConfigIssue(config.GetType(), name, member.Kind, category)).ConfigureAwait(false);
            if (read.Issue is { } issue)
                return new ConfigurationMemberStatus(member, name,
                    issue.Category == ConfigIssueCategory.SourceFailure ? ConfigurationMemberPresence.Failed : ConfigurationMemberPresence.Invalid,
                    null);
            if (string.IsNullOrWhiteSpace(read.Text))
                return new ConfigurationMemberStatus(member, name, member.Kind switch
                {
                    ConfigurationMemberKind.Optional => ConfigurationMemberPresence.Absent,
                    ConfigurationMemberKind.Defaulted => ConfigurationMemberPresence.Defaulted,
                    _ => ConfigurationMemberPresence.Missing,
                }, null);
            if (!ConfigurationValueConverter.TryConvert(member.Property.PropertyType, read.Text.Trim(), out var converted))
                return new ConfigurationMemberStatus(member, name, ConfigurationMemberPresence.Invalid, read.Source);
            if (!member.IsSecret)
                member.Property.SetValue(config, converted);
            return new ConfigurationMemberStatus(member, name, ConfigurationMemberPresence.Present, read.Source);
        }
    }
}
