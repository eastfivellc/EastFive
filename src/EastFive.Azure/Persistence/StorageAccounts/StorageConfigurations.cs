#nullable enable
using EastFive.Configuration;
using EastFive.Persistence.Azure.StorageTables.Driver;

namespace EastFive.Azure.Persistence
{
    /// <summary>The default table store: <c>EastFive.Azure.StorageTables.Account</c> names it,
    /// <c>EastFive.Azure.StorageTables.ConnectionString</c> is the legacy fallback.</summary>
    public sealed class StorageTablesConfiguration : StorageAccountConfiguration
    {
        [ConfigurationMember(EastFive.Azure.AppSettings.Persistence.StorageTables.Account, Kind = ConfigurationMemberKind.Optional)]
        public override string? Account { get; set; }

        [ConfigurationMember(EastFive.Azure.AppSettings.Persistence.StorageTables.ConnectionStringKey,
            Kind = ConfigurationMemberKind.Secret, Purpose = Purpose)]
        public override Secret ConnectionString { get; set; } = default!;

        public override string ConnectionStringKey => EastFive.Azure.AppSettings.Persistence.StorageTables.ConnectionStringKey;

        public AzureTableDriverDynamic LoadDriver() => AzureTableDriverDynamic.FromStorageString(ConnectionString.Reveal());
    }

    /// <summary>The SPA package/asset store: <c>EastFive.Azure.Spa.Account</c> /
    /// <c>EastFive.Azure.Spa.ConnectionString</c>.</summary>
    public sealed class SpaStorageConfiguration : StorageAccountConfiguration
    {
        [ConfigurationMember(EastFive.Azure.AppSettings.SPA.SpaStorageAccount, Kind = ConfigurationMemberKind.Optional)]
        public override string? Account { get; set; }

        [ConfigurationMember(EastFive.Azure.AppSettings.SPA.SpaStorage, Kind = ConfigurationMemberKind.Secret, Purpose = Purpose)]
        public override Secret ConnectionString { get; set; } = default!;

        public override string ConnectionStringKey => EastFive.Azure.AppSettings.SPA.SpaStorage;
    }

    /// <summary>The data lake: <c>EastFive.Azure.DataLake.Account</c> /
    /// <c>EastFive.Azure.DataLake.ConnectionString</c>.</summary>
    public sealed class DataLakeStorageConfiguration : StorageAccountConfiguration
    {
        [ConfigurationMember(EastFive.Azure.AppSettings.Persistence.DataLake.Account, Kind = ConfigurationMemberKind.Optional)]
        public override string? Account { get; set; }

        [ConfigurationMember(EastFive.Azure.AppSettings.Persistence.DataLake.ConnectionStringKey,
            Kind = ConfigurationMemberKind.Secret, Purpose = Purpose)]
        public override Secret ConnectionString { get; set; } = default!;

        public override string ConnectionStringKey => EastFive.Azure.AppSettings.Persistence.DataLake.ConnectionStringKey;
    }
}
