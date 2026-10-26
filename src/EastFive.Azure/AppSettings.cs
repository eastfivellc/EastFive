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
                [System.Obsolete("Read StorageTablesTuningConfiguration.ConnectionCountReductionPoint through typed configuration; its key is StorageTablesTuningConfiguration.ConnectionCountReductionPointKey.")]
                public const string ConnectionCountReductionPoint = EastFive.Azure.Persistence.StorageTablesTuningConfiguration.ConnectionCountReductionPointKey;

                /// <summary>When the total of HTTP connections reaches this point the connection pool will stop growing.</summary>
                [System.Obsolete("Read StorageTablesTuningConfiguration.ConnectionCountGrowthStoppingPoint through typed configuration; its key is StorageTablesTuningConfiguration.ConnectionCountGrowthStoppingPointKey.")]
                public const string ConnectionCountGrowthStoppingPoint = EastFive.Azure.Persistence.StorageTablesTuningConfiguration.ConnectionCountGrowthStoppingPointKey;

                /// <summary>Size of the connection pool will not go below this point.</summary>
                [System.Obsolete("Read StorageTablesTuningConfiguration.MinimumParallelConnections through typed configuration; its key is StorageTablesTuningConfiguration.MinimumParallelConnectionsKey.")]
                public const string MinimumParallelConnections = EastFive.Azure.Persistence.StorageTablesTuningConfiguration.MinimumParallelConnectionsKey;

                /// <summary>Storage ACCOUNT NAME of the default table store (not a secret); its credential is
                /// the secret AzureStorage-{account}-ConnectionString. See <see cref="EastFive.Azure.Persistence.StorageTablesConfiguration"/>.</summary>
                [System.Obsolete("Read StorageTablesConfiguration.Account through typed configuration; its key is StorageTablesConfiguration.AccountKey.")]
                public const string Account = EastFive.Azure.Persistence.StorageTablesConfiguration.AccountKey;


            }

            [Config]
            public static class DataLake
            {
                /// <summary>Storage ACCOUNT NAME of the data lake (not a secret); its credential is the
                /// secret AzureStorage-{account}-ConnectionString.</summary>
                [System.Obsolete("Read DataLakeStorageConfiguration.Account through typed configuration; its key is DataLakeStorageConfiguration.AccountKey.")]
                public const string Account = EastFive.Azure.Persistence.DataLakeStorageConfiguration.AccountKey;
            }

            /// <summary>Keys shared by every <see cref="EastFive.Azure.Persistence.StorageAccountConfiguration"/>.</summary>
            [Config]
            public static class Storage
            {
                /// <summary>Blob port of the local storage emulator (queue and table follow at +1 and +2). Only consulted when a store names the emulator account devstoreaccount1.</summary>
                [System.Obsolete("Use StorageAccountConfiguration.EmulatorPortKey.")]
                public const string EmulatorPort = EastFive.Azure.Persistence.StorageAccountConfiguration.EmulatorPortKey;

                /// <summary>Reserved: an explicit emulator connection string. Absent, one is fabricated from Azure.Storage.Emulator.Port. Never read from a vault.</summary>
                [System.Obsolete("Use StorageAccountConfiguration.EmulatorConnectionStringKey.")]
                public const string EmulatorConnectionString = EastFive.Azure.Persistence.StorageAccountConfiguration.EmulatorConnectionStringKey;
            }
        }


        [Config]
        public static class SPA
        {
            [System.Obsolete("Read SpaConfiguration.BuildConfigPath through typed configuration; its key is SpaConfiguration.BuildConfigPathKey.")]
            public const string BuildConfigPath = EastFive.Azure.SpaConfiguration.BuildConfigPathKey;
            [System.Obsolete("Read SpaConfiguration.IndexHtmlPath through typed configuration; its key is SpaConfiguration.IndexHtmlPathKey.")]
            public const string IndexHtmlPath = EastFive.Azure.SpaConfiguration.IndexHtmlPathKey;
            [System.Obsolete("Read SpaConfiguration.SiteLocation through typed configuration; its key is SpaConfiguration.SiteLocationKey.")]
            public const string SiteLocation = EastFive.Azure.SpaConfiguration.SiteLocationKey;

            /// <summary>Expiration in days for immutable spa files.</summary>
            [System.Obsolete("Read SpaConfiguration.FilesExpirationInDays through typed configuration; its key is SpaConfiguration.FilesExpirationInDaysKey.")]
            public const string FilesExpirationInDays = EastFive.Azure.SpaConfiguration.FilesExpirationInDaysKey;

            /// <summary>Enable dynamic serving of the SPA.</summary>
            [System.Obsolete("Read SpaConfiguration.ServeEnabled through typed configuration; its key is SpaConfiguration.ServeEnabledKey.")]
            public const string ServeEnabled = EastFive.Azure.SpaConfiguration.ServeEnabledKey;

            /// <summary>Storage ACCOUNT NAME of the SPA store; credential is the vault secret Azure-Storage-Account-{name}. Not a secret.</summary>
            [System.Obsolete("Read SpaStorageConfiguration.Account through typed configuration; its key is SpaStorageConfiguration.AccountKey.")]
            public const string SpaStorageAccount = EastFive.Azure.Persistence.SpaStorageConfiguration.AccountKey;

            /// <summary>Comma-separated list of additional SPA package zip blob names (e.g. admin.zip) </summary>
            [System.Obsolete("Read SpaConfiguration.AdditionalPackages through typed configuration; its key is SpaConfiguration.AdditionalPackagesKey.")]
            public const string AdditionalPackages = EastFive.Azure.SpaConfiguration.AdditionalPackagesKey;
        }

        [Config]
        public static class ApplicationInsights
        {
            /// <summary>Identifies the application insights endpoint to which data is posted.</summary>
            [System.Obsolete("Read ApplicationInsightsTelemetryConfiguration.ConnectionString through typed configuration; its key is ApplicationInsightsTelemetryConfiguration.ConnectionStringKey.")]
            public const string ConnectionString = EastFive.Azure.Monitoring.ApplicationInsightsTelemetryConfiguration.ConnectionStringKey;

            /// <summary>Identifies the application insights endpoint to which data is posted.</summary>
            [System.Obsolete("Read ApplicationInsightsTelemetryConfiguration.InstrumentationKey through typed configuration; its key is ApplicationInsightsTelemetryConfiguration.InstrumentationKeyKey.")]
            public const string InstrumentationKey = EastFive.Azure.Monitoring.ApplicationInsightsTelemetryConfiguration.InstrumentationKeyKey;

            /// <summary>Username for API access to Application Insights.</summary>
            [System.Obsolete("Read ApplicationInsightsQueryConfiguration.ApplicationId through typed configuration; its key is ApplicationInsightsQueryConfiguration.ApplicationIdKey.")]
            public const string ApplicationId = EastFive.Azure.ApplicationInsightsQueryConfiguration.ApplicationIdKey;


            [System.Obsolete("No longer read by EastFive.")]
            public const string ApiBase = "EastFive.Azure.ApplicationInsights.ApiBase";

            [System.Obsolete("No longer read by EastFive.")]
            public const string Path = "EastFive.Azure.ApplicationInsights.Path";


            [System.Obsolete("Read TeamsNotificationCardConfiguration.AppIdentification through typed configuration; its key is TeamsNotificationCardConfiguration.AppIdentificationKey.")]
            public const string TeamsAppIdentification = EastFive.Azure.Monitoring.TeamsNotificationCardConfiguration.AppIdentificationKey;

            [System.Obsolete("Read TeamsNotificationCardConfiguration.AppImage through typed configuration; its key is TeamsNotificationCardConfiguration.AppImageKey.")]
            public const string TeamsAppImage = EastFive.Azure.Monitoring.TeamsNotificationCardConfiguration.AppImageKey;
        }

        [Config]
        public static class Search
        {
            /// <summary>Identifies the search endpoint to use.</summary>
            [System.Obsolete("Read SearchConfiguration.EndPoint through typed configuration; its key is SearchConfiguration.EndPointKey.")]
            public const string EndPoint = EastFive.Azure.SearchConfiguration.EndPointKey;

        }

        [System.Obsolete("Read SpaConfiguration.ClientMinimumVersion through typed configuration; its key is SpaConfiguration.ClientMinimumVersionKey.")]
        public const string ClientMinimumVersion = EastFive.Azure.SpaConfiguration.ClientMinimumVersionKey;

        [System.Obsolete("Read ServiceBusConfiguration.FunctionProcessorTriggerName through typed configuration; its key is ServiceBusConfiguration.FunctionProcessorTriggerNameKey.")]
        public const string FunctionProcessorServiceBusTriggerName = EastFive.Azure.ServiceBusConfiguration.FunctionProcessorTriggerNameKey;
        [System.Obsolete("Use ServiceBusConfiguration.FunctionProcessorTriggerNameBinding.")]
        public const string FunctionProcessorServiceBusTriggerNamePercent = EastFive.Azure.ServiceBusConfiguration.FunctionProcessorTriggerNameBinding;

        [System.Obsolete("Read CdnConfiguration.EndpointHostname through typed configuration; its key is CdnConfiguration.EndpointHostnameKey.")]
        public const string CDNEndpointHostname = EastFive.Azure.CdnConfiguration.EndpointHostnameKey;
        [System.Obsolete("Read CdnConfiguration.ApiRoutePrefix through typed configuration; its key is CdnConfiguration.ApiRoutePrefixKey.")]
        public const string CDNApiRoutePrefix = EastFive.Azure.CdnConfiguration.ApiRoutePrefixKey;

        [Config]
        public static class CognitiveServices
        {
            /// <summary>Endpoint used by computer vision to analyze image Content.</summary>
            [System.Obsolete("Read ComputerVisionConfiguration.Endpoint through typed configuration; its key is ComputerVisionConfiguration.EndpointKey.")]
            public const string ComputerVisionEndpoint = EastFive.Azure.ComputerVisionConfiguration.EndpointKey;

        }

        [Config]
        public static class SAML
        {

            /// <summary>The IdP certificate in base64 format (public key only). </summary>
            [System.Obsolete("Read SamlTagConfiguration.MetadataLocation through typed configuration; its key is SamlTagConfiguration.MetadataLocationKey(tag).")]
            public static string GetMetadataLocation(string tag) => EastFive.Azure.Auth.SamlTagConfiguration.MetadataLocationKey(tag);

            /// <summary>Internal page (relative URI) to send the browser to when the tag's </summary>
            [System.Obsolete("Read SamlTagConfiguration.LaunchPage through typed configuration; its key is SamlTagConfiguration.LaunchPageKey(tag).")]
            public static string GetLaunchPage(string tag) => EastFive.Azure.Auth.SamlTagConfiguration.LaunchPageKey(tag);

            /// <summary>The name of the attribute in the SAML assertion whoms value contains a unique key identifying the user. </summary>
            [System.Obsolete("Read SamlConfiguration.LoginIdAttributeName through typed configuration; its key is SamlConfiguration.LoginIdAttributeNameKey.")]
            public const string SAMLLoginIdAttributeName = EastFive.Azure.Auth.SamlConfiguration.LoginIdAttributeNameKey;

            /// <summary>NameID format for SAML metadata.</summary>
            [System.Obsolete("Read SamlConfiguration.NameIdFormat through typed configuration; its key is SamlConfiguration.NameIdFormatKey.")]
            public const string NameIdFormat = EastFive.Azure.Auth.SamlConfiguration.NameIdFormatKey;

            /// <summary>Service provider signing certificate in base64 format (public cert only).</summary>
            [System.Obsolete("Read SamlConfiguration.ServiceProviderCertificate through typed configuration; its key is SamlConfiguration.ServiceProviderCertificateKey.")]
            public const string ServiceProviderCertificate = EastFive.Azure.Auth.SamlConfiguration.ServiceProviderCertificateKey;
        }

        [Config]
        public static class Apple
        {
            /// <summary>Used to prove ownership of this domain to Apple.</summary>
            [System.Obsolete("Read AppleConfiguration.DeveloperSiteAssociation through typed configuration; its key is AppleConfiguration.DeveloperSiteAssociationKey.")]
            public const string DeveloperSiteAssociation = EastFive.Azure.Auth.AppleConfiguration.DeveloperSiteAssociationKey;

            [System.Obsolete("Read AppleConfiguration.AppSiteAssociationId through typed configuration; its key is AppleConfiguration.AppSiteAssociationIdKey.")]
            public const string AppleAppSiteAssociationId = EastFive.Azure.Auth.AppleConfiguration.AppSiteAssociationIdKey;
        }

        [Config]
        public static class AzureADB2C
        {
            /// <summary>Identifies this Tenant to AADB2C</summary>
            [System.Obsolete("Read AzureADB2CGraphConfiguration.Tenant through typed configuration; its key is AzureADB2CGraphConfiguration.TenantKey.")]
            public const string Tenant = EastFive.Azure.Auth.AzureADB2CGraphConfiguration.TenantKey;

            /// <summary>Identifies this application (multiple applications per tenant) to an AADB2C application</summary>
            [System.Obsolete("Read AzureADB2CGraphConfiguration.ApplicationId through typed configuration; its key is AzureADB2CGraphConfiguration.ApplicationIdKey.")]
            public const string ApplicationId = EastFive.Azure.Auth.AzureADB2CGraphConfiguration.ApplicationIdKey;

            /// <summary>The audience used for token validation.</summary>
            [System.Obsolete("Read AzureADB2CGraphConfiguration.Audience through typed configuration; its key is AzureADB2CGraphConfiguration.AudienceKey.")]
            public const string Audience = EastFive.Azure.Auth.AzureADB2CGraphConfiguration.AudienceKey;

            /// <summary>The name of the flow for user sign in.</summary>
            [System.Obsolete("Read AzureADB2CGraphConfiguration.SigninFlow through typed configuration; its key is AzureADB2CGraphConfiguration.SigninFlowKey.")]
            public const string SigninFlow = EastFive.Azure.Auth.AzureADB2CGraphConfiguration.SigninFlowKey;

            /// <summary>The name of the flow for user sign up.</summary>
            [System.Obsolete("Read AzureADB2CGraphConfiguration.SignupFlow through typed configuration; its key is AzureADB2CGraphConfiguration.SignupFlowKey.")]
            public const string SignupFlow = EastFive.Azure.Auth.AzureADB2CGraphConfiguration.SignupFlowKey;

            /// <summary>ID of the client</summary>
            [System.Obsolete("Read AzureADB2CGraphConfiguration.ClientId through typed configuration; its key is AzureADB2CGraphConfiguration.ClientIdKey.")]
            public const string ClientId = EastFive.Azure.Auth.AzureADB2CGraphConfiguration.ClientIdKey;

            /// <summary>The claim identifier used to determine the login subject key.</summary>
            [System.Obsolete("Read AzureADB2CGraphConfiguration.LoginIdClaimType through typed configuration; its key is AzureADB2CGraphConfiguration.LoginIdClaimTypeKey.")]
            public const string LoginIdClaimType = EastFive.Azure.Auth.AzureADB2CGraphConfiguration.LoginIdClaimTypeKey;
        }

        [Config]
        public static class Auth
        {
            [System.Obsolete("Read SessionServerConfiguration.Redirections through typed configuration; its key is SessionServerConfiguration.RedirectionsKey.")]
            public const string Redirections = EastFive.Azure.Auth.SessionServerConfiguration.RedirectionsKey;
            [System.Obsolete("Read SessionServerConfiguration.PauseRedirections through typed configuration; its key is SessionServerConfiguration.PauseRedirectionsKey.")]
            public const string PauseRedirections = EastFive.Azure.Auth.SessionServerConfiguration.PauseRedirectionsKey;
            [System.Obsolete("Read SessionServerConfiguration.DebugLogin through typed configuration; its key is SessionServerConfiguration.DebugLoginKey.")]
            public const string DebugLogin = EastFive.Azure.Auth.SessionServerConfiguration.DebugLoginKey;

            [Config]
            public static class Apple
            {
                /// <summary>ID used to identify the application to apple.</summary>
                [System.Obsolete("Read AppleConfiguration.ClientId through typed configuration; its key is AppleConfiguration.ClientIdKey.")]
                public const string ClientId = EastFive.Azure.Auth.AppleConfiguration.ClientIdKey;

                /// <summary>This is the list of audiences that are valid for the Apple JWT. This should include the ClientID.</summary>
                [System.Obsolete("Read AppleConfiguration.ValidAudiences through typed configuration; its key is AppleConfiguration.ValidAudiencesKey.")]
                public const string ValidAudiences = EastFive.Azure.Auth.AppleConfiguration.ValidAudiencesKey;
            }

            [Config]
            public static class Google
            {
                /// <summary>ID used to identify the application to Google.</summary>
                [System.Obsolete("Read GoogleLoginConfiguration.ClientId through typed configuration; its key is GoogleLoginConfiguration.ClientIdKey.")]
                public const string ClientId = EastFive.Azure.Auth.GoogleLoginConfiguration.ClientIdKey;

            }

            [Config]
            public static class Microsoft
            {
                /// <summary>ID used to identify the application to MS.</summary>
                [System.Obsolete("Read MicrosoftLoginConfiguration.ClientId through typed configuration; its key is MicrosoftLoginConfiguration.ClientIdKey.")]
                public const string ClientId = EastFive.Azure.Auth.MicrosoftLoginConfiguration.ClientIdKey;

            }

            [Config]
            public static class Ping
            {
                [System.Obsolete("No longer read by EastFive.")]
                public const string PingIdentityAthenaRestAuthUsername = "EastFive.Security.LoginProvider.PingIdentity.Athena.RestAuthUsername";
            }

            [Config]
            public static class Salesforce
            {
                /// <summary>URL used to identify the application to Salesforce.</summary>
                [System.Obsolete("Read SalesforceConfiguration.DiscoveryDocument through typed configuration; its key is SalesforceConfiguration.DiscoveryDocumentKey.")]
                public const string DocumentDiscoveryUrl = EastFive.Azure.Auth.SalesforceConfiguration.DiscoveryDocumentKey;

                /// <summary>ID used to identify the application to Salesforce.</summary>
                [System.Obsolete("Read SalesforceConfiguration.ConsumerKey through typed configuration; its key is SalesforceConfiguration.ConsumerKeyKey.")]
                public const string ConsumerKey = EastFive.Azure.Auth.SalesforceConfiguration.ConsumerKeyKey;

            }

            [Config]
            public static class TokenCredential
            {
                /// <summary>
                /// The email address and name from which a token credential is sent.
                /// </summary>
                [System.Obsolete("No longer read by EastFive.")]
                public const string FromEmail = "EastFive.Azure.Auth.TokenCredential.Email.FromEmail";
                [System.Obsolete("No longer read by EastFive.")]
                public const string FromName = "EastFive.Azure.Auth.TokenCredential.Email.FromName";
                /// <summary>
                /// Subject for token credntial email.
                /// </summary>
                [System.Obsolete("No longer read by EastFive.")]
                public const string Subject = "EastFive.Azure.Auth.TokenCredential.Email.Subject";
            }

            /// <summary>Link that is sent (emailed) to the user to login to the application</summary>
            [System.Obsolete("Read SessionServerConfiguration.LandingPage through typed configuration; its key is SessionServerConfiguration.LandingPageKey.")]
            public const string LandingPage = EastFive.Azure.Auth.SessionServerConfiguration.LandingPageKey;

            public const string OpenApiCollectionName = "EastFive.Azure.Auth";

            [System.Obsolete("Read SessionServerConfiguration.TokenExpirationInMinutes through typed configuration; its key is SessionServerConfiguration.TokenExpirationInMinutesKey.")]
            public const string TokenExpirationInMinutes = EastFive.Azure.Auth.SessionServerConfiguration.TokenExpirationInMinutesKey;
            [System.Obsolete("Read SessionServerConfiguration.TokenExpirationTimeOfDay through typed configuration; its key is SessionServerConfiguration.TokenExpirationTimeOfDayKey.")]
            public const string TokenExpirationTimeOfDay = EastFive.Azure.Auth.SessionServerConfiguration.TokenExpirationTimeOfDayKey; //ex: "02:00:00 -05:00"

            [System.Obsolete("Read SuperAdminConfiguration.AllowLocalHostGlobalSuperAdmin through typed configuration; its key is SuperAdminConfiguration.AllowLocalHostGlobalSuperAdminKey.")]
            public static string AllowLocalHostGlobalSuperAdmin = EastFive.Api.SuperAdminConfiguration.AllowLocalHostGlobalSuperAdminKey;
        }

        [Config]
        public static class Communications
        {
            /// <summary>The startup-loaded <see cref="AzureCommunicationsConfiguration"/>.</summary>
            public static IProvideCommunicationsSettings Default => AzureCommunicationsConfiguration.Startup;

            [System.Obsolete("Read SendGridConfiguration.MuteEmailToAddress through typed configuration; its key is SendGridConfiguration.MuteEmailToAddressKey.")]
            public const string MuteEmailToAddress = EastFive.Azure.Communications.SendGridConfiguration.MuteEmailToAddressKey;
            [System.Obsolete("Read SendGridConfiguration.BccAllAddresses through typed configuration; its key is SendGridConfiguration.BccAllAddressesKey.")]
            public const string BccAllAddresses = EastFive.Azure.Communications.SendGridConfiguration.BccAllAddressesKey;
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
        [System.Obsolete("Read AzureClientApplicationConfiguration.TenantId through typed configuration; its key is AzureClientApplicationConfiguration.TenantIdKey.")]
        public const string TenantId = EastFive.Azure.ResourceManagement.AzureClientApplicationConfiguration.TenantIdKey;

        /// <summary>Azure Client ID.</summary>
        [System.Obsolete("Read AzureClientApplicationConfiguration.ClientId through typed configuration; its key is AzureClientApplicationConfiguration.ClientIdKey.")]
        public const string ClientId = EastFive.Azure.ResourceManagement.AzureClientApplicationConfiguration.ClientIdKey;
    }
}
