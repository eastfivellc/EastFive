#nullable enable
using System;

using EastFive.Configuration;

namespace EastFive.Azure.Monitoring
{
    /// <summary>
    /// Where telemetry is SENT. Values only, separate from <see cref="ApplicationInsightsQueryConfiguration"/>
    /// (which holds the query API key) so telemetry never depends on a secret it does not use.
    /// </summary>
    [ConfigurationType("ApplicationInsightsTelemetry", Label = "Application Insights telemetry",
        Description = "Where this application's telemetry is sent, server-side and from the browser.", Scope = ConfigurationScope.Environment)]
    public sealed class ApplicationInsightsTelemetryConfiguration : IProvideConfiguration
    {
        public const string ConnectionStringKey = "APPLICATIONINSIGHTS_CONNECTION_STRING";
        public const string InstrumentationKeyKey = "EastFive.Azure.ApplicationInsights.InstrumentationKey";

        [ConfigurationMember(ConnectionStringKey, Kind = ConfigurationMemberKind.Optional,
            Label = "Connection string", Description = "Application Insights connection string; also injected into served SPA index files. Not a secret.",
            Location = "Azure portal | Application Insights | Overview")]
        public string? ConnectionString { get; set; }

        [ConfigurationMember(InstrumentationKeyKey, Kind = ConfigurationMemberKind.Optional,
            Label = "Instrumentation key", Description = "Legacy instrumentation key injected into served SPA index files. Not a secret.",
            Location = "Azure portal | Application Insights | Overview")]
        public string? InstrumentationKey { get; set; }

        public static ApplicationInsightsTelemetryConfiguration? Current()
            => Configured.Get<ApplicationInsightsTelemetryConfiguration, ApplicationInsightsTelemetryConfiguration?>(config => config, _ => null);
    }

    /// <summary>How exception notification cards identify this application. Values only: the webhook
    /// they post to is the secret on <see cref="TeamsNotificationConfiguration"/>.</summary>
    [ConfigurationType("TeamsNotificationCard", Label = "Teams notification card",
        Description = "The name and image exception notification cards show for this application.", Scope = ConfigurationScope.Environment)]
    public sealed class TeamsNotificationCardConfiguration : IProvideConfiguration
    {
        public const string AppIdentificationKey = "EastFive.Azure.ApplicationInsights.TeamsAppIdentification";
        public const string AppImageKey = "EastFive.Azure.ApplicationInsights.TeamsAppImage";

        public const string DefaultAppIdentification = "App Logger";

        [ConfigurationMember(AppIdentificationKey, Kind = ConfigurationMemberKind.Defaulted,
            Default = DefaultAppIdentification, Label = "Application name", Description = "Name shown on notification cards.")]
        public string AppIdentification { get; set; } = default!;

        [ConfigurationMember(AppImageKey, Kind = ConfigurationMemberKind.Optional, Scope = ConfigurationScope.Shared,
            Label = "Application image", Description = "Absolute URL of the image shown on notification cards.")]
        public Uri? AppImage { get; set; }
    }

    /// <summary>What Azure App Service says about the host — read to describe the server in notifications.</summary>
    [ConfigurationType("AppService", Label = "App Service host",
        Description = "The variables Azure App Service sets on the host, used to name the server in notifications.", Scope = ConfigurationScope.Environment)]
    public sealed class AppServiceConfiguration : IProvideConfiguration
    {
        [ConfigurationMember("WEBSITE_SITE_NAME", Kind = ConfigurationMemberKind.Optional,
            Label = "Site name", Description = "Set by App Service: the site's name.", Location = "Set by Azure App Service")]
        public string? SiteName { get; set; }

        [ConfigurationMember("WEBSITE_HOSTNAME", Kind = ConfigurationMemberKind.Optional,
            Label = "Host name", Description = "Set by App Service: the site's default host name.", Location = "Set by Azure App Service")]
        public string? HostName { get; set; }

        public static AppServiceConfiguration? Current()
            => Configured.Get<AppServiceConfiguration, AppServiceConfiguration?>(config => config, _ => null);
    }
}
