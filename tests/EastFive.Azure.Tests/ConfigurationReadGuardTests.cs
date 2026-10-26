using Xunit;

using EastFive.Api.Tests;

namespace EastFive.Azure.Tests;

/// <summary>The structural configuration-read guard over EastFive.Azure and EastFive.Sheets (Core and
/// Api are guarded in EastFive.Api.Tests). See <see cref="ConfigurationReadGuard"/>.</summary>
public class ConfigurationReadGuardTests
{
    [Fact]
    public void AzureAndSheets_ExposeNoRawConfigurationRead()
        => ConfigurationReadGuard.AssertNoPublicRawRead(
            typeof(EastFive.Azure.Persistence.StorageAccount).Assembly, typeof(EastFive.Sheets.ISheet).Assembly);

    [Fact]
    public void Azure_HasNoRawConfigurationReads() => ConfigurationReadGuard.AssertNoRawReads("Azure");

    [Fact]
    public void Sheets_HasNoRawConfigurationReads() => ConfigurationReadGuard.AssertNoRawReads("Sheets");
}
