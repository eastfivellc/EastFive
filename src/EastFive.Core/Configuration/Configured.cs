#nullable enable
using System;
using System.Threading.Tasks;

namespace EastFive.Configuration
{
    /// <summary>
    /// How framework code reads its own typed configuration at the point of use. A VALUE-ONLY type
    /// is loaded synchronously from the ambient values every time (cheap — reflection is cached and the
    /// values are an in-memory lookup — and always current, exactly like the raw reads it replaces). A
    /// type with SECRET members is served from the host's startup load
    /// (<see cref="StartupConfiguration"/>) and never loaded here, so nothing blocks on a vault. A
    /// KEYED type with secret members is not in that load — its instances are not known at startup —
    /// so <see cref="GetAsync"/> loads an instance on first use and caches it
    /// (<see cref="StartupConfiguration.Instances"/>), and the synchronous <c>Get</c> reads that cache.
    /// </summary>
    public static class Configured
    {
        public static TResult Get<TConfig, TResult>(Func<TConfig, TResult> onConfigured, Func<ConfigIssue, TResult> onUnavailable)
            where TConfig : IProvideConfiguration, new()
            => ConfigurationMembers.For<TConfig>().HasSecrets
                ? StartupConfiguration.Get(onConfigured, onUnavailable)
                : Get(() => new TConfig(), onConfigured, onUnavailable);

        /// <summary>
        /// A type built by <paramref name="construct"/> (typically KEYED). Value-only: read live from
        /// the ambient values. With secret members: the cached instance, never loaded here — a keyed
        /// instance nothing has loaded yet (see <see cref="GetAsync"/>) reaches
        /// <paramref name="onUnavailable"/> with a <see cref="ConfigIssueCategory.NotLoaded"/> issue.
        /// </summary>
        public static TResult Get<TConfig, TResult>(Func<TConfig> construct, Func<TConfig, TResult> onConfigured, Func<ConfigIssue, TResult> onUnavailable)
            where TConfig : IProvideConfiguration
        {
            var instance = construct();
            var type = instance.GetType();
            if (!ConfigurationMembers.For(type).HasSecrets)
                return ConfigurationLoader.Load(construct, ConfigurationLoader.ReadAmbient, onConfigured,
                    (issue, _) => onUnavailable(issue), onUnavailable, onUnavailable);
            return instance is IProvideConfigurationInstanceKey keyed
                ? StartupConfiguration.Instances.Get(type, keyed.InstanceKey, config => onConfigured((TConfig)config), onUnavailable)
                : StartupConfiguration.Get(type, config => onConfigured((TConfig)config), onUnavailable);
        }

        /// <summary>
        /// <see cref="Get{TConfig, TResult}(Func{TConfig}, Func{TConfig, TResult}, Func{ConfigIssue, TResult})"/>
        /// for a caller that can await: a KEYED instance with secret members is loaded from the ambient
        /// values and <see cref="SecretStore.Current"/> on first use and cached — a loaded configuration
        /// until it is refreshed, an issue for <see cref="ConfigurationInstanceCache.IssueLifetime"/>.
        /// Every other type is read exactly as the synchronous <c>Get</c> reads it.
        /// </summary>
        public static Task<TResult> GetAsync<TConfig, TResult>(Func<TConfig> construct, Func<TConfig, TResult> onConfigured, Func<ConfigIssue, TResult> onUnavailable)
            where TConfig : IProvideConfiguration
        {
            var instance = construct();
            return instance is IProvideConfigurationInstanceKey && ConfigurationMembers.For(instance.GetType()).HasSecrets
                ? StartupConfiguration.Instances.GetOrLoadUntypedAsync(() => construct(), config => onConfigured((TConfig)config), onUnavailable)
                : Task.FromResult(Get(construct, onConfigured, onUnavailable));
        }

        /// <summary>
        /// One member of a typed configuration, in the continuation shape the raw readers had, so a
        /// raw read becomes a typed one without reshaping its callers: <paramref name="onFound"/> with
        /// the value; when it is unset or the configuration is unavailable, <paramref name="onNotSpecified"/>
        /// if given, else <paramref name="onUnspecified"/> with the reason, else a
        /// <see cref="EastFive.Web.ConfigurationException"/> naming <paramref name="key"/> — exactly
        /// what the raw reader did with no handler.
        /// </summary>
        public static TResult Member<TConfig, TValue, TResult>(Func<TConfig, TValue?> select, string key,
            Func<TValue, TResult> onFound,
            Func<string, TResult>? onUnspecified = null,
            Func<TResult>? onNotSpecified = null)
            where TConfig : IProvideConfiguration, new()
            where TValue : class
        {
            return Get<TConfig, TResult>(
                config => select(config) is { } value ? onFound(value) : Unspecified($"[{key}] is not specified."),
                issue => Unspecified(issue.ToString()));

            TResult Unspecified(string why)
                => onNotSpecified != null ? onNotSpecified()
                    : onUnspecified != null ? onUnspecified(why)
                    : throw new EastFive.Web.ConfigurationException(key, typeof(TValue), why);
        }

        /// <summary><see cref="Member{TConfig, TValue, TResult}"/> for a value-typed member.</summary>
        public static TResult MemberValue<TConfig, TValue, TResult>(Func<TConfig, TValue?> select, string key,
            Func<TValue, TResult> onFound,
            Func<string, TResult>? onUnspecified = null,
            Func<TResult>? onNotSpecified = null)
            where TConfig : IProvideConfiguration, new()
            where TValue : struct
        {
            return Get<TConfig, TResult>(
                config => select(config) is { } value ? onFound(value) : Unspecified($"[{key}] is not specified."),
                issue => Unspecified(issue.ToString()));

            TResult Unspecified(string why)
                => onNotSpecified != null ? onNotSpecified()
                    : onUnspecified != null ? onUnspecified(why)
                    : throw new EastFive.Web.ConfigurationException(key, typeof(TValue), why);
        }

        /// <summary>
        /// A string member read as a URI that may be RELATIVE — which a <see cref="Uri"/> member cannot
        /// hold, since typed members accept absolute URIs only. Unparseable text is unspecified, as the
        /// raw URI reader treated it.
        /// </summary>
        public static TResult MemberUri<TConfig, TResult>(Func<TConfig, string?> select, string key,
            Func<Uri, TResult> onFound,
            Func<string, TResult>? onUnspecified = null,
            Func<TResult>? onNotSpecified = null)
            where TConfig : IProvideConfiguration, new()
            => Member<TConfig, string, TResult>(select, key,
                text => Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out var uri)
                    ? onFound(uri)
                    : onUnspecified != null ? onUnspecified($"[{key}] is not a URI.")
                    : throw new EastFive.Web.ConfigurationException(key, typeof(Uri), $"[{key}] is not a URI."),
                onUnspecified, onNotSpecified);

        /// <summary>
        /// The value of a key that some typed configuration member DECLARES — for the few public APIs
        /// that take a key name. An undeclared key is <paramref name="onUnspecified"/>, never read, so
        /// such an API cannot be used to reach configuration nothing describes.
        /// </summary>
        public static TResult ReadDeclaredValue<TResult>(string key, Func<string, TResult> onFound, Func<string, TResult> onUnspecified)
        {
            if (!ConfigurationCatalog.IsDeclaredValueKey(key))
                return onUnspecified($"[{key}] is not declared by any typed configuration member.");
            string? value;
            try
            {
                value = ConfigurationLoader.ReadAmbient(key);
            }
            catch (Exception)
            {
                return onUnspecified($"[{key}] could not be read.");
            }
            return string.IsNullOrWhiteSpace(value) ? onUnspecified($"[{key}] is not specified.") : onFound(value);
        }
    }
}
