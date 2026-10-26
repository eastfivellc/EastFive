#nullable enable
using EastFive.Configuration;
using EastFive.Persistence.Azure.StorageTables.Driver;

namespace EastFive.Azure.Persistence
{
    /// <summary>The default table store, named by <c>EastFive.Azure.StorageTables.Account</c>.</summary>
    [ConfigurationType(StorageAccountConfiguration.TypeName, Label = "Table storage", Description = "The default storage account for tables, blobs and queues.", Scope = ConfigurationScope.Environment)]
    public sealed class StorageTablesConfiguration : StorageAccountConfiguration
    {
        public const string AccountKey = "EastFive.Azure.StorageTables.Account";

        [ConfigurationMember(AccountKey, Account = true, Label = "Account", Description = "Storage account name of the default store; names its connection-string secret.")]
        public override string Account { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Connection string", Description = "Connection string of the default storage account.")]
        public override Secret ConnectionString { get; set; } = default!;

        public AzureTableDriverDynamic LoadDriver() => AzureTableDriverDynamic.FromStorageString(ConnectionString.Reveal());
    }

    /// <summary>The SPA package/asset store, named by <c>EastFive.Azure.Spa.Account</c>.</summary>
    [ConfigurationType(StorageAccountConfiguration.TypeName, Label = "SPA storage", Description = "The storage account and container SPA packages are served from.", Scope = ConfigurationScope.Environment)]
    public sealed class SpaStorageConfiguration : StorageAccountConfiguration
    {
        public const string AccountKey = "EastFive.Azure.Spa.Account";
        public const string ContainerNameKey = "EastFive.Azure.Spa.ContainerName";

        [ConfigurationMember(AccountKey, Account = true, Label = "Account", Description = "Storage account name SPA packages are served from; names its connection-string secret.")]
        public override string Account { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Connection string", Description = "Connection string of the SPA storage account.")]
        public override Secret ConnectionString { get; set; } = default!;

        [ConfigurationMember(ContainerNameKey, Kind = ConfigurationMemberKind.Optional,
            Label = "Container", Description = "Blob container holding the SPA packages.")]
        public string? ContainerName { get; set; }
    }

    /// <summary>The data lake, named by <c>EastFive.Azure.DataLake.Account</c>.</summary>
    [ConfigurationType(StorageAccountConfiguration.TypeName, Label = "Data lake storage", Description = "The storage account the data lake lives in.", Scope = ConfigurationScope.Environment)]
    public sealed class DataLakeStorageConfiguration : StorageAccountConfiguration
    {
        public const string AccountKey = "EastFive.Azure.DataLake.Account";

        [ConfigurationMember(AccountKey, Account = true, Label = "Account", Description = "Storage account name of the data lake; names its connection-string secret.")]
        public override string Account { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Connection string", Description = "Connection string of the data lake storage account.")]
        public override Secret ConnectionString { get; set; } = default!;
    }

    /// <summary>The table driver's HTTP connection pool. Values only; each unset member keeps the
    /// driver's built-in default.</summary>
    [ConfigurationType("StorageTablesTuning", Label = "Table storage connection pool",
        Description = "When the table driver's HTTP connection pool grows, stops growing, and shrinks.", Scope = ConfigurationScope.Environment)]
    public sealed class StorageTablesTuningConfiguration : IProvideConfiguration
    {
        public const string ConnectionCountGrowthStoppingPointKey = "EastFive.Azure.Persistence.StorageTables.ConnectionCountGrowthStoppingPoint";
        public const string ConnectionCountReductionPointKey = "EastFive.Azure.Persistence.StorageTables.ConnectionCountReductionPoint";
        public const string MinimumParallelConnectionsKey = "EastFive.Azure.Persistence.StorageTables.MinimumParallelConnections";

        [ConfigurationMember(ConnectionCountReductionPointKey,
            Kind = ConfigurationMemberKind.Optional, Scope = ConfigurationScope.Shared,
            Label = "Reduction point", Description = "Machine-wide HTTP connection count at which the pool begins to shrink (default 200).")]
        public long? ConnectionCountReductionPoint { get; set; }

        [ConfigurationMember(ConnectionCountGrowthStoppingPointKey,
            Kind = ConfigurationMemberKind.Optional, Scope = ConfigurationScope.Shared,
            Label = "Growth stopping point", Description = "Connection count at which the pool stops growing; below the reduction point (default 150).")]
        public long? ConnectionCountGrowthStoppingPoint { get; set; }

        [ConfigurationMember(MinimumParallelConnectionsKey,
            Kind = ConfigurationMemberKind.Optional, Scope = ConfigurationScope.Shared,
            Label = "Minimum parallel connections", Description = "The pool never shrinks below this size (default 20).")]
        public long? MinimumParallelConnections { get; set; }
    }
}
