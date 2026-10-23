using EastFive.Web;

namespace EastFive.Azure.Persistence
{
    [ConfigAttribute]
    public static class AppSettings
    {
        /// <summary>Default azure storage tables connection string</summary>
        public const string Storage = "EastFive.Azure.StorageTables.ConnectionString";


        /// <summary>Default azure spa container name</summary>
        public const string SpaContainer = "EastFive.Azure.Spa.ContainerName";

        public static class Backup
        {
            /// <summary>The assemblies containing backup resources</summary>
            public const string StorageResourceAssemblies = "EastFive.Azure.StorageTables.Backup.StorageResourceAssemblies";

            /// <summary>The default backup destination connection string</summary>
            public const string DefaultDestination = "EastFive.Azure.StorageTables.Backup.Default";

            /// <summary>The Sunday backup destination connection string</summary>
            public const string SundayDestination = "EastFive.Azure.StorageTables.Backup.Sunday";

            /// <summary>The Monday backup destination connection string</summary>
            public const string MondayDestination = "EastFive.Azure.StorageTables.Backup.Monday";

            /// <summary>The Tuesday backup destination connection string</summary>
            public const string TuesdayDestination = "EastFive.Azure.StorageTables.Backup.Tuesday";

            /// <summary>The Wednesday backup destination connection string</summary>
            public const string WednesdayDestination = "EastFive.Azure.StorageTables.Backup.Wednesday";

            /// <summary>The Thursday backup destination connection string</summary>
            public const string ThursdayDestination = "EastFive.Azure.StorageTables.Backup.Thursday";

            /// <summary>The Friday backup destination connection string</summary>
            public const string FridayDestination = "EastFive.Azure.StorageTables.Backup.Friday";

            /// <summary>The Saturday backup destination connection string</summary>
            public const string SaturdayDestination = "EastFive.Azure.StorageTables.Backup.Saturday";

            /// <summary>The Month backup destination connection string</summary>
            public const string MonthDestination = "EastFive.Azure.StorageTables.Backup.Month";

            /// <summary>The Quarter backup destination connection string</summary>
            public const string QuarterDestination = "EastFive.Azure.StorageTables.Backup.Quarter";

        }
    }
}
