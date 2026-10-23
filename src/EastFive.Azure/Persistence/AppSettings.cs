using EastFive.Web;

namespace EastFive.Azure.Persistence
{
    [ConfigAttribute]
    public static class AppSettings
    {
        /// <summary>Default azure spa container name</summary>
        public const string SpaContainer = "EastFive.Azure.Spa.ContainerName";
    }
}
