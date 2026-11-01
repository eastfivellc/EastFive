using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Xunit;

using EastFive.Api;
using EastFive.Configuration;

// This test assembly adopts two framework types, as an app adopts the library configuration it uses.
[assembly: AdoptsConfiguration(typeof(CorsConfiguration),
    Label = "Browser access", Description = "Which origins the test SPA calls from.",
    Location = "The SPA's deployed origin", MoreInfo = "https://example.test/cors", Required = true)]
[assembly: AdoptsConfiguration(typeof(RoutingConfiguration))]

namespace EastFive.Api.Tests;

/// <summary>
/// Configuration adoption: an assembly adopts a configuration type declared in an assembly it
/// references (<see cref="AdoptsConfigurationAttribute"/>), overriding only presentation —
/// label, usage description, location, more-info link and whether the app requires it. Placement
/// over a host's reference graph: the adopter closest to the roots wins, ties by root order;
/// the others are listed as also using it. Adoption never changes keys, secret names or defaults.
/// </summary>
public class ConfigurationAdoptionTests
{
    [ConfigurationType("LocalThing")]
    private sealed class LocalThingConfiguration : IProvideConfiguration
    {
        [ConfigurationMember("Local.Thing")]
        public string Thing { get; set; } = default!;
    }

    private static readonly Assembly Tests = typeof(ConfigurationAdoptionTests).Assembly;
    private static readonly Assembly Api = typeof(CorsConfiguration).Assembly;
    private static readonly Assembly Core = typeof(IProvideConfiguration).Assembly;

    [Fact]
    public void AnAssemblyListsWhatItAdopts_WithItsOverrides()
    {
        var adoptions = ConfigurationAdoptions.Of(Tests);

        Assert.Equal(new[] { typeof(CorsConfiguration), typeof(RoutingConfiguration) },
            adoptions.Select(adoption => adoption.ConfigurationType).OrderBy(type => type.Name));
        var cors = adoptions.Single(adoption => adoption.ConfigurationType == typeof(CorsConfiguration));
        Assert.Same(Tests, cors.Adopter);
        Assert.Equal(("Browser access", "Which origins the test SPA calls from.", "The SPA's deployed origin", "https://example.test/cors", true),
            (cors.Label, cors.Description, cors.Location, cors.MoreInfo, cors.Required));
        var routing = adoptions.Single(adoption => adoption.ConfigurationType == typeof(RoutingConfiguration));
        Assert.Equal(((string?)null, (string?)null, false), (routing.Label, routing.Description, routing.Required));
        Assert.Empty(ConfigurationAdoptions.Of(Core));
    }

    [Fact]
    public void AnAdoptionTheAssemblyCannotSee_IsADeclarationError()
    {
        // EastFive.Core does not reference EastFive.Api, so it cannot adopt one of Api's types.
        var unseen = Assert.Throws<InvalidOperationException>(() => ConfigurationAdoptions.From(Core,
            new[] { new AdoptsConfigurationAttribute(typeof(CorsConfiguration)) }));
        Assert.Contains("does not reference", unseen.Message);
        Assert.Contains(nameof(CorsConfiguration), unseen.Message);

        var own = Assert.Throws<InvalidOperationException>(() => ConfigurationAdoptions.From(Tests,
            new[] { new AdoptsConfigurationAttribute(typeof(LocalThingConfiguration)) }));
        Assert.Contains("declares it", own.Message);

        var notConfiguration = Assert.Throws<InvalidOperationException>(() => ConfigurationAdoptions.From(Tests,
            new[] { new AdoptsConfigurationAttribute(typeof(Uri)) }));
        Assert.Contains("not a concrete configuration type", notConfiguration.Message);

        var twice = Assert.Throws<InvalidOperationException>(() => ConfigurationAdoptions.From(Tests,
            new[] { new AdoptsConfigurationAttribute(typeof(CorsConfiguration)), new AdoptsConfigurationAttribute(typeof(CorsConfiguration)) }));
        Assert.Contains("more than once", twice.Message);
    }

    [Fact]
    public void AdoptionChangesNoKeySecretNameOrDefault()
    {
        var withAdoption = ConfigurationCatalog.Describe(typeof(CorsConfiguration));
        Assert.Equal(
            ConfigurationMembers.For(typeof(CorsConfiguration)).Members.Select(member => (member.Property.Name, member.Key, member.Default)),
            withAdoption.Members.Select(member => (member.Property, member.Key, member.Default)));
        Assert.Equal(typeof(CorsConfiguration).GetCustomAttribute<ConfigurationTypeAttribute>()?.Description, withAdoption.Description);
    }

    // -------------------------------------------------------------------------------------------
    // Placement over a reference graph. Real assemblies stand in as nodes; the edges and the
    // adoptions are supplied, so every shape of graph can be exercised.

    private static readonly Assembly A = typeof(FactAttribute).Assembly;   // xunit.core
    private static readonly Assembly B = typeof(Assert).Assembly;          // xunit.assert
    private static readonly Assembly C = typeof(Enumerable).Assembly;      // System.Linq

    private static ConfigurationAdoption Adopt(Assembly adopter, string? description = null, bool required = false)
        => new(adopter, typeof(CorsConfiguration), null, description, null, null, required);

    private static ConfigurationGraph Graph(Assembly[] roots, Dictionary<Assembly, Assembly[]> edges,
            params ConfigurationAdoption[] adoptions)
        => ConfigurationGraph.Of(roots,
            assembly => edges.TryGetValue(assembly, out var references) ? references : Array.Empty<Assembly>(),
            assembly => adoptions.Where(adoption => adoption.Adopter == assembly).ToArray());

    [Fact]
    public void Order_IsRootsFirst_ThenByDistance_TiesByRootOrder()
    {
        // Tests → A → Api ; Core → B ; A → C ; Api → Core.
        var graph = Graph(new[] { Tests, Core },
            new Dictionary<Assembly, Assembly[]> { [Tests] = new[] { A }, [A] = new[] { Api, C }, [Core] = new[] { B }, [Api] = new[] { Core } });

        Assert.Equal(new[] { Tests, Core, A, B, Api, C }, graph.Assemblies);
        Assert.Equal((0, 0, 1, 1, 2), (graph.DistanceOf(Tests), graph.DistanceOf(Core), graph.DistanceOf(A), graph.DistanceOf(B), graph.DistanceOf(Api)));
        Assert.True(graph.IsRoot(Core));
        Assert.False(graph.IsRoot(A));
    }

    [Fact]
    public void Placement_ClosestAdopterWins_OthersAlsoUseIt()
    {
        var graph = Graph(new[] { Tests },
            new Dictionary<Assembly, Assembly[]> { [Tests] = new[] { A }, [A] = new[] { B }, [B] = new[] { Api } },
            Adopt(B, "B's use"), Adopt(A, "A's use"));

        var placement = graph.Place(typeof(CorsConfiguration));

        Assert.Same(Api, placement.DeclaredIn);
        Assert.Same(A, placement.Group);
        Assert.Same(A, placement.AdoptedBy!.Adopter);
        Assert.Equal(new[] { "B's use" }, placement.AlsoUsedBy.Select(adoption => adoption.Description));
    }

    [Fact]
    public void Placement_TiesGoByRootOrder()
    {
        var graph = Graph(new[] { A, B }, new Dictionary<Assembly, Assembly[]> { [A] = new[] { Api }, [B] = new[] { Api } },
            Adopt(B, "second root"), Adopt(A, "first root"));

        var placement = graph.Place(typeof(CorsConfiguration));

        Assert.Same(A, placement.Group);
        Assert.Equal(new[] { B }, placement.AlsoUsedBy.Select(adoption => adoption.Adopter));
    }

    [Fact]
    public void Placement_WithoutAnAdopterInTheGraph_IsTheDeclaringAssembly()
    {
        // C adopts, but nothing the roots reach references C: it is not this host's adopter.
        var graph = Graph(new[] { Tests }, new Dictionary<Assembly, Assembly[]> { [Tests] = new[] { Api } }, Adopt(C));

        var placement = graph.Place(typeof(CorsConfiguration));

        Assert.Same(Api, placement.Group);
        Assert.Null(placement.AdoptedBy);
        Assert.Empty(placement.AlsoUsedBy);
    }

    [Fact]
    public void TheRealGraph_FindsThisAssemblysAdoption()
    {
        var placement = ConfigurationGraph.Of(new[] { Tests }).Place(typeof(CorsConfiguration));

        Assert.Same(Tests, placement.Group);
        Assert.True(placement.AdoptedBy!.Required);
        Assert.Contains(Api, ConfigurationGraph.Of(new[] { Tests }).Assemblies);
    }
}
