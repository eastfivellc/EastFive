using System;
using System.Collections.Generic;
using System.Linq;

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

    #region Edge cases: structs

    private static EntityStruct Entity() => new()
    {
        Id = StepId,
        Name = "Ada",
        Address = new AddressStruct { Line = "1 Main", Zip = 73301 },
        Addresses = new[] { new AddressStruct { Line = "a", Zip = 1 }, new AddressStruct { Line = "b", Zip = 2 } },
        Count = 9,
    };

    [Fact]
    public void StructsRoundTripThroughTheBoxIncludingNestedStructsAndStructArrays()
    {
        AssertParity(Entity());
        var read = AssertRoundTrip(Entity());
        Assert.Equal(73301, read.Address.Zip);
        Assert.Equal("b", read.Addresses![1].Line);
        Assert.Equal(9, read.Count);
    }

    [Fact]
    public void PopulateWorksOnABoxedStruct()
    {
        var populated = JsonBinding.Populate(JObject.Parse("""{"name":"Grace","address":{"line":"2 Side","zip":5}}"""), Entity());

        Assert.Equal("Grace", populated.Name);
        Assert.Equal(new AddressStruct { Line = "2 Side", Zip = 5 }, populated.Address);
        Assert.Equal(StepId, populated.Id);
        Assert.Equal(9, populated.Count);
        Assert.Equal(2, populated.Addresses!.Length);
    }

    [Fact]
    public void ANestedFailureNamesTheFullKeyPath()
    {
        var ex = Assert.Throws<JsonBindingException>(() =>
            JsonBinding.Read<EntityStruct>(JObject.Parse("""{"addresses":[{"line":"a","zip":1},{"line":"b","zip":"x"}]}""")));

        Assert.Equal("addresses[1].zip", ex.KeyPath);
        Assert.StartsWith("'addresses[1].zip' must be Int32: ", ex.Message);
    }

    #endregion

    #region Edge cases: records and init members

    [Fact]
    public void PositionalRecordsBindThroughTheirPrimaryConstructor()
    {
        var record = new PositionalRecord("n", "Display", 3);
        AssertParity(record);
        Assert.Equal("""{"name":"n","display_name":"Display","count":3}""", Text(JsonBinding.Write(record)));
        Assert.Equal(record, AssertRoundTrip(record));

        var plain = new PlainPositional("p", 2, "note") { Extra = "e" };
        AssertParity(plain);
        Assert.Equal(plain, AssertRoundTrip(plain));

        var partial = JsonBinding.Read<PlainPositional>(JObject.Parse("""{"name":"only"}"""));
        Assert.Equal(new PlainPositional("only", 0, null), partial);
    }

    [Fact]
    public void PopulateWorksOnAPositionalRecord()
    {
        var populated = JsonBinding.Populate(JObject.Parse("""{"count":8}"""), new PositionalRecord("n", "d", 3));
        Assert.Equal(new PositionalRecord("n", "d", 8), populated);
    }

    [Fact]
    public void InitMembersAreSettable()
    {
        var record = new InitRecord { Name = "n", Tags = new[] { "a", "b" } };
        AssertParity(record);
        AssertRoundTrip(record);
        var plain = new PlainInit { Name = "p", Count = 4 };
        AssertParity(plain);
        Assert.Equal(plain, AssertRoundTrip(plain));
    }

    #endregion

    #region Edge cases: refs

    private static readonly Guid RefA = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid RefB = Guid.Parse("22222222-2222-4222-8222-222222222222");

    [Fact]
    public void RefsWriteAsIdsByDeclaredAndRuntimeType()
    {
        var holder = new RefHolder
        {
            Ref = new EastFive.Ref<RefTarget>(RefA),
            Optional = new EastFive.RefOptional<RefTarget>(RefB),
            Refs = new EastFive.Refs<RefTarget>(new[] { RefA, RefB }),
            NullableRef = new EastFive.Ref<RefTarget>(RefB),
            Concrete = new EastFive.Ref<RefTarget>(RefA),
            ConcreteOptional = new EastFive.RefOptional<RefTarget>(RefA),
            RefArray = new EastFive.IRef<RefTarget>[] { new EastFive.Ref<RefTarget>(RefB) },
            Boxed = new EastFive.Ref<RefTarget>(RefA),
        };

        Assert.Equal(
            """{"ref":"11111111-1111-4111-8111-111111111111","optional":"22222222-2222-4222-8222-222222222222","refs":["11111111-1111-4111-8111-111111111111","22222222-2222-4222-8222-222222222222"],"nullable_ref":"22222222-2222-4222-8222-222222222222","concrete":"11111111-1111-4111-8111-111111111111","concrete_optional":"11111111-1111-4111-8111-111111111111","ref_array":["22222222-2222-4222-8222-222222222222"],"boxed":"11111111-1111-4111-8111-111111111111"}""",
            Text(JsonBinding.Write(holder)));
    }

    [Fact]
    public void EmptyAndNullRefsWriteAsNull()
    {
        var holder = new RefHolder
        {
            Ref = null,
            Optional = EastFive.RefOptional<RefTarget>.Empty(),
            Refs = null,
            NullableRef = null,
            ConcreteOptional = new EastFive.RefOptional<RefTarget>((Guid?)null),
        };

        var json = JsonBinding.Write(holder);

        Assert.Equal(JTokenType.Null, json["ref"]!.Type);
        Assert.Equal(JTokenType.Null, json["optional"]!.Type);
        Assert.Equal(JTokenType.Null, json["nullable_ref"]!.Type);
        Assert.Equal(JTokenType.Null, json["concrete_optional"]!.Type);
    }

    [Fact]
    public void RefsRoundTrip()
    {
        var holder = new RefHolder
        {
            Ref = new EastFive.Ref<RefTarget>(RefA),
            Optional = new EastFive.RefOptional<RefTarget>(RefB),
            Refs = new EastFive.Refs<RefTarget>(new[] { RefA, RefB }),
            NullableRef = new EastFive.Ref<RefTarget>(RefB),
            Concrete = new EastFive.Ref<RefTarget>(RefA),
            ConcreteOptional = new EastFive.RefOptional<RefTarget>(RefB),
            RefArray = new EastFive.IRef<RefTarget>[] { new EastFive.Ref<RefTarget>(RefB) },
        };

        foreach (var json in new[] { JsonBinding.Write(holder), JToken.Parse(Text(JsonBinding.Write(holder))) })
        {
            var read = JsonBinding.Read<RefHolder>(json);
            Assert.Equal(RefA, read.Ref!.id);
            Assert.Equal(RefB, read.Optional!.id);
            Assert.Equal(new[] { RefA, RefB }, read.Refs!.ids);
            Assert.Equal(RefB, read.NullableRef!.Value.id);
            Assert.Equal(RefA, read.Concrete.id);
            Assert.Equal(RefB, read.ConcreteOptional.id);
            Assert.Equal(RefB, Assert.Single(read.RefArray!).id);
        }
    }

    [Theory]
    [InlineData("""{"optional":null}""")]
    [InlineData("""{"optional":""}""")]
    [InlineData("""{"optional":"00000000-0000-0000-0000-000000000000"}""")]
    [InlineData("""{}""")]
    public void AnEmptyOptionalRefReadsAsAnEmptyOptional(string json)
    {
        var read = JsonBinding.Read<RefHolder>(JObject.Parse(json));
        Assert.NotNull(read.Optional);
        Assert.False(read.Optional!.HasValue);
    }

    [Fact]
    public void NullRefsReadAsNullAndNullRefsCollectionsAsEmpty()
    {
        var read = JsonBinding.Read<RefHolder>(JObject.Parse("""{"ref":null,"refs":null,"nullable_ref":null}"""));
        Assert.Null(read.Ref);
        Assert.Null(read.NullableRef);
        Assert.NotNull(read.Refs);
        Assert.Empty(read.Refs!.ids);
    }

    [Theory]
    [InlineData("""{"ref":{"id":"11111111-1111-4111-8111-111111111111"}}""", "ref")]
    [InlineData("""{"optional":{"HasValue":false}}""", "optional")]
    [InlineData("""{"concrete_optional":{"id":null,"HasValue":false}}""", "concrete_optional")]
    [InlineData("""{"refs":[{"id":"11111111-1111-4111-8111-111111111111"}]}""", "refs[0]")]
    [InlineData("""{"ref_array":[{"id":"11111111-1111-4111-8111-111111111111"}]}""", "ref_array[0]")]
    public void ObjectShapedRefsAreRefusedWithTheirKeyPath(string json, string keyPath)
    {
        var ex = Assert.Throws<JsonBindingException>(() => JsonBinding.Read<RefHolder>(JObject.Parse(json)));
        Assert.Equal(keyPath, ex.KeyPath);
    }

    [Fact]
    public void PopulateKeepsAnAbsentOptionalRef()
    {
        var existing = new RefHolder { Optional = new EastFive.RefOptional<RefTarget>(RefA) };
        var populated = JsonBinding.Populate(JObject.Parse("""{"ref":"22222222-2222-4222-8222-222222222222"}"""), existing);
        Assert.Equal(RefA, populated.Optional!.id);
        Assert.Equal(RefB, populated.Ref!.id);
    }

    #endregion

    #region Edge cases: JToken members

    [Fact]
    public void JTokenMembersPassThroughVerbatimAndAreClonedBothWays()
    {
        var obj = JObject.Parse("""{"a":[1,{"b":"2026-11-09T05:51:33Z"}],"c":1.10}""");
        var holder = new TokenHolder { Obj = obj, Arr = new JArray(1, "two", JValue.CreateNull()), Any = new JValue("x") };
        AssertParity(holder);

        var json = JsonBinding.Write(holder);
        obj["c"] = 99;
        Assert.Equal(1.10m, json["obj"]!["c"]!.Value<decimal>());

        var read = JsonBinding.Read<TokenHolder>(json);
        Assert.True(JToken.DeepEquals(json["obj"], read.Obj));
        Assert.True(JToken.DeepEquals(json["arr"], read.Arr));
        Assert.True(JToken.DeepEquals(json["any"], read.Any));
        ((JObject)json["obj"]!)["c"] = 100;
        Assert.Equal(1.10m, read.Obj!["c"]!.Value<decimal>());

        var viaText = JsonBinding.Read<TokenHolder>(JToken.Parse(Text(JsonBinding.Write(new TokenHolder { Any = new JObject { ["k"] = "v" } }))));
        Assert.Equal("v", (string?)viaText.Any!["k"]);
    }

    [Fact]
    public void AJObjectMemberRefusesANonObject()
    {
        var ex = Assert.Throws<JsonBindingException>(() => JsonBinding.Read<TokenHolder>(JObject.Parse("""{"obj":[1]}""")));
        Assert.Equal("obj", ex.KeyPath);
    }

    #endregion

    #region Edge cases: object members and dictionaries

    private static Bag FullBag() => new()
    {
        Extra = new Dictionary<string, object?>
        {
            ["s"] = "x",
            ["n"] = 1L,
            ["f"] = 1.5,
            ["b"] = true,
            ["z"] = null,
            ["o"] = new Dictionary<string, object?> { ["k"] = "v" },
            ["a"] = new object?[] { 1L, "two" },
            ["dotted.key"] = "d",
        },
        IExtra = new Dictionary<string, object?> { ["i"] = 2L },
        Labels = new Dictionary<string, string?> { ["en"] = "Hello", ["none"] = null },
        ILabels = new Dictionary<string, string> { ["fr"] = "Bonjour" },
        Addresses = new Dictionary<string, AddressStruct> { ["home"] = new AddressStruct { Line = "1", Zip = 2 } },
        Value = "plain",
    };

    [Fact]
    public void DictionariesAndObjectsWriteByRuntimeType()
    {
        AssertParity(FullBag());
        AssertParity(FullBag(), omitNulls: true);
        AssertParity(new Bag { Value = new AddressStruct { Line = "l", Zip = 3 } });
        AssertParity(new Bag { Value = 42 });
        AssertParity(new Bag { Extra = new Dictionary<string, object?>() });
    }

    [Fact]
    public void DictionariesAndObjectsReadToJsonNatives()
    {
        AssertRoundTrip(FullBag());

        var read = JsonBinding.Read<Bag>(JsonBinding.Write(new Bag { Extra = new Dictionary<string, object?>() }));
        Assert.NotNull(read.Extra);
        Assert.Empty(read.Extra!);
        Assert.IsType<Dictionary<string, object?>>(JsonBinding.Read<Bag>(JsonBinding.Write(FullBag())).IExtra);
    }

    [Fact]
    public void AnObjectMemberReadsADateLookingStringAsTheString()
    {
        var parsed = JObject.Parse("""{"value":"2026-11-09T05:51:33Z","extra":{"when":"2026-11-09T05:51:33Z"}}""");
        var read = JsonBinding.Read<Bag>(parsed);
        Assert.Equal("2026-11-09T05:51:33Z", read.Value);
        Assert.Equal("2026-11-09T05:51:33Z", read.Extra!["when"]);
    }

    [Fact]
    public void AnObjectMemberHoldingAGuidReadsTheString()
    {
        var read = JsonBinding.Read<Bag>(JsonBinding.Write(new Bag { Value = RefA }));
        Assert.Equal(RefA.ToString(), read.Value);
    }

    #endregion

    #region Edge cases: enums

    [Fact]
    public void EnumsWriteNames()
    {
        var json = JsonBinding.Write(new EnumHolder { Color = Color.Green, Perms = Perms.Read | Perms.Write, Maybe = Color.Blue });
        Assert.Equal("""{"color":"Green","perms":"Read, Write","maybe":"Blue"}""", Text(json));
    }

    [Theory]
    [InlineData("""{"color":"green","perms":"read, exec","maybe":null}""")]
    [InlineData("""{"color":1,"perms":5}""")]
    public void EnumsReadNamesCaseInsensitivelyAndIntegers(string json)
    {
        var read = JsonBinding.Read<EnumHolder>(JObject.Parse(json));
        Assert.Equal(Color.Green, read.Color);
        Assert.Equal(Perms.Read | Perms.Exec, read.Perms);
        Assert.Null(read.Maybe);
    }

    [Fact]
    public void FlagsEnumsRoundTrip()
    {
        AssertRoundTrip(new EnumHolder { Color = Color.Blue, Perms = Perms.Read | Perms.Write | Perms.Exec, Maybe = Color.Red });
        AssertRoundTrip(new EnumHolder { Perms = Perms.None });
    }

    #endregion

    #region Edge cases: nullable value types

    [Fact]
    public void NullableValueTypesKeepNullAndValues()
    {
        var empty = new NullableHolder();
        AssertParity(empty);
        AssertRoundTrip(empty);

        var full = new NullableHolder
        {
            When = new DateTime(2026, 11, 9, 5, 51, 33, DateTimeKind.Utc),
            Id = RefA,
            Count = 0,
            Flag = false,
            Color = Color.Red,
        };
        AssertRoundTrip(full);
        AssertParity(new NullableHolder { When = full.When, Id = RefA, Count = 0, Flag = false });
    }

    [Fact]
    public void AbsentNullableValueTypesStayDefault()
    {
        var read = JsonBinding.Read<NullableHolder>(new JObject());
        Assert.Null(read.When);
        Assert.Null(read.Id);
        Assert.Null(read.Count);
        Assert.Null(read.Flag);
        Assert.Null(read.Color);
    }

    #endregion

    #region Edge cases: dates

    private static DateHolder Dates() => new()
    {
        Utc = new DateTime(2026, 11, 9, 5, 51, 33, 123, DateTimeKind.Utc),
        Local = new DateTime(2026, 11, 9, 5, 51, 33, DateTimeKind.Local),
        Unspecified = new DateTime(1930, 12, 30),
        Offset = new DateTimeOffset(2026, 11, 9, 5, 51, 33, TimeSpan.FromHours(5)),
        LooksLikeIso = "2026-11-09T05:51:33Z",
        LooksLikeUs = "12/30/1930",
    };

    [Fact]
    public void DatesRoundTripWithKindAndOffset()
    {
        AssertParity(Dates());
        foreach (var json in new[] { JsonBinding.Write(Dates()), JToken.Parse(Text(JsonBinding.Write(Dates()))), JsonBinding.Parse(Text(JsonBinding.Write(Dates()))) })
        {
            var read = JsonBinding.Read<DateHolder>(json);
            Assert.Equal(Dates().Utc, read.Utc);
            Assert.Equal(DateTimeKind.Utc, read.Utc.Kind);
            Assert.Equal(Dates().Local, read.Local);
            Assert.Equal(DateTimeKind.Local, read.Local.Kind);
            Assert.Equal(Dates().Unspecified, read.Unspecified);
            Assert.Equal(DateTimeKind.Unspecified, read.Unspecified.Kind);
            Assert.Equal(Dates().Offset, read.Offset);
        }

        // JToken.Parse's default date sniffing converts "+05:00" to local time before
        // any binder runs (stock ToObject loses it the same way); the written token and
        // JsonBinding.Parse keep the offset.
        foreach (var json in new[] { JsonBinding.Write(Dates()), JsonBinding.Parse(Text(JsonBinding.Write(Dates()))) })
            Assert.Equal(TimeSpan.FromHours(5), JsonBinding.Read<DateHolder>(json).Offset.Offset);
    }

    [Fact]
    public void StringsThatLookLikeDatesReadBackByteIdentical()
    {
        const string iso = "2026-11-09T05:51:33Z";
        const string isoFraction = "2026-11-09T05:51:33.1234567Z";
        const string us = "12/30/1930";

        var parsed = JObject.Parse($$"""{"looks_like_iso":"{{iso}}","looks_like_us":"{{us}}"}""");
        Assert.Equal(JTokenType.Date, parsed["looks_like_iso"]!.Type); // the trap
        var fromParse = JsonBinding.Read<DateHolder>(parsed);
        Assert.Equal(iso, fromParse.LooksLikeIso);
        Assert.Equal(us, fromParse.LooksLikeUs);

        var fraction = JsonBinding.Read<DateHolder>(JObject.Parse($$"""{"looks_like_iso":"{{isoFraction}}"}"""));
        Assert.Equal(isoFraction, fraction.LooksLikeIso);

        var fromObject = JsonBinding.Read<DateHolder>(JObject.FromObject(new { looks_like_iso = iso, looks_like_us = us }));
        Assert.Equal(iso, fromObject.LooksLikeIso);
        Assert.Equal(us, fromObject.LooksLikeUs);

        var viaParse = JsonBinding.Read<DateHolder>(JsonBinding.Parse($$"""{"looks_like_iso":"2026-11-09T05:51:33.000+00:00"}"""));
        Assert.Equal("2026-11-09T05:51:33.000+00:00", viaParse.LooksLikeIso);

        Assert.Equal(Dates().LooksLikeIso, AssertRoundTrip(Dates()).LooksLikeIso);
    }

    #endregion

    #region Edge cases: numbers

    private static NumberHolder Numbers() => new()
    {
        Money = 12345678901234567890.123456789m,
        Big = 9007199254740993L,
        I = int.MinValue,
        S = short.MaxValue,
        B = byte.MaxValue,
        F = 0.1f,
        D = 0.1,
        U = ulong.MaxValue,
        Span = new TimeSpan(1, 2, 3, 4, 500),
        Bytes = new byte[] { 0, 1, 254, 255 },
        Text = "007",
    };

    [Fact]
    public void NumbersWriteExactly()
    {
        AssertParity(Numbers());
        var json = Text(JsonBinding.Write(Numbers()));
        Assert.Contains("\"money\":12345678901234567890.123456789", json);
        Assert.Contains("\"big\":9007199254740993", json);
        Assert.Contains("\"f\":0.1,", json);
        Assert.Contains("\"u\":18446744073709551615", json);
        Assert.Contains("\"span\":\"1.02:03:04.5000000\"", json);
        Assert.Contains("\"bytes\":\"AAH+/w==\"", json);
    }

    [Fact]
    public void NumbersRoundTrip()
    {
        var read = JsonBinding.Read<NumberHolder>(JsonBinding.Write(Numbers()));
        Assert.Equivalent(Numbers(), read, strict: true);
        Assert.Equal(Numbers().Money, read.Money);

        var viaText = Numbers();
        viaText.Money = 1234567.89m;
        AssertRoundTrip(viaText);
    }

    [Fact]
    public void StringsReadIntoNumbersAndNumbersIntoStrings()
    {
        var read = JsonBinding.Read<NumberHolder>(JObject.Parse("""{"money":"12.50","i":"42","d":"1.5","f":"0.25","u":"18446744073709551615","big":"9007199254740993","text":1.50}"""));
        Assert.Equal(12.50m, read.Money);
        Assert.Equal("12.50", read.Money.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(42, read.I);
        Assert.Equal(1.5, read.D);
        Assert.Equal(0.25f, read.F);
        Assert.Equal(ulong.MaxValue, read.U);
        Assert.Equal(9007199254740993L, read.Big);
        Assert.Equal("1.5", read.Text);

        Assert.Equal("42", JsonBinding.Read<NumberHolder>(JObject.Parse("""{"text":42}""")).Text);
        Assert.Equal("12.50", JsonBinding.Read<NumberHolder>(JObject.FromObject(new { text = 12.50m })).Text);
        Assert.Equal("true", JsonBinding.Read<NumberHolder>(JObject.Parse("""{"text":true}""")).Text);
    }

    [Fact]
    public void AnOverflowingNumberFailsWithItsKeyPath()
    {
        var ex = Assert.Throws<JsonBindingException>(() => JsonBinding.Read<NumberHolder>(JObject.Parse("""{"b":300}""")));
        Assert.Equal("b", ex.KeyPath);
    }

    #endregion

    #region Edge cases: collections

    private static CollectionHolder Collections() => new()
    {
        Nulls = null,
        Empties = Array.Empty<string>(),
        List = new List<string> { "a", "b" },
        Seq = new[] { 1, 2, 3 },
        IList = new List<AddressStruct> { new() { Line = "l", Zip = 1 } },
        ReadOnly = new List<string> { "r" },
        Set = new HashSet<string> { "s" },
        EmptyObject = new EmptyPlain(),
        EmptyList = new List<int>(),
    };

    [Fact]
    public void CollectionsWriteAsArrays()
    {
        AssertParity(Collections());
        AssertParity(Collections(), omitNulls: true);
    }

    [Fact]
    public void CollectionsRoundTripAndNullStaysDistinctFromEmpty()
    {
        var read = AssertRoundTrip(Collections());
        Assert.Null(read.Nulls);
        Assert.NotNull(read.Empties);
        Assert.Empty(read.Empties!);
        Assert.NotNull(read.EmptyObject);
        Assert.NotNull(read.EmptyList);
        Assert.Equal(new[] { 1, 2, 3 }, read.Seq);
    }

    #endregion

    #region Edge cases: missing and unknown keys, computed members

    [Fact]
    public void UnknownKeysAreIgnoredAndMissingMembersKeepTheirDefault()
    {
        var read = JsonBinding.Read<StepDefinition>(JObject.Parse("""{"title":"t","unknown":1,"nested":{"x":[1]}}"""));
        Assert.Equal("t", read.Title);
        Assert.Equal(Guid.Empty, read.Id);
        Assert.Null(read.OutputVariable);
        Assert.Equal(0, read.Order);
    }

    [Fact]
    public void GetOnlyAndComputedMembersAreNeverWrittenOrRead()
    {
        Assert.Equal("""{"name":"n"}""", Text(JsonBinding.Write(new ComputedHolder { Name = "n" })));
        Assert.Equal("""{"Name":"abc"}""", Text(JsonBinding.Write(new PlainComputed { Name = "abc" })));

        var read = JsonBinding.Read<ComputedHolder>(JObject.Parse("""{"name":"n","upper":"X","fixed":1}"""));
        Assert.Equal("N", read.Upper);
        Assert.Equal(7, read.Fixed);
        Assert.Equal(3, JsonBinding.Read<PlainComputed>(JObject.Parse("""{"Name":"abc","Length":99}""")).Length);
    }

    #endregion

    #region Edge cases: [JsonConverter]

    [Fact]
    public void StringEnumConverterMembersAreAcceptedAndMatchNewtonsoft()
    {
        var holder = new EnumConverterHolder { Color = Color.Blue, Maybe = Color.Green };
        AssertParity(holder);
        AssertRoundTrip(holder);
    }

    [Fact]
    public void AnyOtherJsonConverterFailsLoudly()
    {
        var member = Assert.Throws<NotSupportedException>(() => JsonBinding.Write(new CustomConverterHolder { Value = "v" }));
        Assert.Contains(nameof(CustomConverterHolder.Value), member.Message);
        Assert.Contains(nameof(UpperConverter), member.Message);

        Assert.Throws<NotSupportedException>(() => JsonBinding.Read<CustomConverterHolder>(JObject.Parse("""{"value":"v"}""")));
        Assert.Throws<NotSupportedException>(() => JsonBinding.Write(new PlainCustomConverterHolder { Value = "v" }));

        var type = Assert.Throws<NotSupportedException>(() => JsonBinding.Write(new ConvertedTypeHolder { Converted = new ConvertedType { Value = "v" } }));
        Assert.Contains(nameof(ConvertedTypeConverter), type.Message);
    }

    #endregion

    #region Review follow-ups

    [Fact]
    public void NullCollectionElementsRoundTrip()
    {
        var items = new NullItems
        {
            Tags = new List<string?> { "a", null },
            Names = new string?[] { null, "b" },
            Maybe = new AddressStruct?[] { null, new AddressStruct { Line = "l", Zip = 1 } },
            Shapes = new BaseShape?[] { null, new BaseShape { Name = "s" } },
        };
        AssertParity(items);
        AssertRoundTrip(items);
    }

    [Fact]
    public void NestedValuesWriteByRuntimeType()
    {
        var holder = new ShapeHolder
        {
            One = new DerivedShape { Name = "n", Extra = 7 },
            Many = new List<BaseShape> { new DerivedShape { Name = "m", Extra = 8 }, new BaseShape { Name = "b" } },
            Iface = new ShapeImpl { Name = "i", Sides = 3 },
            Lazy = Enumerable.Range(1, 3).Select(i => i * 2),
        };
        AssertParity(holder);
    }

    [Fact]
    public void WireNamesWithPathCharactersRoundTrip()
    {
        var value = new ODataLike { Meta = "m", First = "f", Plain = "p" };
        AssertParity(value);
        AssertRoundTrip(value);
        var populated = JsonBinding.Populate(JObject.Parse("""{"odata.metadata":"m2"}"""), new ODataLike { Meta = "m", First = "f" });
        Assert.Equal("m2", populated.Meta);
        Assert.Equal("f", populated.First);
    }

    [Fact]
    public void ParseKeepsDecimalsExactAndDoublesIntact()
    {
        var numbers = Numbers();
        numbers.Money = 0.1234567890123456789012345m;
        numbers.D = 1e-30;
        var read = JsonBinding.Read<NumberHolder>(JsonBinding.Parse(Text(JsonBinding.Write(numbers))));
        Assert.Equal(0.1234567890123456789012345m, read.Money);
        Assert.Equal(1e-30, read.D);

        numbers.D = 1e300;
        Assert.Equal(1e300, JsonBinding.Read<NumberHolder>(JsonBinding.Parse(Text(JsonBinding.Write(numbers)))).D);
    }

    [Fact]
    public void DataContractAndIgnoreDataMemberFollowNewtonsoft()
    {
        var contract = new ContractPlain { Id = 1, Label = "l", Created = 2, Secret = "s" };
        AssertParity(contract);
        Assert.Equal("""{"id":1,"label":"l","hidden":"s"}""", Text(JsonBinding.Write(contract)));
        var read = JsonBinding.Read<ContractPlain>(JsonBinding.Write(contract));
        Assert.Equal("s", read.Secret);
        Assert.Equal(0, read.Created);

        var ignored = new IgnoreDataPlain { Name = "n", Secret = "s", Transient = "t" };
        AssertParity(ignored);
        Assert.Equal("""{"Name":"n"}""", Text(JsonBinding.Write(ignored)));
    }

    [Fact]
    public void UrisWriteTheirOriginalString()
    {
        var holder = new UriHolder { Link = new Uri("HTTP://Example.com/a%20b") };
        AssertParity(holder);
        Assert.Equal("HTTP://Example.com/a%20b", JsonBinding.Read<UriHolder>(JsonBinding.Write(holder)).Link!.OriginalString);
        Assert.Equal("/relative", JsonBinding.Read<UriHolder>(JObject.Parse("""{"Link":"/relative"}""")).Link!.OriginalString);
    }

    [Fact]
    public void RuntimeTypesWithOnlyParameterisedConstructorsStillWrite()
    {
        var holder = new ConstructedHolder
        {
            Shape = new ConstructedShape(3) { Name = "tri" },
            Only = new ConstructedOnly("v"),
        };
        AssertParity(holder);
    }

    [Fact]
    public void ParseDecidesDecimalPrecisionPerValue()
    {
        var token = JsonBinding.Parse("""{"money":1234567890123456.78,"d":1e30,"tiny":1e-30}""");
        Assert.Equal(1234567890123456.78m, token["money"]!.Value<decimal>());
        Assert.IsType<decimal>(((JValue)token["money"]!).Value);
        Assert.Equal(1e30, token["d"]!.Value<double>());
        Assert.Equal(1e-30, token["tiny"]!.Value<double>());
    }

    #endregion
}
