using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using Newtonsoft.Json;

using EastFive;
using EastFive.Api.Meta.Postman.Resources.Collection;

namespace EastFive.Api.Meta.Flows.Scripted
{
    /// <summary>
    /// Walks a scripted-flow expression — a single
    /// <c>Expression&lt;Func&lt;TApi, FlowNode&gt;&gt;</c> built from nested success-branch
    /// continuations — and emits a Postman <see cref="Collection"/>. Nothing in the flow is
    /// executed; only the captured expression tree is read.
    ///
    /// <para>Each controller-method call is one step. A step's success/threading branch is the
    /// response delegate whose single parameter type equals the resource type; its body holds
    /// the next step, and its lambda parameter is this step's output. Member access on an
    /// earlier step's output parameter (e.g. <c>doc.signableDocumentRef</c>) becomes a Postman
    /// environment capture on the producing step and a <c>{{var}}</c> reference on the
    /// consumer.</para>
    /// </summary>
    public static class FlowScriptReader
    {
        #region Public entry points

        /// <summary>
        /// Reflects the single <see cref="ScriptedFlowAttribute"/>-marked member on
        /// <paramref name="flowHostType"/>, reads its flow expression, and emits the collection.
        /// </summary>
        public static Collection FromHost(Type flowHostType)
        {
            var (attr, expression) = ResolveFlowMember(flowHostType);
            return FromExpression(expression, attr.Name, attr.Version);
        }

        /// <summary>
        /// Emits a Postman collection from an already-resolved flow expression.
        /// </summary>
        public static Collection FromExpression(LambdaExpression flow, string name, string version)
        {
            var steps = ParseSteps(flow);
            ResolveReferences(steps);

            var info = new Info
            {
                _postman_id = Guid.NewGuid(),
                name = name,
                description = version.HasBlackSpace() ? $"**Version**: {version}" : null,
                schema = "https://schema.getpostman.com/json/collection/v2.1.0/collection.json",
            };

            var items = steps
                .GroupBy(step => step.Scope ?? string.Empty)
                .SelectMany(scopeGrp =>
                {
                    var stepItems = scopeGrp.Select(BuildItem).ToArray();
                    if (string.IsNullOrWhiteSpace(scopeGrp.Key))
                        return stepItems;
                    return new[]
                    {
                        new Item
                        {
                            name = scopeGrp.Key,
                            item = stepItems,
                        },
                    };
                })
                .ToArray();

            return new Collection
            {
                info = info,
                item = items,
            };
        }

        #endregion

        #region Flow discovery

        /// <summary>
        /// Scans loaded assemblies for every static <see cref="ScriptedFlowAttribute"/>-marked
        /// member. The attribute is read eagerly (cheap); the flow expression is resolved lazily
        /// via the returned delegate so unrelated flows are never invoked.
        /// </summary>
        private static IEnumerable<(ScriptedFlowAttribute attr, Func<LambdaExpression> resolve)> EnumerateFlowMembers()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }
                catch
                {
                    continue;
                }

                foreach (var type in types)
                {
                    IEnumerable<(ScriptedFlowAttribute attr, Func<LambdaExpression> resolve)> members;
                    try
                    {
                        members = ReadFlowMembers(type);
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (var member in members)
                        yield return member;
                }
            }
        }

        /// <summary>
        /// Reads the scripted-flow members declared directly on <paramref name="type"/>. Foreign
        /// attributes are never instantiated — presence is checked via metadata
        /// (<see cref="MemberInfo.CustomAttributes"/>) so unrelated attribute constructors cannot
        /// throw during discovery.
        /// </summary>
        private static List<(ScriptedFlowAttribute attr, Func<LambdaExpression> resolve)> ReadFlowMembers(Type type)
        {
            const BindingFlags staticPublic = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;
            var found = new List<(ScriptedFlowAttribute, Func<LambdaExpression>)>();

            foreach (var prop in type.GetProperties(staticPublic))
            {
                if (!HasScriptedFlowMetadata(prop))
                    continue;
                var attr = prop.GetCustomAttribute<ScriptedFlowAttribute>();
                if (attr == null)
                    continue;
                var captured = prop;
                found.Add((attr, () => captured.GetValue(null) as LambdaExpression));
            }

            foreach (var method in type.GetMethods(staticPublic))
            {
                if (method.GetParameters().Length != 0)
                    continue;
                if (!HasScriptedFlowMetadata(method))
                    continue;
                var attr = method.GetCustomAttribute<ScriptedFlowAttribute>();
                if (attr == null)
                    continue;
                var captured = method;
                found.Add((attr, () => captured.Invoke(null, Array.Empty<object>()) as LambdaExpression));
            }

            return found;
        }

        private static bool HasScriptedFlowMetadata(MemberInfo member)
            => member.CustomAttributes.Any(data => data.AttributeType == typeof(ScriptedFlowAttribute));

        /// <summary>
        /// Returns the (name, version) of every discoverable scripted flow.
        /// </summary>
        public static IEnumerable<(string Name, string Version)> EnumerateFlows()
            => EnumerateFlowMembers()
                .Select(member => (member.attr.Name, member.attr.Version))
                .Distinct();

        /// <summary>
        /// Builds the Postman collection for the scripted flow named <paramref name="flowName"/>,
        /// if one is defined. Returns <c>false</c> when no scripted flow matches, letting callers
        /// fall back to the legacy attribute-driven path.
        /// </summary>
        public static bool TryGetCollection(string flowName, out Collection collection)
        {
            foreach (var (attr, resolve) in EnumerateFlowMembers())
            {
                if (!string.Equals(attr.Name, flowName, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (resolve() is not LambdaExpression lambda)
                    continue;
                collection = FromExpression(lambda, attr.Name, attr.Version);
                return true;
            }

            collection = null;
            return false;
        }

        private static (ScriptedFlowAttribute, LambdaExpression) ResolveFlowMember(Type flowHostType)
        {
            foreach (var prop in flowHostType.GetProperties(BindingFlags.Public | BindingFlags.Static))
            {
                var attr = prop.GetCustomAttribute<ScriptedFlowAttribute>();
                if (attr == null)
                    continue;
                if (prop.GetValue(null) is LambdaExpression lambda)
                    return (attr, lambda);
            }

            foreach (var method in flowHostType.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                var attr = method.GetCustomAttribute<ScriptedFlowAttribute>();
                if (attr == null)
                    continue;
                if (method.GetParameters().Length != 0)
                    continue;
                if (method.Invoke(null, Array.Empty<object>()) is LambdaExpression lambda)
                    return (attr, lambda);
            }

            throw new ArgumentException(
                $"Type '{flowHostType.FullName}' has no static [ScriptedFlow] member returning a LambdaExpression.");
        }

        #endregion

        #region Parse

        /// <summary>A single controller-method call within the flow.</summary>
        private sealed class Step
        {
            public MethodCallExpression Call;
            public MethodInfo ControllerMethod;
            public Type ResourceType;
            public string Scope;
            public string StepName;
            public Expression BodyInit;

            // Surfaced scalar arguments, split by binding slot on the controller parameter.
            public List<(string key, Expression value)> Queries = new();
            public List<(string key, Expression value)> BodyFields = new();
            public ParameterExpression OutputParam;

            // member name -> environment variable name, for values consumed by later steps.
            public Dictionary<string, string> Captures = new(StringComparer.Ordinal);

            // shared across steps: maps each step's output parameter to the producing step.
            public Dictionary<ParameterExpression, Step> ProducerLookup;
        }

        private static List<Step> ParseSteps(LambdaExpression flow)
        {
            var steps = new List<Step>();
            var producers = new Dictionary<ParameterExpression, Step>();
            MethodCallExpression terminalCapture = null;

            var current = Unwrap(flow.Body);
            while (current is MethodCallExpression call)
            {
                if (IsFlowHelper(call, nameof(Flow.Capture)))
                {
                    terminalCapture = call;
                    break;
                }

                var flowMethod = call.Method.GetCustomAttribute<FlowMethodAttribute>();
                if (flowMethod == null)
                    throw new ArgumentException(
                        $"Flow step '{call.Method.Name}' is missing [FlowMethod]; only generated flow extensions may be called.");

                var step = new Step
                {
                    Call = call,
                    ResourceType = flowMethod.ResourceType,
                    ControllerMethod = ResolveControllerMethod(flowMethod),
                };

                var parameters = call.Method.GetParameters();

                // Argument 0 is the source chain: api.Resource.InScope(..).Named(..)
                ApplySourceAnnotations(call.Arguments[0], step);

                Expression nextCall = null;
                for (var i = 1; i < parameters.Length; i++)
                {
                    var parameter = parameters[i];
                    var argument = call.Arguments[i];

                    if (IsSuccessBranch(parameter, step.ResourceType))
                    {
                        if (Unwrap(argument) is LambdaExpression continuation)
                        {
                            step.OutputParam = continuation.Parameters[0];
                            nextCall = Unwrap(continuation.Body);
                        }
                        continue;
                    }

                    if (IsResponseDelegate(parameter.ParameterType))
                        continue; // non-success branch: ignored for emission

                    if (parameter.ParameterType == step.ResourceType)
                    {
                        step.BodyInit = Unwrap(argument);
                        continue;
                    }

                    if (!IsOmittedDefault(argument, parameter))
                    {
                        var (isBody, wireName) = ClassifyScalar(step.ControllerMethod, parameter.Name);
                        var entry = (wireName, Unwrap(argument));
                        if (isBody)
                            step.BodyFields.Add(entry);
                        else
                            step.Queries.Add(entry);
                    }
                }

                steps.Add(step);
                if (step.OutputParam != null)
                    producers[step.OutputParam] = step;

                current = nextCall;
            }

            // Stash producer lookup on each step for reference resolution.
            foreach (var step in steps)
                step.ProducerLookup = producers;

            // A terminal Flow.Capture(...) exports step outputs that no later request
            // references; scanning it registers each value on its producing step.
            if (terminalCapture != null)
            {
                var captureConsumer = new Step { ProducerLookup = producers };
                ScanReferences(terminalCapture, captureConsumer);
            }

            return steps;
        }

        private static void ApplySourceAnnotations(Expression source, Step step)
        {
            var current = source;
            while (current is MethodCallExpression call
                && call.Method.DeclaringType == typeof(FlowAnnotations))
            {
                var value = EvaluateString(call.Arguments[1]);
                if (call.Method.Name == nameof(FlowAnnotations.InScope))
                    step.Scope ??= value;
                else if (call.Method.Name == nameof(FlowAnnotations.Named))
                    step.StepName ??= value;
                current = call.Arguments[0];
            }
        }

        #endregion

        #region Reference resolution

        private static void ResolveReferences(List<Step> steps)
        {
            foreach (var step in steps)
            {
                if (step.BodyInit != null)
                    ScanReferences(step.BodyInit, step);
                foreach (var (_, value) in step.Queries)
                    ScanReferences(value, step);
                foreach (var (_, value) in step.BodyFields)
                    ScanReferences(value, step);
            }
        }

        private static void ScanReferences(Expression expression, Step consumer)
        {
            switch (expression)
            {
                case MemberExpression member when TryGetProducerMember(member, consumer, out _, out _):
                    // recorded below by ReferenceVariableName
                    ReferenceVariableName(member, consumer);
                    break;
                case MemberExpression member:
                    ScanReferences(member.Expression, consumer);
                    break;
                case MemberInitExpression init:
                    foreach (var binding in init.Bindings.OfType<MemberAssignment>())
                        ScanReferences(binding.Expression, consumer);
                    break;
                case NewArrayExpression array:
                    foreach (var element in array.Expressions)
                        ScanReferences(element, consumer);
                    break;
                case MethodCallExpression call:
                    foreach (var argument in call.Arguments)
                        ScanReferences(argument, consumer);
                    if (call.Object != null)
                        ScanReferences(call.Object, consumer);
                    break;
                case UnaryExpression unary:
                    ScanReferences(unary.Operand, consumer);
                    break;
            }
        }

        /// <summary>
        /// Resolves a cross-step reference, registering the capture on the producing step and
        /// returning the deterministic Postman variable name.
        /// </summary>
        private static string ReferenceVariableName(MemberExpression member, Step consumer)
        {
            if (!TryGetProducerMember(member, consumer, out var producer, out var referencedMember))
                return null;
            var variableName = referencedMember.Name;
            producer.Captures[referencedMember.Name] = variableName;
            return variableName;
        }

        private static bool TryGetProducerMember(MemberExpression member, Step consumer,
            out Step producer, out MemberInfo referencedMember)
        {
            producer = null;
            referencedMember = null;
            if (member.Expression is not ParameterExpression parameterExpression)
                return false;
            if (!consumer.ProducerLookup.TryGetValue(parameterExpression, out producer))
                return false;
            referencedMember = member.Member;
            return true;
        }

        #endregion

        #region Emit

        private static Item BuildItem(Step step)
        {
            var item = new Item
            {
                name = step.StepName ?? DefaultStepName(step),
                request = BuildRequest(step),
            };

            var scriptLines = BuildCaptureScript(step);
            if (scriptLines.Length > 0)
            {
                item._event = new[]
                {
                    new Event
                    {
                        listen = "test",
                        script = new Script
                        {
                            type = "text/javascript",
                            exec = scriptLines,
                        },
                    },
                };
            }

            return item;
        }

        private static Request BuildRequest(Step step)
        {
            var ns = ControllerNamespace(step.ResourceType);
            var route = ControllerRoute(step.ResourceType);

            var query = step.Queries
                .Select(scalar => new QueryItem
                {
                    key = scalar.key,
                    value = RenderInline(scalar.value, step),
                })
                .ToArray();

            var url = new Url
            {
                raw = $"{Url.VariableHostName}/{ns}/{route}",
                host = new[] { Url.VariableHostName },
                path = new[] { ns, route },
                query = query.Length == 0 ? null : query,
            };

            var rawBody = step.BodyInit != null
                ? RenderJson(step.BodyInit, step)
                : step.BodyFields.Count > 0
                    ? RenderBodyFields(step)
                    : null;

            var body = rawBody == null ? null : new Body
            {
                mode = "raw",
                options = new Options { raw = new Raw { language = "json" } },
                raw = rawBody,
            };

            return new Request
            {
                method = ControllerVerb(step.ControllerMethod),
                header = Array.Empty<Header>(),
                url = url,
                body = body,
            };
        }

        private static string[] BuildCaptureScript(Step step)
        {
            if (step.Captures.Count == 0)
                return Array.Empty<string>();

            var lines = new List<string> { "var response = pm.response.json();" };
            foreach (var capture in step.Captures.OrderBy(c => c.Key, StringComparer.Ordinal))
            {
                var jsonName = JsonName(step.ResourceType, capture.Key);
                lines.Add($"pm.environment.set(\"{capture.Value}\", response.{jsonName});");
            }
            return lines.ToArray();
        }

        #endregion

        #region JSON body rendering

        private static string RenderJson(Expression bodyInit, Step step)
        {
            var builder = new StringBuilder();
            using var stringWriter = new StringWriter(builder);
            using var writer = new JsonTextWriter(stringWriter) { Formatting = Formatting.Indented };
            WriteValue(writer, bodyInit, step);
            return builder.ToString();
        }

        /// <summary>
        /// Assembles a JSON object body from individual <c>[Body]</c>-bound scalar arguments
        /// (the field name is the controller parameter's wire name).
        /// </summary>
        private static string RenderBodyFields(Step step)
        {
            var builder = new StringBuilder();
            using var stringWriter = new StringWriter(builder);
            using var writer = new JsonTextWriter(stringWriter) { Formatting = Formatting.Indented };
            writer.WriteStartObject();
            foreach (var (key, value) in step.BodyFields)
            {
                writer.WritePropertyName(key);
                WriteValue(writer, value, step);
            }
            writer.WriteEndObject();
            return builder.ToString();
        }

        private static void WriteValue(JsonWriter writer, Expression expression, Step step)
        {
            expression = Unwrap(expression);

            switch (expression)
            {
                case MethodCallExpression call when IsFlowHelper(call, nameof(Flow.NewId)):
                    writer.WriteValue("{{$guid}}");
                    return;

                case MethodCallExpression call when IsFlowHelper(call, nameof(Flow.Input)):
                    writer.WriteValue($"{{{{{EvaluateString(call.Arguments[0])}}}}}");
                    return;

                case MethodCallExpression call when IsRefsHelper(call, out var single):
                    // IRefs<T> built from a single IRef<T> (e.g. .AsRefs()) renders as a
                    // one-element array so the consumer can reference an earlier step's id.
                    writer.WriteStartArray();
                    WriteValue(writer, single, step);
                    writer.WriteEndArray();
                    return;

                case MemberExpression member when TryGetProducerMember(member, step, out _, out _):
                    writer.WriteValue($"{{{{{ReferenceVariableName(member, step)}}}}}");
                    return;

                case MemberInitExpression init:
                    writer.WriteStartObject();
                    foreach (var binding in init.Bindings.OfType<MemberAssignment>())
                    {
                        writer.WritePropertyName(JsonName(binding.Member));
                        WriteValue(writer, binding.Expression, step);
                    }
                    writer.WriteEndObject();
                    return;

                case NewArrayExpression array:
                    writer.WriteStartArray();
                    foreach (var element in array.Expressions)
                        WriteValue(writer, element, step);
                    writer.WriteEndArray();
                    return;

                default:
                    WriteScalar(writer, Evaluate(expression));
                    return;
            }
        }

        private static void WriteScalar(JsonWriter writer, object value)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }
            if (value is Enum enumValue)
            {
                writer.WriteValue(enumValue.ToString());
                return;
            }
            writer.WriteValue(value);
        }

        private static string RenderInline(Expression expression, Step step)
        {
            expression = Unwrap(expression);
            switch (expression)
            {
                case MethodCallExpression call when IsFlowHelper(call, nameof(Flow.NewId)):
                    return "{{$guid}}";
                case MethodCallExpression call when IsFlowHelper(call, nameof(Flow.Input)):
                    return $"{{{{{EvaluateString(call.Arguments[0])}}}}}";
                case MemberExpression member when TryGetProducerMember(member, step, out _, out _):
                    return $"{{{{{ReferenceVariableName(member, step)}}}}}";
                default:
                    return Evaluate(expression)?.ToString() ?? string.Empty;
            }
        }

        #endregion

        #region Controller reflection

        private static MethodInfo ResolveControllerMethod(FlowMethodAttribute flowMethod)
        {
            var candidates = flowMethod.ResourceType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(m => m.Name == flowMethod.MethodName)
                .ToArray();

            if (candidates.Length == 1)
                return candidates[0];

            if (flowMethod.ParameterNames.Length > 0)
            {
                var matched = candidates.FirstOrDefault(m =>
                    m.GetParameters().Select(p => p.Name).SequenceEqual(flowMethod.ParameterNames));
                if (matched != null)
                    return matched;
            }

            if (candidates.Length == 0)
                throw new ArgumentException(
                    $"No method '{flowMethod.MethodName}' on '{flowMethod.ResourceType.FullName}'.");

            return candidates[0];
        }

        private static string ControllerVerb(MethodInfo controllerMethod)
        {
            foreach (var attribute in controllerMethod.GetCustomAttributes())
            {
                for (var type = attribute.GetType(); type != null; type = type.BaseType)
                {
                    if (type.Name != "HttpVerbAttribute")
                        continue;
                    var methodProperty = type.GetProperty("Method");
                    if (methodProperty?.GetValue(attribute) is string verb)
                        return verb;
                }
            }
            return "GET";
        }

        private static string ControllerNamespace(Type resourceType)
        {
            var fvc = resourceType.GetCustomAttribute<FunctionViewControllerAttribute>();
            return fvc?.Namespace ?? "api";
        }

        private static string ControllerRoute(Type resourceType)
        {
            var fvc = resourceType.GetCustomAttribute<FunctionViewControllerAttribute>();
            if (fvc != null && fvc.Route.HasBlackSpace())
                return fvc.Route;
            return resourceType.Name;
        }

        private static string DefaultStepName(Step step)
        {
            var route = ControllerRoute(step.ResourceType);
            return ControllerVerb(step.ControllerMethod).ToUpperInvariant() switch
            {
                "POST" => $"Create {route}",
                "PUT" => $"Replace {route}",
                "PATCH" => $"Update {route}",
                "DELETE" => $"Delete {route}",
                _ => $"Get {route}",
            };
        }

        #endregion

        #region JSON names

        private static string JsonName(Type resourceType, string memberName)
        {
            var member = resourceType
                .GetMember(memberName, BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault();
            return member != null ? JsonName(member) : memberName;
        }

        private static string JsonName(MemberInfo member)
        {
            var jsonProperty = member.GetCustomAttribute<JsonPropertyAttribute>();
            if (jsonProperty != null && jsonProperty.PropertyName.HasBlackSpace())
                return jsonProperty.PropertyName;

            foreach (var attribute in member.GetCustomAttributes())
            {
                if (attribute.GetType().Name != "ApiPropertyAttribute")
                    continue;
                var propertyNameProperty = attribute.GetType().GetProperty("PropertyName");
                if (propertyNameProperty?.GetValue(attribute) is string name && name.HasBlackSpace())
                    return name;
            }

            return member.Name;
        }

        #endregion

        #region Expression helpers

        private static Expression Unwrap(Expression expression)
        {
            while (true)
            {
                switch (expression)
                {
                    case UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked or ExpressionType.Quote } unary:
                        expression = unary.Operand;
                        continue;
                    case MethodCallExpression call when call.Method.DeclaringType == typeof(FlowAnnotations)
                            && call.Method.Name == nameof(FlowAnnotations.PostmanDescription):
                        expression = call.Arguments[0];
                        continue;
                    default:
                        return expression;
                }
            }
        }

        private static bool IsFlowHelper(MethodCallExpression call, string name)
            => call.Method.DeclaringType == typeof(Flow) && call.Method.Name == name;

        /// <summary>
        /// Determines whether a surfaced scalar argument binds to the request body or the query
        /// string, and resolves its wire name, by inspecting the matching controller parameter's
        /// V3 binding attribute (<c>[Body]</c>/<c>[Query]</c>/...). Falls back to a query slot
        /// keyed by the parameter name when no binding attribute is present.
        /// </summary>
        private static (bool isBody, string wireName) ClassifyScalar(MethodInfo controllerMethod, string paramName)
        {
            var controllerParam = controllerMethod?
                .GetParameters()
                .FirstOrDefault(p => p.Name == paramName);
            if (controllerParam == null)
                return (false, paramName);

            foreach (var attribute in controllerParam.GetCustomAttributes())
            {
                if (attribute is Binding.IProvideMemberScope scoped)
                {
                    var isBody = scoped.MemberScope?.Name == "RequestBody";
                    return (isBody, ReadAttributeName(attribute) ?? paramName);
                }

                var typeName = attribute.GetType().Name;
                if (typeName == "PropertyAttribute" || typeName == "PropertyOptionalAttribute")
                    return (true, ReadAttributeName(attribute) ?? paramName);

                // Storage loader attributes (EastFive.Azure) bind an id from the query/route.
                if (typeName == "StorageEntityFromQueryIdAttribute"
                    || typeName == "StorageEntityFromQueryParamAttribute"
                    || typeName == "StorageEntityFromRouteAttribute")
                    return (false, ReadAttributeName(attribute) ?? paramName);
            }

            return (false, paramName);
        }

        private static string ReadAttributeName(Attribute attribute)
        {
            var nameProperty = attribute.GetType().GetProperty("Name");
            if (nameProperty?.GetValue(attribute) is string name && name.HasBlackSpace())
                return name;
            return null;
        }

        /// <summary>
        /// Recognizes the <c>IRef&lt;T&gt;.AsRefs()</c> helper (an extension method whose single
        /// argument is the source ref), surfacing that ref so an <c>IRefs&lt;T&gt;</c> assignment
        /// renders as a one-element array.
        /// </summary>
        private static bool IsRefsHelper(MethodCallExpression call, out Expression single)
        {
            single = null;
            if (call.Method.Name != "AsRefs" || call.Arguments.Count != 1)
                return false;
            single = call.Arguments[0];
            return true;
        }

        private static bool IsSuccessBranch(ParameterInfo parameter, Type resourceType)
        {
            var invoke = parameter.ParameterType.GetMethod("Invoke");
            if (invoke == null || invoke.ReturnType != typeof(FlowNode))
                return false;
            var delegateParameters = invoke.GetParameters();
            return delegateParameters.Length >= 1 && delegateParameters[0].ParameterType == resourceType;
        }

        private static bool IsResponseDelegate(Type type)
        {
            var invoke = type.GetMethod("Invoke");
            return invoke != null && invoke.ReturnType == typeof(FlowNode);
        }

        /// <summary>
        /// True when an argument carries no caller-supplied value: an explicit <c>null</c>, or a
        /// constant equal to the optional parameter's default (the compiler materializes omitted
        /// optional arguments as constants), so unspecified body/query fields are not emitted.
        /// </summary>
        private static bool IsOmittedDefault(Expression argument, ParameterInfo parameter)
        {
            if (argument is not ConstantExpression constant)
                return false;
            if (constant.Value == null)
                return true;
            return parameter.HasDefaultValue && Equals(constant.Value, parameter.DefaultValue);
        }

        private static string EvaluateString(Expression expression)
            => Evaluate(expression) as string;

        private static object Evaluate(Expression expression)
        {
            if (expression is ConstantExpression constant)
                return constant.Value;
            var lambda = Expression.Lambda(Expression.Convert(expression, typeof(object)));
            return lambda.Compile().DynamicInvoke();
        }

        #endregion
    }
}
