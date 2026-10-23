#nullable enable
using EastFive.Configuration;
using EastFive.Persistence.Azure.StorageTables.Driver;

namespace EastFive.Azure.Persistence
{
    /// <summary>The default table store, named by <c>EastFive.Azure.StorageTables.Account</c>.</summary>
    public sealed class StorageTablesConfiguration : StorageAccountConfiguration
    {
        [ConfigurationMember(EastFive.Azure.AppSettings.Persistence.StorageTables.Account, Account = true)]
        public override string Account { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        public override Secret ConnectionString { get; set; } = default!;

        public AzureTableDriverDynamic LoadDriver() => AzureTableDriverDynamic.FromStorageString(ConnectionString.Reveal());
    }

    /// <summary>The SPA package/asset store, named by <c>EastFive.Azure.Spa.Account</c>.</summary>
    public sealed class SpaStorageConfiguration : StorageAccountConfiguration
    {
        [ConfigurationMember(EastFive.Azure.AppSettings.SPA.SpaStorageAccount, Account = true)]
        public override string Account { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        public override Secret ConnectionString { get; set; } = default!;

        [ConfigurationMember(EastFive.Azure.Persistence.AppSettings.SpaContainer, Kind = ConfigurationMemberKind.Optional,
            Label = "Container", Description = "Blob container holding the SPA packages.")]
        public string? ContainerName { get; set; }
    }

    /// <summary>The data lake, named by <c>EastFive.Azure.DataLake.Account</c>.</summary>
    public sealed class DataLakeStorageConfiguration : StorageAccountConfiguration
    {
        [ConfigurationMember(EastFive.Azure.AppSettings.Persistence.DataLake.Account, Account = true)]
        public override string Account { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        public override Secret ConnectionString { get; set; } = default!;
    }

    /// <summary>The table driver's HTTP connection pool. Values only; each unset member keeps the
    /// driver's built-in default.</summary>
    [ConfigurationType("StorageTablesTuning", Label = "Table storage connection pool",
        Description = "When the table driver's HTTP connection pool grows, stops growing, and shrinks.")]
    public sealed class StorageTablesTuningConfiguration : IProvideConfiguration
    {
        [ConfigurationMember(EastFive.Azure.AppSettings.Persistence.StorageTables.ConnectionCountReductionPoint,
            Kind = ConfigurationMemberKind.Optional, Scope = ConfigurationScope.Shared,
            Label = "Reduction point", Description = "Machine-wide HTTP connection count at which the pool begins to shrink (default 200).")]
        public long? ConnectionCountReductionPoint { get; set; }

        [ConfigurationMember(EastFive.Azure.AppSettings.Persistence.StorageTables.ConnectionCountGrowthStoppingPoint,
            Kind = ConfigurationMemberKind.Optional, Scope = ConfigurationScope.Shared,
            Label = "Growth stopping point", Description = "Connection count at which the pool stops growing; below the reduction point (default 150).")]
        public long? ConnectionCountGrowthStoppingPoint { get; set; }

        [ConfigurationMember(EastFive.Azure.AppSettings.Persistence.StorageTables.MinimumParallelConnections,
            Kind = ConfigurationMemberKind.Optional, Scope = ConfigurationScope.Shared,
            Label = "Minimum parallel connections", Description = "The pool never shrinks below this size (default 20).")]
        public long? MinimumParallelConnections { get; set; }
    }
}
