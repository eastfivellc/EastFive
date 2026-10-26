#nullable enable
using System;

using EastFive.Configuration;

namespace EastFive.Azure
{
    /// <summary>Azure AI Search: the endpoint is a value, the admin key is <c>AzureSearch-AdminApiKey</c>.
    /// Loaded at startup (index clients are built synchronously).</summary>
    [ConfigurationType("AzureSearch", Label = "Azure AI Search", Description = "Azure AI Search: the endpoint and the admin key used to index and query.", Scope = ConfigurationScope.Environment)]
    public sealed class SearchConfiguration : IProvideConfiguration
    {
        public const string EndPointKey = "EastFive.Azure.Search.SearchServiceEndPoint";

        [ConfigurationMember(EndPointKey, Label = "Endpoint", Description = "The search service endpoint.")]
        public Uri EndPoint { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Admin API key",
            Location = "Azure portal | Search service | Keys", Description = "Admin key of the search service.")]
        public Secret AdminApiKey { get; set; } = default!;
    }

    /// <summary>Computer Vision: the endpoint is a value, the key is <c>ComputerVision-SubscriptionKey</c>.</summary>
    [ConfigurationType("ComputerVision", Label = "Computer Vision", Description = "Azure AI Computer Vision: the endpoint and its subscription key.", Scope = ConfigurationScope.Environment)]
    public sealed class ComputerVisionConfiguration : IProvideConfiguration
    {
        public const string EndpointKey = "EastFive.Azure.CognitiveServices.ComputerVisionEndpoint";

        [ConfigurationMember(EndpointKey, Label = "Endpoint", Description = "The Computer Vision resource endpoint.")]
        public Uri Endpoint { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Subscription key", Description = "Subscription key of the Computer Vision resource.")]
        public Secret SubscriptionKey { get; set; } = default!;
    }

    /// <summary>The Application Insights query API: the application id is a value, the API key is
    /// <c>ApplicationInsights-{applicationId}-ApiKey</c>.</summary>
    [ConfigurationType("ApplicationInsights", Label = "Application Insights query API", Description = "Reads telemetry back from Application Insights through its query API.", Scope = ConfigurationScope.Environment)]
    public sealed class ApplicationInsightsQueryConfiguration : IProvideConfiguration
    {
        public const string ApplicationIdKey = "EastFive.Azure.ApplicationInsights.ApplicationId";

        [ConfigurationMember(ApplicationIdKey, Account = true, Label = "Application ID", Description = "The Application Insights application queried; it also names the API-key secret.")]
        public string ApplicationId { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "API key", Description = "API key for the Application Insights query API.")]
        public Secret ApiKey { get; set; } = default!;
    }

    /// <summary>The Teams incoming-webhook URL exception notifications post to; the URL embeds its
    /// credential, so it is the secret <c>TeamsNotifications-WebhookUrl</c>. Absent = notifications off.</summary>
    [ConfigurationType("TeamsNotifications", Label = "Teams exception notifications", Description = "The Teams incoming webhook exception notifications post to.", Scope = ConfigurationScope.Environment)]
    public sealed class TeamsNotificationConfiguration : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, FieldKind = ConfigurationFieldKind.Secret, Label = "Webhook URL", Description = "Incoming-webhook URL; it embeds its credential. Absent, notifications are off.")]
        public Secret WebhookUrl { get; set; } = default!;

        public Uri Webhook => new(WebhookUrl.Reveal());

        public string? Validate()
            => WebhookUrl == null || Uri.TryCreate(WebhookUrl.Reveal(), UriKind.Absolute, out _)
                ? null
                : ((IProvideConfiguration)this).SecretReference(nameof(WebhookUrl)).Name;
    }

    /// <summary>The Service Bus namespace the function processor uses: <c>ServiceBus-ConnectionString</c>,
    /// and the queue invocation messages are sent to.</summary>
    [ConfigurationType("ServiceBus", Label = "Service Bus", Description = "The Service Bus namespace and queue the function processor uses.", Scope = ConfigurationScope.Environment)]
    public sealed class ServiceBusConfiguration : IProvideConfiguration
    {
        public const string FunctionProcessorTriggerNameKey = "EastFive.Azure.Functions.ServiceBusTriggerName";

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Connection string", Description = "Connection string of the Service Bus namespace.")]
        public Secret ConnectionString { get; set; } = default!;

        [ConfigurationMember(FunctionProcessorTriggerNameKey, Kind = ConfigurationMemberKind.Optional,
            Label = "Function processor queue", Description = "The queue invocation messages are sent to for the function processor.")]
        public string? FunctionProcessorTriggerName { get; set; }

        /// <summary>The queue name as an Azure Functions binding expression, for <c>[ServiceBusTrigger]</c>.</summary>
        public const string FunctionProcessorTriggerNameBinding = "%" + FunctionProcessorTriggerNameKey + "%";

        /// <summary>The function processor's queue, in the raw reader's continuation shape.</summary>
        public static TResult WithFunctionProcessorTriggerName<TResult>(Func<string, TResult> onFound, Func<string, TResult> onUnspecified)
            => Configured.Member<ServiceBusConfiguration, string, TResult>(config => config.FunctionProcessorTriggerName,
                FunctionProcessorTriggerNameKey, onFound, onUnspecified);
    }

    /// <summary>The token that unlocks the table-information diagnostics endpoint:
    /// <c>StorageDiagnostics-TableInformationToken</c>. Absent = the endpoint is closed.</summary>
    [ConfigurationType("StorageDiagnostics", Label = "Storage diagnostics", Description = "The token that opens the table-information diagnostics endpoint.", Scope = ConfigurationScope.Environment)]
    public sealed class TableInformationConfiguration : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Table information token", Description = "Token a caller presents to read table information; absent, the endpoint is closed.")]
        public Secret TableInformationToken { get; set; } = default!;
    }
}
