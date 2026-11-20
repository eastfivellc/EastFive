using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

using EastFive.Generators;
using EastFive.Persistence.Azure.StorageTables;

namespace EastFive.Azure.Tests.Analyzers;

/// <summary>
/// <see cref="StorageTableETagAnalyzer"/> over in-memory compilations. The rule follows the
/// driver: a table type is saved conditionally only when it has a PUBLIC field or property,
/// declared or inherited, carrying an attribute that implements
/// <c>EastFive.Persistence.IModifyAzureStorageTableETag</c> (see <c>StorageUpdateETagTests</c>).
/// </summary>
public class StorageTableETagAnalyzerTests
{
    private const string Usings = """
        using System;
        using EastFive.Persistence;
        using EastFive.Persistence.Azure.StorageTables;

        """;

    private static readonly ImmutableArray<MetadataReference> References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Concat(new[]
            {
                typeof(StorageTableAttribute).Assembly.Location,
                typeof(EastFive.IRef<>).Assembly.Location,
            })
            .Distinct()
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();

    private static CSharpCompilation Compile(params string[] sources)
    {
        var trees = sources
            .Select(source => CSharpSyntaxTree.ParseText(Usings + source,
                new CSharpParseOptions(LanguageVersion.Latest)))
            .ToArray();
        var compilation = CSharpCompilation.Create("Probe", trees, References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        // An unresolved attribute would make every negative case pass, so any error fails.
        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        return compilation;
    }

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(CSharpCompilation compilation) =>
        compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new StorageTableETagAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

    /// <summary>The type names E5001 is reported on, in order.</summary>
    private static async Task<string[]> FlaggedAsync(params string[] sources)
    {
        var diagnostics = await AnalyzeAsync(Compile(sources));
        Assert.All(diagnostics, diagnostic =>
        {
            Assert.Equal("E5001", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        });
        return diagnostics
            .Select(diagnostic => diagnostic.Location.SourceTree!.GetText()
                .ToString(diagnostic.Location.SourceSpan))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    [Fact]
    public async Task ATableWithNoETagMemberIsFlaggedOnceOnItsName()
    {
        Assert.Equal(new[] { "Order" }, await FlaggedAsync("""
            [StorageTable]
            public class Order
            {
                [Storage] public string note;
            }
            """));
    }

    [Fact]
    public async Task TheMessageSaysWhatHappensAndWhatToDo()
    {
        var diagnostic = Assert.Single(await AnalyzeAsync(Compile("""
            [StorageTable]
            public class Order { }
            """)));

        var message = diagnostic.GetMessage();
        Assert.Contains("'Order'", message);
        Assert.Contains("last writer wins", message);
        Assert.Contains("[ETag] public string eTag;", message);
        Assert.Contains("suppress", message);
    }

    [Theory]
    [InlineData("[ETag] public string eTag;")]
    [InlineData("[ETag] public string ETag { get; set; }")]
    [InlineData("[ETag] public string ETag { get; private set; }")]
    public async Task APublicETagFieldOrPropertyIsNotFlagged(string member)
    {
        Assert.Empty(await FlaggedAsync($$"""
            [StorageTable]
            public class Order
            {
                {{member}}
            }
            """));
    }

    [Theory]
    [InlineData("[ETag] internal string eTag;")]
    [InlineData("[ETag] private string eTag;")]
    [InlineData("[ETag] protected string eTag;")]
    [InlineData("[ETag] internal string ETag { get; set; }")]
    public async Task ANonPublicETagMemberCountsAsNone(string member)
    {
        Assert.Equal(new[] { "Order" }, await FlaggedAsync($$"""
            [StorageTable]
            public class Order
            {
                {{member}}
            }
            """));
    }

    [Fact]
    public async Task SubclassedAttributesAreMatchedByInterface()
    {
        Assert.Equal(new[] { "Bare" }, await FlaggedAsync("""
            public class TenantTableAttribute : StorageTableAttribute { }
            public class VersionAttribute : ETagAttribute { }

            [TenantTable]
            public class Bare { }

            [TenantTable]
            public class Versioned
            {
                [Version] public string version;
            }
            """));
    }

    [Fact]
    public async Task AnAttributeOnlyNamedLikeTheFrameworksIsIgnored()
    {
        Assert.Empty(await FlaggedAsync("""
            namespace Elsewhere
            {
                public class StorageTableAttribute : System.Attribute { }

                [StorageTable]
                public class NotATable { }
            }
            """));
    }

    [Fact]
    public async Task AnETagAttributeOnlyNamedLikeTheFrameworksDoesNotCount()
    {
        Assert.Equal(new[] { "Order" }, await FlaggedAsync("""
            public class Fake
            {
                public class ETagAttribute : System.Attribute { }
            }

            [StorageTable]
            public class Order
            {
                [Fake.ETag] public string eTag;
            }
            """));
    }

    [Fact]
    public async Task APartialTypeIsOneType()
    {
        Assert.Equal(new[] { "Bare" }, await FlaggedAsync(
            """
            [StorageTable]
            public partial class Versioned { }

            [StorageTable]
            public partial class Bare { }
            """,
            """
            public partial class Versioned
            {
                [ETag] public string eTag;
            }

            public partial class Bare
            {
                [Storage] public string note;
            }
            """));
    }

    [Fact]
    public async Task AnInheritedPublicMemberCountsAndAnInheritedNonPublicOneDoesNot()
    {
        Assert.Equal(new[] { "FromInternal" }, await FlaggedAsync("""
            public class Versioned
            {
                [ETag] public string eTag;
            }

            public class HiddenVersion
            {
                [ETag] internal string eTag;
            }

            [StorageTable]
            public class FromPublic : Versioned { }

            [StorageTable]
            public class FromInternal : HiddenVersion { }
            """));
    }

    [Fact]
    public async Task ATypeThatIsNotATableIsNotFlagged()
    {
        Assert.Empty(await FlaggedAsync("""
            public class Plain
            {
                [Storage] public string note;
            }
            """));
    }

    [Theory]
    [InlineData("public class")]
    [InlineData("internal class")]
    [InlineData("public struct")]
    [InlineData("public record")]
    [InlineData("public record struct")]
    public async Task EveryKindOfTypeIsChecked(string declaration)
    {
        Assert.Equal(new[] { "Bare" }, await FlaggedAsync($$"""
            [StorageTable]
            {{declaration}} Bare
            {
                [Storage] public string note;
            }

            [StorageTable]
            {{declaration}} Versioned
            {
                [ETag] public string eTag;
            }
            """));
    }

    [Fact]
    public async Task APragmaSuppressesIt()
    {
        Assert.Empty(await FlaggedAsync("""
            #pragma warning disable E5001
            [StorageTable]
            public class Order { }
            #pragma warning restore E5001
            """));
    }
}
