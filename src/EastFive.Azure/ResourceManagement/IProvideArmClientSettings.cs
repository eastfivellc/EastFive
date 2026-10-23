using System;
using System.Threading.Tasks;
using Azure.Identity;
using EastFive.Configuration;
using EastFive.Extensions;
using Azure.ResourceManager;

namespace EastFive.Azure.ResourceManagement;

/// <summary>An Entra application's client credentials, RESOLVED (tenant and client id are values,
/// the secret came from the secret store) — never configuration key names.</summary>
public interface IProvideArmClientSettings
{
    TResult WithCredentials<TResult>(Func<string, string, Secret, TResult> onCredentials, Func<string, TResult> onUnavailable);
}

/// <summary>The application EastFive's Azure management calls authenticate as: tenant and client id
/// from the values store, the secret <c>AzureClientApplication-{clientId}-ClientSecret</c>. Loaded at startup.</summary>
[ConfigurationType("AzureClientApplication", Label = "Azure client application",
    Description = "Entra application used for Azure Resource Manager and Event Grid calls.")]
public sealed class AzureClientApplicationConfiguration : IProvideConfiguration, IProvideArmClientSettings
{
    [ConfigurationMember(EastFive.Azure.AppSettings.TenantId, Label = "Tenant ID")]
    public string TenantId { get; set; } = default!;

    [ConfigurationMember(EastFive.Azure.AppSettings.ClientId, Account = true, Label = "Client ID")]
    public string ClientId { get; set; } = default!;

    [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Client secret")]
    public Secret ClientSecret { get; set; } = default!;

    public TResult WithCredentials<TResult>(Func<string, string, Secret, TResult> onCredentials, Func<string, TResult> onUnavailable)
        => onCredentials(TenantId, ClientId, ClientSecret);

    /// <summary>The host's application, as loaded at startup; never loads.</summary>
    public static IProvideArmClientSettings Startup { get; } = new StartupLoaded();

    private sealed class StartupLoaded : IProvideArmClientSettings
    {
        public TResult WithCredentials<TResult>(Func<string, string, Secret, TResult> onCredentials, Func<string, TResult> onUnavailable)
            => StartupConfiguration.Get<AzureClientApplicationConfiguration, TResult>(
                configuration => configuration.WithCredentials(onCredentials, onUnavailable),
                issue => onUnavailable(issue.ToString()));
    }
}

public static class ArmClientSettingsExtensions
{
    public static TResult CreateClientSecretCredential<TResult>(
        this IProvideArmClientSettings settings,
        Func<ClientSecretCredential, TResult> onClient,
        Func<string, TResult> onFailure)
        => settings.WithCredentials(
            (tenantId, clientId, clientSecret) => onClient(new ClientSecretCredential(tenantId, clientId, clientSecret.Reveal())),
            onFailure);

    public static TResult CreateARMClient<TResult>(
        this IProvideArmClientSettings settings,
        Func<ArmClient, TResult> onCreated,
        Func<string, TResult> onFailure)
    {
        return settings.CreateClientSecretCredential(
            clientSecretCred =>
            {
                var armClient = new ArmClient(clientSecretCred);
                return onCreated(armClient);
            },
            why => onFailure(why));
    }
}
