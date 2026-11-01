#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace EastFive.Configuration
{
    /// <summary>
    /// Declares that this assembly USES a configuration type declared in an assembly it references,
    /// so a host's editor groups that type under this assembly rather than under the library that
    /// declares it. Adoption changes presentation and metadata only, never behaviour: the library
    /// still reads its own keys and secret names, with its own defaults and validation. The adopter
    /// may say how it uses the type (<see cref="Description"/>), relabel it, point at where its
    /// values come from, link to more information, and require it (<see cref="Required"/>: missing
    /// values make the type incomplete for this app, not merely unconfigured). Adopting a type adopts
    /// all of its instances.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
    public sealed class AdoptsConfigurationAttribute : Attribute
    {
        public AdoptsConfigurationAttribute(Type configurationType)
            => ConfigurationType = configurationType ?? throw new ArgumentNullException(nameof(configurationType));

        /// <summary>The adopted <see cref="IProvideConfiguration"/> type.</summary>
        public Type ConfigurationType { get; }

        /// <summary>The label this app shows for the type (default: the type's own).</summary>
        public string? Label { get; init; }

        /// <summary>What this app uses the type for — the card's usage text.</summary>
        public string? Description { get; init; }

        /// <summary>Where an operator finds this app's values for the type.</summary>
        public string? Location { get; init; }

        /// <summary>A link with more about how this app uses the type.</summary>
        public string? MoreInfo { get; init; }

        /// <summary>This app needs the type: missing values make it incomplete, not unconfigured.</summary>
        public bool Required { get; init; }
    }

    /// <summary>One assembly's adoption of one configuration type, with its presentation overrides.</summary>
    public sealed record ConfigurationAdoption(
        Assembly Adopter,
        Type ConfigurationType,
        string? Label,
        string? Description,
        string? Location,
        string? MoreInfo,
        bool Required)
    {
        public string AdopterName => Adopter.GetName().Name ?? Adopter.FullName ?? "?";
    }

    /// <summary>The adoptions an assembly declares, validated.</summary>
    public static class ConfigurationAdoptions
    {
        private static readonly ConcurrentDictionary<Assembly, IReadOnlyList<ConfigurationAdoption>> cache = new();

        /// <summary>
        /// What <paramref name="adopter"/> adopts (<see cref="AdoptsConfigurationAttribute"/>). A
        /// malformed adoption throws <see cref="InvalidOperationException"/> — a declaration error, as
        /// a malformed configuration type is.
        /// </summary>
        public static IReadOnlyList<ConfigurationAdoption> Of(Assembly adopter)
            => cache.GetOrAdd(adopter, assembly => From(assembly, assembly.GetCustomAttributes<AdoptsConfigurationAttribute>()));

        /// <summary>
        /// <paramref name="declared"/> as <paramref name="adopter"/>'s adoptions. Refused: a type that
        /// is not a concrete configuration type, a type the adopter declares itself, a type from an
        /// assembly the adopter does not reference, and the same type adopted twice.
        /// </summary>
        public static IReadOnlyList<ConfigurationAdoption> From(Assembly adopter, IEnumerable<AdoptsConfigurationAttribute> declared)
        {
            var adopterName = adopter.GetName().Name;
            var adoptions = declared
                .Select(attribute =>
                {
                    var type = attribute.ConfigurationType;
                    if (type is not { IsClass: true, IsAbstract: false, ContainsGenericParameters: false }
                            || !typeof(IProvideConfiguration).IsAssignableFrom(type))
                        throw new InvalidOperationException(
                            $"{adopterName} adopts {type.FullName}, which is not a concrete configuration type (IProvideConfiguration).");
                    if (type.Assembly == adopter)
                        throw new InvalidOperationException(
                            $"{adopterName} adopts {type.FullName}, but it declares it: an assembly adopts only a type another assembly declares.");
                    var declaring = type.Assembly.GetName().Name;
                    if (!adopter.GetReferencedAssemblies().Any(reference => string.Equals(reference.Name, declaring, StringComparison.Ordinal)))
                        throw new InvalidOperationException(
                            $"{adopterName} adopts {type.FullName}, but does not reference {declaring}, which declares it.");
                    return new ConfigurationAdoption(adopter, type, attribute.Label, attribute.Description, attribute.Location,
                        attribute.MoreInfo, attribute.Required);
                })
                .ToArray();
            var twice = adoptions.GroupBy(adoption => adoption.ConfigurationType).FirstOrDefault(group => group.Count() > 1);
            if (twice != null)
                throw new InvalidOperationException($"{adopterName} adopts {twice.Key.FullName} more than once.");
            return adoptions;
        }
    }

    /// <summary>Where a host shows one configuration type: the group it sits in and who uses it.</summary>
    /// <param name="DeclaredIn">The assembly that declares the type (it reads the keys).</param>
    /// <param name="Group">The placement adopter's assembly, else <paramref name="DeclaredIn"/>.</param>
    /// <param name="AdoptedBy">The adopter closest to the host's roots, if any adopter is in its graph.</param>
    /// <param name="AlsoUsedBy">Every other adopter in the graph, closest first.</param>
    public sealed record ConfigurationPlacement(
        Type ConfigurationType,
        Assembly DeclaredIn,
        Assembly Group,
        ConfigurationAdoption? AdoptedBy,
        IReadOnlyList<ConfigurationAdoption> AlsoUsedBy);

    /// <summary>
    /// A host's assembly reference graph from its root assemblies, in a stable order — roots first
    /// (in root order), then by distance from the nearest root, ties by the order of the root that
    /// reaches the assembly, then by name — and the placement of each configuration type in it.
    /// </summary>
    public sealed class ConfigurationGraph
    {
        private readonly Dictionary<Assembly, (int distance, int root)> rank;
        private readonly Func<Assembly, IReadOnlyList<ConfigurationAdoption>> adoptionsOf;

        private ConfigurationGraph(IReadOnlyList<Assembly> roots, IReadOnlyList<Assembly> ordered,
            Dictionary<Assembly, (int distance, int root)> rank, Func<Assembly, IReadOnlyList<ConfigurationAdoption>> adoptionsOf)
        {
            Roots = roots;
            Assemblies = ordered;
            this.rank = rank;
            this.adoptionsOf = adoptionsOf;
        }

        /// <summary>The host's root assemblies, in the host's order.</summary>
        public IReadOnlyList<Assembly> Roots { get; }

        /// <summary>Every assembly the roots reach (themselves included), in graph order.</summary>
        public IReadOnlyList<Assembly> Assemblies { get; }

        public bool IsRoot(Assembly assembly) => rank.TryGetValue(assembly, out var at) && at.distance == 0;

        public bool Contains(Assembly assembly) => rank.ContainsKey(assembly);

        /// <summary>Reference hops from the nearest root; -1 when the roots do not reach it.</summary>
        public int DistanceOf(Assembly assembly) => rank.TryGetValue(assembly, out var at) ? at.distance : -1;

        /// <summary>
        /// The graph of <paramref name="roots"/>. <paramref name="references"/> and
        /// <paramref name="adoptions"/> default to the assemblies' own references (System and
        /// Microsoft assemblies are not walked; one that cannot load is skipped) and
        /// <see cref="ConfigurationAdoptions.Of"/>.
        /// </summary>
        public static ConfigurationGraph Of(IEnumerable<Assembly> roots,
            Func<Assembly, IEnumerable<Assembly>>? references = null,
            Func<Assembly, IReadOnlyList<ConfigurationAdoption>>? adoptions = null)
        {
            var rootList = roots.Distinct().ToArray();
            var referencesOf = references ?? Referenced;
            var rank = new Dictionary<Assembly, (int distance, int root)>();
            var ordered = new List<Assembly>();
            var frontier = rootList.Select((assembly, index) => (assembly, root: index)).ToList();
            foreach (var (assembly, root) in frontier)
                rank[assembly] = (0, root);
            for (var distance = 0; frontier.Count > 0; distance++)
            {
                frontier = frontier.OrderBy(entry => entry.root).ThenBy(entry => NameOf(entry.assembly), StringComparer.Ordinal).ToList();
                ordered.AddRange(frontier.Select(entry => entry.assembly));
                var next = new List<(Assembly assembly, int root)>();
                foreach (var (assembly, root) in frontier)
                    foreach (var reference in referencesOf(assembly))
                        if (!rank.ContainsKey(reference))
                        {
                            rank[reference] = (distance + 1, root);
                            next.Add((reference, root));
                        }
                frontier = next;
            }
            return new ConfigurationGraph(rootList, ordered, rank, adoptions ?? ConfigurationAdoptions.Of);
        }

        /// <summary>
        /// Where <paramref name="configurationType"/> sits in this host: under the adopter closest to
        /// the roots (ties by root order, then name), with every other adopter in the graph listed;
        /// under its declaring assembly when no assembly in the graph adopts it.
        /// </summary>
        public ConfigurationPlacement Place(Type configurationType)
        {
            var adopters = Assemblies
                .SelectMany(assembly => adoptionsOf(assembly).Where(adoption => adoption.ConfigurationType == configurationType))
                .OrderBy(adoption => rank[adoption.Adopter].distance)
                .ThenBy(adoption => rank[adoption.Adopter].root)
                .ThenBy(adoption => NameOf(adoption.Adopter), StringComparer.Ordinal)
                .ToArray();
            var placed = adopters.FirstOrDefault();
            return new ConfigurationPlacement(configurationType, configurationType.Assembly,
                placed?.Adopter ?? configurationType.Assembly, placed, adopters.Skip(1).ToArray());
        }

        private static string NameOf(Assembly assembly) => assembly.GetName().Name ?? assembly.FullName ?? string.Empty;

        private static IEnumerable<Assembly> Referenced(Assembly assembly)
        {
            if (assembly.IsDynamic)
                yield break;
            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                if (reference.Name is null
                    || reference.Name.StartsWith("System", StringComparison.Ordinal)
                    || reference.Name.StartsWith("Microsoft", StringComparison.Ordinal)
                    || reference.Name is "netstandard" or "mscorlib")
                    continue;
                Assembly? loaded;
                try
                {
                    loaded = Assembly.Load(reference);
                }
                catch (Exception ex) when (ex is System.IO.FileNotFoundException or System.IO.FileLoadException or BadImageFormatException)
                {
                    loaded = null;
                }
                if (loaded != null)
                    yield return loaded;
            }
        }
    }
}
