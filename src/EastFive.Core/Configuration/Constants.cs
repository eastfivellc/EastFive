
using EastFive.Web;

namespace EastFive.Web
{
    [Config]
    public class AppSettings
    {
        /// <summary>Forces the token to invalidated and a 401 returned if it was generated before a certain time</summary>
        [System.Obsolete("Read TokenSigningConfiguration.ForceRefreshBefore through typed configuration; its key is TokenSigningConfiguration.ForceRefreshBeforeKey.")]
        public const string TokenForceRefreshTime = "EastFive.Web.TokenForceRefreshTime";

        [System.Obsolete("Read TokenSigningConfiguration.ForceRefreshMessage through typed configuration; its key is TokenSigningConfiguration.ForceRefreshMessageKey.")]
        public const string TokenForceRefreshMessage = "EastFive.Web.TokenForceRefreshMessage";

        [Config]
        public static class KeyVault
        {
            /// <summary>Location of the KeyVault</summary>
            [System.Obsolete("No longer read by EastFive: a host names its vaults when it creates KeyVaultSecretStore.")]
            public const string Url = "KeyVault.Url";

            /// <summary>The KeyValue at the url is multitennant so the client id specifies the tennant</summary>
            [System.Obsolete("No longer read by EastFive: a host names its vaults when it creates KeyVaultSecretStore.")]
            public const string ClientId = "KeyVault.ClientId";

            /// <summary>Entra tenant that issued the KeyVault service principal</summary>
            [System.Obsolete("No longer read by EastFive: a host names its vaults when it creates KeyVaultSecretStore.")]
            public const string TenantId = "KeyVault.TenantId";

            /// <summary>Used by the application to authenticate to the KeyVault</summary>
            [System.Obsolete("No longer read by EastFive: a host names its vaults when it creates KeyVaultSecretStore.")]
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
        /// <summary>Limits scope of provided access for a given security token.</summary>
        [System.Obsolete("Read TokenSigningConfiguration.Scope through typed configuration; its key is TokenSigningConfiguration.ScopeKey.")]
        public const string TokenScope = "EastFive.Security.Token.Scope";

        /// <summary>Identifies issuer of token (essentially scopes token use).</summary>
        [System.Obsolete("Read TokenSigningConfiguration.Issuer through typed configuration; its key is TokenSigningConfiguration.IssuerKey.")]
        public const string TokenIssuer = "EastFive.Security.Token.Issuer";

        [System.Obsolete("Read TokenSigningConfiguration.Algorithm through typed configuration; its key is TokenSigningConfiguration.AlgorithmKey.")]
        public const string TokenAlgorithm = "EastFive.Security.Token.Algorithm";

        /// <summary>Skip validation of tokens.</summary>
        [System.Obsolete("Read TokenSigningConfiguration.AllValid through typed configuration; its key is TokenSigningConfiguration.AllValidKey.")]
        public const string TokensAllValid = "EastFive.Security.Token.AllValid";

        // Voucher tools
        [System.Obsolete("Read VoucherConfiguration.ProviderId through typed configuration; its key is VoucherConfiguration.ProviderIdKey.")]
        public const string CredentialProviderVoucherProviderId = EastFive.Security.VoucherConfiguration.ProviderIdKey;
    }
}