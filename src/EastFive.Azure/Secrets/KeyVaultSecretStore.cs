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
    /// <summary>How a <see cref="KeyVaultAccess"/> entry reaches its vault.</summary>
    public enum KeyVaultAccessMode
    {
        /// <summary>No usable access: the caller owns the warning.</summary>
        None,
        /// <summary>A service principal: tenant, client id and client secret.</summary>
        ClientSecret,
        /// <summary>The host's managed identity.</summary>
        ManagedIdentity,
        /// <summary>The developer's Azure CLI sign-in (<c>az login</c>).</summary>
        AzureCli,
    }

    /// <summary>
    /// One vault and how to reach it: ACCESS MATERIAL, deliberately not configuration. An entry
    /// declares exactly ONE access mode: a service principal (tenant/client/secret from a
    /// gitignored manifest), <see cref="ManagedIdentity"/>, or <see cref="UseAzureCli"/> (the
    /// developer's <c>az login</c>, so no secret sits on disk). Deployed to App Service
    /// (<c>WEBSITE_SITE_NAME</c>), managed identity applies unless a service principal is given,
    /// and Azure CLI sign-in is ignored with a warning.
    /// </summary>
    public sealed class KeyVaultAccess
    {
        public string? Url { get; set; }

        /// <summary>The service principal's tenant; with <see cref="UseAzureCli"/>, the tenant
        /// the CLI token is requested for (optional).</summary>
        public string? TenantId { get; set; }
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }

        /// <summary>Set to use the host's managed identity even off App Service.</summary>
        public bool ManagedIdentity { get; set; }

        /// <summary>Set to use the developer's Azure CLI sign-in. Never applies on a deployed host.</summary>
        public bool UseAzureCli { get; set; }

        public bool TryGetUri(out Uri vault)
            => Uri.TryCreate(Url, UriKind.Absolute, out vault!) && vault.Scheme == Uri.UriSchemeHttps;

        /// <summary>
        /// The access mode this entry resolves to, with a warning when a declared mode does not
        /// apply here; <paramref name="onInvalid"/> when the entry declares more than one mode.
        /// <paramref name="deployed"/> defaults to whether <c>WEBSITE_SITE_NAME</c> is set.
        /// </summary>
        public TResult ResolveMode<TResult>(
            Func<KeyVaultAccessMode, string?, TResult> onMode,
            Func<string, TResult> onInvalid,
            bool? deployed = null)
        {
            var servicePrincipal = !string.IsNullOrEmpty(ClientId) || !string.IsNullOrEmpty(ClientSecret);
            var declared = new[]
                {
                    (servicePrincipal, "a client secret"),
                    (ManagedIdentity, "managedIdentity"),
                    (UseAzureCli, "useAzureCli"),
                }
                .Where(mode => mode.Item1)
                .Select(mode => mode.Item2)
                .ToArray();
            if (declared.Length > 1)
                return onInvalid($"Vault {Url ?? "(no url)"} declares more than one access mode ({string.Join(", ", declared)}); keep exactly one.");

            var onAppService = deployed ?? IsDeployed();
            var complete = !string.IsNullOrEmpty(TenantId) && !string.IsNullOrEmpty(ClientId) && !string.IsNullOrEmpty(ClientSecret);
            if (complete)
                return onMode(KeyVaultAccessMode.ClientSecret, null);
            if (UseAzureCli && onAppService)
                return onMode(KeyVaultAccessMode.ManagedIdentity,
                    $"Vault {Url ?? "(no url)"} asks for Azure CLI sign-in, which never applies on a deployed host; using managed identity.");
            if (UseAzureCli)
                return onMode(KeyVaultAccessMode.AzureCli, null);
            if (ManagedIdentity || onAppService)
                return onMode(KeyVaultAccessMode.ManagedIdentity, null);
            return onMode(KeyVaultAccessMode.None, null);
        }

        /// <summary>
        /// The credential the store uses for this entry: the ONE construction, so a probe builds
        /// exactly what a read does. <paramref name="onNoAccess"/> when nothing is declared (and
        /// not deployed); <paramref name="onInvalid"/> for a mixed-mode entry.
        /// </summary>
        public TResult CreateCredential<TResult>(
            Func<TokenCredential, KeyVaultAccessMode, TResult> onCredential,
            Func<TResult> onNoAccess,
            Func<string, TResult> onInvalid,
            Action<string>? onWarning = null,
            bool? deployed = null)
            => ResolveMode(
                (mode, warning) =>
                {
                    if (warning != null)
                        onWarning?.Invoke(warning);
                    return mode switch
                    {
                        KeyVaultAccessMode.ClientSecret => onCredential(new ClientSecretCredential(TenantId, ClientId, ClientSecret), mode),
                        KeyVaultAccessMode.ManagedIdentity => onCredential(new ManagedIdentityCredential(), mode),
                        KeyVaultAccessMode.AzureCli => onCredential(new AzureCliCredential(new AzureCliCredentialOptions
                        {
                            TenantId = string.IsNullOrEmpty(TenantId) ? null : TenantId,
                        }), mode),
                        _ => onNoAccess(),
                    };
                },
                onInvalid,
                deployed);

        private static bool IsDeployed() => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));
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
        /// url (never credentials) of each entry with no https url, no credential path, or more
        /// than one declared mode; <paramref name="onWarning"/> receives why (a mixed-mode entry,
        /// Azure CLI sign-in ignored on a deployed host), naming the vault and never a credential.
        /// </summary>
        public static KeyVaultSecretStore From(IEnumerable<KeyVaultAccess> accesses,
            Action<string>? onUnusable = null, TimeSpan? reloadInterval = null, Action<string>? onWarning = null)
        {
            var usable = accesses
                .Select(access => (access, vault: access.TryGetUri(out var vault) ? vault : null,
                    credential: access.CreateCredential(
                        (credential, _) => credential,
                        () => (TokenCredential?)null,
                        problem =>
                        {
                            onWarning?.Invoke(problem);
                            return null;
                        },
                        onWarning)))
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
