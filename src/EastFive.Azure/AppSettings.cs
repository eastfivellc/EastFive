using EastFive.Azure.Communications;
using EastFive.Azure.ResourceManagement;
using EastFive.Configuration;
using EastFive.Web;

namespace EastFive.Azure
{
    [Config]
    public static class AppSettings
    {
        [Config]
        public static class Persistence
        {
            [Config]
            public static class StorageTables
            {
                /// <summary>When the total of HTTP connections reaches this point the connection poolb will _begin_ being reduced.</summary>
                public const string ConnectionCountReductionPoint = "EastFive.Azure.Persistence.StorageTables.ConnectionCountReductionPoint";

                /// <summary>When the total of HTTP connections reaches this point the connection pool will stop growing.</summary>
                public const string ConnectionCountGrowthStoppingPoint = "EastFive.Azure.Persistence.StorageTables.ConnectionCountGrowthStoppingPoint";

                /// <summary>Size of the connection pool will not go below this point.</summary>
                public const string MinimumParallelConnections = "EastFive.Azure.Persistence.StorageTables.MinimumParallelConnections";

                /// <summary>Storage ACCOUNT NAME of the default table store (not a secret); its credential is
                /// the secret AzureStorage-{account}-ConnectionString. See <see cref="EastFive.Azure.Persistence.StorageTablesConfiguration"/>.</summary>
                public const string Account = "EastFive.Azure.StorageTables.Account";


            }

            [Config]
            public static class DataLake
            {
                /// <summary>Storage ACCOUNT NAME of the data lake (not a secret); its credential is the
                /// secret AzureStorage-{account}-ConnectionString.</summary>
                public const string Account = "EastFive.Azure.DataLake.Account";
            }

            /// <summary>Keys shared by every <see cref="EastFive.Azure.Persistence.StorageAccountConfiguration"/>.</summary>
            [Config]
            public static class Storage
            {
                /// <summary>Blob port of the local storage emulator (queue and table follow at +1 and +2). Only consulted when a store names the emulator account devstoreaccount1.</summary>
                public const string EmulatorPort = "Azure.Storage.Emulator.Port";

                /// <summary>Reserved: an explicit emulator connection string. Absent, one is fabricated from Azure.Storage.Emulator.Port. Never read from a vault.</summary>
                public const string EmulatorConnectionString = "Azure.Storage.Emulator.ConnectionString";
            }
        }


        [Config]
        public static class SPA
        {
            public const string BuildConfigPath = "EastFive.Azure.SPA.BuildConfigPath";
            public const string IndexHtmlPath = "EastFive.Azure.SPA.IndexHtmlPath";
            public const string SiteLocation = "EastFive.Azure.SpaSiteLocation";

            /// <summary>Expiration in days for immutable spa files.</summary>
            public const string FilesExpirationInDays = "EastFive.Azure.SpaFilesExpirationInDays";

            /// <summary>Enable dynamic serving of the SPA.</summary>
            public const string ServeEnabled = "EastFive.Azure.SpaServeEnabled";

            /// <summary>Storage ACCOUNT NAME of the SPA store; credential is the vault secret Azure-Storage-Account-{name}. Not a secret.</summary>
            public const string SpaStorageAccount = "EastFive.Azure.Spa.Account";

            /// <summary>Comma-separated list of additional SPA package zip blob names (e.g. admin.zip) </summary>
            public const string AdditionalPackages = "EastFive.Azure.Spa.AdditionalPackages";
        }

        [Config]
        public static class ApplicationInsights
        {
            /// <summary>Identifies the application insights endpoint to which data is posted.</summary>
            public const string ConnectionString = "APPLICATIONINSIGHTS_CONNECTION_STRING";

            /// <summary>Identifies the application insights endpoint to which data is posted.</summary>
            public const string InstrumentationKey = "EastFive.Azure.ApplicationInsights.InstrumentationKey";

            /// <summary>Username for API access to Application Insights.</summary>
            public const string ApplicationId = "EastFive.Azure.ApplicationInsights.ApplicationId";


            public const string ApiBase = "EastFive.Azure.ApplicationInsights.ApiBase";

            public const string Path = "EastFive.Azure.ApplicationInsights.Path";


            public const string TeamsAppIdentification = "EastFive.Azure.ApplicationInsights.TeamsAppIdentification";

            public const string TeamsAppImage = "EastFive.Azure.ApplicationInsights.TeamsAppImage";
        }

        [Config]
        public static class Search
        {
            /// <summary>Identifies the search endpoint to use.</summary>
            public const string EndPoint = "EastFive.Azure.Search.SearchServiceEndPoint";

        }

        public const string ClientMinimumVersion = "EastFive.Azure.Modules.ClientMinimumVersion";

        public const string FunctionProcessorServiceBusTriggerName = "EastFive.Azure.Functions.ServiceBusTriggerName";
        public const string FunctionProcessorServiceBusTriggerNamePercent = "%" + FunctionProcessorServiceBusTriggerName + "%";

        public const string CDNEndpointHostname = "EastFive.Azure.CDNEndpointHostname";
        public const string CDNApiRoutePrefix = "EastFive.Azure.CDNApiRoutePrefix";

        [Config]
        public static class CognitiveServices
        {
            /// <summary>Endpoint used by computer vision to analyze image Content.</summary>
            public const string ComputerVisionEndpoint = "EastFive.Azure.CognitiveServices.ComputerVisionEndpoint";

        }

        [Config]
        public static class SAML
        {

            /// <summary>The IdP certificate in base64 format (public key only). </summary>
            public static string GetMetadataLocation(string tag) => $"EastFive.Security.CredentialProvider.SAML.{tag}.MetadataLocation";

            /// <summary>Internal page (relative URI) to send the browser to when the tag's </summary>
            public static string GetLaunchPage(string tag) => $"EastFive.Security.CredentialProvider.SAML.{tag}.LaunchPage";

            /// <summary>The name of the attribute in the SAML assertion whoms value contains a unique key identifying the user. </summary>
            public const string SAMLLoginIdAttributeName = "EastFive.Security.CredentialProvider.SAML.LoginIdAttributeName";

            /// <summary>NameID format for SAML metadata.</summary>
            public const string NameIdFormat = "EastFive.Security.CredentialProvider.SAML.NameIdFormat";

            /// <summary>Service provider signing certificate in base64 format (public cert only).</summary>
            public const string ServiceProviderCertificate = "EastFive.Security.CredentialProvider.SAML.ServiceProviderCertificate";
        }

        [Config]
        public static class Apple
        {
            /// <summary>Used to prove ownership of this domain to Apple.</summary>
            public const string DeveloperSiteAssociation = "EastFive.Azure.Apple.DeveloperSiteAssociation";

            public const string AppleAppSiteAssociationId = "EastFive.Azure.Apple.AppleAppSiteAssociation.AppId";
        }

        [Config]
        public static class AzureADB2C
        {
            /// <summary>Identifies this Tenant to AADB2C</summary>
            public const string Tenant = "EastFive.Azure.AzureADB2C.Tenant";

            /// <summary>Identifies this application (multiple applications per tenant) to an AADB2C application</summary>
            public const string ApplicationId = "EastFive.Azure.AzureADB2C.ApplicationId";

            /// <summary>The audience used for token validation.</summary>
            public const string Audience = "EastFive.Azure.AzureADB2C.Audience";

            /// <summary>The name of the flow for user sign in.</summary>
            public const string SigninFlow = "EastFive.Azure.AzureADB2C.SigninFlow";

            /// <summary>The name of the flow for user sign up.</summary>
            public const string SignupFlow = "EastFive.Azure.AzureADB2C.SignupFlow";

            /// <summary>ID of the client</summary>
            public const string ClientId = "EastFive.Azure.AzureADB2C.ClientId";

            /// <summary>The claim identifier used to determine the login subject key.</summary>
            public const string LoginIdClaimType = "EastFive.Azure.Auth.AzureADB2C.LoginIdClaimType";
        }

        [Config]
        public static class Auth
        {
            public const string Redirections = "EastFive.Azure.Auth.Redirections";
            public const string PauseRedirections = "EastFive.Azure.Auth.PauseRedirections";
            public const string DebugLogin = "EastFive.Azure.Auth.DebugLogin";

            [Config]
            public static class Apple
            {
                /// <summary>ID used to identify the application to apple.</summary>
                public const string ClientId = "EastFive.Apple.ClientId";

                /// <summary>This is the list of audiences that are valid for the Apple JWT. This should include the ClientID.</summary>
                public const string ValidAudiences = "EastFive.Apple.ValidAudiences";
            }

            [Config]
            public static class Google
            {
                /// <summary>ID used to identify the application to Google.</summary>
                public const string ClientId = "EastFive.Google.ClientID";

            }

            [Config]
            public static class Microsoft
            {
                /// <summary>ID used to identify the application to MS.</summary>
                public const string ClientId = "EastFive.Microsoft.ClientID";

            }

            [Config]
            public static class Ping
            {
                public const string PingIdentityAthenaRestAuthUsername = "EastFive.Security.LoginProvider.PingIdentity.Athena.RestAuthUsername";
            }

            [Config]
            public static class Salesforce
            {
                /// <summary>URL used to identify the application to Salesforce.</summary>
                public const string DocumentDiscoveryUrl = "EastFive.Salesforce.DocumentDiscoveryUrl";

                /// <summary>ID used to identify the application to Salesforce.</summary>
                public const string ConsumerKey = "EastFive.Salesforce.ConsumerKey";

            }

            [Config]
            public static class TokenCredential
            {
                /// <summary>
                /// The email address and name from which a token credential is sent.
                /// </summary>
                public const string FromEmail = "EastFive.Azure.Auth.TokenCredential.Email.FromEmail";
                public const string FromName = "EastFive.Azure.Auth.TokenCredential.Email.FromName";
                /// <summary>
                /// Subject for token credntial email.
                /// </summary>
                public const string Subject = "EastFive.Azure.Auth.TokenCredential.Email.Subject";
            }

            /// <summary>Link that is sent (emailed) to the user to login to the application</summary>
            public const string LandingPage = "EastFive.Security.SessionServer.RouteDefinitions.LandingPage";

            public const string OpenApiCollectionName = "EastFive.Azure.Auth";

            public const string TokenExpirationInMinutes = "EastFive.Security.SessionServer.tokenExpirationInMinutes";
            public const string TokenExpirationTimeOfDay = "EastFive.Security.TokenExpirationTimeOfDay"; //ex: "02:00:00 -05:00"

            public static string AllowLocalHostGlobalSuperAdmin = "EastFive.Azure.Auth.AllowLocalHostGlobalSuperAdmin";
        }

        [Config]
        public static class Communications
        {
            /// <summary>The startup-loaded <see cref="AzureCommunicationsConfiguration"/>.</summary>
            public static IProvideCommunicationsSettings Default => AzureCommunicationsConfiguration.Startup;

            public const string MuteEmailToAddress = "EastFive.SendGrid.MuteToAddress";
            public const string BccAllAddresses = "EastFive.SendGrid.BlindCopyAllAddresses";
        }

        // [Config]
        // public static class Communications
        // {
        //     // public static class SendGrid
        //     // {
        //     //             //     //     public const string ApiKey = "EastFive.SendGrid.ApiKey";
        //     // }

        //             //     public const string ConnectionString = "EastFive.Azure.Communications.ConnectionString";

        //     // [Config]
        //     // public static class Email
        //     // {
        //     //             //     //     public const string SenderDomain = "EastFive.Azure.Communications.Email.SenderDomain";
        //     // }

        // }

        [Config]
        public static class AzureClientApplications
        {
            /// <summary>The startup-loaded <see cref="AzureClientApplicationConfiguration"/>.</summary>
            public static IProvideArmClientSettings Default => AzureClientApplicationConfiguration.Startup;
        }

        /// <summary>Azure Tenant ID.</summary>
        public const string TenantId = "EastFive.Azure.TenantId";

        /// <summary>Azure Client ID.</summary>
        public const string ClientId = "EastFive.Azure.ClientId";
    }
}
