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
