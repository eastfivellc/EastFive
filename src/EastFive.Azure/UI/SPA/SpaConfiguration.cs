#nullable enable
using System;
using System.Linq;

using EastFive.Configuration;

namespace EastFive.Azure
{
    /// <summary>How the single-page application is served from its package.</summary>
    [ConfigurationType("EastFiveSpa", Label = "Single-page application",
        Description = "Where the SPA lives and how its package is served.")]
    public sealed class SpaConfiguration : IProvideConfiguration
    {
        [ConfigurationMember(AppSettings.SPA.SiteLocation, Kind = ConfigurationMemberKind.Optional,
            Label = "Site location", Description = "Public address the SPA is served from.")]
        public Uri? SiteLocation { get; set; }

        [ConfigurationMember(AppSettings.SPA.BuildConfigPath, Kind = ConfigurationMemberKind.Optional, Scope = ConfigurationScope.Shared,
            Label = "Build config path", Description = "Path of the build description (routes, build time) inside the package; unset disables SPA serving.")]
        public string? BuildConfigPath { get; set; }

        [ConfigurationMember(AppSettings.SPA.IndexHtmlPath, Kind = ConfigurationMemberKind.Optional, Scope = ConfigurationScope.Shared,
            Label = "Index page path", Description = "Path of the index page inside the package.")]
        public string? IndexHtmlPath { get; set; }

        [ConfigurationMember(AppSettings.SPA.ServeEnabled, Kind = ConfigurationMemberKind.Defaulted, Default = "false",
            Label = "Dynamic serving", Description = "Serve the index page dynamically rather than as a static file.")]
        public bool ServeEnabled { get; set; }

        [ConfigurationMember(AppSettings.SPA.AdditionalPackages, Kind = ConfigurationMemberKind.Optional,
            Label = "Additional packages", Description = "Comma-separated names of packages served beside the primary one.")]
        public string? AdditionalPackages { get; set; }

        [ConfigurationMember(AppSettings.SPA.FilesExpirationInDays, Kind = ConfigurationMemberKind.Defaulted, Default = "1", Scope = ConfigurationScope.Shared,
            Label = "File cache lifetime (days)", Description = "Cache lifetime of the package's immutable files.")]
        public double FilesExpirationInDays { get; set; }

        [ConfigurationMember(AppSettings.ClientMinimumVersion, Kind = ConfigurationMemberKind.Optional,
            Label = "Minimum client build", Description = "Clients born before this build are told to reload.")]
        public long? ClientMinimumVersion { get; set; }

        public string[] AdditionalPackageNames
            => (AdditionalPackages ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        /// <summary>The SPA configuration, or null when it cannot be read.</summary>
        public static SpaConfiguration? Current()
            => Configured.Get<SpaConfiguration, SpaConfiguration?>(config => config, _ => null);
    }

    /// <summary>The CDN this application's static calls go through.</summary>
    [ConfigurationType("EastFiveCdn", Label = "CDN",
        Description = "The CDN endpoint that fronts this application's API.")]
    public sealed class CdnConfiguration : IProvideConfiguration
    {
        public static readonly Uri NoEndpoint = new("http://example.com");

        [ConfigurationMember(AppSettings.CDNEndpointHostname, Kind = ConfigurationMemberKind.Optional,
            Label = "Endpoint", Description = "The CDN endpoint's address.")]
        public Uri? EndpointHostname { get; set; }

        [ConfigurationMember(AppSettings.CDNApiRoutePrefix, Kind = ConfigurationMemberKind.Defaulted, Default = "api", Scope = ConfigurationScope.Shared,
            Label = "API route prefix", Description = "Route prefix of the API behind the CDN.")]
        public string ApiRoutePrefix { get; set; } = default!;

        /// <summary>The endpoint and prefix, falling back to the placeholder endpoint and "api".</summary>
        public static (Uri endpoint, string apiRoutePrefix) Current()
            => Configured.Get<CdnConfiguration, (Uri, string)>(
                config => (config.EndpointHostname ?? NoEndpoint, config.ApiRoutePrefix),
                _ => (NoEndpoint, "api"));
    }
}
