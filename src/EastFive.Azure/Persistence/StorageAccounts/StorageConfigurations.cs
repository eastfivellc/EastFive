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
    }

    /// <summary>The data lake, named by <c>EastFive.Azure.DataLake.Account</c>.</summary>
    public sealed class DataLakeStorageConfiguration : StorageAccountConfiguration
    {
        [ConfigurationMember(EastFive.Azure.AppSettings.Persistence.DataLake.Account, Account = true)]
        public override string Account { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        public override Secret ConnectionString { get; set; } = default!;
    }
}
