using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace EastFive.Generators
{
    /// <summary>
    /// Emits, for any assembly that opts in with <c>[assembly: GenerateFlowApi]</c>, a single
    /// <c>{Assembly}Api</c> struct describing the whole API surface (one
    /// <c>IQueryable&lt;T&gt;</c> member per <c>[FunctionViewController]</c> resource) plus a
    /// delegates-last <c>IQueryable&lt;T&gt;</c> extension per controller method. These
    /// extensions are the vocabulary for authoring scripted flows
    /// (<c>Expression&lt;Func&lt;{Assembly}Api, FlowNode&gt;&gt;</c>); the body is never
    /// executed, so each extension is a stub (<c>=&gt; default</c>) carrying a
    /// <c>[FlowMethod]</c> stamp that maps the call back to the controller method.
    ///
    /// <para>Like the test-harness generator, this works purely against Roslyn symbols and
    /// classifies parameters by attribute simple-name; any method with a parameter it cannot
    /// model is skipped with an explanatory comment rather than emitting broken code.</para>
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public class FlowExtensionGenerator : IIncrementalGenerator
    {
        private const string ResponseReturnTypeName = "IHttpResponse";
        private const string OptInAttributeName = "GenerateFlowApiAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterSourceOutput(context.CompilationProvider,
                static (spc, compilation) =>
                {
                    var optIn = compilation.Assembly.GetAttributes()
                        .FirstOrDefault(a => a.AttributeClass?.Name == OptInAttributeName);
                    if (optIn is null)
                        return;

                    var controllers = EnumerateControllers(compilation.Assembly)
                        .Select(BuildController)
                        .Where(c => c.HasValue)
                        .Select(c => c!.Value)
                        .OrderBy(c => c.SimpleName, StringComparer.Ordinal)
                        .ToImmutableArray();
                    if (controllers.IsDefaultOrEmpty)
                        return;

                    var assemblyName = compilation.AssemblyName ?? "Assembly";
                    var sanitized = Sanitize(assemblyName);
                    var apiTypeName = ReadApiTypeName(optIn) ?? sanitized + "Api";
                    var genNamespace = sanitized + ".Flows.Generated";

                    spc.AddSource($"{apiTypeName}.g.cs",
                        SourceText.From(RenderApiStruct(genNamespace, apiTypeName, controllers), Encoding.UTF8));

                    foreach (var controller in controllers)
                    {
                        spc.AddSource($"{controller.HintName}",
                            SourceText.From(RenderExtensions(genNamespace, controller), Encoding.UTF8));
                    }
                });
        }

        // ---- Symbol enumeration ------------------------------------------------

        private static IEnumerable<INamedTypeSymbol> EnumerateControllers(IAssemblySymbol assembly)
        {
            foreach (var type in EnumerateTypes(assembly.GlobalNamespace))
            {
                if (type.TypeKind is TypeKind.Class or TypeKind.Struct
                    && type.DeclaredAccessibility == Accessibility.Public
                    && !type.IsGenericType
                    && HasAttribute(type, "FunctionViewControllerAttribute"))
                    yield return type;
            }
        }

        private static IEnumerable<INamedTypeSymbol> EnumerateTypes(INamespaceSymbol ns)
        {
            foreach (var member in ns.GetMembers())
            {
                if (member is INamespaceSymbol child)
                {
                    foreach (var nested in EnumerateTypes(child))
                        yield return nested;
                }
                else if (member is INamedTypeSymbol type)
                {
                    yield return type;
                    foreach (var nested in type.GetTypeMembers())
                        yield return nested;
                }
            }
        }

        // ---- Model construction ------------------------------------------------

        private static ControllerModel? BuildController(INamedTypeSymbol typeSymbol)
        {
            var methods = typeSymbol.GetMembers()
                .OfType<IMethodSymbol>()
                .Where(static m => m.MethodKind == MethodKind.Ordinary)
                .Where(static m => m.GetAttributes().Any(a => DerivesFrom(a.AttributeClass, "HttpVerbAttribute")))
                .Select(BuildMethod)
                .ToImmutableArray();

            if (methods.IsDefaultOrEmpty)
                return null;

            var fq = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var hint = (fq.Replace("global::", string.Empty).Replace("<", "_").Replace(">", "_")
                .Replace(",", "_").Replace(" ", string.Empty)) + ".Flow.g.cs";

            return new ControllerModel(
                simpleName: typeSymbol.Name,
                fullyQualified: fq,
                methods: methods,
                hintName: hint);
        }

        private static MethodModel BuildMethod(IMethodSymbol method)
        {
            var allParamNames = method.Parameters.Select(p => p.Name).ToImmutableArray();
            var surfaced = ImmutableArray.CreateBuilder<SurfacedParam>();
            var responses = ImmutableArray.CreateBuilder<ResponseBranch>();
            string? skip = null;

            foreach (var p in method.Parameters)
            {
                if (skip != null)
                    break;

                if (IsResponseDelegate(p.Type))
                {
                    responses.Add(BuildResponseBranch(p));
                    continue;
                }

                var classification = Classify(p);
                switch (classification.Kind)
                {
                    case SlotKind.Query:
                    case SlotKind.Body:
                    case SlotKind.WholeBody:
                        surfaced.Add(new SurfacedParam(
                            paramName: p.Name,
                            surfacedTypeFq: classification.SurfacedTypeFq!,
                            isNonNullableValueType: classification.IsNonNullableValueType));
                        break;
                    case SlotKind.Skip:
                        continue;
                    case SlotKind.Unsupported:
                        skip = classification.SkipReason
                            ?? $"unsupported parameter '{p.Name}' of type '{p.Type.ToDisplayString()}'";
                        break;
                }
            }

            if (skip == null && responses.Count == 0)
                skip = "method exposes no response branch to thread the flow";

            return new MethodModel(
                name: method.Name,
                surfaced: surfaced.ToImmutable(),
                responses: responses.ToImmutable(),
                allParamNames: allParamNames,
                skipReason: skip);
        }

        private static ResponseBranch BuildResponseBranch(IParameterSymbol p)
        {
            var invoke = (p.Type as INamedTypeSymbol)?.DelegateInvokeMethod;
            if (invoke is null)
                return new ResponseBranch(p.Name, ImmutableArray<string>.Empty);

            // Drop trailing optional delegate parameters (e.g. CreatedBodyResponse's
            // `string contentType = default`) so the surfaced Func matches how the branch is
            // threaded — leading required args only.
            var parameters = invoke.Parameters;
            var end = parameters.Length;
            while (end > 0 && parameters[end - 1].IsOptional)
                end--;

            var argTypes = parameters
                .Take(end)
                .Select(ip => ip.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .ToImmutableArray();
            return new ResponseBranch(p.Name, argTypes);
        }

        private static Classification Classify(IParameterSymbol p)
        {
            foreach (var a in p.GetAttributes())
            {
                var name = a.AttributeClass?.Name;
                switch (name)
                {
                    case "QueryAttribute":
                    case "QueryOptionalAttribute":
                    case "RouteAttribute":
                        return Classification.Slot(SlotKind.Query, Fq(p.Type), IsNonNullableValueType(p.Type));

                    case "BodyAttribute":
                    case "PropertyAttribute":
                    case "PropertyOptionalAttribute":
                        return Classification.Slot(SlotKind.Body, Fq(p.Type), IsNonNullableValueType(p.Type));

                    case "ResourceAttribute":
                        return Classification.Slot(SlotKind.WholeBody, Fq(p.Type), IsNonNullableValueType(p.Type));

                    case "StorableEntityFromResourceAttribute":
                    {
                        var inner = SingleTypeArg(p.Type);
                        if (inner is null)
                            return Classification.Unsupported($"cannot read entity type from '{p.Type.ToDisplayString()}'");
                        return Classification.Slot(SlotKind.WholeBody, Fq(inner), IsNonNullableValueType(inner));
                    }

                    case "StorageEntityFromQueryIdAttribute":
                    {
                        var inner = SingleTypeArg(p.Type);
                        if (inner is null)
                            return Classification.Unsupported($"cannot read entity type from '{p.Type.ToDisplayString()}'");
                        return Classification.Slot(SlotKind.Query, $"global::EastFive.IRef<{Fq(inner)}>", false);
                    }

                    case "StorageEntitiesAttribute":
                    case "StorageResourcesAttribute":
                        return Classification.Skip();

                    case "HeaderAttribute":
                        return Classification.Unsupported("header-bound parameters are not yet supported by flows");
                }
            }

            var typeName = p.Type.Name;
            if (typeName is "CancellationToken" or "IHttpRequest" or "IApplication"
                or "ElevenLabsHMACSignature" or "SessionToken" or "SessionTokenMaybe"
                or "PracticeEnvironmentRef" or "AuthorizedAccount" or "IProvideClaims"
                or "Security" or "IProvideDocumentSigningFlow")
                return Classification.Skip();

            if (p.Type is INamedTypeSymbol { Name: "IQueryable", TypeArguments.Length: 1 } queryable
                && queryable.ContainingNamespace?.ToDisplayString() == "System.Linq")
                return Classification.Skip();

            return Classification.Unsupported(
                $"parameter '{p.Name}' of type '{p.Type.ToDisplayString()}' has no binding attribute and is not a known instigator");
        }

        // ---- Rendering ---------------------------------------------------------

        private static string RenderApiStruct(string genNamespace, string apiTypeName,
            ImmutableArray<ControllerModel> controllers)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/> EastFive scripted-flow API surface.");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
            sb.AppendLine($"namespace {genNamespace}");
            sb.AppendLine("{");
            sb.AppendLine($"    /// <summary>The whole API surface, one queryable per controller, for authoring scripted flows.</summary>");
            sb.AppendLine($"    public partial struct {apiTypeName}");
            sb.AppendLine("    {");

            var usedMembers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var controller in controllers)
            {
                var member = controller.SimpleName;
                var suffix = 2;
                while (!usedMembers.Add(member))
                    member = controller.SimpleName + (suffix++);
                sb.AppendLine($"        /// <summary>The <see cref=\"{controller.FullyQualified}\"/> resource collection.</summary>");
                sb.AppendLine($"        public global::System.Linq.IQueryable<{controller.FullyQualified}> {member};");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string RenderExtensions(string genNamespace, ControllerModel controller)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/> EastFive scripted-flow extensions.");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
            sb.AppendLine($"namespace {genNamespace}");
            sb.AppendLine("{");

            var emittable = controller.Methods.Where(m => m.SkipReason == null).ToArray();
            var skipped = controller.Methods.Where(m => m.SkipReason != null).ToArray();

            foreach (var m in skipped)
                sb.AppendLine($"    // SKIPPED {controller.SimpleName}.{m.Name}: {m.SkipReason}");
            if (skipped.Length > 0)
                sb.AppendLine();

            sb.AppendLine($"    /// <summary>Scripted-flow extensions for <see cref=\"{controller.FullyQualified}\"/>.</summary>");
            sb.AppendLine($"    public static class {controller.SimpleName}FlowExtensions");
            sb.AppendLine("    {");

            var usedNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var m in emittable)
                RenderExtension(sb, controller, m, usedNames);

            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static void RenderExtension(StringBuilder sb, ControllerModel controller, MethodModel m,
            HashSet<string> usedNames)
        {
            var extName = m.Name;
            var suffix = 2;
            while (!usedNames.Add(extName))
                extName = $"{m.Name}{suffix++}";

            var paramNamesLiteral = string.Join(", ", m.AllParamNames.Select(n => $"\"{n}\""));
            var flowMethodArgs = m.AllParamNames.IsDefaultOrEmpty
                ? $"typeof({controller.FullyQualified}), \"{m.Name}\""
                : $"typeof({controller.FullyQualified}), \"{m.Name}\", {paramNamesLiteral}";

            sb.AppendLine($"        /// <summary>Flow step for <c>{controller.FullyQualified}.{m.Name}</c>.</summary>");
            sb.AppendLine($"        [global::EastFive.Api.Meta.Flows.Scripted.FlowMethod({flowMethodArgs})]");
            sb.AppendLine($"        public static TResult {extName}<TResult>(");
            sb.Append($"            this global::System.Linq.IQueryable<{controller.FullyQualified}> __source");

            foreach (var s in m.Surfaced)
                sb.Append($",\n            {s.SurfacedTypeFq} {Escape(s.ParamName)} = default!");

            foreach (var r in m.Responses)
            {
                var typeArgs = r.ArgTypes.IsDefaultOrEmpty
                    ? "TResult"
                    : string.Join(", ", r.ArgTypes) + ", TResult";
                sb.Append($",\n            global::System.Func<{typeArgs}>? {Escape(r.ParamName)} = null");
            }

            sb.AppendLine(")");
            sb.AppendLine("            => default!;");
            sb.AppendLine();
        }

        // ---- Symbol helpers ----------------------------------------------------

        private static bool IsResponseDelegate(ITypeSymbol type)
        {
            if (type is not INamedTypeSymbol named)
                return false;
            var invoke = named.DelegateInvokeMethod;
            return invoke is not null && invoke.ReturnType.Name == ResponseReturnTypeName;
        }

        private static bool IsNonNullableValueType(ITypeSymbol type)
        {
            if (!type.IsValueType)
                return false;
            if (type is INamedTypeSymbol named && named.ConstructedFrom?.SpecialType == SpecialType.System_Nullable_T)
                return false;
            if (type.NullableAnnotation == NullableAnnotation.Annotated)
                return false;
            return true;
        }

        private static ITypeSymbol? SingleTypeArg(ITypeSymbol type)
            => type is INamedTypeSymbol named && named.TypeArguments.Length == 1
                ? named.TypeArguments[0]
                : null;

        private static string Fq(ITypeSymbol type)
            => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        private static string? ReadApiTypeName(AttributeData attr)
        {
            foreach (var na in attr.NamedArguments)
            {
                if (na.Key == "ApiTypeName" && na.Value.Value is string s && !string.IsNullOrEmpty(s))
                    return s;
            }
            return null;
        }

        private static string Sanitize(string assemblyName)
        {
            var chars = assemblyName
                .Select(c => char.IsLetterOrDigit(c) ? c : '_')
                .ToArray();
            var result = new string(chars);
            return char.IsDigit(result.Length > 0 ? result[0] : '_') ? "_" + result : result;
        }

        private static bool HasAttribute(ISymbol symbol, string attributeName)
            => symbol.GetAttributes().Any(a => DerivesFrom(a.AttributeClass, attributeName));

        private static bool DerivesFrom(INamedTypeSymbol? attributeClass, string attributeName)
        {
            for (var t = attributeClass; t is not null; t = t.BaseType)
            {
                if (t.Name == attributeName)
                    return true;
            }
            return false;
        }

        private static string Escape(string identifier)
            => SyntaxKeywords.Contains(identifier) ? "@" + identifier : identifier;

        private static readonly HashSet<string> SyntaxKeywords = new(StringComparer.Ordinal)
        {
            "ref", "out", "in", "params", "this", "base", "default", "event", "object",
            "string", "int", "bool", "class", "struct", "namespace", "static", "void",
        };

        // ---- Models ------------------------------------------------------------

        private enum SlotKind { Query, Body, WholeBody, Skip, Unsupported }

        private readonly struct Classification
        {
            public SlotKind Kind { get; }
            public string? SurfacedTypeFq { get; }
            public bool IsNonNullableValueType { get; }
            public string? SkipReason { get; }

            private Classification(SlotKind kind, string? typeFq, bool valueType, string? skipReason)
            {
                Kind = kind; SurfacedTypeFq = typeFq; IsNonNullableValueType = valueType; SkipReason = skipReason;
            }

            public static Classification Slot(SlotKind kind, string typeFq, bool valueType)
                => new(kind, typeFq, valueType, null);
            public static Classification Skip() => new(SlotKind.Skip, null, false, null);
            public static Classification Unsupported(string reason) => new(SlotKind.Unsupported, null, false, reason);
        }

        private readonly struct ControllerModel
        {
            public string SimpleName { get; }
            public string FullyQualified { get; }
            public ImmutableArray<MethodModel> Methods { get; }
            public string HintName { get; }

            public ControllerModel(string simpleName, string fullyQualified,
                ImmutableArray<MethodModel> methods, string hintName)
            {
                SimpleName = simpleName; FullyQualified = fullyQualified;
                Methods = methods; HintName = hintName;
            }
        }

        private readonly struct MethodModel
        {
            public string Name { get; }
            public ImmutableArray<SurfacedParam> Surfaced { get; }
            public ImmutableArray<ResponseBranch> Responses { get; }
            public ImmutableArray<string> AllParamNames { get; }
            public string? SkipReason { get; }

            public MethodModel(string name, ImmutableArray<SurfacedParam> surfaced,
                ImmutableArray<ResponseBranch> responses, ImmutableArray<string> allParamNames, string? skipReason)
            {
                Name = name; Surfaced = surfaced; Responses = responses;
                AllParamNames = allParamNames; SkipReason = skipReason;
            }
        }

        private readonly struct SurfacedParam
        {
            public string ParamName { get; }
            public string SurfacedTypeFq { get; }
            public bool IsNonNullableValueType { get; }

            public SurfacedParam(string paramName, string surfacedTypeFq, bool isNonNullableValueType)
            {
                ParamName = paramName; SurfacedTypeFq = surfacedTypeFq; IsNonNullableValueType = isNonNullableValueType;
            }
        }

        private readonly struct ResponseBranch
        {
            public string ParamName { get; }
            public ImmutableArray<string> ArgTypes { get; }
            public ResponseBranch(string paramName, ImmutableArray<string> argTypes)
            {
                ParamName = paramName; ArgTypes = argTypes;
            }
        }
    }
}
