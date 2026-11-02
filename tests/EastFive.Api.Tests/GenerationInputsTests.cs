using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

using Xunit;

using EastFive.Configuration;

namespace EastFive.Api.Tests;

/// <summary>
/// Generation inputs: a generator declares what it needs from the user beyond the draft (an admin
/// credential, a common name); the catalog describes them, the runner refuses a generate that lacks a
/// required one, the context hands them over, and neither the context nor a result prints them.
/// A generator may also fail cleanly with a reason.
/// </summary>
public class GenerationInputsTests
{
    private const string AdminSecret = "admin-credential-do-not-echo-5f1c";

    private sealed class NeedsInputs : Attribute, IGenerateConfigurationValue
    {
        public string Label => "Needs inputs";

        public IReadOnlyList<ConfigurationGenerationInput> Inputs { get; } = new[]
        {
            new ConfigurationGenerationInput("admin", "Admin credential", Secret: true, Description: "A bearer token holding the admin scope."),
            new ConfigurationGenerationInput("name", "Common name", Optional: true),
        };

        public Task<ConfigurationGenerationResult> GenerateAsync(ConfigurationGenerationContext context)
            => Task.FromResult(context.InputValue("admin") == AdminSecret
                ? ConfigurationGenerationResult.Of(nameof(WithInputs.Value), (context.InputValue("name") ?? "default") + "-generated")
                : ConfigurationGenerationResult.Failed("The registry refused the credential (403)."));
    }

    [ConfigurationType("WithInputs", Label = "With inputs", Description = "A generator with inputs.")]
    private sealed class WithInputs : IProvideConfiguration
    {
        [ConfigurationMember("WithInputs.Value", Kind = ConfigurationMemberKind.Optional)]
        [NeedsInputs]
        public string? Value { get; set; }

        [ConfigurationMember("WithInputs.Plain", Kind = ConfigurationMemberKind.Optional)]
        [GenerateGuid]
        public string? Plain { get; set; }
    }

    private static ConfigurationGenerationContext Context(string member, IReadOnlyDictionary<string, string?>? inputs = null)
        => new(typeof(WithInputs), ConfigurationMembers.For<WithInputs>().Members.Single(candidate => candidate.Property.Name == member),
            new Dictionary<string, string?>())
        {
            Inputs = inputs ?? new Dictionary<string, string?>(),
        };

    private static Task<(ConfigurationGenerationResult? result, string? refused)> RunAsync(ConfigurationGenerationContext context)
        => ConfigurationGeneration.GenerateAsync<(ConfigurationGenerationResult?, string?)>(context,
            result => (result, null),
            why => throw new Xunit.Sdk.XunitException(why),
            why => (null, why));

    [Fact]
    public void Inputs_AreDeclaredByTheGenerator_AndDescribedByTheCatalog()
    {
        var described = ConfigurationCatalog.Describe(typeof(WithInputs)).Members.ToDictionary(member => member.Property);
        var inputs = described[nameof(WithInputs.Value)].GeneratorInputs!;
        Assert.Equal(new[] { "admin", "name" }, inputs.Select(input => input.Name));
        Assert.True(inputs[0].Secret);
        Assert.False(inputs[0].Optional);
        Assert.Equal("A bearer token holding the admin scope.", inputs[0].Description);
        Assert.True(inputs[1].Optional);
        Assert.Empty(described[nameof(WithInputs.Plain)].GeneratorInputs!);
    }

    [Fact]
    public async Task AMissingRequiredInput_IsRefused_BeforeTheGeneratorRuns()
    {
        var (result, refused) = await RunAsync(Context(nameof(WithInputs.Value)));
        Assert.Null(result);
        Assert.Contains("Admin credential", refused);
        Assert.Contains("admin", refused);

        var (_, blank) = await RunAsync(Context(nameof(WithInputs.Value), new Dictionary<string, string?> { ["admin"] = "  " }));
        Assert.Contains("Admin credential", blank);
    }

    [Fact]
    public async Task Inputs_ReachTheGenerator_ByCaseInsensitiveName_AndOptionalOnesMayBeAbsent()
    {
        var (result, _) = await RunAsync(Context(nameof(WithInputs.Value), new Dictionary<string, string?> { ["ADMIN"] = AdminSecret }));
        Assert.Equal("default-generated", result!.Values[nameof(WithInputs.Value)]);

        var (named, _) = await RunAsync(Context(nameof(WithInputs.Value),
            new Dictionary<string, string?> { ["admin"] = AdminSecret, ["name"] = "sp.example" }));
        Assert.Equal("sp.example-generated", named!.Values[nameof(WithInputs.Value)]);
    }

    [Fact]
    public async Task AGeneratorFailure_IsARefusalWithItsReason_AndNeverEchoesAnInput()
    {
        var (result, refused) = await RunAsync(Context(nameof(WithInputs.Value), new Dictionary<string, string?> { ["admin"] = "wrong-credential" }));
        Assert.Null(result);
        Assert.Equal("The registry refused the credential (403).", refused);
        Assert.DoesNotContain("wrong-credential", refused);
    }

    [Fact]
    public void NeitherTheContextNorAFailedResult_PrintsAnInput()
    {
        var context = Context(nameof(WithInputs.Value), new Dictionary<string, string?> { ["admin"] = AdminSecret });
        Assert.DoesNotContain(AdminSecret, context.ToString());
        Assert.Equal("WithInputs.Value", context.ToString());
        Assert.DoesNotContain(AdminSecret, ConfigurationGenerationResult.Failed("refused").ToString());
    }

    [Fact]
    public async Task UnknownInputs_AreIgnored_AndAGeneratorWithoutInputsRunsAsBefore()
    {
        var (result, _) = await RunAsync(Context(nameof(WithInputs.Plain), new Dictionary<string, string?> { ["stray"] = "x" }));
        Assert.True(Guid.TryParse(result!.Values[nameof(WithInputs.Plain)], out _));
    }

    // ---- the access-token signing GUID ----

    [Fact]
    public async Task TheAccessTokenSigningSecret_GeneratesAGuid_ThatSignsAndVerifiesAUrl()
    {
        var member = ConfigurationMembers.For<AccessTokenSecretConfiguration>().Members.Single();
        var generated = await ConfigurationGeneration.GenerateAsync(
            new ConfigurationGenerationContext(typeof(AccessTokenSecretConfiguration), member, new Dictionary<string, string?>()),
            result => result, why => throw new Xunit.Sdk.XunitException(why), why => throw new Xunit.Sdk.XunitException(why));
        var value = generated.Values[nameof(AccessTokenSecretConfiguration.SigningSecret)];
        Assert.True(Guid.TryParseExact(value, "D", out _));
        Assert.Contains(generated.Instructions, instruction => instruction.Text.Contains("stop verifying", StringComparison.Ordinal));

        var configuration = new AccessTokenSecretConfiguration { SigningSecret = new Secret(value) };
        Assert.Null(configuration.Validate());
        var prior = StartupConfiguration.Get<AccessTokenSecretConfiguration, AccessTokenSecretConfiguration?>(loaded => loaded, _ => null);
        StartupConfiguration.Set(configuration);
        try
        {
            var original = new Uri("https://api.example.test/files/7?x=1");
            var (session, account) = (Guid.NewGuid(), Guid.NewGuid());
            var signed = original.SignWithAccessTokenAccount(session, account, DateTime.UtcNow.AddHours(1), url => url,
                () => throw new Xunit.Sdk.XunitException("not configured"));
            var token = System.Web.HttpUtility.ParseQueryString(signed.Query)[AccessTokenAccountExtensions.QueryParameter]!;
            var bytes = Convert.FromBase64String(token.Replace('-', '+').Replace('_', '/').PadRight(token.Length + (4 - token.Length % 4) % 4, '='));
            var seconds = BitConverter.ToUInt32(bytes, 32);
            var claimsLength = (int)BitConverter.ToUInt32(bytes, 36);
            var signature = bytes.Skip(40 + claimsLength).ToArray();
            Assert.True(signature.VerifySignature(session, account, seconds, original, () => true, () => false));

            StartupConfiguration.Set(new AccessTokenSecretConfiguration { SigningSecret = new Secret(Guid.NewGuid().ToString()) });
            Assert.False(signature.VerifySignature(session, account, seconds, original, () => true, () => false));
        }
        finally
        {
            if (prior != null) StartupConfiguration.Set(prior);
            else StartupConfiguration.Forget<AccessTokenSecretConfiguration>();
        }
    }
}
