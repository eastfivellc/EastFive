
using EastFive.Web;

namespace EastFive.Web
{
    [Config]
    public class AppSettings
    {
        /// <summary>Forces the token to invalidated and a 401 returned if it was generated before a certain time</summary>
        public const string TokenForceRefreshTime = "EastFive.Web.TokenForceRefreshTime";

        public const string TokenForceRefreshMessage = "EastFive.Web.TokenForceRefreshMessage";

        [Config]
        public static class KeyVault
        {
            /// <summary>Location of the KeyVault</summary>
            public const string Url = "KeyVault.Url";

            /// <summary>The KeyValue at the url is multitennant so the client id specifies the tennant</summary>
            public const string ClientId = "KeyVault.ClientId";

            /// <summary>Entra tenant that issued the KeyVault service principal</summary>
            public const string TenantId = "KeyVault.TenantId";

            /// <summary>Used by the application to authenticate to the KeyVault</summary>
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
        public const string TokenScope = "EastFive.Security.Token.Scope";

        /// <summary>Identifies issuer of token (essentially scopes token use).</summary>
        public const string TokenIssuer = "EastFive.Security.Token.Issuer";

        public const string TokenAlgorithm = "EastFive.Security.Token.Algorithm";

        /// <summary>Skip validation of tokens.</summary>
        public const string TokensAllValid = "EastFive.Security.Token.AllValid";

        // Voucher tools
        public const string CredentialProviderVoucherProviderId = "EastFive.Security.CredentialProvider.Voucher.Provider";
    }
}