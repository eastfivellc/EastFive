#nullable enable
using System;

using EastFive.Configuration;

namespace EastFive.Azure.Auth
{
    /// <summary>Google sign-in: the client id is a value, the client secret is
    /// <c>Google-{clientId without .apps.googleusercontent.com}-ClientSecret</c> in the secret store.</summary>
    [ConfigurationType("Google", Label = "Google sign-in", Description = "Sign in with Google: the OAuth client this application uses.", Scope = ConfigurationScope.Environment)]
    public sealed class GoogleLoginConfiguration : IProvideConfiguration
    {
        public const string ClientIdKey = "EastFive.Google.ClientID";

        [ConfigurationMember(ClientIdKey, Account = true, Label = "Client ID",
            Location = "Google Cloud console | APIs & Services | Credentials", Description = "OAuth client id; it also names the client-secret secret (dots become dashes).")]
        [GoogleClientIdSegment]
        public string ClientId { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Client secret", Description = "OAuth client secret.")]
        public Secret ClientSecret { get; set; } = default!;
    }

    /// <summary>Microsoft sign-in: <c>Microsoft-{clientId}-ClientSecret</c>.</summary>
    [ConfigurationType("Microsoft", Label = "Microsoft sign-in", Description = "Sign in with Microsoft: the Entra application this application uses.", Scope = ConfigurationScope.Environment)]
    public sealed class MicrosoftLoginConfiguration : IProvideConfiguration
    {
        public const string ClientIdKey = "EastFive.Microsoft.ClientID";

        [ConfigurationMember(ClientIdKey, Account = true, Label = "Application (client) ID",
            Location = "Entra admin center | App registrations", Description = "Entra application (client) id; it also names the client-secret secret.")]
        public string ClientId { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Client secret", Description = "Entra application client secret.")]
        public Secret ClientSecret { get; set; } = default!;
    }

    /// <summary>Salesforce sign-in and API refresh: the consumer key is a value, the consumer secret
    /// is <c>Salesforce-ConsumerSecret</c> (consumer keys are not name segments).</summary>
    [ConfigurationType("Salesforce", Label = "Salesforce", Description = "Sign in with Salesforce: the connected app and its OpenID discovery document.", Scope = ConfigurationScope.Environment)]
    public sealed class SalesforceConfiguration : IProvideConfiguration
    {
        public const string ConsumerKeyKey = "EastFive.Salesforce.ConsumerKey";
        public const string DiscoveryDocumentKey = "EastFive.Salesforce.DocumentDiscoveryUrl";

        [ConfigurationMember(ConsumerKeyKey, Label = "Consumer key", Description = "Consumer key of the Salesforce connected app.")]
        public string ConsumerKey { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Consumer secret", Description = "Consumer secret of the Salesforce connected app.")]
        public Secret ConsumerSecret { get; set; } = default!;

        [ConfigurationMember(DiscoveryDocumentKey, Kind = ConfigurationMemberKind.Defaulted,
            Default = "https://login.salesforce.com/.well-known/openid-configuration", Scope = ConfigurationScope.Shared, Label = "Discovery document", Description = "OpenID Connect discovery document of the Salesforce instance.")]
        public Uri DiscoveryDocument { get; set; } = default!;
    }

    /// <summary>Azure AD B2C Graph access: tenant and client id are values, the client secret is
    /// <c>AzureADB2C-ClientSecret</c> (a tenant domain is not a name segment). Loaded at startup.</summary>
    [ConfigurationType("AzureADB2C", Label = "Azure AD B2C", Description = "Azure AD B2C sign-in: the tenant, its user flows, and the Graph client that manages accounts.", Scope = ConfigurationScope.Environment)]
    public sealed class AzureADB2CGraphConfiguration : IProvideConfiguration
    {
        public const string ApplicationIdKey = "EastFive.Azure.AzureADB2C.ApplicationId";
        public const string AudienceKey = "EastFive.Azure.AzureADB2C.Audience";
        public const string ClientIdKey = "EastFive.Azure.AzureADB2C.ClientId";
        public const string LoginIdClaimTypeKey = "EastFive.Azure.Auth.AzureADB2C.LoginIdClaimType";
        public const string SigninFlowKey = "EastFive.Azure.AzureADB2C.SigninFlow";
        public const string SignupFlowKey = "EastFive.Azure.AzureADB2C.SignupFlow";
        public const string TenantKey = "EastFive.Azure.AzureADB2C.Tenant";

        [ConfigurationMember(TenantKey, Label = "Tenant", Description = "The B2C tenant (e.g. contoso.onmicrosoft.com).")]
        public string Tenant { get; set; } = default!;

        [ConfigurationMember(ClientIdKey, Label = "Graph client ID", Description = "Application id of the Graph client that manages B2C accounts.")]
        public string ClientId { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Graph client secret", Description = "Secret of the Graph client that manages B2C accounts.")]
        public Secret ClientSecret { get; set; } = default!;

        [ConfigurationMember(ApplicationIdKey, Kind = ConfigurationMemberKind.Optional,
            Label = "Application ID", Description = "The B2C application users sign in to.")]
        public string? ApplicationId { get; set; }

        [ConfigurationMember(AudienceKey, Kind = ConfigurationMemberKind.Optional,
            Label = "Audience", Description = "Audience of the B2C tokens this application accepts.")]
        public string? Audience { get; set; }

        [ConfigurationMember(SigninFlowKey, Kind = ConfigurationMemberKind.Optional,
            Label = "Sign-in flow", Description = "B2C user flow (policy) for signing in.")]
        public string? SigninFlow { get; set; }

        [ConfigurationMember(SignupFlowKey, Kind = ConfigurationMemberKind.Optional,
            Label = "Sign-up flow", Description = "B2C user flow (policy) for signing up.")]
        public string? SignupFlow { get; set; }

        [ConfigurationMember(LoginIdClaimTypeKey, Kind = ConfigurationMemberKind.Optional,
            Label = "Login id claim", Description = "The B2C claim whose value identifies the user.")]
        public string? LoginIdClaimType { get; set; }

        /// <summary>The B2C configuration loaded at startup, or a <see cref="EastFive.Web.ConfigurationException"/>.</summary>
        public static AzureADB2CGraphConfiguration Loaded()
            => StartupConfiguration.Get<AzureADB2CGraphConfiguration, AzureADB2CGraphConfiguration>(config => config,
                issue => throw new EastFive.Web.ConfigurationException(issue.Key, typeof(AzureADB2CGraphConfiguration), issue.ToString()));

        /// <summary>A required B2C value, or a <see cref="EastFive.Web.ConfigurationException"/> naming its key.</summary>
        public static string Required(string? value, string key)
            => value ?? throw new EastFive.Web.ConfigurationException(key, typeof(string), $"[{key}] is not specified.");
    }

    /// <summary>The Ping/Athena REST API key: <c>PingAthena-RestApiKey</c>.</summary>
    [ConfigurationType("PingAthena", Label = "Ping (Athena) REST", Description = "The key for Ping Identity's athenahealth REST API.", Scope = ConfigurationScope.Environment)]
    public sealed class PingAthenaConfiguration : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "REST API key", Description = "Key for Ping Identity's athenahealth REST API.")]
        public Secret RestApiKey { get; set; } = default!;
    }

    /// <summary>The EastFive login service client: <c>EastFiveLogin-ClientSecret</c>; the client key
    /// (a GUID) is a value.</summary>
    [ConfigurationType("EastFiveLogin", Label = "EastFive login service", Description = "EastFive's hosted login service: the client this application signs in as.", Scope = ConfigurationScope.Environment)]
    public sealed class EastFiveLoginConfiguration : IProvideConfiguration
    {
        public const string ClientKeyKey = "EastFive.Azure.Login.ClientKey";

        [ConfigurationMember(ClientKeyKey, Label = "Client key", Description = "The client id (a GUID) this application signs in to the login service as.")]
        public Guid ClientKey { get; set; }

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Client secret", Description = "The client secret for the login service.")]
        public Secret ClientSecret { get; set; } = default!;
    }

    /// <summary>A Google OAuth client id is <c>{project}-{id}.apps.googleusercontent.com</c>; the dots
    /// cannot be a secret-name segment and the domain is the same for every client, so the segment
    /// is the id without it.</summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class GoogleClientIdSegmentAttribute : Attribute, IProvideSecretAccountSegment
    {
        public const string Domain = ".apps.googleusercontent.com";

        public string SecretAccountSegment(string account)
            => account.EndsWith(Domain, StringComparison.OrdinalIgnoreCase) ? account[..^Domain.Length] : account;
    }
}
