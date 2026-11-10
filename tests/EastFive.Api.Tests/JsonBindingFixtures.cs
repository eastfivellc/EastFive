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
