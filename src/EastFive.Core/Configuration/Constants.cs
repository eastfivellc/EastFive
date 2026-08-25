
using EastFive.Web;

namespace EastFive.Web
{
    [Config]
    public class AppSettings
    {
        [ConfigKeyAttribute("Forces the token to invalidated and a 401 returned if it was generated before a certain time", DeploymentOverrides.Suggested,
                Location = "This is should be date time of the WebUI client publish",
                DeploymentSecurityConcern = false,
                PrivateRepositoryOnly = false,
            MoreInfo = "This may be necessary if the server is trying to force the client to refresh their browser.")]
        public const string TokenForceRefreshTime = "EastFive.Web.TokenForceRefreshTime";

        public const string TokenForceRefreshMessage = "EastFive.Web.TokenForceRefreshMessage";

        [Config]
        public static class KeyVault
        {
            [ConfigKeyAttribute("Location of the KeyVault", DeploymentOverrides.Suggested,
                Location ="Azure Portal -> KeyValue -> Settings -> (I'm making this up)",
                DeploymentSecurityConcern = true)]
            public const string Url = "KeyVault.Url";

            [ConfigKeyAttribute("The KeyValue at the url is multitennant so the client id specifies the tennant",
                DeploymentOverrides.Suggested,
                DeploymentSecurityConcern = true)]
            public const string ClientId = "KeyVault.ClientId";

            [ConfigKeyAttribute("Entra tenant that issued the KeyVault service principal",
                DeploymentOverrides.Suggested,
                DeploymentSecurityConcern = true)]
            public const string TenantId = "KeyVault.TenantId";

            [ConfigKeyAttribute("Used by the application to authenticate to the KeyVault",
                DeploymentOverrides.Suggested,
                DeploymentSecurityConcern = true)]
            public const string ClientSecret = "KeyVault.ClientSecret";
        }
    }
}

namespace EastFive.Security
{
    [Config]
    public class AppSettings
    {
        // Key Signature
        [ConfigKey("Limits scope of provided access for a given security token.",
            DeploymentOverrides.Suggested,
            DeploymentSecurityConcern = true,
            Location = "App dependent")]
        public const string TokenScope = "EastFive.Security.Token.Scope";

        [ConfigKey("Identifies issuer of token (essentially scopes token use).",
            DeploymentOverrides.Suggested,
            DeploymentSecurityConcern = true,
            Location = "App dependent")]
        public const string TokenIssuer = "EastFive.Security.Token.Issuer";

        [ConfigKey("Identifies issuer of token (essentially scopes token use).",
            DeploymentOverrides.Suggested,
            DeploymentSecurityConcern = true,
            PrivateRepositoryOnly = true,
            Location = "Generated RSA key (tool available in Admin Portal)")]
        public const string TokenKey = "EastFive.Security.Token.Key";
        public const string TokenAlgorithm = "EastFive.Security.Token.Algorithm";

        [ConfigKey("Skip validation of tokens.",
            DeploymentOverrides.Mandatory,
            DeploymentSecurityConcern = true,
            PrivateRepositoryOnly = false,
            Location = "Set to true to locally debug using tokens from other servers.")]
        public const string TokensAllValid = "EastFive.Security.Token.AllValid";

        // Voucher tools
        public const string CredentialProviderVoucherKey = "EastFive.Security.CredentialProvider.Voucher.Key";
        public const string CredentialProviderVoucherProviderId = "EastFive.Security.CredentialProvider.Voucher.Provider";
    }
}