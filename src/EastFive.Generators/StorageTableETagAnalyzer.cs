using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EastFive.Generators
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class StorageTableETagAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "E5001";

        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Storage table has no public ETag member",
            "'{0}' has no public ETag member",
            "EastFive.Storage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
        }
    }
}
