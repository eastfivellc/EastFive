#nullable enable
using System;

using EastFive.Configuration;

namespace EastFive.Api
{
    /// <summary>The shared API key <c>[ApiSecurity]</c> endpoints accept (query <c>ApiSecurityKey</c>
    /// or the Authorization header). Secret <c>EastFiveApi-ApiKey</c>; absent = no API-key access.</summary>
    [ConfigurationType("EastFiveApi", Label = "API key", Description = "Shared key for [ApiSecurity] endpoints.")]
    public sealed class ApiKeyConfiguration : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "API key")]
        public Secret ApiKey { get; set; } = default!;

        public bool Accepts(string? presented)
            => !string.IsNullOrEmpty(presented) && string.Equals(presented, ApiKey.Reveal(), StringComparison.Ordinal);
    }

    /// <summary>A static bearer value that authenticates as the site super-admin. Secret
    /// <c>EastFiveSiteAdmin-Authorization</c>; absent = the back door is closed.</summary>
    [ConfigurationType("EastFiveSiteAdmin", Label = "Site admin authorization",
        Description = "A static Authorization value that is granted the super-admin actor. Leave unset unless required.")]
    public sealed class SiteAdminAuthorizationConfiguration : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        public Secret Authorization { get; set; } = default!;

        public bool Accepts(string? presented)
            => !string.IsNullOrEmpty(presented) && string.Equals(presented, Authorization.Reveal(), StringComparison.Ordinal);
    }

    /// <summary>The GUID mixed into signed access-token URLs. Secret <c>EastFiveAccessTokens-SigningSecret</c>.</summary>
    [ConfigurationType("EastFiveAccessTokens", Label = "Access-token URL signing",
        Description = "A GUID mixed into the envelope of signed access-token URLs.")]
    public sealed class AccessTokenSecretConfiguration : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Description = "A GUID.")]
        public Secret SigningSecret { get; set; } = default!;

        public Guid SigningGuid => Guid.Parse(SigningSecret.Reveal());

        public string? Validate()
            => SigningSecret == null || Guid.TryParse(SigningSecret.Reveal(), out _)
                ? null
                : ((IProvideConfiguration)this).SecretReference(nameof(SigningSecret)).Name;
    }

    /// <summary>The Postman API key the collection and monitoring endpoints use. Secret <c>Postman-ApiKey</c>.</summary>
    [ConfigurationType("Postman", Label = "Postman")]
    public sealed class PostmanConfiguration : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "API key", Location = "Postman | Settings | API keys")]
        public Secret ApiKey { get; set; } = default!;

        [ConfigurationMember(AppSettings.Postman.MonitoringCollectionId, Kind = ConfigurationMemberKind.Optional,
            Label = "Monitoring collection", Description = "Id of the Postman collection that receives monitored requests.")]
        public string? MonitoringCollectionId { get; set; }
    }
}
