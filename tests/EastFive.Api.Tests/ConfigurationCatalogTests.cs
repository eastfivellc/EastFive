using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

using EastFive.Configuration;

namespace EastFive.Api.Tests;

/// <summary>
/// Member metadata (one attribute: key, kind, label, description, location, scope, field kind),
/// the lookup/test hooks discovered by interface, and the catalog an editor or doctor renders.
/// </summary>
public class ConfigurationCatalogTests
{
    private sealed class PhoneNumbersLookup : Attribute, IProvideConfigurationLookup
    {
        public async Task<TResult> LookupAsync<TResult>(ConfigurationMember member, ConfigurationDraft draft,
            Func<IReadOnlyList<ConfigurationChoice>, TResult> onChoices, Func<string, TResult> onUnavailable)
            => await draft.Secrets.GetSecretAsync("Phone-acme-ApiKey",
                (_, _) => onChoices(new[] { new ConfigurationChoice("+15550100", "Main line") }),
                () => onUnavailable("Enter the API key first."),
                why => onUnavailable(why));
    }

    private sealed class PhoneTest : Attribute, ITestConfiguration
    {
        public Task<TResult> TestAsync<TResult>(Type configurationType, ConfigurationDraft draft,
            Func<string, TResult> onPassed, Func<string, TResult> onFailed)
            => Task.FromResult(draft.ReadValue("Phone.Account") is { Length: > 0 } ? onPassed("Authenticated.") : onFailed("No account."));
    }

    [ConfigurationType("Phone", Label = "Phone provider", Description = "Outbound calling.")]
    [PhoneTest]
    private sealed class Phone : IProvideConfiguration
    {
        [ConfigurationMember("Phone.Account", Account = true, Label = "Account", Location = "Console | Account")]
        public string Account { get; set; } = default!;

        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret, Description = "API v2 key.", MoreInfo = "https://phone.example/keys")]
        public Secret ApiKey { get; set; } = default!;

        [ConfigurationMember("Phone.Endpoint", Scope = ConfigurationScope.Shared)]
        public Uri Endpoint { get; set; } = default!;

        [ConfigurationMember("Phone.From", Kind = ConfigurationMemberKind.Optional, FieldKind = ConfigurationFieldKind.Enum)]
        [PhoneNumbersLookup]
        public string? From { get; set; }

        [ConfigurationMember("Phone.Timeout", Kind = ConfigurationMemberKind.Defaulted, Default = "00:00:30")]
        public TimeSpan Timeout { get; set; }

        [ConfigurationMember("Phone.Retries", Kind = ConfigurationMemberKind.Defaulted, Default = "2")]
        public int Retries { get; set; }
    }

    private sealed class HandNamed : IProvideConfiguration
    {
        [ConfigurationMember(Kind = ConfigurationMemberKind.Secret)]
        public Secret Token { get; set; } = default!;

        public SecretReference SecretReference(string purpose) => new("Legacy-Token", purpose);
    }

    [Fact]
    public void Metadata_IsDeclaredOnTheMember_AndFieldKindIsDerived()
    {
        var members = ConfigurationMembers.For<Phone>().Members.ToDictionary(member => member.Property.Name, member => member.Metadata!);
        Assert.Equal(("Account", "Console | Account", ConfigurationFieldKind.Account), (members["Account"].Label, members["Account"].Location, members["Account"].FieldKind));
        Assert.Equal(("API v2 key.", "https://phone.example/keys", ConfigurationFieldKind.Secret), (members["ApiKey"].Description, members["ApiKey"].MoreInfo, members["ApiKey"].FieldKind));
        Assert.Equal((ConfigurationScope.Shared, ConfigurationFieldKind.Url), (members["Endpoint"].Scope, members["Endpoint"].FieldKind));
        Assert.Equal(ConfigurationScope.Environment, members["Account"].Scope);
        Assert.Equal(ConfigurationFieldKind.Enum, members["From"].FieldKind);
        Assert.Equal(ConfigurationFieldKind.Duration, members["Timeout"].FieldKind);
        Assert.Equal(ConfigurationFieldKind.Number, members["Retries"].FieldKind);
        Assert.Equal("ApiKey", members["ApiKey"].Label);
    }

    [Fact]
    public void Catalog_ListsTypes_Members_AndGeneratedNames()
    {
        var catalog = ConfigurationCatalog.For(new[] { typeof(Phone), typeof(HandNamed), typeof(IProvideConfiguration), typeof(string) });
        var phone = catalog.Single(type => type.Type == typeof(Phone));
        Assert.Equal(("Phone", "Phone provider", "Outbound calling.", "Phone.Account"), (phone.TypeName, phone.Label, phone.Description, phone.AccountKey));
        Assert.True(phone.GeneratesSecretNames);
        Assert.True(phone.HasTest);
        Assert.Equal(new[] { "Account", "ApiKey", "Endpoint", "From", "Timeout", "Retries" }, phone.Members.Select(member => member.Property));
        Assert.Equal("Phone-{account}-ApiKey", phone.Members.Single(member => member.Kind == ConfigurationMemberKind.Secret).SecretNameTemplate);
        Assert.Equal(new[] { "Phone-acme-ApiKey" }, phone.SecretNames("acme"));
        Assert.True(phone.Members.Single(member => member.Property == "From").HasLookup);
        Assert.Equal(2, catalog.Length); // interfaces and non-configuration types are not listed

        var handNamed = catalog.Single(type => type.Type == typeof(HandNamed));
        Assert.False(handNamed.GeneratesSecretNames);
        Assert.Null(handNamed.Members.Single().SecretNameTemplate);
    }

    [Fact]
    public async Task Hooks_AreFoundByInterface_AndReadOnlyTheDraft()
    {
        var phone = ConfigurationCatalog.Describe(typeof(Phone));
        var from = ConfigurationMembers.For<Phone>().Members.Single(member => member.Property.Name == "From");
        var empty = ConfigurationDraft.From(new Dictionary<string, string>(), new Dictionary<string, string>());
        var keyed = ConfigurationDraft.From(new Dictionary<string, string> { ["Phone.Account"] = "acme" },
            new Dictionary<string, string> { ["Phone-acme-ApiKey"] = "k-draft" });

        Assert.Equal("Enter the API key first.", await from.Lookup!.LookupAsync(from, empty, choices => "choices", why => why));
        Assert.Equal("+15550100", await from.Lookup!.LookupAsync(from, keyed, choices => choices.Single().Value, why => why));
        Assert.Equal("No account.", await phone.Test!.TestAsync(typeof(Phone), empty, ok => ok, why => why));
        Assert.Equal("Authenticated.", await phone.Test!.TestAsync(typeof(Phone), keyed, ok => ok, why => why));
    }

    [Fact]
    public void LegacyConfigKeyMetadata_IsGone()
    {
        var core = typeof(EastFive.Web.ConfigAttribute).Assembly;
        Assert.Null(core.GetType("EastFive.Web.ConfigKeyAttribute"));
        Assert.Null(core.GetType("EastFive.Web.DeploymentOverrides"));
    }
}
