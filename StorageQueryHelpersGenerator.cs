using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace EastFive.Generators
{
    [Generator(LanguageNames.CSharp)]
    public class StorageQueryHelpersGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var candidates = context.SyntaxProvider
                .CreateSyntaxProvider(
                    static (node, _) => node is TypeDeclarationSyntax tds && tds.AttributeLists.Count > 0,
                    static (syntaxContext, _) => GetPartialEntity(syntaxContext))
                .Where(static entity => entity is EntityModel)
                .Select(static (entity, _) => entity.GetValueOrDefault());

            var allCandidates = candidates.Collect();

            context.RegisterSourceOutput(allCandidates, static (spc, entities) => EmitAll(spc, entities));
        }

        private static EntityModel? GetPartialEntity(GeneratorSyntaxContext context)
        {
            if (context.Node is not TypeDeclarationSyntax typeSyntax)
                return null;

            if (context.SemanticModel.GetDeclaredSymbol(typeSyntax) is not INamedTypeSymbol typeSymbol)
                return null;

            if (!HasAttributeOrSubclass(typeSymbol, "StorageTableAttribute"))
                return null;

            var rawMembers = ImmutableArray.CreateBuilder<RawMember>();

            foreach (var memberSyntax in typeSyntax.Members)
            {
                switch (memberSyntax)
                {
                    case FieldDeclarationSyntax field when !IsStaticModifier(field.Modifiers):
                        var fieldTypeName = ResolveType(context.SemanticModel, field.Declaration.Type);
                        foreach (var variable in field.Declaration.Variables)
                        {
                            var fieldRaw = BuildRawMember(variable.Identifier.ValueText, fieldTypeName, field.AttributeLists);
                            if (fieldRaw is RawMember rawField)
                                rawMembers.Add(rawField);
                        }
                        break;

                    case PropertyDeclarationSyntax property when !IsStaticModifier(property.Modifiers):
                        var propertyTypeName = ResolveType(context.SemanticModel, property.Type);
                        var propertyRaw = BuildRawMember(property.Identifier.ValueText, propertyTypeName, property.AttributeLists);
                        if (propertyRaw is RawMember rawProperty)
                            rawMembers.Add(rawProperty);
                        break;
                }
            }

            var containingNamespace = typeSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            var typeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var entityName = typeSymbol.Name;
            var hintName = $"{containingNamespace}.{entityName}.Storage.g.cs".Replace('<', '_').Replace('>', '_');

            var typeKeyword = GetTypeKeyword(typeSyntax);
            var isPartial = typeSyntax.Modifiers.Any(static m => m.IsKind(SyntaxKind.PartialKeyword));

            return new EntityModel(containingNamespace, typeName, entityName, hintName, typeKeyword, isPartial, rawMembers.ToImmutable());
        }

        // Each partial declaration of a [StorageTable] type produces its own
        // EntityModel carrying only the members declared in that file. All
        // partials are merged here before rendering so that scoped lookups can
        // resolve scope members regardless of which partial declares them.
        private static void EmitAll(SourceProductionContext spc, ImmutableArray<EntityModel> entities)
        {
            var emitted = new HashSet<string>(StringComparer.Ordinal);

            foreach (var group in entities.GroupBy(static e => e.HintName, StringComparer.Ordinal))
            {
                var first = group.First();

                var rawByName = new Dictionary<string, RawMember>(StringComparer.Ordinal);
                foreach (var partial in group)
                    foreach (var raw in partial.RawMembers)
                        if (!rawByName.ContainsKey(raw.MemberName))
                            rawByName.Add(raw.MemberName, raw);

                var rawMembers = rawByName.Values.ToImmutableArray();
                var scopeMembers = rawMembers
                    .Where(static r => r.ScopeString is not null)
                    .ToImmutableArray();

                var members = rawMembers
                    .Select(raw => ToMemberModel(raw, scopeMembers))
                    .ToImmutableArray();

                var rowKeyMembers = members.Where(static m => m.IsRowKey).ToImmutableArray();
                var hasSingleIRefRowKey = rowKeyMembers.Length == 1 && IsSingleIRefRowKey(rowKeyMembers[0], first.EntityName);
                var needsCompositeGetById = rowKeyMembers.Length > 0 && !hasSingleIRefRowKey;

                var hasQueryableMembers = members.Any(static m => m.IsStorageQuery || m.IsSingleLookup || m.IsScopedLookup);
                if (!hasQueryableMembers && !needsCompositeGetById)
                    continue;

                if (!emitted.Add(first.HintName))
                    continue;

                var isPartial = group.Any(static e => e.IsPartial);

                var render = new RenderModel(
                    first.NamespaceName,
                    first.EntityTypeName,
                    first.EntityName,
                    first.TypeKeyword,
                    isPartial,
                    members,
                    rowKeyMembers,
                    needsCompositeGetById);

                var source = RenderEntityHelpers(render);
                spc.AddSource(first.HintName, SourceText.From(source, Encoding.UTF8));
            }
        }

        private static MemberModel ToMemberModel(RawMember raw, ImmutableArray<RawMember> scopeMembers)
        {
            var scopeParameters = ImmutableArray<ScopeParam>.Empty;
            if (raw.IsScopedLookup)
            {
                scopeParameters = scopeMembers
                    .Where(s => !string.Equals(s.MemberName, raw.MemberName, StringComparison.Ordinal)
                        && (string.Equals(s.ScopeString, raw.ScopedRow, StringComparison.Ordinal)
                            || string.Equals(s.ScopeString, raw.ScopedPartition, StringComparison.Ordinal)))
                    .Select(static s => new ScopeParam(s.EscapedMemberName, s.TypeName, s.MethodSuffix))
                    .ToImmutableArray();
            }

            return new MemberModel(
                raw.MemberName,
                raw.EscapedMemberName,
                raw.MethodSuffix,
                raw.TypeName,
                raw.IsStorageQuery,
                raw.IsIdHashLookup || raw.IsStringHashLookup,
                raw.IsScopedLookup && scopeParameters.Length > 0,
                raw.IsRowKey,
                scopeParameters);
        }

        // Reads a member's flags and scope arguments directly from its own
        // declaration syntax. This is the authoritative source: reading attribute
        // data via the member ISymbol (GetAttributes / DeclaringSyntaxReferences)
        // can resolve to a SIBLING member's attributes for partial types whose
        // members share identically shaped nameof()-based attributes.
        private static RawMember? BuildRawMember(string rawName, string typeName, SyntaxList<AttributeListSyntax> attributeLists)
        {
            var isStorageQuery = false;
            var isIdHashLookup = false;
            var isStringHashLookup = false;
            var isScopedLookup = false;
            var isRowKey = false;
            string? scopeString = null;
            string? scopedRow = null;
            string? scopedPartition = null;

            foreach (var attributeList in attributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    if (AttributeNameMatches(attribute.Name, "StorageQuery"))
                        isStorageQuery = true;
                    else if (AttributeNameMatches(attribute.Name, "IdHashXX32Lookup"))
                        isIdHashLookup = true;
                    else if (AttributeNameMatches(attribute.Name, "StringLookupHashXX32"))
                        isStringHashLookup = true;
                    else if (AttributeNameMatches(attribute.Name, "RowKey"))
                        isRowKey = true;
                    else if (AttributeNameMatches(attribute.Name, "ScopeString"))
                        scopeString = GetPositionalArg(attribute, 0);
                    else if (AttributeNameMatches(attribute.Name, "ScopedLookup"))
                    {
                        isScopedLookup = true;
                        scopedRow = GetPositionalArg(attribute, 0);
                        scopedPartition = GetPositionalArg(attribute, 1);
                    }
                }
            }

            if (!isStorageQuery && !isIdHashLookup && !isStringHashLookup && !isScopedLookup && !isRowKey && scopeString is null)
                return null;

            var escapedName = rawName.StartsWith("@", StringComparison.Ordinal)
                ? rawName
                : NeedsAtPrefix(rawName) ? "@" + rawName : rawName;

            return new RawMember(
                rawName,
                escapedName,
                ToMethodSuffix(rawName, typeName),
                typeName,
                isStorageQuery,
                isIdHashLookup,
                isStringHashLookup,
                isScopedLookup,
                isRowKey,
                scopeString,
                scopedRow,
                scopedPartition);
        }

        private static string ResolveType(SemanticModel semanticModel, TypeSyntax typeSyntax)
        {
            var typeSymbol = semanticModel.GetTypeInfo(typeSyntax).Type;
            return typeSymbol is null
                ? typeSyntax.ToString()
                : typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        private static string? GetPositionalArg(AttributeSyntax attribute, int index)
        {
            if (attribute.ArgumentList is null)
                return null;

            var positional = attribute.ArgumentList.Arguments
                .Where(static a => a.NameEquals is null)
                .ToList();
            if (positional.Count <= index)
                return null;

            return ExtractStringFromExpression(positional[index].Expression);
        }

        private static bool IsStaticModifier(SyntaxTokenList modifiers)
        {
            foreach (var modifier in modifiers)
            {
                if (modifier.IsKind(SyntaxKind.StaticKeyword) || modifier.IsKind(SyntaxKind.ConstKeyword))
                    return true;
            }

            return false;
        }

        private static string GetTypeKeyword(TypeDeclarationSyntax typeSyntax)
        {
            if (typeSyntax is RecordDeclarationSyntax record)
            {
                return record.ClassOrStructKeyword.IsKind(SyntaxKind.None)
                    ? "record"
                    : $"record {record.ClassOrStructKeyword.ValueText}";
            }

            return typeSyntax.Keyword.ValueText;
        }

        private static bool IsSingleIRefRowKey(MemberModel member, string entityName)
        {
            if (!member.IsRowKey)
                return false;

            return member.TypeName.StartsWith("global::EastFive.IRef<", StringComparison.Ordinal)
                && member.TypeName.EndsWith($"{entityName}>", StringComparison.Ordinal);
        }

        private static string RenderEntityHelpers(RenderModel entity)
        {
            var builder = new StringBuilder();
            builder.AppendLine("// <auto-generated />");
            builder.AppendLine("#nullable enable");
            builder.AppendLine("using System.Linq;");
            builder.AppendLine("using System.Threading.Tasks;");
            builder.AppendLine("using EastFive.Linq.Async;");
            builder.AppendLine("using EastFive.Azure.Persistence.AzureStorageTables;");
            if (!string.IsNullOrWhiteSpace(entity.NamespaceName))
            {
                builder.AppendLine($"namespace {entity.NamespaceName}");
                builder.AppendLine("{");
            }

            builder.AppendLine($"    public static partial class {entity.EntityName}StorageQueries");
            builder.AppendLine("    {");

            var usedNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (var member in entity.Members)
            {
                if (member.IsStorageQuery)
                {
                    var methodName = EnsureUnique("By" + member.MethodSuffix, usedNames);
                    builder.AppendLine($"        public static IEnumerableAsync<{entity.EntityTypeName}> {methodName}(this IQueryable<{entity.EntityTypeName}> source, {member.TypeName} value)");
                    builder.AppendLine("        {");
                    builder.AppendLine($"            return source.Where(entity => entity.{member.EscapedMemberName} == value).StorageExecute();");
                    builder.AppendLine("        }");
                    builder.AppendLine();
                }

                if (member.IsSingleLookup)
                {
                    var methodName = EnsureUnique("On" + member.MethodSuffix, usedNames);
                    builder.AppendLine($"        public static IEnumerableAsync<{entity.EntityTypeName}> {methodName}(this IQueryable<{entity.EntityTypeName}> source, {member.TypeName} value)");
                    builder.AppendLine("        {");
                    builder.AppendLine($"            var driver = (source as global::EastFive.Azure.Persistence.AzureStorageTables.StorageQuery<{entity.EntityTypeName}>)?.StorageDriver");
                    builder.AppendLine($"                ?? global::EastFive.Azure.Persistence.StorageTables.StorageDriverScope.Resolve(typeof({entity.EntityTypeName})).GetDriver();");
                    builder.AppendLine($"            return driver.StorageFindByProperty(value, ({entity.EntityTypeName} entity) => entity.{member.EscapedMemberName});");
                    builder.AppendLine("        }");
                    builder.AppendLine();
                }

                if (member.IsScopedLookup)
                {
                    var nameBuilder = new StringBuilder("On" + member.MethodSuffix);
                    foreach (var scope in member.ScopeParameters)
                        nameBuilder.Append("And").Append(scope.MethodSuffix);
                    var methodName = EnsureUnique(nameBuilder.ToString(), usedNames);

                    var parameters = new StringBuilder($"this IQueryable<{entity.EntityTypeName}> source, {member.TypeName} value");
                    foreach (var scope in member.ScopeParameters)
                        parameters.Append($", {scope.TypeName} {scope.EscapedName}");

                    var whereClauses = new StringBuilder($".Where(entity => entity.{member.EscapedMemberName} == value)");
                    foreach (var scope in member.ScopeParameters)
                        whereClauses.Append($".Where(entity => entity.{scope.EscapedName} == {scope.EscapedName})");

                    builder.AppendLine($"        public static IEnumerableAsync<{entity.EntityTypeName}> {methodName}({parameters})");
                    builder.AppendLine("        {");
                    builder.AppendLine($"            return source{whereClauses}.StorageExecute();");
                    builder.AppendLine("        }");
                    builder.AppendLine();
                }
            }

            if (entity.NeedsCompositeGetById)
            {
                var parameters = string.Join(", ",
                    entity.RowKeyMembers.Select((member, index) => $"{member.TypeName} key{index}"));
                builder.AppendLine($"        public static async Task<{entity.EntityTypeName}?> GetByIdAsync(this IQueryable<{entity.EntityTypeName}> source, {parameters})");
                builder.AppendLine("        {");

                var clauses = string.Concat(
                    entity.RowKeyMembers.Select((member, index) =>
                        $".Where(entity => entity.{member.EscapedMemberName} == key{index})"));

                builder.AppendLine($"            var matches = await source{clauses}.StorageExecute().ToArrayAsync();");
                builder.AppendLine("            return matches.Length == 0 ? default : matches[0];");
                builder.AppendLine("        }");
                builder.AppendLine();
            }

            builder.AppendLine("    }");

            AppendStaticHelpers(builder, entity);

            if (!string.IsNullOrWhiteSpace(entity.NamespaceName))
                builder.AppendLine("}");

            return builder.ToString();
        }

        // Emits driver-resolving static query methods directly on the resource
        // type (as a partial). These mirror the IQueryable extension methods but
        // accept an optional driver; when omitted the driver is resolved via
        // reflection (type then assembly) through StorageDriverScope.Resolve.
        // Only emitted for types declared `partial` that expose query members.
        private static void AppendStaticHelpers(StringBuilder builder, RenderModel entity)
        {
            var hasQueryMembers = entity.Members.Any(static m => m.IsStorageQuery || m.IsSingleLookup || m.IsScopedLookup);
            if (!entity.IsPartial || !hasQueryMembers)
                return;

            var usedNames = new HashSet<string>(StringComparer.Ordinal);

            builder.AppendLine();
            builder.AppendLine($"    public partial {entity.TypeKeyword} {entity.EntityName}");
            builder.AppendLine("    {");

            foreach (var member in entity.Members)
            {
                if (member.IsStorageQuery)
                {
                    var methodName = EnsureUnique("By" + member.MethodSuffix, usedNames);
                    AppendStaticLookup(builder, entity, member, methodName, member.ScopeParameters, includeScopes: false, useFindBy: false);
                }

                if (member.IsSingleLookup)
                {
                    var methodName = EnsureUnique("On" + member.MethodSuffix, usedNames);
                    AppendStaticLookup(builder, entity, member, methodName, member.ScopeParameters, includeScopes: false, useFindBy: true);
                }

                if (member.IsScopedLookup)
                {
                    var nameBuilder = new StringBuilder("On" + member.MethodSuffix);
                    foreach (var scope in member.ScopeParameters)
                        nameBuilder.Append("And").Append(scope.MethodSuffix);
                    var methodName = EnsureUnique(nameBuilder.ToString(), usedNames);
                    AppendStaticLookup(builder, entity, member, methodName, member.ScopeParameters, includeScopes: true, useFindBy: false);
                }
            }

            builder.AppendLine("    }");
        }

        private static void AppendStaticLookup(
            StringBuilder builder,
            RenderModel entity,
            MemberModel member,
            string methodName,
            ImmutableArray<ScopeParam> scopeParameters,
            bool includeScopes,
            bool useFindBy)
        {
            const string driverType = "global::EastFive.Persistence.Azure.StorageTables.Driver.AzureTableDriverDynamic";
            const string storageQueryType = "global::EastFive.Azure.Persistence.AzureStorageTables.StorageQuery";
            const string driverScopeType = "global::EastFive.Azure.Persistence.StorageTables.StorageDriverScope";

            var parameters = new StringBuilder($"{member.TypeName} value");
            if (includeScopes)
                foreach (var scope in scopeParameters)
                    parameters.Append($", {scope.TypeName} {scope.EscapedName}");
            parameters.Append($", {driverType}? driver = null");

            builder.AppendLine($"        public static IEnumerableAsync<{entity.EntityTypeName}> {methodName}({parameters})");
            builder.AppendLine("        {");
            builder.AppendLine($"            var resolvedDriver = driver ?? {driverScopeType}.Resolve(typeof({entity.EntityTypeName})).GetDriver();");

            if (useFindBy)
            {
                // Hash-lookup members ([IdHashXX32Lookup]/[StringLookupHashXX32]) resolve via the
                // lookup table, not a table scan; they have no IProvideTableQuery attribute.
                builder.AppendLine($"            return resolvedDriver.StorageFindByProperty(value, ({entity.EntityTypeName} entity) => entity.{member.EscapedMemberName});");
            }
            else
            {
                var whereClauses = new StringBuilder($".Where(entity => entity.{member.EscapedMemberName} == value)");
                if (includeScopes)
                    foreach (var scope in scopeParameters)
                        whereClauses.Append($".Where(entity => entity.{scope.EscapedName} == {scope.EscapedName})");

                builder.AppendLine($"            return new {storageQueryType}<{entity.EntityTypeName}>(resolvedDriver){whereClauses}.StorageExecute();");
            }

            builder.AppendLine("        }");
            builder.AppendLine();
        }

        // Like HasAttribute, but also matches when the attribute's class derives
        // (directly or transitively) from a type whose name matches. This lets
        // the generator pick up subclasses of [StorageTable] (e.g. [StorageTable2]).
        private static bool HasAttributeOrSubclass(ISymbol symbol, string attributeTypeName)
        {
            var shortName = attributeTypeName.EndsWith("Attribute", StringComparison.Ordinal)
                ? attributeTypeName.Substring(0, attributeTypeName.Length - "Attribute".Length)
                : attributeTypeName;

            foreach (var attr in symbol.GetAttributes())
            {
                for (var cls = attr.AttributeClass; cls is not null; cls = cls.BaseType)
                {
                    var className = cls.Name;
                    var fullName = cls.ToDisplayString();

                    if (string.Equals(className, attributeTypeName, StringComparison.Ordinal)
                        || string.Equals(className, shortName, StringComparison.Ordinal)
                        || string.Equals(fullName, attributeTypeName, StringComparison.Ordinal)
                        || fullName.EndsWith("." + attributeTypeName, StringComparison.Ordinal)
                        || fullName.EndsWith("." + shortName, StringComparison.Ordinal))
                        return true;
                }
            }

            return false;
        }

        private static bool NeedsAtPrefix(string identifier)
        {
            return SyntaxFacts.GetKeywordKind(identifier) != Microsoft.CodeAnalysis.CSharp.SyntaxKind.None;
        }

        private static string ToMethodSuffix(string memberName, string typeName)
        {
            var trimmed = memberName.TrimStart('@');
            if (trimmed.Length == 0)
                return "Member";

            // For reference-typed members, drop a trailing "Ref" so a field like
            // `executableDocumentRef` reads as `OnExecutableDocument`.
            var isRef = typeName.StartsWith("global::EastFive.IRef<", StringComparison.Ordinal)
                || typeName.StartsWith("global::EastFive.IRefOptional<", StringComparison.Ordinal);
            if (isRef
                && trimmed.Length > 3
                && trimmed.EndsWith("Ref", StringComparison.Ordinal))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 3);
            }

            return char.ToUpperInvariant(trimmed[0]) + trimmed.Substring(1);
        }

        private static string EnsureUnique(string baseName, ISet<string> usedNames)
        {
            if (usedNames.Add(baseName))
                return baseName;

            var suffix = 2;
            while (!usedNames.Add(baseName + suffix))
                suffix++;
            return baseName + suffix;
        }

        private static bool AttributeNameMatches(NameSyntax nameSyntax, string attrShortName)
        {
            var name = nameSyntax switch
            {
                QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
                IdentifierNameSyntax id => id.Identifier.ValueText,
                _ => nameSyntax.ToString(),
            };

            return string.Equals(name, attrShortName, StringComparison.Ordinal)
                || string.Equals(name, attrShortName + "Attribute", StringComparison.Ordinal);
        }

        private static string? ExtractStringFromExpression(ExpressionSyntax expr)
        {
            switch (expr)
            {
                case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression):
                    return literal.Token.ValueText;
                case InvocationExpressionSyntax invocation
                    when invocation.Expression is IdentifierNameSyntax id
                        && string.Equals(id.Identifier.ValueText, "nameof", StringComparison.Ordinal)
                        && invocation.ArgumentList.Arguments.Count == 1:
                    return GetSimpleName(invocation.ArgumentList.Arguments[0].Expression);
                default:
                    return null;
            }
        }

        private static string? GetSimpleName(ExpressionSyntax expr)
        {
            return expr switch
            {
                IdentifierNameSyntax id => id.Identifier.ValueText,
                MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
                _ => null,
            };
        }

        private readonly struct EntityModel
        {
            public EntityModel(
                string namespaceName,
                string entityTypeName,
                string entityName,
                string hintName,
                string typeKeyword,
                bool isPartial,
                ImmutableArray<RawMember> rawMembers)
            {
                NamespaceName = namespaceName;
                EntityTypeName = entityTypeName;
                EntityName = entityName;
                HintName = hintName;
                TypeKeyword = typeKeyword;
                IsPartial = isPartial;
                RawMembers = rawMembers;
            }

            public string NamespaceName { get; }
            public string EntityTypeName { get; }
            public string EntityName { get; }
            public string HintName { get; }
            public string TypeKeyword { get; }
            public bool IsPartial { get; }
            public ImmutableArray<RawMember> RawMembers { get; }
        }

        private readonly struct RenderModel
        {
            public RenderModel(
                string namespaceName,
                string entityTypeName,
                string entityName,
                string typeKeyword,
                bool isPartial,
                ImmutableArray<MemberModel> members,
                ImmutableArray<MemberModel> rowKeyMembers,
                bool needsCompositeGetById)
            {
                NamespaceName = namespaceName;
                EntityTypeName = entityTypeName;
                EntityName = entityName;
                TypeKeyword = typeKeyword;
                IsPartial = isPartial;
                Members = members;
                RowKeyMembers = rowKeyMembers;
                NeedsCompositeGetById = needsCompositeGetById;
            }

            public string NamespaceName { get; }
            public string EntityTypeName { get; }
            public string EntityName { get; }
            public string TypeKeyword { get; }
            public bool IsPartial { get; }
            public ImmutableArray<MemberModel> Members { get; }
            public ImmutableArray<MemberModel> RowKeyMembers { get; }
            public bool NeedsCompositeGetById { get; }
        }

        private readonly struct RawMember
        {
            public RawMember(
                string memberName,
                string escapedMemberName,
                string methodSuffix,
                string typeName,
                bool isStorageQuery,
                bool isIdHashLookup,
                bool isStringHashLookup,
                bool isScopedLookup,
                bool isRowKey,
                string? scopeString,
                string? scopedRow,
                string? scopedPartition)
            {
                MemberName = memberName;
                EscapedMemberName = escapedMemberName;
                MethodSuffix = methodSuffix;
                TypeName = typeName;
                IsStorageQuery = isStorageQuery;
                IsIdHashLookup = isIdHashLookup;
                IsStringHashLookup = isStringHashLookup;
                IsScopedLookup = isScopedLookup;
                IsRowKey = isRowKey;
                ScopeString = scopeString;
                ScopedRow = scopedRow;
                ScopedPartition = scopedPartition;
            }

            public string MemberName { get; }
            public string EscapedMemberName { get; }
            public string MethodSuffix { get; }
            public string TypeName { get; }
            public bool IsStorageQuery { get; }
            public bool IsIdHashLookup { get; }
            public bool IsStringHashLookup { get; }
            public bool IsScopedLookup { get; }
            public bool IsRowKey { get; }
            public string? ScopeString { get; }
            public string? ScopedRow { get; }
            public string? ScopedPartition { get; }
        }

        private readonly struct MemberModel
        {
            public MemberModel(
                string memberName,
                string escapedMemberName,
                string methodSuffix,
                string typeName,
                bool isStorageQuery,
                bool isSingleLookup,
                bool isScopedLookup,
                bool isRowKey,
                ImmutableArray<ScopeParam> scopeParameters)
            {
                MemberName = memberName;
                EscapedMemberName = escapedMemberName;
                MethodSuffix = methodSuffix;
                TypeName = typeName;
                IsStorageQuery = isStorageQuery;
                IsSingleLookup = isSingleLookup;
                IsScopedLookup = isScopedLookup;
                IsRowKey = isRowKey;
                ScopeParameters = scopeParameters;
            }

            public string MemberName { get; }
            public string EscapedMemberName { get; }
            public string MethodSuffix { get; }
            public string TypeName { get; }
            public bool IsStorageQuery { get; }
            public bool IsSingleLookup { get; }
            public bool IsScopedLookup { get; }
            public bool IsRowKey { get; }
            public ImmutableArray<ScopeParam> ScopeParameters { get; }
        }

        private readonly struct ScopeParam
        {
            public ScopeParam(string escapedName, string typeName, string methodSuffix)
            {
                EscapedName = escapedName;
                TypeName = typeName;
                MethodSuffix = methodSuffix;
            }

            public string EscapedName { get; }
            public string TypeName { get; }
            public string MethodSuffix { get; }
        }
    }
}
