#nullable enable
using System;
using System.Linq;

using EastFive.Configuration;

namespace EastFive.Azure.Auth
{
    /// <summary>EastFive's own sessions and logins: lifetimes, landing page, redirections.</summary>
    [ConfigurationType("EastFiveSessions", Label = "Sessions and logins",
        Description = "How long sessions last, where logins land, and how login redirections behave.", Scope = ConfigurationScope.Environment)]
    public sealed class SessionServerConfiguration : IProvideConfiguration
    {
        public const string DebugLoginKey = "EastFive.Azure.Auth.DebugLogin";
        public const string LandingPageKey = "EastFive.Security.SessionServer.RouteDefinitions.LandingPage";
        public const string PauseRedirectionsKey = "EastFive.Azure.Auth.PauseRedirections";
        public const string RedirectionsKey = "EastFive.Azure.Auth.Redirections";
        public const string TokenExpirationInMinutesKey = "EastFive.Security.SessionServer.tokenExpirationInMinutes";
        public const string TokenExpirationTimeOfDayKey = "EastFive.Security.TokenExpirationTimeOfDay";

        [ConfigurationMember(TokenExpirationTimeOfDayKey, Kind = ConfigurationMemberKind.Optional,
            Label = "Expire at time of day", Description = "Sessions end at this time of day (e.g. \"02:00:00 -05:00\"); wins over the lifetime in minutes.")]
        public string? TokenExpirationTimeOfDay { get; set; }

        [ConfigurationMember(TokenExpirationInMinutesKey, Kind = ConfigurationMemberKind.Optional,
            Label = "Session lifetime (minutes)", Description = "Minutes a session lasts when no time of day is set.")]
        public double? TokenExpirationInMinutes { get; set; }

        [ConfigurationMember(LandingPageKey, Kind = ConfigurationMemberKind.Optional,
            Label = "Landing page", Description = "Where a completed login lands when the request names no return address.")]
        public string? LandingPage { get; set; }

        [ConfigurationMember(PauseRedirectionsKey, Kind = ConfigurationMemberKind.Defaulted, Default = "false",
            Label = "Pause logins", Description = "Refuse every login with a 'logins are paused' message.")]
        public bool PauseRedirections { get; set; }

        [ConfigurationMember(RedirectionsKey, Kind = ConfigurationMemberKind.Optional,
            Label = "Redirections", Description = "JSON redirection rules for the redirection modules.")]
        public string? Redirections { get; set; }

        [ConfigurationMember(DebugLoginKey, Kind = ConfigurationMemberKind.Optional,
            Label = "Debug login redirect", Description = "Developers only: send Ping responses here instead of processing them.")]
        public string? DebugLogin { get; set; }

        public static TResult WithLandingPage<TResult>(Func<Uri, TResult> onFound,
            Func<string, TResult>? onUnspecified = null, Func<TResult>? onNotSpecified = null)
            => Configured.MemberUri<SessionServerConfiguration, TResult>(config => config.LandingPage,
                LandingPageKey, onFound, onUnspecified, onNotSpecified);

        public static TResult WithDebugLogin<TResult>(Func<Uri, TResult> onFound,
            Func<string, TResult>? onUnspecified = null, Func<TResult>? onNotSpecified = null)
            => Configured.MemberUri<SessionServerConfiguration, TResult>(config => config.DebugLogin,
                DebugLoginKey, onFound, onUnspecified, onNotSpecified);

        /// <summary>The redirection rules deserialized as <typeparamref name="TRules"/>; JSON that does not
        /// deserialize is <paramref name="onInvalid"/>.</summary>
        public static TResult WithRedirections<TRules, TResult>(Func<TRules, TResult> onParsed,
            Func<string, TResult> onInvalid, Func<TResult> onNotSpecified)
            => Configured.Member<SessionServerConfiguration, string, TResult>(config => config.Redirections,
                RedirectionsKey,
                json =>
                {
                    TRules rules;
                    try
                    {
                        rules = Newtonsoft.Json.JsonConvert.DeserializeObject<TRules>(json)!;
                    }
                    catch (Newtonsoft.Json.JsonException ex)
                    {
                        return onInvalid(ex.Message);
                    }
                    return onParsed(rules);
                },
                onInvalid, onNotSpecified);

        public static bool LoginsPaused()
            => Configured.Get<SessionServerConfiguration, bool>(config => config.PauseRedirections, _ => false);
    }

    /// <summary>Sign in with Apple and Apple's domain-association files.</summary>
    [ConfigurationType("EastFiveApple", Label = "Apple",
        Description = "Sign in with Apple, and the files Apple fetches to associate this domain with an app.", Scope = ConfigurationScope.Environment)]
    public sealed class AppleConfiguration : IProvideConfiguration
    {
        public const string AppSiteAssociationIdKey = "EastFive.Azure.Apple.AppleAppSiteAssociation.AppId";
        public const string ClientIdKey = "EastFive.Apple.ClientId";
        public const string DeveloperSiteAssociationKey = "EastFive.Azure.Apple.DeveloperSiteAssociation";
        public const string ValidAudiencesKey = "EastFive.Apple.ValidAudiences";

        [ConfigurationMember(ClientIdKey, Kind = ConfigurationMemberKind.Optional,
            Label = "Client ID", Description = "Services ID identifying this application to Apple.")]
        public string? ClientId { get; set; }

        [ConfigurationMember(ValidAudiencesKey, Kind = ConfigurationMemberKind.Optional,
            Label = "Valid audiences", Description = "Comma-separated audiences accepted in Apple's JWT; include the client id.")]
        public string? ValidAudiences { get; set; }

        [ConfigurationMember(AppSiteAssociationIdKey, Kind = ConfigurationMemberKind.Optional,
            Label = "App site association", Description = "App id served in apple-app-site-association.")]
        public string? AppSiteAssociationId { get; set; }

        [ConfigurationMember(DeveloperSiteAssociationKey, Kind = ConfigurationMemberKind.Optional,
            Label = "Developer domain association", Description = "Content Apple fetches to prove ownership of this domain.")]
        public string? DeveloperSiteAssociation { get; set; }

        public string[] ValidAudienceList
            => (ValidAudiences ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>One Ping redirect, per tag. Keys are <c>EastFive.Auth.PingRedirect.{tag}.*</c>; the former
    /// <c>AffirmHealth.PDMS.PingRedirect.{tag}.*</c> spellings are read as a fallback.</summary>
    [ConfigurationType("EastFivePingRedirect", Label = "Ping redirect",
        Description = "A Ping login redirect, one per tag: the Ping auth name and report set.", Scope = ConfigurationScope.Environment)]
    public sealed class PingRedirectTagConfiguration : IProvideConfiguration, IProvideConfigurationInstanceKey
    {
        public PingRedirectTagConfiguration(string tag) => InstanceKey = tag;

        public string InstanceKey { get; }

        [ConfigurationMember("EastFive.Auth.PingRedirect.{key}.PingAuthName", FallbackKey = "AffirmHealth.PDMS.PingRedirect.{key}.PingAuthName",
            Kind = ConfigurationMemberKind.Optional, Label = "Ping auth name", Description = "The Ping authentication name for this tag.")]
        public string? PingAuthName { get; set; }

        [ConfigurationMember("EastFive.Auth.PingRedirect.{key}.PingReportSetId", FallbackKey = "AffirmHealth.PDMS.PingRedirect.{key}.PingReportSetId",
            Kind = ConfigurationMemberKind.Optional, Label = "Ping report set", Description = "The Ping report set id for this tag.")]
        public Guid? PingReportSetId { get; set; }

        public static PingRedirectTagConfiguration? For(string tag)
            => Configured.Get(() => new PingRedirectTagConfiguration(tag), config => config, _ => (PingRedirectTagConfiguration?)null);
    }

    /// <summary>One SAML identity provider, per tag.</summary>
    [ConfigurationType("EastFiveSamlProvider", Label = "SAML provider",
        Description = "A SAML identity provider, one per tag: where its metadata is and where a launch lands.", Scope = ConfigurationScope.Environment)]
    public sealed class SamlTagConfiguration : IProvideConfiguration, IProvideConfigurationInstanceKey
    {
        public SamlTagConfiguration(string tag) => InstanceKey = tag;

        public string InstanceKey { get; }

        public static string MetadataLocationKey(string tag) => $"EastFive.Security.CredentialProvider.SAML.{tag}.MetadataLocation";
        public static string LaunchPageKey(string tag) => $"EastFive.Security.CredentialProvider.SAML.{tag}.LaunchPage";

        [ConfigurationMember("EastFive.Security.CredentialProvider.SAML.{key}.MetadataLocation", Kind = ConfigurationMemberKind.Optional,
            Label = "Metadata location", Description = "The identity provider's SAML metadata document.")]
        public Uri? MetadataLocation { get; set; }

        [ConfigurationMember("EastFive.Security.CredentialProvider.SAML.{key}.LaunchPage", Kind = ConfigurationMemberKind.Optional,
            Label = "Launch page", Description = "Where an identity-provider-initiated launch lands.")]
        public string? LaunchPage { get; set; }

        public static SamlTagConfiguration? For(string tag)
            => Configured.Get(() => new SamlTagConfiguration(tag), config => config, _ => (SamlTagConfiguration?)null);

        public static TResult WithMetadataLocation<TResult>(string tag, Func<Uri, TResult> onFound, Func<string, TResult> onUnspecified)
            => For(tag)?.MetadataLocation is { } location
                ? onFound(location)
                : onUnspecified($"[{MetadataLocationKey(tag)}] is not specified.");

        public static TResult WithLaunchPage<TResult>(string tag, Func<string, TResult> onFound, Func<string, TResult> onUnspecified)
            => For(tag)?.LaunchPage is { } launchPage && !string.IsNullOrWhiteSpace(launchPage)
                ? onFound(launchPage)
                : onUnspecified($"[{LaunchPageKey(tag)}] is not specified.");
    }

    /// <summary>This application as a SAML service provider.</summary>
    [ConfigurationType("EastFiveSaml", Label = "SAML service provider",
        Description = "How this application describes itself to SAML identity providers.", Scope = ConfigurationScope.Environment)]
    public sealed class SamlConfiguration : IProvideConfiguration
    {
        public const string LoginIdAttributeNameKey = "EastFive.Security.CredentialProvider.SAML.LoginIdAttributeName";
        public const string NameIdFormatKey = "EastFive.Security.CredentialProvider.SAML.NameIdFormat";
        public const string ServiceProviderCertificateKey = "EastFive.Security.CredentialProvider.SAML.ServiceProviderCertificate";

        [ConfigurationMember(LoginIdAttributeNameKey, Kind = ConfigurationMemberKind.Optional,
            Label = "Login id attribute", Description = "The assertion attribute whose value identifies the user.")]
        public string? LoginIdAttributeName { get; set; }

        [ConfigurationMember(NameIdFormatKey, Kind = ConfigurationMemberKind.Optional, Scope = ConfigurationScope.Shared,
            Label = "NameID format", Description = "NameID format declared in the service-provider metadata.")]
        public string? NameIdFormat { get; set; }

        [ConfigurationMember(ServiceProviderCertificateKey, Kind = ConfigurationMemberKind.Optional,
            Label = "Service-provider certificate", Description = "Public signing certificate (base64) published in the metadata. Public, not a secret.")]
        public string? ServiceProviderCertificate { get; set; }

        public static SamlConfiguration? Current()
            => Configured.Get<SamlConfiguration, SamlConfiguration?>(config => config, _ => null);
    }
}

namespace EastFive.Azure.OAuth.Server
{
    /// <summary>EastFive's OAuth 2.0 server.</summary>
    [ConfigurationType("EastFiveOAuthServer", Label = "OAuth server",
        Description = "Token lifetimes and advertised scopes of EastFive's OAuth 2.0 server.", Scope = ConfigurationScope.Environment)]
    public sealed class OAuthServerConfiguration : IProvideConfiguration
    {
        public const string AccessTokenExpirationInMinutesKey = "EastFive.Azure.OAuth.AccessTokenExpirationInMinutes";
        public const string RefreshTokenExpirationInDaysKey = "EastFive.Azure.OAuth.RefreshTokenExpirationInDays";
        public const string ScopesSupportedKey = "EastFive.Azure.OAuth.ScopesSupported";

        [ConfigurationMember(AccessTokenExpirationInMinutesKey, Kind = ConfigurationMemberKind.Defaulted, Default = "60",
            Label = "Access-token lifetime (minutes)", Description = "Lifetime of access tokens issued by /oauth/token.")]
        public double AccessTokenExpirationInMinutes { get; set; }

        [ConfigurationMember(RefreshTokenExpirationInDaysKey, Kind = ConfigurationMemberKind.Defaulted, Default = "30",
            Label = "Refresh-token lifetime (days)", Description = "Lifetime of refresh tokens.")]
        public double RefreshTokenExpirationInDays { get; set; }

        [ConfigurationMember(ScopesSupportedKey, Kind = ConfigurationMemberKind.Defaulted, Default = OAuthServer.McpScope,
            Scope = ConfigurationScope.Shared, Label = "Scopes supported",
            Description = "Space-separated scopes advertised in discovery metadata (advisory; endpoints enforce [RequiredScope]).")]
        public string ScopesSupported { get; set; } = default!;

        public static OAuthServerConfiguration? Current()
            => Configured.Get<OAuthServerConfiguration, OAuthServerConfiguration?>(config => config, _ => null);
    }
}
