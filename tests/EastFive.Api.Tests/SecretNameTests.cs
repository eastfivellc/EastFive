using System;
using System.Collections.Generic;

using Xunit;

using EastFive.Configuration;

namespace EastFive.Api.Tests;

/// <summary>
/// Generated secret names: <c>{Type}-{Account}-{Property}</c> for account-scoped types,
/// <c>{Type}-{Property}</c> for global ones, refusal of account ids that are not a name
/// segment, and the sharing rule (same type + same account = one secret).
/// </summary>
public class SecretNameTests
{
    [ConfigurationType("Telnyx")]
    private sealed class TelnyxAccount : IProvideConfiguration
    {
        [ConfigurationMember("Telnyx.Account", Account = true)]
        public string Account { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        public Secret ApiKey { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Purpose = "webhook")]
        public Secret WebhookKey { get; set; } = default!;
    }

    [ConfigurationType("Search")]
    private sealed class GlobalSearch : IProvideConfiguration
    {
        [ConfigurationMember("Search.Endpoint")]
        public Uri Endpoint { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        public Secret AdminKey { get; set; } = default!;
    }

    /// <summary>Two stores on one account: the shared base declares the type name.</summary>
    [ConfigurationType("AzureStorage")]
    private abstract class StoreBase : IProvideConfiguration
    {
        public abstract string Account { get; set; }
        public abstract Secret ConnectionString { get; set; }
    }

    private sealed class TablesStore : StoreBase
    {
        [ConfigurationMember("Tables.Account", Account = true)]
        public override string Account { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        public override Secret ConnectionString { get; set; } = default!;
    }

    private sealed class BlobsStore : StoreBase
    {
        [ConfigurationMember("Blobs.Account", Account = true)]
        public override string Account { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        public override Secret ConnectionString { get; set; } = default!;
    }

    private static SecretReference Reference(IProvideConfiguration config, string purpose) => config.SecretReference(purpose);

    [Fact]
    public void AccountScoped_IsTypeAccountProperty()
    {
        var telnyx = new TelnyxAccount { Account = "acme-prod" };
        Assert.Equal(new SecretReference("Telnyx-acme-prod-ApiKey", "ApiKey"), Reference(telnyx, "ApiKey"));
        Assert.Equal(new SecretReference("Telnyx-acme-prod-WebhookKey", "webhook"), Reference(telnyx, "webhook"));
        Assert.Equal("Telnyx", ConfigurationMembers.For<TelnyxAccount>().TypeName);
        Assert.Equal("Account", ConfigurationMembers.For<TelnyxAccount>().AccountMember!.Property.Name);
    }

    [Fact]
    public void Global_IsTypeProperty()
    {
        Assert.Equal("Search-AdminKey", Reference(new GlobalSearch(), "AdminKey").Name);
        Assert.Null(ConfigurationMembers.For<GlobalSearch>().AccountMember);
    }

    [Theory]
    [InlineData("acme.prod")]
    [InlineData("acme_prod")]
    [InlineData("acme prod")]
    [InlineData("-acme")]
    [InlineData("acme-")]
    [InlineData("")]
    public void InvalidAccountCharacters_AreRefused_NeverMapped(string account)
    {
        Assert.False(SecretNames.IsValidAccount(account));
        Assert.Throws<InvalidOperationException>(() => Reference(new TelnyxAccount { Account = account }, "ApiKey"));

        var values = new Dictionary<string, string> { ["Telnyx.Account"] = account };
        var outcome = ConfigurationLoader.LoadDraft<TelnyxAccount, string>(key => values.GetValueOrDefault(key), _ => "k",
            onConfigured: _ => "configured",
            onMissing: (issue, _) => $"missing:{issue.Key}",
            onInvalid: issue => $"invalid:{issue.Key}",
            onSourceFailure: issue => $"source:{issue.Key}");
        Assert.Equal(account.Length == 0 ? "missing:Telnyx.Account" : "invalid:Telnyx.Account", outcome);
    }

    [Fact]
    public void TwoTypesSharingAnAccount_ShareOneSecret_WhenTheyShareATypeName()
    {
        var tables = Reference(new TablesStore { Account = "rosemarydev" }, "ConnectionString");
        var blobs = Reference(new BlobsStore { Account = "rosemarydev" }, "ConnectionString");
        Assert.Equal("AzureStorage-rosemarydev-ConnectionString", tables.Name);
        Assert.Equal(tables, blobs);

        // Different type names on the same account never collide.
        var telnyx = Reference(new TelnyxAccount { Account = "rosemarydev" }, "ApiKey");
        Assert.NotEqual(tables.Name, telnyx.Name);
    }

    [Fact]
    public void GeneratedNames_AreVaultNames()
    {
        Assert.True(SecretNames.IsValidName("AzureStorage-rosemarydev-ConnectionString"));
        Assert.False(SecretNames.IsValidName("Azure.Storage.Account.rosemarydev"));
        Assert.False(SecretNames.IsValidName(new string('a', SecretNames.MaximumLength + 1)));
        Assert.Throws<ArgumentException>(() => SecretNames.Generate("Telnyx", new string('a', 130), "ApiKey"));
    }

    private sealed class SecretsWithoutTypeName : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        public Secret Token { get; set; } = default!;
    }

    [ConfigurationType("Late")]
    private sealed class AccountAfterSecret : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        public Secret Token { get; set; } = default!;

        [ConfigurationMember("Late.Account", Account = true)]
        public string Account { get; set; } = default!;
    }

    [ConfigurationType("Opt")]
    private sealed class OptionalAccount : IProvideConfiguration
    {
        [ConfigurationMember("Opt.Account", Kind = ConfigurationMemberKind.Optional, Account = true)]
        public string? Account { get; set; }
    }

    [ConfigurationType("Bad")]
    private sealed class UnderscoredSecret : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        public Secret Api_Key { get; set; } = default!;
    }

    private sealed class KeylessValue : IProvideConfiguration
    {
        [ConfigurationMember]
        public string Value { get; set; } = default!;
    }

    [Fact]
    public void MalformedNamingDeclarations_Throw()
    {
        Assert.Throws<InvalidOperationException>(() => ((IProvideConfiguration)new SecretsWithoutTypeName()).SecretReference("Token"));
        Assert.Throws<InvalidOperationException>(() => ConfigurationMembers.For<AccountAfterSecret>());
        Assert.Throws<InvalidOperationException>(() => ConfigurationMembers.For<OptionalAccount>());
        Assert.Throws<InvalidOperationException>(() => ConfigurationMembers.For<UnderscoredSecret>());
        Assert.Throws<InvalidOperationException>(() => ConfigurationMembers.For<KeylessValue>());
        Assert.Throws<ArgumentException>(() => new ConfigurationTypeAttribute("Azure-Storage"));
    }
}
