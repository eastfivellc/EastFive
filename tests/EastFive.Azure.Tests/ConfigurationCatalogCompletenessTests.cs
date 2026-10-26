using System;
using System.Linq;
using System.Reflection;

using Xunit;

using EastFive.Configuration;

namespace EastFive.Azure.Tests;

/// <summary>
/// Every typed configuration in the four framework assemblies is fully described, because an
/// editor and the doctor render from nothing else: the type has a label and description, every
/// member has its own label and description and a DECLARED scope (on the member, or on its type),
/// every configuration type is in the catalog, and a keyed type enumerates its instances.
/// </summary>
public class ConfigurationCatalogCompletenessTests
{
    private static readonly Assembly[] Framework =
    {
        typeof(Secret).Assembly,
        typeof(EastFive.Api.ApiKeyConfiguration).Assembly,
        typeof(EastFive.Azure.Persistence.StorageAccount).Assembly,
        typeof(EastFive.Sheets.ISheet).Assembly,
    };

    private static Type[] ConfigurationTypes()
        => Framework
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false }
                && typeof(IProvideConfiguration).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

    [Fact]
    public void EveryConfigurationType_IsInTheCatalog()
        => Assert.Equal(
            ConfigurationTypes().Select(type => type.FullName),
            ConfigurationCatalog.For(Framework).Select(description => description.Type.FullName));

    [Fact]
    public void EveryTypeAndMember_IsDescribed()
    {
        var gaps = ConfigurationCatalog.For(Framework)
            .SelectMany(type =>
            {
                var attribute = type.Type.GetCustomAttribute<ConfigurationTypeAttribute>(inherit: true);
                var typeGaps = new[]
                    {
                        attribute?.Label is { Length: > 0 } ? null : "no type label",
                        attribute?.Description is { Length: > 0 } ? null : "no type description",
                    }
                    .OfType<string>()
                    .Select(gap => $"{type.Type.Name}: {gap}");
                var memberGaps = type.Members.SelectMany(member =>
                {
                    var declared = type.Type.GetProperty(member.Property)!.GetCustomAttribute<ConfigurationMemberAttribute>(inherit: true)!;
                    return new[]
                        {
                            declared.Label is { Length: > 0 } ? null : "no label",
                            declared.Description is { Length: > 0 } ? null : "no description",
                            member.Metadata.ScopeDeclared ? null : "no declared scope",
                        }
                        .OfType<string>()
                        .Select(gap => $"{type.Type.Name}.{member.Property}: {gap}");
                });
                return typeGaps.Concat(memberGaps);
            })
            .ToArray();
        Assert.True(gaps.Length == 0, $"{gaps.Length} gaps:{Environment.NewLine}{string.Join(Environment.NewLine, gaps)}");
    }

    [Fact]
    public void EveryKeyedType_EnumeratesItsInstances()
    {
        var keyed = ConfigurationCatalog.For(Framework).Where(type => type.IsKeyed).ToArray();
        Assert.NotEmpty(keyed);
        Assert.All(keyed, type =>
        {
            var key = type.Members.First(member => member.Key != null).Key!
                .Replace(IProvideConfigurationInstanceKey.Placeholder, "sample", StringComparison.Ordinal);
            Assert.Equal(new[] { "sample" }, ConfigurationCatalog.InstanceKeys(type.Type, new[] { key, "Unrelated.Key" }));
        });
    }
}
