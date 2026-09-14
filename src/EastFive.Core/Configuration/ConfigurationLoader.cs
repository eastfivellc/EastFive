#nullable enable
using System;
using System.Collections.Immutable;
using System.Linq;

using EastFive.Web.Configuration;

namespace EastFive.Configuration
{
    /// <summary>
    /// Loads one coherent, typed configuration from a read source instead of two-to-five
    /// independent string reads, and hands the caller the <c>TResult</c> pattern for every
    /// outcome. <paramref name="readSetting"/> IS the source: the default wraps the ambient
    /// EastFive static configuration (which the whole-vault provider feeds), a describer passes
    /// its lowered dictionary, a test passes an in-memory one. A dictionary source never touches
    /// ambient configuration, which is what keeps the empty-dictionary discovery probe honest.
    /// </summary>
    /// <remarks>
    /// Missing semantics, per member kind:
    /// <list type="bullet">
    /// <item><c>Required</c> / <c>Secret</c> absent: <c>onMissing(issue, supply)</c>. The caller
    /// returns its own result (no silent continue) OR calls <c>supply(text)</c>, and the load
    /// resumes with that text converted and validated like a read value. A further missing
    /// member reaches <c>onMissing</c> again, in declaration order. Account identity from
    /// validated JWT claims is a constructor INPUT to the configuration type, not a supplied
    /// setting, and claims never carry secret material.</item>
    /// <item><c>Optional</c> absent: the member is <c>null</c> in the typed config.</item>
    /// <item><c>Defaulted</c> absent: the declared default is applied, converted and validated.</item>
    /// <item>The source THROWS (vault denied, throttled, timed out, configuration never
    /// initialized): <c>onSourceFailure</c>, never <c>onMissing</c>.</item>
    /// <item>A read, defaulted or supplied value fails conversion, or
    /// <see cref="IProvideConfiguration.Validate"/> names a member: <c>onInvalid</c>.</item>
    /// </list>
    /// <c>TResult</c> may be a <c>Task&lt;T&gt;</c>; the loader itself is synchronous because
    /// secret references currently resolve to global keys the configuration provider already
    /// holds. Per-account vault resolution will need an async seam, not a blocking call here.
    /// </remarks>
    public static class ConfigurationLoader
    {
        /// <summary>The default read source: the ambient EastFive static configuration.
        /// Unset reads as <c>null</c>; an uninitialized configuration throws, which the loader
        /// reports as <see cref="ConfigIssueCategory.SourceFailure"/>.</summary>
        public static string? ReadAmbient(string key)
            => key.ConfigurationString(value => value, _ => (string?)null);

        public static TResult Load<TConfig, TResult>(
            Func<TConfig, TResult> onConfigured,
            Func<ConfigIssue, Func<string, TResult>, TResult> onMissing,
            Func<ConfigIssue, TResult> onInvalid,
            Func<ConfigIssue, TResult> onSourceFailure)
            where TConfig : IProvideConfiguration, new()
            => Load(ReadAmbient, onConfigured, onMissing, onInvalid, onSourceFailure);

        public static TResult Load<TConfig, TResult>(
            Func<string, string?> readSetting,
            Func<TConfig, TResult> onConfigured,
            Func<ConfigIssue, Func<string, TResult>, TResult> onMissing,
            Func<ConfigIssue, TResult> onInvalid,
            Func<ConfigIssue, TResult> onSourceFailure)
            where TConfig : IProvideConfiguration, new()
            => Load(() => new TConfig(), readSetting, onConfigured, onMissing, onInvalid, onSourceFailure);

        /// <summary>For configuration types whose constructor takes inputs (account or tenant
        /// identity that selects which secret to read). <paramref name="construct"/> is invoked
        /// once per attempt, so a resumed load starts from a fresh instance.</summary>
        public static TResult Load<TConfig, TResult>(
            Func<TConfig> construct,
            Func<string, string?> readSetting,
            Func<TConfig, TResult> onConfigured,
            Func<ConfigIssue, Func<string, TResult>, TResult> onMissing,
            Func<ConfigIssue, TResult> onInvalid,
            Func<ConfigIssue, TResult> onSourceFailure)
            where TConfig : IProvideConfiguration
            => Attempt(construct, readSetting, ImmutableDictionary<string, string>.Empty.WithComparers(StringComparer.Ordinal),
                onConfigured, onMissing, onInvalid, onSourceFailure);

        /// <summary>
        /// The doctor's view: every Required or Secret key the source does not answer, in
        /// declaration order, without constructing a configuration. <c>Load</c> stops at the
        /// FIRST missing member (the caller may supply it); a checklist wants all of them at once.
        /// A throwing source propagates, since a checklist over an unreachable source is not a
        /// checklist.
        /// </summary>
        public static string[] Missing<TConfig>(Func<string, string?> readSetting)
            where TConfig : IProvideConfiguration, new()
        {
            var config = new TConfig();
            return ConfigurationMembers.For<TConfig>().Members
                .Where(member => member.Kind is ConfigurationMemberKind.Required or ConfigurationMemberKind.Secret)
                .Select(member => member.IsSecret ? config.SecretReference(member.Purpose!).Key : member.Key)
                .Where(key => string.IsNullOrWhiteSpace(readSetting(key)))
                .ToArray();
        }

        private static TResult Attempt<TConfig, TResult>(
            Func<TConfig> construct,
            Func<string, string?> readSetting,
            ImmutableDictionary<string, string> supplied,
            Func<TConfig, TResult> onConfigured,
            Func<ConfigIssue, Func<string, TResult>, TResult> onMissing,
            Func<ConfigIssue, TResult> onInvalid,
            Func<ConfigIssue, TResult> onSourceFailure)
            where TConfig : IProvideConfiguration
        {
            var config = construct();
            var configurationType = config.GetType();
            var declared = ConfigurationMembers.For(configurationType);

            foreach (var member in declared.Members)
            {
                var key = member.IsSecret ? config.SecretReference(member.Purpose!).Key : member.Key;
                ConfigIssue Issue(ConfigIssueCategory category) => new(configurationType, key, member.Kind, category);

                string? text;
                if (supplied.TryGetValue(key, out var suppliedText))
                {
                    // A caller that supplies blank has not mitigated anything; report it rather than loop.
                    if (string.IsNullOrWhiteSpace(suppliedText))
                        return onInvalid(Issue(ConfigIssueCategory.Invalid));
                    text = suppliedText;
                }
                else
                {
                    // The source is the only thing that may throw here (vault denied/throttled/timed out,
                    // configuration never initialized); that is a SourceFailure outcome, not a missing member.
                    try
                    {
                        text = readSetting(key);
                    }
                    catch (Exception)
                    {
                        return onSourceFailure(Issue(ConfigIssueCategory.SourceFailure));
                    }

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
                                return onMissing(Issue(ConfigIssueCategory.Missing),
                                    value => Attempt(construct, readSetting, supplied.SetItem(key, value),
                                        onConfigured, onMissing, onInvalid, onSourceFailure));
                        }
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
                .Where(member => string.Equals(member.Key, invalidKey, StringComparison.Ordinal))
                .Select(member => member.Kind)
                .DefaultIfEmpty(ConfigurationMemberKind.Required)
                .First();
            return onInvalid(new ConfigIssue(configurationType, invalidKey, invalidKind, ConfigIssueCategory.Invalid));
        }
    }
}
