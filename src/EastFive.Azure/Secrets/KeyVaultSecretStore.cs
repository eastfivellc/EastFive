#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

using EastFive.Configuration;

namespace EastFive.Azure.Secrets
{
    /// <summary>
    /// One vault and how to reach it: ACCESS MATERIAL, deliberately not configuration. Locally a
    /// service principal (tenant/client/secret from a gitignored manifest); deployed, managed
    /// identity (no material at all).
    /// </summary>
    public sealed class KeyVaultAccess
    {
        public string? Url { get; set; }
        public string? TenantId { get; set; }
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }

        /// <summary>Set to use the host's managed identity even off App Service.</summary>
        public bool ManagedIdentity { get; set; }

        /// <summary>
        /// A service principal when all three of tenant/client/secret are set; otherwise managed
        /// identity when asked for or when deployed to App Service (<c>WEBSITE_SITE_NAME</c>);
        /// otherwise <c>null</c> (unusable: the caller owns the warning).
        /// </summary>
        public TokenCredential? ResolveCredential()
            => (TenantId, ClientId, ClientSecret) switch
            {
                ({ Length: > 0 } tenantId, { Length: > 0 } clientId, { Length: > 0 } clientSecret) =>
                    new ClientSecretCredential(tenantId, clientId, clientSecret),
                _ => ManagedIdentity || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"))
                    ? new ManagedIdentityCredential()
                    : null,
            };

        public bool TryGetUri(out Uri vault)
            => Uri.TryCreate(Url, UriKind.Absolute, out vault!) && vault.Scheme == Uri.UriSchemeHttps;
    }

    /// <summary>
    /// The environment's vaults, in order, read by secret NAME through one <see cref="SecretClient"/>
    /// per vault (never as an <c>IConfiguration</c> provider, so no dot/dash mapping exists). The
    /// first vault holding the name wins; a 404 falls through to the next; any other error is a
    /// failure and stops the walk. Found and missing outcomes are cached per name for the reload
    /// interval (default 30 minutes, matching the old provider's reload); failures are not cached.
    /// </summary>
    public sealed class KeyVaultSecretStore : ISecretStore
    {
        public static readonly TimeSpan DefaultReloadInterval = TimeSpan.FromMinutes(30);

        private readonly (Uri vault, SecretClient client)[] vaults;
        private readonly TimeSpan reloadInterval;
        private readonly TimeProvider time;
        private readonly ConcurrentDictionary<string, Cached> cache = new(StringComparer.OrdinalIgnoreCase);

        private sealed record Cached(string? Value, int VaultIndex, DateTimeOffset FetchedAt);

        /// <summary>The seam: clients supplied by the caller (tests subclass <see cref="SecretClient"/>).</summary>
        public KeyVaultSecretStore(IEnumerable<(Uri vault, SecretClient client)> vaults,
            TimeSpan? reloadInterval = null, TimeProvider? time = null)
        {
            this.vaults = vaults.ToArray();
            this.reloadInterval = reloadInterval ?? DefaultReloadInterval;
            this.time = time ?? TimeProvider.System;
        }

        /// <summary>
        /// One client per USABLE access entry, in order. <paramref name="onUnusable"/> receives the
        /// url (never credentials) of each entry with no https url or no credential path.
        /// </summary>
        public static KeyVaultSecretStore From(IEnumerable<KeyVaultAccess> accesses,
            Action<string>? onUnusable = null, TimeSpan? reloadInterval = null)
        {
            var usable = accesses
                .Select(access => (access, vault: access.TryGetUri(out var vault) ? vault : null,
                    credential: access.ResolveCredential()))
                .Where(entry =>
                {
                    if (entry.vault != null && entry.credential != null)
                        return true;
                    onUnusable?.Invoke(entry.access.Url ?? "(no url)");
                    return false;
                })
                .Select(entry => (entry.vault!, new SecretClient(entry.vault!, entry.credential!)))
                .ToArray();
            return new KeyVaultSecretStore(usable, reloadInterval);
        }

        public IReadOnlyList<SecretSource> Sources
            => vaults.Select(entry => new SecretSource(SecretSource.Vault, entry.vault.Host)).ToArray();

        public async Task<TResult> GetSecretAsync<TResult>(string name,
            Func<Secret, SecretSource, TResult> onFound,
            Func<TResult> onMissing,
            Func<string, TResult> onFailure)
        {
            if (!SecretNames.IsValidName(name))
                return onFailure($"[{name}] is not a valid secret name.");

            if (cache.TryGetValue(name, out var cached) && time.GetUtcNow() - cached.FetchedAt < reloadInterval)
                return Answer(cached);

            for (var index = 0; index < vaults.Length; index++)
            {
                var (vault, client) = vaults[index];
                string? value;
                try
                {
                    var response = await client.GetSecretAsync(name).ConfigureAwait(false);
                    value = response.Value?.Value;
                }
                catch (RequestFailedException error) when (error.Status == 404)
                {
                    continue;
                }
                catch (RequestFailedException error)
                {
                    return onFailure($"Vault {vault.Host} answered {error.Status} for [{name}].");
                }
                catch (Exception error) when (error is AuthenticationFailedException or CredentialUnavailableException
                    or System.Net.Http.HttpRequestException or TaskCanceledException)
                {
                    return onFailure($"Vault {vault.Host} could not be reached for [{name}] ({error.GetType().Name}).");
                }

                if (string.IsNullOrWhiteSpace(value))
                    continue;
                return Answer(cache[name] = new Cached(value, index, time.GetUtcNow()));
            }
            return Answer(cache[name] = new Cached(null, -1, time.GetUtcNow()));

            TResult Answer(Cached entry)
                => entry.Value == null
                    ? onMissing()
                    : onFound(new Secret(entry.Value), new SecretSource(SecretSource.Vault, vaults[entry.VaultIndex].vault.Host));
        }
    }
}
