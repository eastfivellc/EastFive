#nullable enable
using System;

using EastFive.Configuration;
using EastFive.Persistence.Azure.StorageTables.Driver;

namespace EastFive.Azure.Persistence
{
    /// <summary>
    /// A handle on one store, named by its <see cref="StorageAccountConfiguration"/> TYPE — what the
    /// framework's storage APIs take instead of a configuration key. It resolves through
    /// <see cref="StartupConfiguration"/> (the host loads its stores once), so it never reads a
    /// connection string from the values store and never blocks.
    /// </summary>
    public sealed class StorageAccount : IEquatable<StorageAccount>
    {
        public Type ConfigurationType { get; }

        private StorageAccount(Type configurationType) => ConfigurationType = configurationType;

        public static StorageAccount Of<TConfig>() where TConfig : StorageAccountConfiguration, new() => new(typeof(TConfig));

        public static StorageAccount Of(Type configurationType)
            => typeof(StorageAccountConfiguration).IsAssignableFrom(configurationType)
                ? new StorageAccount(configurationType)
                : throw new ArgumentException($"{configurationType.Name} is not a {nameof(StorageAccountConfiguration)}.", nameof(configurationType));

        /// <summary>The default table store.</summary>
        public static StorageAccount Tables { get; } = Of<StorageTablesConfiguration>();

        public static StorageAccount DataLake { get; } = Of<DataLakeStorageConfiguration>();

        public static StorageAccount Spa { get; } = Of<SpaStorageConfiguration>();

        public TResult Configuration<TResult>(Func<StorageAccountConfiguration, TResult> onLoaded, Func<ConfigIssue, TResult> onUnavailable)
            => StartupConfiguration.Get<TResult>(ConfigurationType,
                config => onLoaded((StorageAccountConfiguration)config), onUnavailable);

        public TResult ConnectionString<TResult>(Func<string, TResult> onLoaded, Func<ConfigIssue, TResult> onUnavailable)
            => Configuration(config => onLoaded(config.ConnectionString.Reveal()), onUnavailable);

        /// <summary>The connection string, or a <see cref="EastFive.Web.ConfigurationException"/> naming the store.</summary>
        public string ConnectionStringOrThrow()
            => ConnectionString(connectionString => connectionString, issue => throw Unavailable(issue));

        /// <summary>The storage account name, or a <see cref="EastFive.Web.ConfigurationException"/> naming the store.</summary>
        public string AccountName
            => Configuration(config => config.Account.Trim(), issue => throw Unavailable(issue));

        public AzureTableDriverDynamic Driver() => AzureTableDriverDynamic.FromStorageString(ConnectionStringOrThrow());

        private EastFive.Web.ConfigurationException Unavailable(ConfigIssue issue)
            => new(issue.Key, ConfigurationType, issue.ToString());

        public bool Equals(StorageAccount? other) => other is not null && other.ConfigurationType == ConfigurationType;
        public override bool Equals(object? obj) => Equals(obj as StorageAccount);
        public override int GetHashCode() => ConfigurationType.GetHashCode();
        public override string ToString() => ConfigurationType.Name;
    }
}
