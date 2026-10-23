#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EastFive.Configuration
{
    /// <summary>
    /// Typed configuration resolved ONCE by the host and cached, for synchronous call sites that
    /// cannot await (attributes, static helpers, the storage driver factory) — the explicit
    /// alternative to blocking on <c>LoadAsync(...).Result</c>. The host awaits
    /// <see cref="LoadAsync(IEnumerable{Type})"/> after initializing configuration and
    /// <see cref="SecretStore"/>; sync code calls <see cref="Get{TConfig, TResult}"/>, which never
    /// loads and never blocks. <see cref="RefreshAsync"/> re-reads everything loaded so far (a host
    /// may schedule it to pick up rotated secrets).
    /// </summary>
    public static class StartupConfiguration
    {
        private sealed record Outcome(IProvideConfiguration? Configuration, ConfigIssue? Issue);

        private static readonly ConcurrentDictionary<Type, Outcome> loaded = new();

        /// <summary>Loads <typeparamref name="TConfig"/> from the ambient values and
        /// <see cref="SecretStore.Current"/> and caches the outcome; returns the issue, if any.</summary>
        public static Task<ConfigIssue?> LoadAsync<TConfig>() where TConfig : IProvideConfiguration, new()
            => LoadAsync(typeof(TConfig));

        public static async Task<ConfigIssue[]> LoadAsync(IEnumerable<Type> configurationTypes)
        {
            var issues = await Task.WhenAll(configurationTypes.Distinct().Select(LoadAsync));
            return issues.OfType<ConfigIssue>().ToArray();
        }

        public static async Task<ConfigIssue?> LoadAsync(Type configurationType)
        {
            if (!typeof(IProvideConfiguration).IsAssignableFrom(configurationType) || configurationType.GetConstructor(Type.EmptyTypes) == null)
                throw new ArgumentException($"{configurationType.Name} is not a configuration type with a parameterless constructor.", nameof(configurationType));
            var outcome = await ConfigurationLoader.LoadAsync<IProvideConfiguration, Outcome>(
                () => (IProvideConfiguration)Activator.CreateInstance(configurationType)!,
                ConfigurationLoader.ReadAmbient, SecretStore.Current,
                config => new Outcome(config, null),
                (issue, _) => Task.FromResult(new Outcome(null, issue)),
                issue => new Outcome(null, issue),
                issue => new Outcome(null, issue));
            loaded[configurationType] = outcome;
            return outcome.Issue;
        }

        /// <summary>Re-reads every type loaded so far.</summary>
        public static Task<ConfigIssue[]> RefreshAsync() => LoadAsync(loaded.Keys.ToArray());

        /// <summary>Re-reads every <paramref name="interval"/> until <paramref name="stop"/>; failures keep the
        /// previous outcome rather than replacing a working configuration with an outage.</summary>
        public static async Task RefreshEveryAsync(TimeSpan interval, CancellationToken stop)
        {
            using var timer = new PeriodicTimer(interval);
            while (await timer.WaitForNextTickAsync(stop).ConfigureAwait(false))
            {
                foreach (var type in loaded.Keys.ToArray())
                {
                    var previous = loaded[type];
                    var issue = await LoadAsync(type).ConfigureAwait(false);
                    if (issue?.Category == ConfigIssueCategory.SourceFailure && previous.Configuration != null)
                        loaded[type] = previous;
                }
            }
        }

        /// <summary>Hosts and tests that already hold a configuration (a test fixture, a
        /// configuration loaded by other means) install it directly.</summary>
        public static void Set<TConfig>(TConfig configuration) where TConfig : IProvideConfiguration
            => loaded[typeof(TConfig)] = new Outcome(configuration, null);

        public static void Forget<TConfig>() where TConfig : IProvideConfiguration
            => loaded.TryRemove(typeof(TConfig), out _);

        public static bool IsLoaded<TConfig>() where TConfig : IProvideConfiguration
            => loaded.ContainsKey(typeof(TConfig));

        /// <summary>The configuration loaded at startup. Never loads: a type the host did not load
        /// reaches <paramref name="onUnavailable"/> with a <see cref="ConfigIssueCategory.NotLoaded"/> issue.</summary>
        public static TResult Get<TConfig, TResult>(Func<TConfig, TResult> onLoaded, Func<ConfigIssue, TResult> onUnavailable)
            where TConfig : IProvideConfiguration
            => Get(typeof(TConfig), config => onLoaded((TConfig)config), onUnavailable);

        public static TResult Get<TResult>(Type configurationType, Func<IProvideConfiguration, TResult> onLoaded, Func<ConfigIssue, TResult> onUnavailable)
            => loaded.TryGetValue(configurationType, out var outcome)
                ? outcome.Configuration is { } config ? onLoaded(config) : onUnavailable(outcome.Issue!)
                : onUnavailable(new ConfigIssue(configurationType, configurationType.Name, ConfigurationMemberKind.Required, ConfigIssueCategory.NotLoaded));

        /// <summary>
        /// Loads every concrete configuration type with a parameterless constructor in
        /// <paramref name="assemblies"/> (the host's and the framework's). Unconfigured types are
        /// recorded as issues, not errors: the feature that needs one reports it when used.
        /// </summary>
        public static Task<ConfigIssue[]> LoadAsync(IEnumerable<System.Reflection.Assembly> assemblies)
            => LoadAsync(ConfigurationCatalog.For(assemblies)
                .Select(description => description.Type)
                .Where(type => type.GetConstructor(Type.EmptyTypes) != null));
    }
}
