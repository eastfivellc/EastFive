#nullable enable
using System;

namespace EastFive.Configuration
{
    /// <summary>
    /// How framework code reads its own typed configuration at the point of use. A VALUE-ONLY type
    /// is loaded synchronously from the ambient values every time (cheap — reflection is cached and the
    /// values are an in-memory lookup — and always current, exactly like the raw reads it replaces). A
    /// type with SECRET members is served from the host's startup load
    /// (<see cref="StartupConfiguration"/>) and never loaded here, so nothing blocks on a vault.
    /// </summary>
    public static class Configured
    {
        public static TResult Get<TConfig, TResult>(Func<TConfig, TResult> onConfigured, Func<ConfigIssue, TResult> onUnavailable)
            where TConfig : IProvideConfiguration, new()
            => ConfigurationMembers.For<TConfig>().HasSecrets
                ? StartupConfiguration.Get(onConfigured, onUnavailable)
                : Get(() => new TConfig(), onConfigured, onUnavailable);

        /// <summary>A value-only (typically KEYED) type built by <paramref name="construct"/>.</summary>
        public static TResult Get<TConfig, TResult>(Func<TConfig> construct, Func<TConfig, TResult> onConfigured, Func<ConfigIssue, TResult> onUnavailable)
            where TConfig : IProvideConfiguration
            => ConfigurationLoader.Load(construct, ConfigurationLoader.ReadAmbient, onConfigured,
                (issue, _) => onUnavailable(issue), onUnavailable, onUnavailable);

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
