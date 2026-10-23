#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using EastFive.Configuration;

namespace EastFive.Azure.Persistence
{
    /// <summary>
    /// One Azure Storage account, NAMED by the values store and CREDENTIALED by the secret store.
    /// The committed <c>appsettings</c> carries <c>&lt;Store&gt;.Account</c> (an account name is not
    /// a secret); the secret store carries one secret per account,
    /// <c>AzureStorage-{account}-ConnectionString</c> (generated: <see cref="ConfigurationTypeAttribute"/>
    /// <c>AzureStorage</c>, the account member, the property). Several stores on one account share
    /// that one secret, so rotation is one write.
    /// </summary>
    /// <remarks>
    /// The account is Required: there is no legacy <c>&lt;Store&gt;.ConnectionString</c> fallback and
    /// no reader bypasses this type. <c>devstoreaccount1</c> is the emulator and is never a
    /// secret-store read — a hermetic run must not depend on the network, and a vault entry of that
    /// name must not be able to redirect it. The reserved VALUES key
    /// <c>Azure.Storage.Emulator.ConnectionString</c> wins if set (machine files); otherwise the
    /// credential is FABRICATED as Azurite's well-known string from <c>Azure.Storage.Emulator.Port</c>
    /// (blob; queue and table at +1/+2). <see cref="Validate"/> refuses a credential for a different
    /// account than the one named, and an emulator credential whose endpoints leave loopback.
    /// </remarks>
    [ConfigurationType(TypeName)]
    public abstract class StorageAccountConfiguration : IProvideConfiguration, IResolveConfigurationMember
    {
        public const string TypeName = "AzureStorage";
        public const string Purpose = nameof(ConnectionString);
        public const string EmulatorAccount = "devstoreaccount1";
        public const int EmulatorDefaultPort = 10000;
        public const string EmulatorSource = "emulator";

        // Azurite's published well-known key: public knowledge, not a secret.
        private const string EmulatorAccountKey =
            "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

        /// <summary>The storage account name; the concrete type declares its <c>&lt;Store&gt;.Account</c>
        /// key with <c>Account = true</c>.</summary>
        public abstract string Account { get; set; }

        /// <summary>The credential; the concrete type declares it keyless
        /// (<c>[ConfigurationMember(Kind = Secret)]</c>).</summary>
        public abstract Secret ConnectionString { get; set; }

        /// <summary>The <c>&lt;Store&gt;.Account</c> key the concrete type declares.</summary>
        public static string AccountKey<TConfig>() where TConfig : StorageAccountConfiguration
            => ConfigurationMembers.For<TConfig>().AccountMember?.Key
                ?? throw new InvalidOperationException($"{typeof(TConfig).Name} declares no account member.");

        public static bool IsEmulator(string? account)
            => string.Equals(account?.Trim(), EmulatorAccount, StringComparison.OrdinalIgnoreCase);

        /// <summary>The generated secret name for an account's credential.</summary>
        public static string SecretName(string account) => SecretNames.Generate(TypeName, account.Trim(), Purpose);

        /// <summary>The secret name this instance's credential is read by (its account must be set).</summary>
        public string ConnectionStringName => ((IProvideConfiguration)this).SecretReference(Purpose).Name;

        public string? Validate()
        {
            if (string.IsNullOrWhiteSpace(Account) || ConnectionString == null)
                return null;
            var pairs = Parse(ConnectionString.Reveal());
            pairs.TryGetValue("AccountName", out var accountName);
            if (IsEmulator(Account))
            {
                if (accountName != null && !IsEmulator(accountName))
                    return ConnectionStringName;
                var offLoopback = pairs
                    .Where(pair => pair.Key.EndsWith("Endpoint", StringComparison.OrdinalIgnoreCase))
                    .Any(pair => !Uri.TryCreate(pair.Value, UriKind.Absolute, out var endpoint) || !endpoint.IsLoopback);
                return offLoopback ? ConnectionStringName : null;
            }
            return accountName != null && !string.Equals(accountName.Trim(), Account.Trim(), StringComparison.OrdinalIgnoreCase)
                ? ConnectionStringName
                : null;
        }

        /// <summary>The emulator's credential, without consulting the secret store.</summary>
        public TResult ResolveMember<TResult>(ConfigurationMember member, Func<string, string?> readValue,
            Func<string, string, TResult> onResolved,
            Func<TResult> onNotResolved,
            Func<ConfigIssue, TResult> onInvalid)
        {
            if (!member.IsSecret || !IsEmulator(Account))
                return onNotResolved();
            ConfigIssue Issue(string key, ConfigIssueCategory category)
                => new(GetType(), key, ConfigurationMemberKind.Defaulted, category);
            string? explicitString;
            string? portText;
            try
            {
                explicitString = readValue(EastFive.Azure.AppSettings.Persistence.Storage.EmulatorConnectionString);
                portText = readValue(EastFive.Azure.AppSettings.Persistence.Storage.EmulatorPort);
            }
            catch (Exception)
            {
                return onInvalid(Issue(EastFive.Azure.AppSettings.Persistence.Storage.EmulatorPort, ConfigIssueCategory.SourceFailure));
            }
            if (!string.IsNullOrWhiteSpace(explicitString))
                return onResolved(explicitString, EmulatorSource);
            if (string.IsNullOrWhiteSpace(portText))
                return onResolved(EmulatorConnectionString(), EmulatorSource);
            return int.TryParse(portText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var port)
                    && port is >= 1 and <= 65533
                ? onResolved(EmulatorConnectionString(port), EmulatorSource)
                : onInvalid(Issue(EastFive.Azure.AppSettings.Persistence.Storage.EmulatorPort, ConfigIssueCategory.Invalid));
        }

        /// <summary>Azurite's connection string for a blob port (queue and table follow at +1 and +2).</summary>
        public static string EmulatorConnectionString(int blobPort = EmulatorDefaultPort)
            => $"DefaultEndpointsProtocol=http;AccountName={EmulatorAccount};AccountKey={EmulatorAccountKey};"
                + $"BlobEndpoint=http://127.0.0.1:{blobPort}/{EmulatorAccount};"
                + $"QueueEndpoint=http://127.0.0.1:{blobPort + 1}/{EmulatorAccount};"
                + $"TableEndpoint=http://127.0.0.1:{blobPort + 2}/{EmulatorAccount}";

        /// <summary>The account name a connection string carries (<c>AccountName=</c>), or <c>null</c>.</summary>
        public static string? AccountNameOf(string connectionString)
            => Parse(connectionString).TryGetValue("AccountName", out var accountName) ? accountName : null;

        private static Dictionary<string, string> Parse(string connectionString)
            => connectionString.Split(';')
                .Select(part => part.Split('=', 2))
                .Where(part => part.Length == 2 && part[0].Trim().Length > 0)
                .GroupBy(part => part[0].Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First()[1].Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
