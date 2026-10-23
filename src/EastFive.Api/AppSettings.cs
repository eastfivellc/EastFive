using EastFive.Web;

namespace EastFive.Api
{
    [Config]
    public static class AppSettings
    {
        /// <summary>The UUID of the site administration account</summary>
        [System.Obsolete("Read SuperAdminConfiguration.ActorId through typed configuration; its key is SuperAdminConfiguration.ActorIdKey.")]
        public const string ActorIdSuperAdmin = EastFive.Api.SuperAdminConfiguration.ActorIdKey;
        
        /// <summary>The name of the claim that holds the session id</summary>
        [System.Obsolete("Read ClaimTypesConfiguration.SessionIdClaimType through typed configuration; its key is ClaimTypesConfiguration.SessionIdClaimTypeKey.")]
        public const string SessionIdClaimType = EastFive.Api.ClaimTypesConfiguration.SessionIdClaimTypeKey;
        
        /// <summary>The name of the claim that holds the authorization id</summary>
        [System.Obsolete("Read ClaimTypesConfiguration.ActorIdClaimType through typed configuration; its key is ClaimTypesConfiguration.ActorIdClaimTypeKey.")]
        public const string ActorIdClaimType = EastFive.Api.ClaimTypesConfiguration.ActorIdClaimTypeKey;
        


        /// <summary>Default directory scoping of the application</summary>
        [System.Obsolete("Read RoutingConfiguration.DefaultNamespace through typed configuration; its key is RoutingConfiguration.DefaultNamespaceKey.")]
        public const string DefaultNamespace = EastFive.Api.RoutingConfiguration.DefaultNamespaceKey;

        /// <summary>URL of the server.</summary>
        [System.Obsolete("Read RoutingConfiguration.SiteUrl through typed configuration; its key is RoutingConfiguration.SiteUrlKey.")]
        public const string SiteUrl = EastFive.Api.RoutingConfiguration.SiteUrlKey;

        /// <summary>Secret GUID that can be used to make any session a admin session.</summary>
        [System.Obsolete("Read SuperAdminConfiguration.AuthorizationId through typed configuration; its key is SuperAdminConfiguration.AuthorizationIdKey.")]
        public const string AuthorizationIdSuperAdmin = EastFive.Api.SuperAdminConfiguration.AuthorizationIdKey;


        /// <summary>Expiration in minutes for access tokens.</summary>
        [System.Obsolete("Read AccessTokenLifetimeConfiguration.ExpirationInMinutes through typed configuration; its key is AccessTokenLifetimeConfiguration.ExpirationInMinutesKey.")]
        public const string AccessTokenExpirationInMinutes = EastFive.Api.AccessTokenLifetimeConfiguration.ExpirationInMinutesKey;

        /// <summary>Enable E5 Cors handlers.</summary>
        [System.Obsolete("Read CorsConfiguration.Enabled through typed configuration; its key is CorsConfiguration.EnabledKey.")]
        public const string CorsCorrection = EastFive.Api.CorsConfiguration.EnabledKey;

        public static class Postman
        {

            /// <summary>ID of Postman Collection that receives monitoring request items</summary>
            [System.Obsolete("Read PostmanConfiguration.MonitoringCollectionId through typed configuration; its key is PostmanConfiguration.MonitoringCollectionIdKey.")]
            public const string MonitoringCollectionId = EastFive.Api.PostmanConfiguration.MonitoringCollectionIdKey;
        }

        public static class Auth
        {
            [System.Obsolete("Read SuperAdminConfiguration.AllowLocalHostGlobalSecurityRole through typed configuration; its key is SuperAdminConfiguration.AllowLocalHostGlobalSecurityRoleKey.")]
            public static string AllowLocalHostGlobalSecurityRole = EastFive.Api.SuperAdminConfiguration.AllowLocalHostGlobalSecurityRoleKey;
        }
    }
}
