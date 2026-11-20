using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EastFive.Generators
{
    /// <summary>
    /// E5001: a storage table type with no public ETag member. The storage driver reads a row's
    /// ETag from the entity's public member whose attribute implements
    /// <c>IModifyAzureStorageTableETag</c>; with none it replaces with <c>*</c>, so updates and
    /// locked updates of that table do not check for a concurrent change. Types and interfaces
    /// are matched by metadata name, so this assembly never loads EastFive.Azure.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class StorageTableETagAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "E5001";

        private const string ProvideEntityName = "EastFive.Persistence.Azure.StorageTables.IProvideEntity";
        private const string ModifyETagName = "EastFive.Persistence.IModifyAzureStorageTableETag";

        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Storage table has no public ETag member",
            "'{0}' has no public ETag member, so its updates and locked updates are saved without checking for a concurrent change: the last writer wins and two callers can hold its lock. Declare '[ETag] public string eTag;', or suppress this warning where that is intended.",
            "EastFive.Storage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "StorageUpdateAsync and StorageLockedUpdateAsync save conditionally only when the table type has a public field or property, declared or inherited, carrying an attribute that implements IModifyAzureStorageTableETag such as [ETag]. A non-public member is not found. The ETag is row metadata, so adding the member needs no migration.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(start =>
            {
                var provideEntity = start.Compilation.GetTypeByMetadataName(ProvideEntityName);
                var modifyETag = start.Compilation.GetTypeByMetadataName(ModifyETagName);
                if (provideEntity is null || modifyETag is null)
                    return;

                start.RegisterSymbolAction(
                    symbolContext => AnalyzeType(symbolContext, provideEntity, modifyETag),
                    SymbolKind.NamedType);
            });
        }

        private static void AnalyzeType(SymbolAnalysisContext context,
            INamedTypeSymbol provideEntity, INamedTypeSymbol modifyETag)
        {
            var type = (INamedTypeSymbol)context.Symbol;
            if (type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Struct)
                return;
            if (!type.GetAttributes().Any(attribute => Implements(attribute.AttributeClass, provideEntity)))
                return;
            if (HasPublicETagMember(type, modifyETag))
                return;

            context.ReportDiagnostic(Diagnostic.Create(Rule, type.Locations.FirstOrDefault(), type.Name));
        }

        // The driver finds the member with Type.GetMembers(): public members, declared or inherited.
        private static bool HasPublicETagMember(INamedTypeSymbol type, INamedTypeSymbol modifyETag)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var found = current.GetMembers().Any(member =>
                    (member.Kind == SymbolKind.Field || member.Kind == SymbolKind.Property)
                    && member.DeclaredAccessibility == Accessibility.Public
                    && member.GetAttributes().Any(attribute => Implements(attribute.AttributeClass, modifyETag)));
                if (found)
                    return true;
            }
            return false;
        }

        private static bool Implements(INamedTypeSymbol? type, INamedTypeSymbol contract) =>
            type != null && type.AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, contract));
    }
}
