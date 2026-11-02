#nullable enable
using System;

using EastFive.Configuration;
using EastFive.Security;

namespace EastFive.Api
{
    /// <summary>The shared API key <c>[ApiSecurity]</c> endpoints accept (query <c>ApiSecurityKey</c>
    /// or the Authorization header). Secret <c>EastFiveApi-ApiKey</c>; absent = no API-key access.</summary>
    [ConfigurationType("EastFiveApi", Label = "API key", Description = "Shared key for [ApiSecurity] endpoints.", Scope = ConfigurationScope.Environment)]
    public sealed class ApiKeyConfiguration : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "API key", Description = "The key callers present to [ApiSecurity] endpoints; absent, API-key access is off.")]
        [SharedSecret(Instruction = "Give the new key to every caller of the [ApiSecurity] endpoints (query ApiSecurityKey or the Authorization header). Callers presenting the previous key are refused once it is saved.")]
        public SharedSecret ApiKey { get; set; } = default!;

        public bool Accepts(string? presented) => ApiKey.Matches(presented);
    }

    /// <summary>A static bearer value that authenticates as the site super-admin. Secret
    /// <c>EastFiveSiteAdmin-Authorization</c>; absent = the back door is closed.</summary>
    [ConfigurationType("EastFiveSiteAdmin", Label = "Site admin authorization",
        Description = "A static Authorization value that is granted the super-admin actor. Leave unset unless required.", Scope = ConfigurationScope.Environment)]
    public sealed class SiteAdminAuthorizationConfiguration : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Authorization value", Description = "The Authorization header value granted the super-admin actor.")]
        [SharedSecret(Instruction = "Anyone holding this value acts as the super-admin. Give it only to the operator tooling that needs it; the previous value is refused once the new one is saved.")]
        public SharedSecret Authorization { get; set; } = default!;

        public bool Accepts(string? presented) => Authorization.Matches(presented);
    }

    /// <summary>The GUID mixed into signed access-token URLs. Secret <c>EastFiveAccessTokens-SigningSecret</c>.</summary>
    [ConfigurationType("EastFiveAccessTokens", Label = "Access-token URL signing",
        Description = "A GUID mixed into the envelope of signed access-token URLs.", Scope = ConfigurationScope.Environment)]
    public sealed class AccessTokenSecretConfiguration : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Description = "A GUID.", Label = "Signing secret")]
        [GenerateGuid(Instruction = "Signed links issued with the previous value stop verifying once the new value is saved and the host restarts; every host that signs or verifies these links needs the same value.")]
        public Secret SigningSecret { get; set; } = default!;

        public Guid SigningGuid => Guid.Parse(SigningSecret.Reveal());

        public string? Validate()
            => SigningSecret == null || Guid.TryParse(SigningSecret.Reveal(), out _)
                ? null
                : ((IProvideConfiguration)this).SecretReference(nameof(SigningSecret)).Name;
    }

    /// <summary>The Postman API key the collection and monitoring endpoints use. Secret <c>Postman-ApiKey</c>.</summary>
    [ConfigurationType("Postman", Label = "Postman", Description = "The Postman API key, and the collection monitored requests are written to.", Scope = ConfigurationScope.Environment)]
    public sealed class PostmanConfiguration : IProvideConfiguration
    {
        public const string MonitoringCollectionIdKey = "EastFive.Api.Postman.MonitoringCollectionId";

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "API key", Location = "Postman | Settings | API keys", Description = "Postman API key used to write collections.")]
        public Secret ApiKey { get; set; } = default!;

        [ConfigurationMember(MonitoringCollectionIdKey, Kind = ConfigurationMemberKind.Optional,
            Label = "Monitoring collection", Description = "Id of the Postman collection that receives monitored requests.")]
        public string? MonitoringCollectionId { get; set; }
    }
}
