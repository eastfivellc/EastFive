using System;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

using EastFive.Api.Binding.Scopes;
using EastFive.Api.Serialization.Binding;
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

        Assert.Equal(
            """{"id":"0b6f5a52-63a4-4cf2-9d0e-3c43f2f7f0a1","title":"Collect","output_variable":"patient_name","order":2}""",
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
        Assert.StartsWith("'output_variable' must be ", ex.Message);
    }

    [Fact]
    public void ParseKeepsDateLookingStringsAsStrings()
    {
        var token = JsonBinding.Parse("""{"title":"2026-11-09T05:51:33Z"}""");
        Assert.Equal(JTokenType.String, token["title"]!.Type);
        Assert.Throws<JsonReaderException>(() => JsonBinding.Parse("""{} {}"""));
    }

    #endregion
}
