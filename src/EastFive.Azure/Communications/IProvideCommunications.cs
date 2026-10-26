using System;
using System.Text.RegularExpressions;
using Azure.Communication.CallAutomation;
using Azure.Communication.PhoneNumbers;
using Azure.ResourceManager;
using EastFive.Azure.ResourceManagement;
using EastFive.Configuration;


namespace EastFive.Azure.Communications;

/// <summary>An Azure Communication Services connection string, RESOLVED from the secret store.</summary>
public interface IProvideCommunicationsSettings
{
    TResult WithConnectionString<TResult>(Func<string, TResult> onConnectionString, Func<string, TResult> onUnavailable);
}

/// <summary>Azure Communication Services: the connection string is the secret
/// <c>AzureCommunications-ConnectionString</c>. Loaded at startup.</summary>
[ConfigurationType("AzureCommunications", Label = "Azure Communication Services", Description = "Azure Communication Services, for SMS and calls.", Scope = ConfigurationScope.Environment)]
public sealed class AzureCommunicationsConfiguration : IProvideConfiguration, IProvideCommunicationsSettings
{
    [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Label = "Connection string",
        Location = "Azure portal | Communication Service | Keys", Description = "Connection string of the Communication Services resource.")]
    public Secret ConnectionString { get; set; } = default!;

    public TResult WithConnectionString<TResult>(Func<string, TResult> onConnectionString, Func<string, TResult> onUnavailable)
        => onConnectionString(ConnectionString.Reveal());

    /// <summary>The host's service, as loaded at startup; never loads.</summary>
    public static IProvideCommunicationsSettings Startup { get; } = new StartupLoaded();

    private sealed class StartupLoaded : IProvideCommunicationsSettings
    {
        public TResult WithConnectionString<TResult>(Func<string, TResult> onConnectionString, Func<string, TResult> onUnavailable)
            => StartupConfiguration.Get<AzureCommunicationsConfiguration, TResult>(
                configuration => onConnectionString(configuration.ConnectionString.Reveal()),
                issue => onUnavailable(issue.ToString()));
    }
}

public static class CommunicationsSettingsExtensions
{
    public static TResult CreatePhoneNumbersClient<TResult>(
        this IProvideCommunicationsSettings settings,
        Func<PhoneNumbersClient, TResult> onClient,
        Func<string, TResult> onFailure)
    {
        return settings.WithConnectionString(
            connectionString =>
            {
                var client = new PhoneNumbersClient(connectionString);
                return onClient(client);
            },
            why => onFailure(why));
    }

    public static TResult CreateAutomationClient<TResult>(
        this IProvideCommunicationsSettings settings,
        Func<CallAutomationClient, TResult> onClient,
        Func<string, TResult> onFailure)
    {
        return settings.WithConnectionString(
            connectionString =>
            {
                var client = new CallAutomationClient(connectionString);
                return onClient(client);
            },
            why => onFailure(why));
    }

    /// <summary>
    /// Extracts the resource name from an ACS connection string.
    /// Example: endpoint=https://xyz.unitedstates.communication.azure.com/
    /// Returns: xyz
    /// </summary>
    public static TResult ExtractResourceName<TResult>(
        this IProvideCommunicationsSettings settings,
        Func<string, TResult> onExtracted,
        Func<string, TResult> onFailure)
    {
        return settings.WithConnectionString(
            connectionString =>
            {
                var endpointMatch = Regex.Match(connectionString, @"endpoint=https://([^.]+)\.", RegexOptions.IgnoreCase);
                if(endpointMatch.Success)
                    return onExtracted(endpointMatch.Groups[1].Value);
                return onFailure("Could not extract resource name from connection string.");
            },
            why => onFailure(why));
    }

    public static TResult CreateResourceManager<TResult>(
        this IProvideCommunicationsSettings settings, IProvideArmClientSettings armClientSettings,
        Func<ArmClient, string, TResult> onClient,
        Func<string, TResult> onFailure)
    {
        return settings.ExtractResourceName(
            resourceName =>
            {
                return armClientSettings.CreateARMClient(
                    client => onClient(client, resourceName),
                    why => onFailure(why));
            },
            why => onFailure(why));
    }

}