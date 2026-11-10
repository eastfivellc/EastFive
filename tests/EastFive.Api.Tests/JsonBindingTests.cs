using System;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

using EastFive.Api.Binding.Scopes;
using EastFive.Api.Serialization.Binding;
using EastFive.Api.Serialization.Json;
using EastFive.Api.Tests.JsonBindingFixtures;

namespace EastFive.Api.Tests;

/// <summary>
/// <see cref="JsonBinding"/>: entity JSON written and read through the V3
/// binding framework with no request and no application.
/// </summary>
public class JsonBindingTests
{
    private static readonly Guid StepId = Guid.Parse("0b6f5a52-63a4-4cf2-9d0e-3c43f2f7f0a1");

    private static string Text(JToken token) => token.ToString(Formatting.None);

    /// <summary>Today's wire: Newtonsoft with <see cref="ApiPropertyContractResolver"/>.</summary>
    private static string Newtonsoft(object value, bool omitNulls = false) =>
        Text(JToken.FromObject(value, JsonSerializer.Create(new JsonSerializerSettings
        {
            ContractResolver = ApiPropertyContractResolver.Instance,
            NullValueHandling = omitNulls ? NullValueHandling.Ignore : NullValueHandling.Include,
        })));

    /// <summary>Differential: the façade writes exactly today's wire text (so also DeepEquals it).</summary>
    private static void AssertParity(object value, bool omitNulls = false)
    {
        var expected = Newtonsoft(value, omitNulls);
        var actual = JsonBinding.Write(value, options: new JsonBindingOptions { OmitNulls = omitNulls });
        Assert.Equal(expected, Text(actual));
        Assert.True(JToken.DeepEquals(JToken.Parse(expected), JToken.Parse(Text(actual))));
    }

    /// <summary>Write → Read gives an equal value, including when the JSON went through text.</summary>
    private static T AssertRoundTrip<T>(T value)
    {
        var json = JsonBinding.Write(value, typeof(T));
        var direct = JsonBinding.Read<T>(json);
        Assert.Equivalent(value, direct, strict: true);
        var viaText = JsonBinding.Read<T>(JToken.Parse(Text(json)));
        Assert.Equivalent(value, viaText, strict: true);
        return direct;
    }

    private static StepDefinition Step() => new()
    {
        Id = StepId,
        Title = "Collect",
        OutputVariable = "patient_name",
        Order = 2,
        Untagged = "not on the wire",
    };

    #region Façade

    [Fact]
    public void WriteUsesTheResponseBodyScopeByDefault()
    {
        var json = JsonBinding.Write(Step());
        AssertParity(Step());

        Assert.Equal(
            """{"order":2,"id":"0b6f5a52-63a4-4cf2-9d0e-3c43f2f7f0a1","title":"Collect","output_variable":"patient_name"}""",
            Text(json));
    }

    [Fact]
    public void WriteHonoursAnExplicitMemberScope()
    {
        var json = JsonBinding.Write(Step(), memberScope: typeof(PatchBody));

        Assert.Null(json["id"]);
        Assert.Equal("Collect", (string?)json["title"]);
    }

    [Fact]
    public void WriteOfNullIsJsonNull()
    {
        Assert.Equal(JTokenType.Null, JsonBinding.Write(null!).Type);
    }

    [Fact]
    public void ReadRoundTripsAWrite()
    {
        var read = JsonBinding.Read<StepDefinition>(JsonBinding.Write(Step()));

        Assert.Equal(StepId, read.Id);
        Assert.Equal("Collect", read.Title);
        Assert.Equal("patient_name", read.OutputVariable);
        Assert.Equal(2, read.Order);
        Assert.Null(read.Untagged);
    }

    [Fact]
    public void ReadOfJsonNullIsDefault()
    {
        Assert.Null(JsonBinding.Read<StepDefinition>(JValue.CreateNull()));
        Assert.Null(JsonBinding.Read(JValue.CreateNull(), typeof(StepDefinition)));
    }

    [Fact]
    public void ReadCompletesSynchronously()
    {
        // No SynchronizationContext tricks: the call returns a value, not a task,
        // and completes on the calling thread.
        var thread = Environment.CurrentManagedThreadId;
        var read = JsonBinding.Read<StepDefinition>(JObject.Parse("""{"title":"t"}"""));
        Assert.Equal("t", read.Title);
        Assert.Equal(thread, Environment.CurrentManagedThreadId);
    }

    [Fact]
    public void PopulateOverwritesPresentMembersAndKeepsAbsentOnes()
    {
        var existing = Step();

        var populated = JsonBinding.Populate(JObject.Parse("""{"title":"Renamed","order":5}"""), existing);

        Assert.Same(existing, populated);
        Assert.Equal("Renamed", populated.Title);
        Assert.Equal(5, populated.Order);
        Assert.Equal(StepId, populated.Id);
        Assert.Equal("patient_name", populated.OutputVariable);
        Assert.Equal("not on the wire", populated.Untagged);
    }

    [Fact]
    public void PopulateWithJsonNullKeepsTheExistingValue()
    {
        var existing = Step();
        Assert.Same(existing, JsonBinding.Populate(JValue.CreateNull(), existing));
    }

    [Fact]
    public void PopulateOfNullIsARead()
    {
        var read = JsonBinding.Populate<StepDefinition?>(JObject.Parse("""{"title":"t"}"""), null);
        Assert.Equal("t", read!.Title);
    }

    [Fact]
    public void ABindFailureThrowsWithTheKeyPath()
    {
        var ex = Assert.Throws<JsonBindingException>(() =>
            JsonBinding.Read<StepDefinition>(JObject.Parse("""{"title":"t","output_variable":{"nested":true}}""")));

        Assert.Equal("output_variable", ex.KeyPath);
        Assert.StartsWith("'output_variable' must be String: ", ex.Message);
    }

    [Fact]
    public void ParseKeepsDateLookingStringsAsStrings()
    {
        var token = JsonBinding.Parse("""{"title":"2026-11-09T05:51:33Z"}""");
        Assert.Equal(JTokenType.String, token["title"]!.Type);
        Assert.Throws<JsonReaderException>(() => JsonBinding.Parse("""{} {}"""));
    }

    #endregion

    #region Member selection (parity with ApiPropertyContractResolver)

    private static GovernedScalars Governed() => new()
    {
        Name = "n",
        Tagged = "t",
        NotTagged = "x",
        Count = 3,
        Ratio = 0.25,
        When = new DateTime(2026, 11, 9, 5, 51, 33, 120, DateTimeKind.Utc),
        Maybe = null,
        Flag = true,
    };

    private static PlainScalars Plain()
    {
        var plain = new PlainScalars
        {
            Name = "n",
            Display = "d",
            Secret = "s",
            Count = 4,
            Flag = true,
            MaybeId = null,
        };
        plain.Hidden = "h";
        return plain;
    }

    [Fact]
    public void GovernedTypesWriteOnlyTaggedMembersUnderTaggedNamesFieldsFirst()
    {
        Assert.Equal(
            """{"count":3,"name":"n","tagged":"t","Ratio":0.25,"when":"2026-11-09T05:51:33.12Z","maybe":null,"flag":true}""",
            Text(JsonBinding.Write(Governed())));
        AssertParity(Governed());
        AssertParity(Governed(), omitNulls: true);
    }

    [Fact]
    public void UngovernedTypesWriteNewtonsoftsDefaultMembers()
    {
        Assert.Equal(
            """{"Count":4,"hidden":"h","Name":"n","display":"d","Flag":true,"MaybeId":null,"Hidden":"h"}""",
            Text(JsonBinding.Write(Plain())));
        AssertParity(Plain());
        AssertParity(Plain(), omitNulls: true);
    }

    [Fact]
    public void OptInTypesWriteOnlyJsonPropertyMembers()
    {
        AssertParity(new OptInPlain { A = 1, B = 2 });
        Assert.Equal("""{"a":1}""", Text(JsonBinding.Write(new OptInPlain { A = 1, B = 2 })));
    }

    [Fact]
    public void GovernanceFollowsTheDeclaringType()
    {
        var value = new DerivedFromGoverned { BaseName = "b", BaseUntagged = "u", Extra = "e" };
        AssertParity(value);
        Assert.Equal("""{"base_name":"b"}""", Text(JsonBinding.Write(value)));
    }

    [Fact]
    public void GovernedAndUngovernedTypesNest()
    {
        AssertParity(new PlainWrapper { Label = "l", Inner = Governed() });
        AssertParity(new GovernedWrapper { Label = "l", Plain = Plain() });
        AssertParity(new GovernedWrapper { Label = "l", Plain = null }, omitNulls: true);
    }

    [Fact]
    public void MemberSelectionRoundTrips()
    {
        var governed = Governed();
        governed.NotTagged = null;
        AssertRoundTrip(governed);
        Assert.Null(JsonBinding.Read<GovernedScalars>(JsonBinding.Write(Governed())).NotTagged);
        var plain = JsonBinding.Read<PlainScalars>(JsonBinding.Write(Plain()));
        Assert.Equal("h", plain.Hidden);
        Assert.Equal("d", plain.Display);
        Assert.Null(plain.Secret);
        var nested = JsonBinding.Read<GovernedWrapper>(JsonBinding.Write(new GovernedWrapper { Label = "l", Plain = Plain() }));
        Assert.Equal("h", nested.Plain!.Hidden);
    }

    #endregion
}
