#nullable enable
using System;

using EastFive.Configuration;

namespace EastFive.Azure.Auth
{
    /// <summary>Google sign-in: the client id is a value, the client secret is
    /// <c>Google-{clientId}-ClientSecret</c> in the secret store.</summary>
    [ConfigurationType("Google", Label = "Google sign-in")]
    public sealed class GoogleLoginConfiguration : IProvideConfiguration
    {
        [ConfigurationMember(EastFive.Azure.AppSettings.Auth.Google.ClientId, Account = true, Label = "Client ID",
            Location = "Google Cloud console | APIs & Services | Credentials")]
        public string ClientId { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Client secret")]
        public Secret ClientSecret { get; set; } = default!;
    }

    /// <summary>Microsoft sign-in: <c>Microsoft-{clientId}-ClientSecret</c>.</summary>
    [ConfigurationType("Microsoft", Label = "Microsoft sign-in")]
    public sealed class MicrosoftLoginConfiguration : IProvideConfiguration
    {
        [ConfigurationMember(EastFive.Azure.AppSettings.Auth.Microsoft.ClientId, Account = true, Label = "Application (client) ID",
            Location = "Entra admin center | App registrations")]
        public string ClientId { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Client secret")]
        public Secret ClientSecret { get; set; } = default!;
    }

    /// <summary>Salesforce sign-in and API refresh: the consumer key is a value, the consumer secret
    /// is <c>Salesforce-ConsumerSecret</c> (consumer keys are not name segments).</summary>
    [ConfigurationType("Salesforce", Label = "Salesforce")]
    public sealed class SalesforceConfiguration : IProvideConfiguration
    {
        [ConfigurationMember(EastFive.Azure.AppSettings.Auth.Salesforce.ConsumerKey, Label = "Consumer key")]
        public string ConsumerKey { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Consumer secret")]
        public Secret ConsumerSecret { get; set; } = default!;

        [ConfigurationMember(EastFive.Azure.AppSettings.Auth.Salesforce.DocumentDiscoveryUrl, Kind = ConfigurationMemberKind.Defaulted,
            Default = "https://login.salesforce.com/.well-known/openid-configuration", Scope = ConfigurationScope.Shared)]
        public Uri DiscoveryDocument { get; set; } = default!;
    }

    /// <summary>Azure AD B2C Graph access: tenant and client id are values, the client secret is
    /// <c>AzureADB2C-ClientSecret</c> (a tenant domain is not a name segment). Loaded at startup.</summary>
    [ConfigurationType("AzureADB2C", Label = "Azure AD B2C")]
    public sealed class AzureADB2CGraphConfiguration : IProvideConfiguration
    {
        [ConfigurationMember(EastFive.Azure.AppSettings.AzureADB2C.Tenant, Label = "Tenant")]
        public string Tenant { get; set; } = default!;

        [ConfigurationMember(EastFive.Azure.AppSettings.AzureADB2C.ClientId, Label = "Graph client ID")]
        public string ClientId { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Graph client secret")]
        public Secret ClientSecret { get; set; } = default!;
    }

    /// <summary>The Ping/Athena REST API key: <c>PingAthena-RestApiKey</c>.</summary>
    [ConfigurationType("PingAthena", Label = "Ping (Athena) REST")]
    public sealed class PingAthenaConfiguration : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "REST API key")]
        public Secret RestApiKey { get; set; } = default!;
    }

    /// <summary>The EastFive login service client: <c>EastFiveLogin-ClientSecret</c>; the client key
    /// (a GUID) is a value.</summary>
    [ConfigurationType("EastFiveLogin", Label = "EastFive login service")]
    public sealed class EastFiveLoginConfiguration : IProvideConfiguration
    {
        [ConfigurationMember(EastFive.Azure.Login.AppSettings.ClientKey, Label = "Client key")]
        public Guid ClientKey { get; set; }

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Client secret")]
        public Secret ClientSecret { get; set; } = default!;
    }
}
