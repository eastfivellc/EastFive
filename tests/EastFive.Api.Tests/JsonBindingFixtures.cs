using System;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using ApiPropertyV3 = EastFive.Api.Binding.ApiPropertyAttribute;

namespace EastFive.Api.Tests.JsonBindingFixtures;

/// <summary>A governed class: V3-tagged members under wire names, plus an untagged member.</summary>
public sealed class StepDefinition
{
    [ApiPropertyV3(Name = "id", Mutable = false)] public Guid Id { get; set; }
    [ApiPropertyV3(Name = "title")] public string? Title { get; set; }
    [ApiPropertyV3(Name = "output_variable")] public string? OutputVariable { get; set; }
    [ApiPropertyV3(Name = "order")] public int Order;
    public string? Untagged { get; set; }
}

/// <summary>Opts this assembly's V3-tagged fixtures into ApiProperty-governed JSON, as an application does at start.</summary>
internal static class JsonBindingFixtureRegistration
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Register() =>
        EastFive.Api.Serialization.Json.ApiPropertyContractResolver.RegisterManagedAssembly(
            typeof(JsonBindingFixtureRegistration).Assembly);
}

/// <summary>Governed: only V3-tagged members, under their V3 names; [JsonProperty]/[JsonIgnore] don't apply.</summary>
public sealed class GovernedScalars
{
    [ApiPropertyV3(Name = "name")][JsonProperty("json_name")] public string? Name { get; set; }
    [ApiPropertyV3(Name = "tagged")][JsonIgnore] public string? Tagged { get; set; }
    [JsonProperty("not_tagged")] public string? NotTagged { get; set; }
    [ApiPropertyV3(Name = "count")] public int Count;
    [ApiPropertyV3] public double Ratio { get; set; }
    [ApiPropertyV3(Name = "when")] public DateTime When { get; set; }
    [ApiPropertyV3(Name = "maybe")] public int? Maybe { get; set; }
    [ApiPropertyV3(Name = "flag")] public bool Flag { get; set; }
}

/// <summary>Ungoverned: Newtonsoft's default member set and names.</summary>
public sealed class PlainScalars
{
    public string? Name { get; set; }
    [JsonProperty("display")] public string? Display { get; set; }
    [JsonIgnore] public string? Secret { get; set; }
    public int Count;
    [JsonProperty("hidden")] private string? hidden;
    public bool Flag { get; set; }
    public Guid? MaybeId { get; set; }

    public string? Hidden { get => hidden; set => hidden = value; }
}

[JsonObject(MemberSerialization.OptIn)]
public sealed class OptInPlain
{
    [JsonProperty("a")] public int A;
    public int B;
}

/// <summary>Governance is decided per declaring type, as the resolver does.</summary>
public class GovernedBase
{
    [ApiPropertyV3(Name = "base_name")] public string? BaseName { get; set; }
    public string? BaseUntagged { get; set; }
}

public sealed class DerivedFromGoverned : GovernedBase
{
    public string? Extra { get; set; }
}

/// <summary>Ungoverned class nesting a governed one and vice versa.</summary>
public sealed class PlainWrapper
{
    public string? Label { get; set; }
    public GovernedScalars? Inner { get; set; }
}

public sealed class GovernedWrapper
{
    [ApiPropertyV3(Name = "label")] public string? Label { get; set; }
    [ApiPropertyV3(Name = "plain")] public PlainScalars? Plain { get; set; }
}

#region Step 4 edge-case fixtures

public sealed class RefTarget : EastFive.IReferenceable
{
    public Guid id { get; set; }
}

/// <summary>Rosemary entities are partial structs.</summary>
public struct EntityStruct
{
    [ApiPropertyV3(Name = "id")] public Guid Id;
    [ApiPropertyV3(Name = "name")] public string? Name;
    [ApiPropertyV3(Name = "address")] public AddressStruct Address;
    [ApiPropertyV3(Name = "addresses")] public AddressStruct[]? Addresses;
    [ApiPropertyV3(Name = "count")] public int Count { get; set; }
}

public struct AddressStruct
{
    [ApiPropertyV3(Name = "line")] public string? Line;
    [ApiPropertyV3(Name = "zip")] public int Zip;
}

public sealed record PositionalRecord(
    [property: ApiPropertyV3(Name = "name")] string Name,
    [property: ApiPropertyV3(Name = "display_name")] string? DisplayName,
    [property: ApiPropertyV3(Name = "count")] int Count);

public sealed record PlainPositional(string Name, int Count, string? Note = null)
{
    public string? Extra { get; init; }
}

public sealed record InitRecord
{
    [ApiPropertyV3(Name = "name")] public string? Name { get; init; }
    [ApiPropertyV3(Name = "tags")] public string[]? Tags { get; init; }
}

public sealed record PlainInit
{
    public string? Name { get; init; }
    public int Count { get; init; }
}

public struct RefHolder
{
    [ApiPropertyV3(Name = "ref")] public EastFive.IRef<RefTarget>? Ref;
    [ApiPropertyV3(Name = "optional")] public EastFive.IRefOptional<RefTarget>? Optional;
    [ApiPropertyV3(Name = "refs")] public EastFive.IRefs<RefTarget>? Refs;
    [ApiPropertyV3(Name = "nullable_ref")] public EastFive.Ref<RefTarget>? NullableRef;
    [ApiPropertyV3(Name = "concrete")] public EastFive.Ref<RefTarget> Concrete;
    [ApiPropertyV3(Name = "concrete_optional")] public EastFive.RefOptional<RefTarget> ConcreteOptional;
    [ApiPropertyV3(Name = "ref_array")] public EastFive.IRef<RefTarget>[]? RefArray;
    [ApiPropertyV3(Name = "boxed")] public object? Boxed;
}

public sealed class TokenHolder
{
    [ApiPropertyV3(Name = "obj")] public JObject? Obj { get; set; }
    [ApiPropertyV3(Name = "arr")] public JArray? Arr { get; set; }
    [ApiPropertyV3(Name = "any")] public JToken? Any { get; set; }
}

public sealed class Bag
{
    [ApiPropertyV3(Name = "extra")] public Dictionary<string, object?>? Extra;
    [ApiPropertyV3(Name = "iextra")] public IDictionary<string, object?>? IExtra;
    [ApiPropertyV3(Name = "labels")] public Dictionary<string, string?>? Labels;
    [ApiPropertyV3(Name = "ilabels")] public IDictionary<string, string>? ILabels;
    [ApiPropertyV3(Name = "addresses")] public Dictionary<string, AddressStruct>? Addresses;
    [ApiPropertyV3(Name = "value")] public object? Value;
}

public enum Color { Red, Green, Blue }

[Flags]
public enum Perms { None = 0, Read = 1, Write = 2, Exec = 4 }

public sealed class EnumHolder
{
    [ApiPropertyV3(Name = "color")] public Color Color;
    [ApiPropertyV3(Name = "perms")] public Perms Perms;
    [ApiPropertyV3(Name = "maybe")] public Color? Maybe;
}

public sealed class NullableHolder
{
    [ApiPropertyV3(Name = "when")] public DateTime? When;
    [ApiPropertyV3(Name = "id")] public Guid? Id;
    [ApiPropertyV3(Name = "count")] public int? Count;
    [ApiPropertyV3(Name = "flag")] public bool? Flag;
    [ApiPropertyV3(Name = "color")] public Color? Color;
}

public sealed class DateHolder
{
    [ApiPropertyV3(Name = "utc")] public DateTime Utc;
    [ApiPropertyV3(Name = "local")] public DateTime Local;
    [ApiPropertyV3(Name = "unspecified")] public DateTime Unspecified;
    [ApiPropertyV3(Name = "offset")] public DateTimeOffset Offset;
    [ApiPropertyV3(Name = "looks_like_iso")] public string? LooksLikeIso;
    [ApiPropertyV3(Name = "looks_like_us")] public string? LooksLikeUs;
}

public sealed class NumberHolder
{
    [ApiPropertyV3(Name = "money")] public decimal Money;
    [ApiPropertyV3(Name = "big")] public long Big;
    [ApiPropertyV3(Name = "i")] public int I;
    [ApiPropertyV3(Name = "s")] public short S;
    [ApiPropertyV3(Name = "b")] public byte B;
    [ApiPropertyV3(Name = "f")] public float F;
    [ApiPropertyV3(Name = "d")] public double D;
    [ApiPropertyV3(Name = "u")] public ulong U;
    [ApiPropertyV3(Name = "span")] public TimeSpan Span;
    [ApiPropertyV3(Name = "bytes")] public byte[]? Bytes;
    [ApiPropertyV3(Name = "text")] public string? Text;
}

public sealed class EmptyPlain { }

public sealed class CollectionHolder
{
    [ApiPropertyV3(Name = "nulls")] public string[]? Nulls;
    [ApiPropertyV3(Name = "empties")] public string[]? Empties;
    [ApiPropertyV3(Name = "list")] public List<string>? List;
    [ApiPropertyV3(Name = "seq")] public IEnumerable<int>? Seq;
    [ApiPropertyV3(Name = "ilist")] public IList<AddressStruct>? IList;
    [ApiPropertyV3(Name = "readonly")] public IReadOnlyList<string>? ReadOnly;
    [ApiPropertyV3(Name = "set")] public HashSet<string>? Set;
    [ApiPropertyV3(Name = "empty_object")] public EmptyPlain? EmptyObject;
    [ApiPropertyV3(Name = "empty_list")] public List<int>? EmptyList;
}

public sealed class ComputedHolder
{
    [ApiPropertyV3(Name = "name")] public string? Name { get; set; }
    [ApiPropertyV3(Name = "upper")] public string? Upper => Name?.ToUpperInvariant();
    [ApiPropertyV3(Name = "fixed")] public readonly int Fixed = 7;
}

public sealed class PlainComputed
{
    public string? Name { get; set; }
    public int Length => Name?.Length ?? 0;
}

public sealed class EnumConverterHolder
{
    [ApiPropertyV3(Name = "color")]
    [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public Color Color;

    [ApiPropertyV3(Name = "maybe")]
    [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public Color? Maybe;
}

public sealed class UpperConverter : JsonConverter<string>
{
    public override void WriteJson(JsonWriter writer, string? value, JsonSerializer serializer) =>
        writer.WriteValue(value?.ToUpperInvariant());

    public override string? ReadJson(JsonReader reader, Type objectType, string? existingValue, bool hasExistingValue, JsonSerializer serializer) =>
        (string?)reader.Value;
}

public sealed class CustomConverterHolder
{
    [ApiPropertyV3(Name = "value")]
    [JsonConverter(typeof(UpperConverter))]
    public string? Value;
}

public sealed class PlainCustomConverterHolder
{
    [JsonConverter(typeof(UpperConverter))]
    public string? Value;
}

[JsonConverter(typeof(ConvertedTypeConverter))]
public sealed class ConvertedType
{
    public string? Value { get; set; }
}

public sealed class ConvertedTypeConverter : JsonConverter<ConvertedType>
{
    public override void WriteJson(JsonWriter writer, ConvertedType? value, JsonSerializer serializer) =>
        writer.WriteValue(value?.Value);

    public override ConvertedType? ReadJson(JsonReader reader, Type objectType, ConvertedType? existingValue, bool hasExistingValue, JsonSerializer serializer) =>
        new ConvertedType { Value = (string?)reader.Value };
}

public sealed class ConvertedTypeHolder
{
    [ApiPropertyV3(Name = "converted")] public ConvertedType? Converted;
}

#endregion

#region Review follow-up fixtures

public class BaseShape
{
    public string? Name { get; set; }
}

public sealed class DerivedShape : BaseShape
{
    public int Extra { get; set; }
}

public interface IShape
{
    string? Name { get; }
}

public sealed class ShapeImpl : IShape
{
    public string? Name { get; set; }
    public int Sides { get; set; }
}

public sealed class ShapeHolder
{
    public BaseShape? One { get; set; }
    public List<BaseShape>? Many { get; set; }
    public IShape? Iface { get; set; }
    public IEnumerable<int>? Lazy { get; set; }
}

public sealed class NullItems
{
    public List<string?>? Tags { get; set; }
    public string?[]? Names { get; set; }
    public AddressStruct?[]? Maybe { get; set; }
    public BaseShape?[]? Shapes { get; set; }
}

public sealed class ODataLike
{
    [JsonProperty("odata.metadata")] public string? Meta { get; set; }
    [JsonProperty("value[0]")] public string? First { get; set; }
    public string? Plain { get; set; }
}

[System.Runtime.Serialization.DataContract]
public sealed class ContractPlain
{
    [System.Runtime.Serialization.DataMember(Name = "id")] public int Id;
    [JsonProperty("label")] public string? Label;
    public int Created;
    [System.Runtime.Serialization.DataMember(Name = "hidden")] private string? secret;

    public string? Secret { get => secret; set => secret = value; }
}

public sealed class IgnoreDataPlain
{
    public string? Name;
    [System.Runtime.Serialization.IgnoreDataMember] public string? Secret;
    [NonSerialized] public string? Transient;
}

public sealed class UriHolder
{
    public Uri? Link;
}

#endregion

public sealed class ConstructedShape : BaseShape
{
    public ConstructedShape(int sides) { Sides = sides; }
    public int Sides { get; set; }
}

public sealed class ConstructedOnly
{
    public ConstructedOnly(string value) { Value = value; }
    public string Value { get; set; }
}

public sealed class ConstructedHolder
{
    public BaseShape? Shape { get; set; }
    public IShape? Iface { get; set; }
    public ConstructedOnly? Only { get; set; }
}
