using System;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

using EastFive.Api.Binding.Scopes;
using EastFive.Api.Serialization.Binding.Sinks;
using EastFive.Serialization.Binding;

using ApiPropertyV3 = EastFive.Api.Binding.ApiPropertyAttribute;

namespace EastFive.Api.Tests;

/// <summary>
/// <see cref="JTokenBindingSink"/>: the JSON write-side twin of
/// <c>JTokenBindingSource</c>, building a token tree with no request.
/// </summary>
public class JTokenBindingSinkTests
{
    private static string Text(JTokenBindingSink sink) =>
        sink.ToToken()?.ToString(Formatting.None) ?? "<absent>";

    [Fact]
    public void ScalarsSetTheSlotValue()
    {
        var id = Guid.Parse("8d1c6d0e-55a4-4f63-8c7a-3a3c3f0f2b11");
        var when = new DateTime(2026, 11, 9, 5, 51, 33, DateTimeKind.Utc);
        var sink = new JTokenBindingSink();
        sink.Scope("s").WriteString("x");
        sink.Scope("g").WriteGuid(id);
        sink.Scope("b").WriteBool(true);
        sink.Scope("i").WriteInt32(7);
        sink.Scope("l").WriteInt64(9007199254740993L);
        sink.Scope("d").WriteDouble(1.5);
        sink.Scope("t").WriteDateTime(when);
        sink.Scope("y").WriteBytes(new byte[] { 1, 2, 3 });

        Assert.Equal(
            """{"s":"x","g":"8d1c6d0e-55a4-4f63-8c7a-3a3c3f0f2b11","b":true,"i":7,"l":9007199254740993,"d":1.5,"t":"2026-11-09T05:51:33Z","y":"AQID"}""",
            Text(sink));
    }

    [Fact]
    public void ScopesNestAndArraysAppend()
    {
        var sink = new JTokenBindingSink();
        var inner = sink.Scope("outer").Scope("inner");
        inner.WriteString("deep");
        var list = sink.Scope("list");
        list.AppendItem().WriteInt64(1);
        list.AppendItem().Scope("k").WriteString("v");

        Assert.Equal("""{"outer":{"inner":"deep"},"list":[1,{"k":"v"}]}""", Text(sink));
    }

    [Fact]
    public void MembersKeepFirstScopedOrderAndARepeatedKeyReturnsTheSameSlot()
    {
        var sink = new JTokenBindingSink();
        sink.Scope("z").Scope("a").WriteInt64(1);
        sink.Scope("a").WriteInt64(2);
        sink.Scope("z").Scope("b").WriteInt64(3);

        Assert.Equal("""{"z":{"a":1,"b":3},"a":2}""", Text(sink));
    }

    [Fact]
    public void AScopeThatReceivesNothingStaysAbsent()
    {
        var sink = new JTokenBindingSink();
        sink.Scope("present").WriteString("p");
        sink.Scope("absent");

        Assert.Equal("""{"present":"p"}""", Text(sink));
        Assert.Null(new JTokenBindingSink().ToToken());
    }

    [Fact]
    public void WriteNullWritesJsonNull()
    {
        var sink = new JTokenBindingSink();
        sink.Scope("n").WriteNull();
        sink.Scope("s").WriteString(null!);

        Assert.Equal("""{"n":null,"s":null}""", Text(sink));
    }

    [Fact]
    public void OmitNullsDropsNullMembersButKeepsNullItemsAndEntries()
    {
        var sink = new JTokenBindingSink(omitNulls: true);
        sink.Scope("n").WriteNull();
        sink.Scope("v").WriteString("x");
        var list = sink.Scope("list");
        list.AppendItem().WriteNull();
        sink.ScopeEntry("entry").WriteNull();

        Assert.True(sink.OmitNulls);
        Assert.Equal("""{"v":"x","list":[null],"entry":null}""", Text(sink));
    }

    [Fact]
    public void WriteTokenPutsADeepCloneVerbatim()
    {
        var raw = JObject.Parse("""{"a":[1,{"b":2}]}""");
        var sink = new JTokenBindingSink();
        sink.Scope("raw").WriteToken(raw);
        sink.Scope("dec").WriteToken(new JValue(1.10m));
        sink.Scope("nul").WriteToken(JValue.CreateNull());
        raw["a"]![0] = 99;

        Assert.Equal("""{"raw":{"a":[1,{"b":2}]},"dec":1.10,"nul":null}""", Text(sink));
    }

    [Fact]
    public void AScalarWrittenTwiceThrows()
    {
        var sink = new JTokenBindingSink();
        var slot = sink.Scope("x");
        slot.WriteString("a");
        Assert.Throws<InvalidOperationException>(() => slot.WriteString("b"));
        Assert.Throws<InvalidOperationException>(() => slot.WriteNull());
    }

    [Fact]
    public void ShapeMismatchesThrow()
    {
        var scalar = new JTokenBindingSink();
        scalar.WriteInt64(1);
        Assert.Throws<InvalidOperationException>(() => scalar.Scope("k"));
        Assert.Throws<InvalidOperationException>(() => scalar.AppendItem());

        var obj = new JTokenBindingSink();
        obj.Scope("k").WriteInt64(1);
        Assert.Throws<InvalidOperationException>(() => obj.AppendItem());
        Assert.Throws<InvalidOperationException>(() => obj.WriteString("x"));

        var arr = new JTokenBindingSink();
        arr.AppendItem().WriteInt64(1);
        Assert.Throws<InvalidOperationException>(() => arr.Scope("k"));
        Assert.Throws<InvalidOperationException>(() => arr.WriteNull());
    }

    public sealed class SinkFixture
    {
        [ApiPropertyV3(Name = "name")] public string? Name { get; set; }
        [ApiPropertyV3(Name = "count")] public int Count;
        public string? Untagged { get; set; }
    }

    [Fact]
    public void TypeBindingsEmitWritesAScopedEntityWithoutARequest()
    {
        var sink = new JTokenBindingSink();
        var context = new BindingContext(TypeBindings.Default, memberScope: typeof(ResponseBody));

        TypeBindings.Default.Emit(
            typeof(SinkFixture),
            new SinkFixture { Name = "n", Count = 3, Untagged = "hidden" },
            sink,
            context);

        Assert.Equal("""{"name":"n","count":3}""", Text(sink));
    }
}
