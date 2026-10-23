#nullable enable
using System;

using EastFive.Configuration;

namespace EastFive.Azure.ResourceManagement;

/// <summary>
/// Which Entra tenant and application this host IS — the same keys as
/// <see cref="AzureClientApplicationConfiguration"/>, read without its client secret by code that only
/// needs the identity (validating tokens issued to this application, naming it as an Event Grid
/// webhook audience), so those never depend on a secret they do not use.
/// </summary>
[ConfigurationType("AzureApplicationIdentity", Label = "Azure application identity",
    Description = "The Entra tenant and application id incoming service tokens and Event Grid webhooks are validated against.", Scope = ConfigurationScope.Environment)]
public sealed class AzureApplicationIdentityConfiguration : IProvideConfiguration
{
    [ConfigurationMember(EastFive.Azure.ResourceManagement.AzureClientApplicationConfiguration.TenantIdKey, Kind = ConfigurationMemberKind.Optional,
        Label = "Tenant ID", Description = "The Entra tenant that issues this application's tokens.",
        Location = "Azure portal | Microsoft Entra ID | Overview")]
    public string? TenantId { get; set; }

    [ConfigurationMember(EastFive.Azure.ResourceManagement.AzureClientApplicationConfiguration.ClientIdKey, Kind = ConfigurationMemberKind.Optional,
        Label = "Client ID", Description = "This application's Entra application (client) id: the expected token audience.",
        Location = "Azure portal | App registrations | Overview")]
    public string? ClientId { get; set; }

    public static TResult WithTenantId<TResult>(Func<string, TResult> onFound, Func<string, TResult> onUnspecified)
        => Configured.Member<AzureApplicationIdentityConfiguration, string, TResult>(config => config.TenantId,
            EastFive.Azure.ResourceManagement.AzureClientApplicationConfiguration.TenantIdKey, onFound, onUnspecified);

    public static TResult WithClientId<TResult>(Func<string, TResult> onFound, Func<string, TResult> onUnspecified)
        => Configured.Member<AzureApplicationIdentityConfiguration, string, TResult>(config => config.ClientId,
            EastFive.Azure.ResourceManagement.AzureClientApplicationConfiguration.ClientIdKey, onFound, onUnspecified);
}
