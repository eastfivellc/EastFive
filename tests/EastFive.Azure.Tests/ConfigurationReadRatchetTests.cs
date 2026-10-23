using Xunit;

using EastFive.Api.Tests;

namespace EastFive.Azure.Tests;

/// <summary>The configuration-read ratchet over EastFive.Azure and EastFive.Sheets (Core and Api are
/// pinned in EastFive.Api.Tests). See <see cref="ConfigurationReadRatchet"/>.</summary>
public class ConfigurationReadRatchetTests
{
    [Fact]
    public void Azure_RawConfigurationReads_OnlyFall() => ConfigurationReadRatchet.AssertPinned("Azure", 25);

    [Fact]
    public void Sheets_RawConfigurationReads_OnlyFall() => ConfigurationReadRatchet.AssertPinned("Sheets", 0);
}
