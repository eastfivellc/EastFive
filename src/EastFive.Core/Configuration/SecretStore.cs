#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EastFive.Configuration
{
    /// <summary>
    /// Where a secret was found: the kind of source (<see cref="LocalOverride"/>, <see cref="Vault"/>,
    /// <see cref="Draft"/>) and its location (a values section, a vault host). Never a value, so it
    /// is safe in a doctor row or a log line.
    /// </summary>
    public sealed record SecretSource(string Kind, string Location)
    {
        public const string LocalOverride = "local override";
        public const string Vault = "vault";
        public const string Draft = "draft";

        public override string ToString() => $"{Kind} ({Location})";
    }

    /// <summary>Why a secret store could not answer, as a category a tool can act on.</summary>
    public enum SecretFailureKind
    {
        /// <summary>Anything not classified below (an invalid name, an unexpected status).</summary>
        Other,
        /// <summary>The credential was refused by the identity provider (expired or wrong secret,
        /// unknown application or tenant).</summary>
        AuthFailed,
        /// <summary>The credential authenticated but the store denied the read.</summary>
        Forbidden,
        /// <summary>The store or the identity provider could not be reached.</summary>
        Network,
        /// <summary>The credential could not be used at all here (no Azure CLI sign-in, no managed
        /// identity endpoint).</summary>
        CredentialUnavailable,
    }

    /// <summary>
    /// A store's structured "could not answer": the <see cref="Kind"/>, the HTTP status, the
    /// identity provider's AADSTS code and the service's error code when known, and a
    /// <see cref="Message"/> that carries no secret material and no raw SDK text.
    /// </summary>
    public sealed record SecretFailure(
        SecretFailureKind Kind,
        string Message,
        int? Status = null,
        string? Aadsts = null,
        string? Code = null)
    {
        /// <summary>The wire spelling of <see cref="Kind"/>: <c>auth_failed</c>, <c>forbidden</c>,
        /// <c>network</c>, <c>credential_unavailable</c> or <c>other</c>.</summary>
        public string KindName => Kind switch
        {
            SecretFailureKind.AuthFailed => "auth_failed",
            SecretFailureKind.Forbidden => "forbidden",
            SecretFailureKind.Network => "network",
            SecretFailureKind.CredentialUnavailable => "credential_unavailable",
            _ => "other",
        };

        public static SecretFailure Other(string message) => new(SecretFailureKind.Other, message);

        public override string ToString() => Message;
    }

    /// <summary>
    /// The SECRETS store: queried by secret name, never an <c>IConfiguration</c> provider. Values
    /// (non-secret) stay in <c>IConfiguration</c>; framework code reaches a secret only through a
    /// typed configuration loaded by <see cref="ConfigurationLoader"/>.
    /// </summary>
    /// <remarks>
    /// Outcomes: <c>onFound</c> with the secret and where it came from; <c>onMissing</c> when the
    /// store answered and holds no such name; <c>onFailure</c> when it could not answer (denied,
    /// throttled, unreachable, an invalid name) — distinct, so an outage is never "unset". The
    /// <see cref="SecretFailure"/> carries no secret material and no raw SDK message.
    /// </remarks>
    public interface ISecretStore
    {
        Task<TResult> GetSecretAsync<TResult>(string name,
            Func<Secret, SecretSource, TResult> onFound,
            Func<TResult> onMissing,
            Func<SecretFailure, TResult> onFailure);

        /// <summary>The sources this store consults, in order (a doctor lists them).</summary>
        IReadOnlyList<SecretSource> Sources { get; }
    }

    /// <summary>
    /// Local overrides: <c>Secrets:{name}</c> read from the VALUES store (written only in
    /// <c>appsettings.local*.json</c> or the environment, e.g. <c>Secrets__Telnyx-acme-ApiKey</c>).
    /// The one place a secret may sit beside values, and it is still addressed by its secret
    /// name, never by a values key.
    /// </summary>
    public sealed class ConfigurationSecretOverrides : ISecretStore
    {
        public const string Section = "Secrets";

        private readonly Func<string, string?> readValue;

        public ConfigurationSecretOverrides(Func<string, string?> readValue) => this.readValue = readValue;

        /// <summary>The values key an override for <paramref name="name"/> is read from.</summary>
        public static string KeyFor(string name) => $"{Section}:{name}";

        public IReadOnlyList<SecretSource> Sources { get; } = new[] { new SecretSource(SecretSource.LocalOverride, Section) };

        public Task<TResult> GetSecretAsync<TResult>(string name,
            Func<Secret, SecretSource, TResult> onFound,
            Func<TResult> onMissing,
            Func<SecretFailure, TResult> onFailure)
        {
            if (!SecretNames.IsValidName(name))
                return Task.FromResult(onFailure(SecretFailure.Other($"[{name}] is not a valid secret name.")));
            string? text;
            try
            {
                text = readValue(KeyFor(name));
            }
            catch (Exception)
            {
                return Task.FromResult(onFailure(SecretFailure.Other($"The values store could not be read for [{KeyFor(name)}].")));
            }
            return Task.FromResult(string.IsNullOrWhiteSpace(text)
                ? onMissing()
                : onFound(new Secret(text.Trim()), Sources[0]));
        }
    }

    /// <summary>An explicit in-memory set of secrets by name: a draft being edited, a probe, a test.</summary>
    public sealed class InMemorySecretStore : ISecretStore
    {
        private readonly IReadOnlyDictionary<string, string> secrets;
        private readonly SecretSource source;

        public InMemorySecretStore(IReadOnlyDictionary<string, string> secrets, string location = "in-memory")
        {
            this.secrets = secrets;
            source = new SecretSource(SecretSource.Draft, location);
        }

        public IReadOnlyList<SecretSource> Sources => new[] { source };

        public Task<TResult> GetSecretAsync<TResult>(string name,
            Func<Secret, SecretSource, TResult> onFound,
            Func<TResult> onMissing,
            Func<SecretFailure, TResult> onFailure)
            => Task.FromResult(!SecretNames.IsValidName(name)
                ? onFailure(SecretFailure.Other($"[{name}] is not a valid secret name."))
                : secrets.TryGetValue(name, out var text) && !string.IsNullOrWhiteSpace(text)
                    ? onFound(new Secret(text.Trim()), source)
                    : onMissing());
    }

    /// <summary>
    /// Consults its stores in order; the FIRST that finds the name wins. Missing falls through to
    /// the next store. A failure STOPS the walk and is reported: a later store answering would
    /// silently change which secret a host runs with whenever an earlier one has an outage.
    /// </summary>
    public sealed class CompositeSecretStore : ISecretStore
    {
        private readonly ISecretStore[] stores;

        public CompositeSecretStore(params ISecretStore[] stores) => this.stores = stores;

        public CompositeSecretStore(IEnumerable<ISecretStore> stores) => this.stores = stores.ToArray();

        public IReadOnlyList<SecretSource> Sources => stores.SelectMany(store => store.Sources).ToArray();

        public Task<TResult> GetSecretAsync<TResult>(string name,
            Func<Secret, SecretSource, TResult> onFound,
            Func<TResult> onMissing,
            Func<SecretFailure, TResult> onFailure)
            => From(0, name, onFound, onMissing, onFailure);

        private Task<TResult> From<TResult>(int index, string name,
            Func<Secret, SecretSource, TResult> onFound,
            Func<TResult> onMissing,
            Func<SecretFailure, TResult> onFailure)
            => index >= stores.Length
                ? Task.FromResult(onMissing())
                : stores[index].GetSecretAsync(name,
                    (secret, source) => Task.FromResult(onFound(secret, source)),
                    () => From(index + 1, name, onFound, onMissing, onFailure),
                    why => Task.FromResult(onFailure(why))).Unwrap();
    }

    /// <summary>
    /// The ambient secret store, initialized by each host next to
    /// <c>ConfigurationExtensions.Initialize</c> (typically local overrides, then the
    /// environment's vaults). Until a host initializes it, it is the local overrides over the
    /// ambient values — so a host with no vault can still run on <c>Secrets:</c> overrides, and
    /// a missing vault reads as missing, never as a value from somewhere else.
    /// </summary>
    public static class SecretStore
    {
        private static ISecretStore? current;

        public static ISecretStore Current => Volatile.Read(ref current) ?? AmbientOverrides;

        public static ISecretStore AmbientOverrides { get; } = new ConfigurationSecretOverrides(ConfigurationLoader.ReadAmbient);

        public static void Initialize(ISecretStore store)
            => Volatile.Write(ref current, store ?? throw new ArgumentNullException(nameof(store)));
    }
}
