using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace EastFive.Generators
{
    /// <summary>
    /// Emits one strongly-typed test extension per EastFive controller method
    /// (Phase 3 of the TDD-harness plan). For every method on a
    /// <c>[FunctionViewController]</c> type that carries an HTTP verb attribute,
    /// the generator surfaces only the user-supplied (request-bound) parameters,
    /// hides framework/instigator parameters, and exposes the response branches
    /// as typed <c>AssertOn*</c> helpers on a per-method result struct.
    ///
    /// <para>The generator works purely against Roslyn symbols (it cannot
    /// reference EastFive types). Parameter classification is by attribute
    /// simple-name + well-known type names; any method containing a parameter
    /// the generator cannot model is skipped with an explanatory comment rather
    /// than producing code that fails to compile.</para>
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public class TestHarnessGenerator : IIncrementalGenerator
    {
        private const string ResponseReturnTypeName = "IHttpResponse";

        /// <summary>
        /// Consumer wiring, read from compiler-visible build properties (declared
        /// in build/EastFive.Generators.props inside the package):
        /// <c>EastFiveTestHarnessNamespace</c> — namespace holding the harness
        /// contract types (TestSession, ResponseBranchCapture, GeneratedAssert,
        /// HarnessReflection); <c>EastFiveTestHarnessTargetAssemblies</c> —
        /// semicolon-separated assembly names to scan for controllers;
        /// <c>EastFiveTestHarnessGeneratedNamespace</c> — namespace to emit into
        /// (defaults to "{harness namespace}.Generated").
        /// The generator stays inert unless the harness namespace is configured
        /// AND its TestSession marker type is visible in the compilation — so it
        /// emits nothing when the analyzer runs against a production assembly.
        /// </summary>
        private readonly struct HarnessConfig
        {
            public HarnessConfig(string harnessNamespace, string targetAssemblies, string generatedNamespace)
            {
                HarnessNamespace = harnessNamespace;
                TargetAssemblies = targetAssemblies;
                GeneratedNamespace = generatedNamespace;
            }

            public string HarnessNamespace { get; }
            public string TargetAssemblies { get; }
            public string GeneratedNamespace { get; }
            public string TestSessionMetadataName => HarnessNamespace + ".TestSession";
        }

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var config = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) =>
            {
                provider.GlobalOptions.TryGetValue("build_property.EastFiveTestHarnessNamespace", out var harnessNs);
                provider.GlobalOptions.TryGetValue("build_property.EastFiveTestHarnessTargetAssemblies", out var targets);
                provider.GlobalOptions.TryGetValue("build_property.EastFiveTestHarnessGeneratedNamespace", out var generatedNs);
                if (string.IsNullOrWhiteSpace(harnessNs))
                    return default(HarnessConfig?);
                return new HarnessConfig(
                    harnessNs!.Trim(),
                    targets ?? string.Empty,
                    string.IsNullOrWhiteSpace(generatedNs) ? harnessNs.Trim() + ".Generated" : generatedNs!.Trim());
            });

            context.RegisterSourceOutput(context.CompilationProvider.Combine(config),
                static (spc, source) =>
                {
                    var (compilation, maybeConfig) = source;
                    if (maybeConfig is not HarnessConfig cfg)
                        return;

                    // Only run in the test compilation (the one that owns the harness).
                    if (compilation.GetTypeByMetadataName(cfg.TestSessionMetadataName) is null)
                        return;

                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var controllerType in EnumerateControllers(compilation, cfg))
                    {
                        var model = BuildController(controllerType);
                        if (model is not ControllerModel value || value.Methods.IsDefaultOrEmpty)
                            continue;
                        if (!seen.Add(value.HintName))
                            continue;
                        spc.AddSource(value.HintName, SourceText.From(Render(value, cfg), Encoding.UTF8));
                    }
                });
        }

        // ---- Symbol enumeration ------------------------------------------------

        private static IEnumerable<INamedTypeSymbol> EnumerateControllers(Compilation compilation, HarnessConfig cfg)
        {
            var targets = new HashSet<string>(
                cfg.TargetAssemblies.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(a => a.Trim()),
                StringComparer.Ordinal);
            var assemblies = new List<IAssemblySymbol> { compilation.Assembly };
            foreach (var reference in compilation.References)
            {
                if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol asm
                    && targets.Contains(asm.Name))
                    assemblies.Add(asm);
            }

            foreach (var asm in assemblies)
            {
                foreach (var type in EnumerateTypes(asm.GlobalNamespace))
                {
                    if (type.TypeKind is TypeKind.Class or TypeKind.Struct
                        && HasAttribute(type, "FunctionViewControllerAttribute"))
                        yield return type;
                }
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

            var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
                ? null
                : typeSymbol.ContainingNamespace.ToDisplayString();
            var fq = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var hint = (fq.Replace("global::", string.Empty).Replace("<", "_").Replace(">", "_")
                .Replace(",", "_").Replace(" ", string.Empty)) + ".Harness.g.cs";

            return new ControllerModel(
                simpleName: typeSymbol.Name,
                fullyQualified: fq,
                @namespace: ns,
                methods: methods,
                hintName: hint);
        }

        private static MethodModel BuildMethod(IMethodSymbol method)
        {
            var allParamNames = method.Parameters.Select(p => p.Name).ToImmutableArray();
            var surfaced = ImmutableArray.CreateBuilder<SurfacedParam>();
            var responses = ImmutableArray.CreateBuilder<ResponseBranch>();
            string? skip = null;
            var wholeBodyCount = 0;
            var namedBodyCount = 0;

            foreach (var p in method.Parameters)
            {
                if (skip != null)
                    break;

                // Response delegate? (delegate whose Invoke returns IHttpResponse)
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
                        if (classification.Kind == SlotKind.WholeBody)
                            wholeBodyCount++;
                        if (classification.Kind == SlotKind.Body)
                            namedBodyCount++;
                        surfaced.Add(new SurfacedParam(
                            paramName: p.Name,
                            slot: classification.Kind,
                            slotKey: classification.SlotKey ?? p.Name,
                            surfacedTypeFq: classification.SurfacedTypeFq!,
                            optional: classification.Optional,
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

            // The typed envelope can serve at most one whole-body slot, and a
            // whole-body resource cannot coexist with named body slots.
            if (skip == null && wholeBodyCount > 1)
                skip = "method has more than one whole-body parameter";
            if (skip == null && wholeBodyCount >= 1 && namedBodyCount >= 1)
                skip = "method mixes a whole-body resource with named body slots";

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
            var argTypes = invoke is null
                ? ImmutableArray<string>.Empty
                : invoke.Parameters
                    .Select(ip => ip.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                    .ToImmutableArray();
            return new ResponseBranch(p.Name, argTypes);
        }

        private static Classification Classify(IParameterSymbol p)
        {
            var attrs = p.GetAttributes();

            foreach (var a in attrs)
            {
                var name = a.AttributeClass?.Name;
                switch (name)
                {
                    case "QueryAttribute":
                    case "QueryOptionalAttribute":
                    case "RouteAttribute":
                        return Classification.Slot(SlotKind.Query,
                            SlotKey(a) ?? p.Name, Fq(p.Type),
                            optional: name == "QueryOptionalAttribute" || HasDefaultOrNullable(p),
                            valueType: IsNonNullableValueType(p.Type));

                    case "BodyAttribute":
                        return Classification.Slot(SlotKind.Body,
                            SlotKey(a) ?? p.Name, Fq(p.Type), optional: HasDefaultOrNullable(p),
                            valueType: IsNonNullableValueType(p.Type));

                    case "PropertyAttribute":
                        return Classification.Slot(SlotKind.Body,
                            SlotKey(a) ?? p.Name, Fq(p.Type), optional: HasDefaultOrNullable(p),
                            valueType: IsNonNullableValueType(p.Type));

                    case "PropertyOptionalAttribute":
                        return Classification.Slot(SlotKind.Body,
                            SlotKey(a) ?? p.Name, Fq(p.Type), optional: true,
                            valueType: IsNonNullableValueType(p.Type));

                    case "ResourceAttribute":
                        return Classification.Slot(SlotKind.WholeBody, p.Name, Fq(p.Type), optional: false,
                            valueType: IsNonNullableValueType(p.Type));

                    case "StorableEntityFromResourceAttribute":
                    {
                        var inner = SingleTypeArg(p.Type);
                        if (inner is null)
                            return Classification.Unsupported($"cannot read entity type from '{p.Type.ToDisplayString()}'");
                        return Classification.Slot(SlotKind.WholeBody, p.Name, Fq(inner), optional: false,
                            valueType: IsNonNullableValueType(inner));
                    }

                    case "StorageEntityFromQueryIdAttribute":
                    {
                        var inner = SingleTypeArg(p.Type);
                        if (inner is null)
                            return Classification.Unsupported($"cannot read entity type from '{p.Type.ToDisplayString()}'");
                        var refType = $"global::EastFive.IRef<{Fq(inner)}>";
                        // IRef<T> is an interface (reference type).
                        return Classification.Slot(SlotKind.Query, SlotKey(a) ?? "id", refType, optional: false,
                            valueType: false);
                    }

                    case "StorageEntitiesAttribute":
                    case "StorageResourcesAttribute":
                        return Classification.Skip();

                    case "HeaderAttribute":
                        return Classification.Unsupported("header-bound parameters are not yet supported by the harness");
                }
            }

            // No recognized binding attribute — framework/instigator parameters
            // that the harness supplies (instigators) or that need no value.
            var typeName = p.Type.Name;
            if (typeName is "CancellationToken" or "IHttpRequest" or "IApplication"
                or "IAzureApplication"
                or "ElevenLabsHMACSignature" or "SessionToken" or "SessionTokenMaybe"
                or "PracticeEnvironmentRef" or "AuthorizedAccount" or "IProvideClaims"
                or "Security" or "IProvideDocumentSigningFlow")
                return Classification.Skip();

            // Plain IQueryable<TEntity> storage-query parameters carry no binding
            // attribute because the production EastFive pipeline injects them
            // (StorageQueryInvocationAttribute builds a StorageQuery<TEntity> from
            // FromSettings). The harness runs that same pipeline, so it supplies
            // nothing and lets the production instigation resolve the value.
            if (p.Type is INamedTypeSymbol { Name: "IQueryable", TypeArguments.Length: 1 } queryable
                && queryable.ContainingNamespace?.ToDisplayString() == "System.Linq")
                return Classification.Skip();

            return Classification.Unsupported(
                $"parameter '{p.Name}' of type '{p.Type.ToDisplayString()}' has no binding attribute and is not a known instigator");
        }

        // ---- Rendering ---------------------------------------------------------

        private static string Render(ControllerModel controller, HarnessConfig cfg)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/> EastFive test harness extensions.");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine();
            sb.AppendLine($"namespace {cfg.GeneratedNamespace}");
            sb.AppendLine("{");

            var emittable = controller.Methods.Where(m => m.SkipReason == null && !m.Surfaced.IsDefault).ToArray();
            var skipped = controller.Methods.Where(m => m.SkipReason != null).ToArray();

            foreach (var m in skipped)
                sb.AppendLine($"    // SKIPPED {controller.SimpleName}.{m.Name}: {m.SkipReason}");
            if (skipped.Length > 0)
                sb.AppendLine();

            // Result structs
            foreach (var m in emittable)
                RenderResultStruct(sb, controller, m, cfg);

            // Extension class
            sb.AppendLine($"    /// <summary>Generated test extensions for <see cref=\"{controller.FullyQualified}\"/>.</summary>");
            sb.AppendLine($"    public static class {controller.SimpleName}HarnessExtensions");
            sb.AppendLine("    {");
            var usedNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var m in emittable)
                RenderExtension(sb, controller, m, usedNames, cfg);
            sb.AppendLine("    }");

            sb.AppendLine("}");
            return sb.ToString();
        }

        private static void RenderResultStruct(StringBuilder sb, ControllerModel controller, MethodModel m, HarnessConfig cfg)
        {
            var harness = "global::" + cfg.HarnessNamespace;
            var resultType = $"{controller.SimpleName}{m.Name}Result";
            sb.AppendLine($"    /// <summary>Typed result of <c>{controller.SimpleName}_{m.Name}</c>; exposes the captured response branch.</summary>");
            sb.AppendLine($"    public readonly struct {resultType}");
            sb.AppendLine("    {");
            sb.AppendLine($"        private readonly {harness}.ResponseBranchCapture capture;");
            sb.AppendLine($"        public {resultType}({harness}.ResponseBranchCapture capture) => this.capture = capture;");
            sb.AppendLine("        /// <summary>Name of the response delegate the controller invoked, or <c>null</c> if none fired.</summary>");
            sb.AppendLine("        public string? WhichBranch => this.capture.BranchName;");

            foreach (var r in m.Responses)
            {
                var assertName = "AssertOn" + Capitalize(StripLeadingOn(r.ParamName));
                if (r.ArgTypes.IsDefaultOrEmpty)
                {
                    sb.AppendLine($"        /// <summary>Asserts the controller invoked <c>{r.ParamName}</c>.</summary>");
                    sb.AppendLine($"        public void {assertName}() => {harness}.GeneratedAssert.Branch(this.capture, \"{r.ParamName}\");");
                }
                else if (r.ArgTypes.Length <= 3)
                {
                    var typeArgs = string.Join(", ", r.ArgTypes);
                    sb.AppendLine($"        /// <summary>Asserts the controller invoked <c>{r.ParamName}</c>; optionally validates the response arguments.</summary>");
                    sb.AppendLine($"        public void {assertName}(global::System.Action<{typeArgs}>? validate = null) => {harness}.GeneratedAssert.Branch(this.capture, \"{r.ParamName}\", validate);");
                }
                else
                {
                    sb.AppendLine($"        /// <summary>Asserts the controller invoked <c>{r.ParamName}</c> (argument validation unsupported for this arity).</summary>");
                    sb.AppendLine($"        public void {assertName}() => {harness}.GeneratedAssert.Branch(this.capture, \"{r.ParamName}\");");
                }
            }

            sb.AppendLine("    }");
            sb.AppendLine();
        }

        private static void RenderExtension(StringBuilder sb, ControllerModel controller, MethodModel m, HashSet<string> usedNames, HarnessConfig cfg)
        {
            var harness = "global::" + cfg.HarnessNamespace;
            var resultType = $"{controller.SimpleName}{m.Name}Result";
            var extName = $"{controller.SimpleName}_{m.Name}";
            var suffix = 2;
            while (!usedNames.Add(extName))
                extName = $"{controller.SimpleName}_{m.Name}_{suffix++}";

            // Order: required surfaced first, then optional, then overrides.
            var required = m.Surfaced.Where(s => !s.Optional).ToArray();
            var optional = m.Surfaced.Where(s => s.Optional).ToArray();

            sb.AppendLine($"        /// <summary>Invokes <c>{controller.FullyQualified}.{m.Name}</c> through the test dispatch pipeline.</summary>");
            sb.AppendLine($"        public static async global::System.Threading.Tasks.Task<{resultType}> {extName}(");
            sb.AppendLine($"            this {harness}.TestSession session,");
            foreach (var s in required)
                sb.AppendLine($"            {s.SurfacedTypeFq} {Escape(s.ParamName)},");
            foreach (var s in optional)
                sb.AppendLine($"            {s.SurfacedTypeFq} {Escape(s.ParamName)} = default!,");
            sb.AppendLine("            global::System.Collections.Generic.IReadOnlyDictionary<string, object>? overrides = null)");
            sb.AppendLine("        {");
            sb.AppendLine("            var __query = new global::System.Collections.Generic.Dictionary<string, object>(global::System.StringComparer.Ordinal);");
            sb.AppendLine("            var __body = new global::System.Collections.Generic.Dictionary<string, object>(global::System.StringComparer.Ordinal);");

            foreach (var s in m.Surfaced)
            {
                var dict = s.Slot == SlotKind.Query ? "__query" : "__body";
                if (s.IsNonNullableValueType)
                    // `x is null` is illegal on a non-nullable value type, and such
                    // parameters are always supplied, so assign unconditionally.
                    sb.AppendLine($"            {dict}[\"{s.SlotKey}\"] = {Escape(s.ParamName)};");
                else
                    sb.AppendLine($"            if ({Escape(s.ParamName)} is not null) {dict}[\"{s.SlotKey}\"] = {Escape(s.ParamName)};");
            }

            var paramNamesLiteral = string.Join(", ", m.AllParamNames.Select(n => $"\"{n}\""));
            var paramNamesArray = m.AllParamNames.IsDefaultOrEmpty
                ? "new string[] { }"
                : $"new[] {{ {paramNamesLiteral} }}";
            sb.AppendLine($"            var __method = {harness}.HarnessReflection.ResolveControllerMethod(");
            sb.AppendLine($"                typeof({controller.FullyQualified}), \"{m.Name}\", {paramNamesArray});");
            sb.AppendLine("            var __capture = await session.DispatchMethodAsync(__method,");
            sb.AppendLine("                __body.Count == 0 ? null : __body,");
            sb.AppendLine("                __query.Count == 0 ? null : __query,");
            sb.AppendLine("                overrides).ConfigureAwait(false);");
            sb.AppendLine($"            return new {resultType}(__capture);");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        // ---- Symbol helpers ----------------------------------------------------

        private static bool IsResponseDelegate(ITypeSymbol type)
        {
            if (type is not INamedTypeSymbol named)
                return false;
            var invoke = named.DelegateInvokeMethod;
            if (invoke is null)
                return false;
            return invoke.ReturnType.Name == ResponseReturnTypeName;
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

        private static string? SlotKey(AttributeData attr)
        {
            foreach (var na in attr.NamedArguments)
            {
                if (na.Key == "Name" && na.Value.Value is string s && !string.IsNullOrEmpty(s))
                    return s;
            }
            return null;
        }

        private static bool HasDefaultOrNullable(IParameterSymbol p)
        {
            if (p.HasExplicitDefaultValue)
                return true;
            if (p.Type.NullableAnnotation == NullableAnnotation.Annotated)
                return true;
            if (p.Type is INamedTypeSymbol named && named.ConstructedFrom?.SpecialType == SpecialType.System_Nullable_T)
                return true;
            return false;
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

        private static string StripLeadingOn(string name)
            => name.Length > 2 && name.StartsWith("on", StringComparison.Ordinal) && char.IsUpper(name[2])
                ? name.Substring(2)
                : name;

        private static string Capitalize(string name)
            => string.IsNullOrEmpty(name) ? name : char.ToUpperInvariant(name[0]) + name.Substring(1);

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
            public string? SlotKey { get; }
            public string? SurfacedTypeFq { get; }
            public bool Optional { get; }
            public bool IsNonNullableValueType { get; }
            public string? SkipReason { get; }

            private Classification(SlotKind kind, string? slotKey, string? typeFq, bool optional, bool valueType, string? skipReason)
            {
                Kind = kind; SlotKey = slotKey; SurfacedTypeFq = typeFq; Optional = optional;
                IsNonNullableValueType = valueType; SkipReason = skipReason;
            }

            public static Classification Slot(SlotKind kind, string slotKey, string typeFq, bool optional, bool valueType)
                => new(kind, slotKey, typeFq, optional, valueType, null);
            public static Classification Skip() => new(SlotKind.Skip, null, null, false, false, null);
            public static Classification Unsupported(string reason) => new(SlotKind.Unsupported, null, null, false, false, reason);
        }

        private readonly struct ControllerModel
        {
            public string SimpleName { get; }
            public string FullyQualified { get; }
            public string? Namespace { get; }
            public ImmutableArray<MethodModel> Methods { get; }
            public string HintName { get; }

            public ControllerModel(string simpleName, string fullyQualified, string? @namespace,
                ImmutableArray<MethodModel> methods, string hintName)
            {
                SimpleName = simpleName; FullyQualified = fullyQualified; Namespace = @namespace;
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
            public SlotKind Slot { get; }
            public string SlotKey { get; }
            public string SurfacedTypeFq { get; }
            public bool Optional { get; }
            public bool IsNonNullableValueType { get; }

            public SurfacedParam(string paramName, SlotKind slot, string slotKey, string surfacedTypeFq, bool optional, bool isNonNullableValueType)
            {
                ParamName = paramName; Slot = slot; SlotKey = slotKey; SurfacedTypeFq = surfacedTypeFq;
                Optional = optional; IsNonNullableValueType = isNonNullableValueType;
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
