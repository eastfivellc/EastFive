#nullable enable
using System;

using EastFive.Configuration;

namespace EastFive.Api
{
    /// <summary>Which claims carry the acting account and the session.</summary>
    [ConfigurationType("EastFiveClaims", Label = "Claim types",
        Description = "The claim types that carry the acting account id and the session id in EastFive tokens.", Scope = ConfigurationScope.Environment)]
    public sealed class ClaimTypesConfiguration : IProvideConfiguration
    {
        public const string ActorIdClaimTypeKey = "EastFive.Api.Security.AccountIdClaimType";
        public const string SessionIdClaimTypeKey = "EastFive.Api.Security.SessionIdClaimType";

        [ConfigurationMember(ActorIdClaimTypeKey, Kind = ConfigurationMemberKind.Defaulted,
            Default = Auth.ClaimValues.DefaultAccountClaim, Scope = ConfigurationScope.Shared,
            Label = "Account claim type", Description = "The claim whose value is the acting account's id.")]
        public string ActorIdClaimType { get; set; } = default!;

        [ConfigurationMember(SessionIdClaimTypeKey, Kind = ConfigurationMemberKind.Optional, Scope = ConfigurationScope.Shared,
            Label = "Session claim type", Description = "The claim whose value is the session id; unset means sessions are not read from claims.")]
        public string? SessionIdClaimType { get; set; }

        /// <summary>The account claim type, in the raw reader's continuation shape.</summary>
        public static TResult WithActorIdClaimType<TResult>(Func<string, TResult> onFound,
            Func<string, TResult>? onUnspecified = null, Func<TResult>? onNotSpecified = null)
            => Configured.Member<ClaimTypesConfiguration, string, TResult>(config => config.ActorIdClaimType,
                ActorIdClaimTypeKey, onFound, onUnspecified, onNotSpecified);

        /// <summary>The account claim type, or the default when configuration cannot be read.</summary>
        public static string ActorIdClaimTypeOrDefault()
            => Configured.Get<ClaimTypesConfiguration, string>(config => config.ActorIdClaimType, _ => Auth.ClaimValues.DefaultAccountClaim);
    }

    /// <summary>The super-administrator: who it is, and whether localhost is trusted as one.</summary>
    [ConfigurationType("EastFiveSuperAdmin", Label = "Super administrator",
        Description = "The account and authorization that act as super administrator, and the localhost conveniences for development.", Scope = ConfigurationScope.Environment)]
    public sealed class SuperAdminConfiguration : IProvideConfiguration
    {
        public const string ActorIdKey = "EastFive.Api.Security.SiteAdminAccountId";
        public const string AuthorizationIdKey = "EastFive.Api.AuthorizationIdSuperAdmin";

        public const string AllowLocalHostGlobalSuperAdminKey = "EastFive.Azure.Auth.AllowLocalHostGlobalSuperAdmin";
        public const string AllowLocalHostGlobalSecurityRoleKey = "EastFive.Api.Auth.AllowLocalHostGlobalSecurityRole";

        [ConfigurationMember(ActorIdKey, Kind = ConfigurationMemberKind.Optional,
            Label = "Super-admin account", Description = "Account id that is always a super administrator.")]
        public Guid? ActorId { get; set; }

        [ConfigurationMember(AuthorizationIdKey, Kind = ConfigurationMemberKind.Optional,
            Label = "Super-admin authorization", Description = "Authorization id whose sessions are super-administrator sessions.")]
        public Guid? AuthorizationId { get; set; }

        [ConfigurationMember(AllowLocalHostGlobalSecurityRoleKey, Kind = ConfigurationMemberKind.Defaulted, Default = "false",
            Label = "Trust localhost for roles", Description = "Requests from localhost satisfy every [SecurityRoleRequired]. Development only.")]
        public bool AllowLocalHostGlobalSecurityRole { get; set; }

        [ConfigurationMember(AllowLocalHostGlobalSuperAdminKey, Kind = ConfigurationMemberKind.Defaulted, Default = "false",
            Label = "Trust localhost as super admin", Description = "Requests from localhost are super-administrator requests. Development only.")]
        public bool AllowLocalHostGlobalSuperAdmin { get; set; }

        public static TResult Current<TResult>(Func<SuperAdminConfiguration, TResult> onConfigured, Func<TResult> onUnavailable)
            => Configured.Get<SuperAdminConfiguration, TResult>(onConfigured, _ => onUnavailable());

        /// <summary>The super-admin account id, in the raw reader's continuation shape.</summary>
        public static TResult WithActorId<TResult>(Func<Guid, TResult> onFound,
            Func<string, TResult>? onUnspecified = null, Func<TResult>? onNotSpecified = null)
            => Configured.MemberValue<SuperAdminConfiguration, Guid, TResult>(config => config.ActorId,
                ActorIdKey, onFound, onUnspecified, onNotSpecified);

        /// <summary>The super-admin authorization id, in the raw reader's continuation shape.</summary>
        public static TResult WithAuthorizationId<TResult>(Func<Guid, TResult> onFound,
            Func<string, TResult>? onUnspecified = null, Func<TResult>? onNotSpecified = null)
            => Configured.MemberValue<SuperAdminConfiguration, Guid, TResult>(config => config.AuthorizationId,
                AuthorizationIdKey, onFound, onUnspecified, onNotSpecified);
    }

    /// <summary>The CORS correction handler.</summary>
    [ConfigurationType("EastFiveCors", Label = "CORS",
        Description = "EastFive's CORS correction: which origins besides localhost and the server itself may call the API.", Scope = ConfigurationScope.Environment)]
    public sealed class CorsConfiguration : IProvideConfiguration
    {
        public const string EnabledKey = "EastFive.Api.CorsCorrection";

        [ConfigurationMember(EnabledKey, Kind = ConfigurationMemberKind.Defaulted, Default = "false",
            Label = "Enabled", Description = "Answer preflights and add CORS headers.")]
        public bool Enabled { get; set; }

        [ConfigurationMember("cors:Origins", Kind = ConfigurationMemberKind.Optional,
            Label = "Allowed origins", Description = "Comma-separated origins allowed in addition to localhost and the request's own server.")]
        public string? Origins { get; set; }

        [ConfigurationMember("cors:MaxAgeSeconds", Kind = ConfigurationMemberKind.Defaulted, Default = "5", Scope = ConfigurationScope.Shared,
            Label = "Preflight max age", Description = "Seconds a browser may cache a preflight answer.")]
        public long MaxAgeSeconds { get; set; }

        public string[] AllowedOrigins
            => (Origins ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>Where routes live and where the server is.</summary>
    [ConfigurationType("EastFiveRouting", Label = "Routing",
        Description = "The default route directory and this server's public address.", Scope = ConfigurationScope.Environment)]
    public sealed class RoutingConfiguration : IProvideConfiguration
    {
        public const string DefaultNamespaceKey = "EastFive.Api.DefaultNamespace";
        public const string SiteUrlKey = "EastFive.Api.SiteUrl";

        [ConfigurationMember(DefaultNamespaceKey, Kind = ConfigurationMemberKind.Defaulted, Default = "api", Scope = ConfigurationScope.Shared,
            Label = "Default namespace", Description = "Route directory for controllers that declare none.")]
        public string DefaultNamespace { get; set; } = default!;

        [ConfigurationMember(SiteUrlKey, Kind = ConfigurationMemberKind.Optional,
            Label = "Site URL", Description = "This server's public address, used to describe it in notifications.")]
        public string? SiteUrl { get; set; }

        public static string DefaultNamespaceOrApi()
            => Configured.Get<RoutingConfiguration, string>(config => config.DefaultNamespace, _ => "api");
    }

    /// <summary>How long access tokens and signed links live.</summary>
    [ConfigurationType("EastFiveAccessTokenLifetime", Label = "Access-token lifetime",
        Description = "Lifetime of session tokens and signed access-token links.", Scope = ConfigurationScope.Environment)]
    public sealed class AccessTokenLifetimeConfiguration : IProvideConfiguration
    {
        public const string ExpirationInMinutesKey = "EastFive.Api.AccessTokenExpirationInMinutes";

        [ConfigurationMember(ExpirationInMinutesKey, Kind = ConfigurationMemberKind.Optional,
            Label = "Lifetime (minutes)", Description = "Minutes a session token or signed link stays valid.")]
        public double? ExpirationInMinutes { get; set; }

        /// <summary>The configured lifetime, or <paramref name="fallback"/> when unset or unreadable.</summary>
        public static TimeSpan Lifetime(TimeSpan fallback)
            => Configured.Get<AccessTokenLifetimeConfiguration, TimeSpan>(
                config => config.ExpirationInMinutes is { } minutes ? TimeSpan.FromMinutes(minutes) : fallback,
                _ => fallback);

        /// <summary>The configured lifetime; throws naming the key when it is unset.</summary>
        public static TimeSpan RequiredLifetime()
            => Configured.Get<AccessTokenLifetimeConfiguration, TimeSpan>(
                config => config.ExpirationInMinutes is { } minutes
                    ? TimeSpan.FromMinutes(minutes)
                    : throw new EastFive.Web.ConfigurationException(ExpirationInMinutesKey, typeof(double), "Access-token lifetime is not configured."),
                issue => throw new EastFive.Web.ConfigurationException(issue.Key, typeof(double), issue.ToString()));
    }
}
