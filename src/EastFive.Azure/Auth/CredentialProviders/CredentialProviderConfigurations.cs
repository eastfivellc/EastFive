#nullable enable
using EastFive.Configuration;

namespace EastFive.Azure.Auth.CredentialProviders
{
    /// <summary>The RSA key that signs admin-login redirections. Secret <c>EastFiveAdminLogin-SigningKey</c>.</summary>
    [ConfigurationType("EastFiveAdminLogin", Label = "Admin login", Description = "The RSA key that signs admin-login redirections.", Scope = ConfigurationScope.Environment)]
    public sealed class AdminLoginConfiguration : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Description = "The RSA private key admin-login redirections are signed with.", Label = "Signing key")]
        [EastFive.Security.RsaKey]
        public EastFive.Security.RsaPrivateKey SigningKey { get; set; } = default!;
    }
}
