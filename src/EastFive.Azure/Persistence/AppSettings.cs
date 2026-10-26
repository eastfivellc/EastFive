using EastFive.Web;

namespace EastFive.Azure.Persistence
{
    [ConfigAttribute]
    public static class AppSettings
    {
        /// <summary>Default azure spa container name</summary>
        [System.Obsolete("Read SpaStorageConfiguration.ContainerName through typed configuration; its key is SpaStorageConfiguration.ContainerNameKey.")]
        public const string SpaContainer = EastFive.Azure.Persistence.SpaStorageConfiguration.ContainerNameKey;
    }
}
