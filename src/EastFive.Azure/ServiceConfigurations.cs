#nullable enable
using System;

using EastFive.Configuration;

namespace EastFive.Azure
{
    /// <summary>Azure AI Search: the endpoint is a value, the admin key is <c>AzureSearch-AdminApiKey</c>.
    /// Loaded at startup (index clients are built synchronously).</summary>
    [ConfigurationType("AzureSearch", Label = "Azure AI Search")]
    public sealed class SearchConfiguration : IProvideConfiguration
    {
        [ConfigurationMember(EastFive.Azure.AppSettings.Search.EndPoint, Label = "Endpoint")]
        public Uri EndPoint { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Admin API key",
            Location = "Azure portal | Search service | Keys")]
        public Secret AdminApiKey { get; set; } = default!;
    }

    /// <summary>Computer Vision: the endpoint is a value, the key is <c>ComputerVision-SubscriptionKey</c>.</summary>
    [ConfigurationType("ComputerVision", Label = "Computer Vision")]
    public sealed class ComputerVisionConfiguration : IProvideConfiguration
    {
        [ConfigurationMember(EastFive.Azure.AppSettings.CognitiveServices.ComputerVisionEndpoint, Label = "Endpoint")]
        public Uri Endpoint { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Subscription key")]
        public Secret SubscriptionKey { get; set; } = default!;
    }

    /// <summary>The Application Insights query API: the application id is a value, the API key is
    /// <c>ApplicationInsights-{applicationId}-ApiKey</c>.</summary>
    [ConfigurationType("ApplicationInsights", Label = "Application Insights query API")]
    public sealed class ApplicationInsightsQueryConfiguration : IProvideConfiguration
    {
        [ConfigurationMember(EastFive.Azure.AppSettings.ApplicationInsights.ApplicationId, Account = true, Label = "Application ID")]
        public string ApplicationId { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "API key")]
        public Secret ApiKey { get; set; } = default!;
    }

    /// <summary>The Teams incoming-webhook URL exception notifications post to; the URL embeds its
    /// credential, so it is the secret <c>TeamsNotifications-WebhookUrl</c>. Absent = notifications off.</summary>
    [ConfigurationType("TeamsNotifications", Label = "Teams exception notifications")]
    public sealed class TeamsNotificationConfiguration : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, FieldKind = ConfigurationFieldKind.Secret, Label = "Webhook URL")]
        public Secret WebhookUrl { get; set; } = default!;

        public Uri Webhook => new(WebhookUrl.Reveal());

        public string? Validate()
            => WebhookUrl == null || Uri.TryCreate(WebhookUrl.Reveal(), UriKind.Absolute, out _)
                ? null
                : ((IProvideConfiguration)this).SecretReference(nameof(WebhookUrl)).Name;
    }

    /// <summary>The Service Bus namespace the function processor uses: <c>ServiceBus-ConnectionString</c>.</summary>
    [ConfigurationType("ServiceBus", Label = "Service Bus")]
    public sealed class ServiceBusConfiguration : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Connection string")]
        public Secret ConnectionString { get; set; } = default!;
    }

    /// <summary>The token that unlocks the table-information diagnostics endpoint:
    /// <c>StorageDiagnostics-TableInformationToken</c>. Absent = the endpoint is closed.</summary>
    [ConfigurationType("StorageDiagnostics", Label = "Storage diagnostics")]
    public sealed class TableInformationConfiguration : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Table information token")]
        public Secret TableInformationToken { get; set; } = default!;
    }
}
