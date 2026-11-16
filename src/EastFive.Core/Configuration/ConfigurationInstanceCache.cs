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
    /// The instances of KEYED configuration types (<see cref="IProvideConfigurationInstanceKey"/>),
    /// loaded at the point of use and cached by (type, instance key), ignoring the key's case — a
    /// keyed type has no parameterless constructor and its instances are not known at startup, so
    /// the host's startup load never includes it.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><see cref="GetOrLoadAsync"/> loads an instance on first use. Callers that arrive while it
    /// is loading share that one load.</item>
    /// <item><see cref="Get{TConfig, TResult}(string, Func{TConfig, TResult}, Func{ConfigIssue, TResult})"/>
    /// never loads and never blocks: an instance nobody loaded answers
    /// <see cref="ConfigIssueCategory.NotLoaded"/>.</item>
    /// <item>A loaded configuration stays until <see cref="RefreshAsync"/> or <see cref="Forget"/>.
    /// An ISSUE is kept for <see cref="IssueLifetime"/> and then re-read on the next use, so a
    /// secret added to the vault is picked up without a restart and a missing one does not reach
    /// the vault on every request.</item>
    /// <item><see cref="RefreshAsync"/> re-reads every cached instance; a
    /// <see cref="ConfigIssueCategory.SourceFailure"/> never replaces a loaded configuration.</item>
    /// </list>
    /// A host uses the ambient cache, <see cref="StartupConfiguration.Instances"/>; one built over
    /// explicit sources is for an editor, a probe or a test.
    /// </remarks>
    public sealed class ConfigurationInstanceCache
    {
        public static readonly TimeSpan DefaultIssueLifetime = TimeSpan.FromSeconds(60);

        private sealed record Entry(IProvideConfiguration? Configuration, ConfigIssue? Issue, DateTimeOffset At,
            Func<IProvideConfiguration>? Construct);

        private sealed class IdComparer : IEqualityComparer<(Type type, string key)>
        {
            public bool Equals((Type type, string key) x, (Type type, string key) y)
                => x.type == y.type && string.Equals(x.key, y.key, StringComparison.OrdinalIgnoreCase);

            public int GetHashCode((Type type, string key) id)
                => HashCode.Combine(id.type, StringComparer.OrdinalIgnoreCase.GetHashCode(id.key));
        }

        private readonly ConcurrentDictionary<(Type type, string key), Entry> entries = new(new IdComparer());
        private readonly ConcurrentDictionary<(Type type, string key), Lazy<Task<Entry>>> loading = new(new IdComparer());
        private readonly Func<string, string?> readValue;
        private readonly Func<ISecretStore> secrets;
        private readonly Func<DateTimeOffset> clock;
        private long sweptAt;

        /// <summary>How long an issue is served before the next use re-reads it.</summary>
        public TimeSpan IssueLifetime { get; }

        /// <param name="readValue">The values source, by key.</param>
        /// <param name="secrets">The secret store, asked for at each load (a host may install its
        /// store after this cache is built).</param>
        /// <param name="clock">The time issues age by; the system clock when omitted.</param>
        /// <param name="issueLifetime"><see cref="DefaultIssueLifetime"/> when omitted.</param>
        public ConfigurationInstanceCache(Func<string, string?> readValue, Func<ISecretStore> secrets,
            Func<DateTimeOffset>? clock = null, TimeSpan? issueLifetime = null)
        {
            this.readValue = readValue;
            this.secrets = secrets;
            this.clock = clock ?? (() => DateTimeOffset.UtcNow);
            IssueLifetime = issueLifetime ?? DefaultIssueLifetime;
        }

        /// <summary>
        /// The instance <paramref name="construct"/> builds: from the cache, or loaded now (and
        /// cached, whatever the outcome). <paramref name="construct"/> runs to name the instance
        /// and once more per load.
        /// </summary>
        public Task<TResult> GetOrLoadAsync<TConfig, TResult>(Func<TConfig> construct,
            Func<TConfig, TResult> onLoaded, Func<ConfigIssue, TResult> onUnavailable)
            where TConfig : IProvideConfiguration, IProvideConfigurationInstanceKey
            => GetOrLoadUntypedAsync(() => construct(), configuration => onLoaded((TConfig)configuration), onUnavailable);

        /// <summary><see cref="GetOrLoadAsync{TConfig, TResult}"/> for a caller that knows the type
        /// only at run time; <paramref name="construct"/> must build a keyed instance.</summary>
        internal async Task<TResult> GetOrLoadUntypedAsync<TResult>(Func<IProvideConfiguration> construct,
            Func<IProvideConfiguration, TResult> onLoaded, Func<ConfigIssue, TResult> onUnavailable)
        {
            var probe = construct();
            if (probe is not IProvideConfigurationInstanceKey keyed)
                throw new ArgumentException($"{probe.GetType().Name} is not a keyed configuration type.", nameof(construct));
            var id = (probe.GetType(), keyed.InstanceKey ?? string.Empty);
            if (!entries.TryGetValue(id, out var entry) || IsStale(entry))
                entry = await LoadSharedAsync(id, construct).ConfigureAwait(false);
            return entry.Configuration is { } configuration ? onLoaded(configuration) : onUnavailable(entry.Issue!);
        }

        /// <summary>The cached instance. Never loads: an instance that was never loaded reaches
        /// <paramref name="onUnavailable"/> with a <see cref="ConfigIssueCategory.NotLoaded"/> issue.
        /// A cached issue is returned as it stands, however old (once stale it may have been
        /// dropped, and then the instance reads as never loaded).</summary>
        public TResult Get<TConfig, TResult>(string instanceKey, Func<TConfig, TResult> onLoaded, Func<ConfigIssue, TResult> onUnavailable)
            where TConfig : IProvideConfiguration
            => Get(typeof(TConfig), instanceKey, configuration => onLoaded((TConfig)configuration), onUnavailable);

        public TResult Get<TResult>(Type configurationType, string instanceKey,
            Func<IProvideConfiguration, TResult> onLoaded, Func<ConfigIssue, TResult> onUnavailable)
            => entries.TryGetValue((configurationType, instanceKey ?? string.Empty), out var entry)
                ? entry.Configuration is { } configuration ? onLoaded(configuration) : onUnavailable(entry.Issue!)
                : onUnavailable(new ConfigIssue(configurationType, configurationType.Name, ConfigurationMemberKind.Required, ConfigIssueCategory.NotLoaded));

        /// <summary>Hosts and tests that already hold an instance install it directly. It is not
        /// re-read by a refresh (nothing here can rebuild it).</summary>
        public void Set<TConfig>(TConfig configuration)
            where TConfig : IProvideConfiguration, IProvideConfigurationInstanceKey
            => entries[(configuration.GetType(), configuration.InstanceKey ?? string.Empty)] = new Entry(configuration, null, clock(), null);

        /// <summary>Drops one instance (and abandons a load of it in flight); true when it was cached.</summary>
        public bool Forget(Type configurationType, string instanceKey)
        {
            var id = (configurationType, instanceKey ?? string.Empty);
            loading.TryRemove(id, out _);
            return entries.TryRemove(id, out _);
        }

        /// <summary>Whether an outcome — a configuration or an issue — is cached for the instance.</summary>
        public bool IsLoaded(Type configurationType, string instanceKey)
            => entries.ContainsKey((configurationType, instanceKey ?? string.Empty));

        /// <summary>
        /// Re-reads every cached instance (to pick up rotated or newly added secrets) and returns
        /// the issues it met. A <see cref="ConfigIssueCategory.SourceFailure"/> is reported but keeps
        /// the instance's loaded configuration rather than replacing it with an outage.
        /// </summary>
        public async Task<ConfigIssue[]> RefreshAsync()
        {
            var refreshed = await Task.WhenAll(entries
                .Where(cached => cached.Value.Construct != null)
                .Select(async cached =>
                {
                    var previous = cached.Value;
                    var entry = await LoadAsync(previous.Construct!).ConfigureAwait(false);
                    var keepsPrevious = entry.Issue?.Category == ConfigIssueCategory.SourceFailure && previous.Configuration != null;
                    // Forgotten or replaced while this read was out: that newer state stands.
                    if (!keepsPrevious)
                        entries.TryUpdate(cached.Key, entry, previous);
                    return entry.Issue;
                })).ConfigureAwait(false);
            return refreshed.OfType<ConfigIssue>().ToArray();
        }

        private bool IsStale(Entry entry)
            => entry.Issue != null && clock() - entry.At >= IssueLifetime;

        /// <summary>One load per instance at a time: whoever arrives while it runs awaits the same task.</summary>
        private Task<Entry> LoadSharedAsync((Type type, string key) id, Func<IProvideConfiguration> construct)
        {
            return loading.GetOrAdd(id, _ =>
            {
                Lazy<Task<Entry>> marker = null!;
                marker = new Lazy<Task<Entry>>(() => RunAsync(marker));
                return marker;
            }).Value;

            async Task<Entry> RunAsync(Lazy<Task<Entry>> marker)
            {
                // Off the first caller's thread and context: the load belongs to every caller.
                await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
                try
                {
                    // Published between a caller's miss and its arrival here.
                    if (entries.TryGetValue(id, out var published) && !IsStale(published))
                        return published;
                    var entry = await LoadAsync(construct).ConfigureAwait(false);
                    // A Forget during the load removed the marker: the callers already waiting get
                    // the outcome, and it is not cached. Published BEFORE the marker goes (finally),
                    // so a caller that finds no load in flight finds the outcome.
                    if (IsLive())
                    {
                        entries[id] = entry;
                        if (!IsLive())
                            entries.TryRemove(new KeyValuePair<(Type type, string key), Entry>(id, entry));
                        else if (entry.Issue != null)
                            SweepStaleIssues();
                    }
                    return entry;
                }
                finally
                {
                    loading.TryRemove(new KeyValuePair<(Type type, string key), Lazy<Task<Entry>>>(id, marker));
                }

                bool IsLive() => loading.TryGetValue(id, out var live) && ReferenceEquals(live, marker);
            }
        }

        private Task<Entry> LoadAsync(Func<IProvideConfiguration> construct)
            => ConfigurationLoader.LoadAsync<IProvideConfiguration, Entry>(construct, readValue, secrets(),
                configuration => new Entry(configuration, null, clock(), construct),
                (issue, _) => Task.FromResult(new Entry(null, issue, clock(), construct)),
                issue => new Entry(null, issue, clock(), construct),
                issue => new Entry(null, issue, clock(), construct));

        /// <summary>Issues nobody asked about again are dropped once stale, at most once per
        /// lifetime, so instance keys that never resolve (ids from a caller) cannot accumulate.</summary>
        private void SweepStaleIssues()
        {
            var now = clock();
            var last = Interlocked.Read(ref sweptAt);
            if (now.UtcTicks - last < IssueLifetime.Ticks || Interlocked.CompareExchange(ref sweptAt, now.UtcTicks, last) != last)
                return;
            foreach (var cached in entries.Where(cached => IsStale(cached.Value)).ToArray())
                entries.TryRemove(cached);
        }
    }
}
