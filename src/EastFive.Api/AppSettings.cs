using EastFive.Web;

namespace EastFive.Api
{
    [Config]
    public static class AppSettings
    {
        /// <summary>The UUID of the site administration account</summary>
        public const string ActorIdSuperAdmin = "EastFive.Api.Security.SiteAdminAccountId";
        
        /// <summary>The name of the claim that holds the session id</summary>
        public const string SessionIdClaimType = "EastFive.Api.Security.SessionIdClaimType";
        
        /// <summary>The name of the claim that holds the authorization id</summary>
        public const string ActorIdClaimType = "EastFive.Api.Security.AccountIdClaimType";
        


        /// <summary>Default directory scoping of the application</summary>
        public const string DefaultNamespace = "EastFive.Api.DefaultNamespace";

        /// <summary>URL of the server.</summary>
        public const string SiteUrl = "EastFive.Api.SiteUrl";

        /// <summary>Secret GUID that can be used to make any session a admin session.</summary>
        public const string AuthorizationIdSuperAdmin = "EastFive.Api.AuthorizationIdSuperAdmin";


        /// <summary>Expiration in minutes for access tokens.</summary>
        public const string AccessTokenExpirationInMinutes = "EastFive.Api.AccessTokenExpirationInMinutes";

        /// <summary>Enable E5 Cors handlers.</summary>
        public const string CorsCorrection = "EastFive.Api.CorsCorrection";

        public static class Postman
        {

            /// <summary>ID of Postman Collection that receives monitoring request items</summary>
            public const string MonitoringCollectionId = "EastFive.Api.Postman.MonitoringCollectionId";
        }

        public static class Auth
        {
            public static string AllowLocalHostGlobalSecurityRole = "EastFive.Api.Auth.AllowLocalHostGlobalSecurityRole";
        }
    }
}
