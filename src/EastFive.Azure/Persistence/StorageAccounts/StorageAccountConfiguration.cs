#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using EastFive.Configuration;

namespace EastFive.Azure.Persistence
{
    /// <summary>
    /// One Azure Storage account, NAMED by configuration and CREDENTIALED by the vault. The
    /// committed <c>appsettings</c> carries <c>&lt;Store&gt;.Account</c> (an account name is not a
    /// secret); the vault carries one secret per account, <c>Azure-Storage-Account-{name}</c>,
    /// which the whole-vault provider serves as <c>Azure.Storage.Account.{name}</c>. Several
    /// stores that share an account share that one secret, so rotation is one write.
    /// </summary>
    /// <remarks>
    /// Resolution, decided by <see cref="Account"/> alone (an account name, when set, is
    /// authoritative):
    /// <list type="bullet">
    /// <item>blank: the store's legacy global key (<c>&lt;Store&gt;.ConnectionString</c>) — how
    /// every host is configured today, so nothing changes until an account is named;</item>
    /// <item><c>devstoreaccount1</c>: the emulator. Never a vault read — a hermetic run must not
    /// depend on the network, and a vault entry of that name must not be able to redirect it.
    /// The reserved key <c>Azure.Storage.Emulator.ConnectionString</c> wins if set (machine
    /// files); otherwise the loader FABRICATES Azurite's well-known string from
    /// <c>Azure.Storage.Emulator.Port</c> (blob; queue and table at +1/+2). Fabrication only
    /// ever happens for the emulator account and only on a confirmed-missing outcome, never on
    /// a source failure;</item>
    /// <item>anything else: <c>Azure.Storage.Account.{name}</c>.</item>
    /// </list>
    /// <see cref="Validate"/> refuses a credential for a different account than the one named,
    /// and an emulator credential whose endpoints leave loopback.
    /// </remarks>
    public abstract class StorageAccountConfiguration : IProvideConfiguration
    {
        public const string Purpose = "storage";
        public const string EmulatorAccount = "devstoreaccount1";
        public const int EmulatorDefaultPort = 10000;

        // Azurite's published well-known key: public knowledge, not a secret.
        private const string EmulatorAccountKey =
            "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

        [ConfigurationMember(EastFive.Azure.AppSettings.Persistence.Storage.EmulatorPort,
            Kind = ConfigurationMemberKind.Defaulted, Default = "10000")]
        public int EmulatorPort { get; set; }

        /// <summary>The storage account name; the concrete type declares its <c>&lt;Store&gt;.Account</c> key.</summary>
        public abstract string? Account { get; set; }

        /// <summary>The credential; the concrete type declares its legacy <c>&lt;Store&gt;.ConnectionString</c> key.</summary>
        public abstract Secret ConnectionString { get; set; }

        /// <summary>The legacy global key the concrete type's <see cref="ConnectionString"/> is declared with —
        /// what readers that bypass this type (queue triggers, older drivers) still read.</summary>
        public abstract string ConnectionStringKey { get; }

        /// <summary>The <c>&lt;Store&gt;.Account</c> key the concrete type declares.</summary>
        public static string AccountKey<TConfig>() where TConfig : StorageAccountConfiguration
            => ConfigurationMembers.For<TConfig>().Members
                .First(member => string.Equals(member.Property.Name, nameof(Account), StringComparison.Ordinal)).Key;

        public static bool IsEmulator(string? account)
            => string.Equals(account?.Trim(), EmulatorAccount, StringComparison.OrdinalIgnoreCase);

        public static string AccountSecretKey(string account)
            => EastFive.Azure.AppSettings.Persistence.Storage.AccountSecretPrefix + account.Trim().ToLowerInvariant();

        /// <summary>Which key the credential is read from for the current <see cref="Account"/>.</summary>
        public string ResolvedConnectionStringKey
            => string.IsNullOrWhiteSpace(Account) ? ConnectionStringKey
                : IsEmulator(Account) ? EastFive.Azure.AppSettings.Persistence.Storage.EmulatorConnectionString
                : AccountSecretKey(Account);

        public SecretReference SecretReference(string purpose)
            => string.Equals(purpose, Purpose, StringComparison.Ordinal)
                ? new SecretReference(ResolvedConnectionStringKey, purpose)
                : throw new InvalidOperationException($"{GetType().Name} has no secret for purpose '{purpose}'.");

        public string? Validate()
        {
            if (string.IsNullOrWhiteSpace(Account) || ConnectionString == null)
                return null;
            var pairs = Parse(ConnectionString.Reveal());
            pairs.TryGetValue("AccountName", out var accountName);
            if (IsEmulator(Account))
            {
                if (accountName != null && !IsEmulator(accountName))
                    return ConnectionStringKey;
                var offLoopback = pairs
                    .Where(pair => pair.Key.EndsWith("Endpoint", StringComparison.OrdinalIgnoreCase))
                    .Any(pair => !Uri.TryCreate(pair.Value, UriKind.Absolute, out var endpoint) || !endpoint.IsLoopback);
                return offLoopback ? ConnectionStringKey : null;
            }
            return accountName != null && !string.Equals(accountName.Trim(), Account.Trim(), StringComparison.OrdinalIgnoreCase)
                ? ConnectionStringKey
                : null;
        }

        /// <summary>Azurite's connection string for a blob port (queue and table follow at +1 and +2).</summary>
        public static string EmulatorConnectionString(int blobPort = EmulatorDefaultPort)
            => $"DefaultEndpointsProtocol=http;AccountName={EmulatorAccount};AccountKey={EmulatorAccountKey};"
                + $"BlobEndpoint=http://127.0.0.1:{blobPort}/{EmulatorAccount};"
                + $"QueueEndpoint=http://127.0.0.1:{blobPort + 1}/{EmulatorAccount};"
                + $"TableEndpoint=http://127.0.0.1:{blobPort + 2}/{EmulatorAccount}";

        public static TResult Load<TConfig, TResult>(
            Func<TConfig, TResult> onConfigured,
            Func<ConfigIssue, Func<string, TResult>, TResult> onMissing,
            Func<ConfigIssue, TResult> onInvalid,
            Func<ConfigIssue, TResult> onSourceFailure)
            where TConfig : StorageAccountConfiguration, new()
            => Load(ConfigurationLoader.ReadAmbient, onConfigured, onMissing, onInvalid, onSourceFailure);

        /// <summary>
        /// <see cref="ConfigurationLoader.Load{TConfig, TResult}(Func{string, string?}, Func{TConfig, TResult}, Func{ConfigIssue, Func{string, TResult}, TResult}, Func{ConfigIssue, TResult}, Func{ConfigIssue, TResult})"/>
        /// with the emulator fabrication interposed: a missing reserved emulator key is supplied
        /// from the port setting; every other missing member reaches the caller's
        /// <paramref name="onMissing"/> unchanged.
        /// </summary>
        public static TResult Load<TConfig, TResult>(
            Func<string, string?> readSetting,
            Func<TConfig, TResult> onConfigured,
            Func<ConfigIssue, Func<string, TResult>, TResult> onMissing,
            Func<ConfigIssue, TResult> onInvalid,
            Func<ConfigIssue, TResult> onSourceFailure)
            where TConfig : StorageAccountConfiguration, new()
            => ConfigurationLoader.Load<TConfig, TResult>(readSetting, onConfigured,
                (issue, supply) =>
                {
                    if (!string.Equals(issue.Key, EastFive.Azure.AppSettings.Persistence.Storage.EmulatorConnectionString, StringComparison.Ordinal))
                        return onMissing(issue, supply);
                    ConfigIssue PortIssue(ConfigIssueCategory category)
                        => new(typeof(TConfig), EastFive.Azure.AppSettings.Persistence.Storage.EmulatorPort, ConfigurationMemberKind.Defaulted, category);
                    string? portText;
                    try
                    {
                        portText = readSetting(EastFive.Azure.AppSettings.Persistence.Storage.EmulatorPort);
                    }
                    catch (Exception)
                    {
                        return onSourceFailure(PortIssue(ConfigIssueCategory.SourceFailure));
                    }
                    if (string.IsNullOrWhiteSpace(portText))
                        return supply(EmulatorConnectionString());
                    return int.TryParse(portText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var port)
                            && port is >= 1 and <= 65533
                        ? supply(EmulatorConnectionString(port))
                        : onInvalid(PortIssue(ConfigIssueCategory.Invalid));
                },
                onInvalid, onSourceFailure);

        /// <summary>The doctor's checklist, minus the reserved emulator key the loader fabricates.</summary>
        public static string[] Missing<TConfig>(Func<string, string?> readSetting)
            where TConfig : StorageAccountConfiguration, new()
            => ConfigurationLoader.Missing<TConfig>(readSetting)
                .Where(key => !string.Equals(key, EastFive.Azure.AppSettings.Persistence.Storage.EmulatorConnectionString, StringComparison.Ordinal))
                .ToArray();

        private static Dictionary<string, string> Parse(string connectionString)
            => connectionString.Split(';')
                .Select(part => part.Split('=', 2))
                .Where(part => part.Length == 2 && part[0].Trim().Length > 0)
                .GroupBy(part => part[0].Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First()[1].Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
