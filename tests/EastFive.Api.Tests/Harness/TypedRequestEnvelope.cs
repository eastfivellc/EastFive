using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using EastFive.Api;
using EastFive.Api.Binding;
using EastFive.Reflection;
using EastFive;

namespace EastFive.Api.Tests.Harness;

/// <summary>
/// Test-only <see cref="IRequestEnvelope"/> backed by pre-typed values keyed
/// by parameter name. Bypasses JSON serialization so harness callers can hand
/// the dispatcher already-constructed CLR objects instead of round-tripping
/// through Newtonsoft.
/// </summary>
public sealed class TypedRequestEnvelope : IRequestEnvelope, IRequestEnvelopeBody, IParameterOverrideSource
{
    private readonly IReadOnlyDictionary<string, object> body;
    private readonly IReadOnlyDictionary<string, object> query;
    private readonly IReadOnlyDictionary<string, object> overrides;

    public TypedRequestEnvelope(
        IReadOnlyDictionary<string, object>? body = null,
        IReadOnlyDictionary<string, object>? query = null,
        IReadOnlyDictionary<string, object>? overrides = null)
    {
        this.body = body ?? new Dictionary<string, object>(StringComparer.Ordinal);
        this.query = query ?? new Dictionary<string, object>(StringComparer.Ordinal);
        this.overrides = overrides ?? new Dictionary<string, object>(StringComparer.Ordinal);
    }

    bool IParameterOverrideSource.TryGetParameterOverride(string parameterName, out object value)
        => this.overrides.TryGetValue(parameterName, out value!);

    bool IRequestEnvelopeBody.TryGetBody<TBody>(out TBody value)
    {
        if (this.body.Count == 0)
        {
            value = default!;
            return false;
        }

        if (this.body.Count == 1)
        {
            var typed = this.body.Values.First();
            if (typed is null)
            {
                value = default!;
                return false;
            }

            if (typed is TBody already)
            {
                value = already;
                return true;
            }

            if (typeof(TBody) == typeof(JToken) || typeof(TBody) == typeof(JContainer))
            {
                value = (TBody)(object)ToWireToken(typed);
                return true;
            }

            if (typeof(TBody) == typeof(string))
            {
                value = (TBody)(object)ToWireToken(typed).ToString(Newtonsoft.Json.Formatting.None);
                return true;
            }

            value = default!;
            return false;
        }

        if (typeof(TBody) == typeof(JToken) || typeof(TBody) == typeof(JContainer))
        {
            var obj = new JObject();
            foreach (var kvp in this.body)
                obj[kvp.Key] = ToWireToken(kvp.Value);
            value = (TBody)(object)obj;
            return true;
        }

        if (typeof(TBody) == typeof(string))
        {
            var obj = new JObject();
            foreach (var kvp in this.body)
                obj[kvp.Key] = ToWireToken(kvp.Value);
            value = (TBody)(object)obj.ToString(Newtonsoft.Json.Formatting.None);
            return true;
        }

        value = default!;
        return false;
    }

    public bool TryFulfill(BindingRequirement requirement, out ExtractAsyncDelegate extract)
    {
        foreach (var kvp in requirement.Converters)
        {
            var rawType = kvp.Key;
            var converter = kvp.Value;

            if (!TryProduceRawForRequirement(requirement, rawType, out var raw))
                continue;

            extract = (httpApp, request) =>
                Task.FromResult(converter.Convert(raw, requirement.Parameter, httpApp, request));
            return true;
        }
        extract = null!;
        return false;
    }

    private bool TryProduceRawForRequirement(BindingRequirement requirement, Type rawType, out object raw)
    {
        var source = requirement.Source;
        var path = requirement.Path ?? string.Empty;

        // Per-field body binders ask for the body as a whole JContainer and
        // extract their own key; absent keys must fail the requirement so the
        // binder applies optional defaults.
        if ((source & BindingSource.Body) != 0
            && !string.IsNullOrEmpty(path)
            && (rawType == typeof(JContainer) || rawType == typeof(JToken)))
        {
            if (!this.body.ContainsKey(path))
            {
                raw = null!;
                return false;
            }
            raw = BuildBodyContainer();
            return true;
        }

        if (!TryGetTyped(requirement, out var typedValue))
        {
            raw = null!;
            return false;
        }
        return TryProduceRaw(typedValue, rawType, out raw);
    }

    private JObject BuildBodyContainer()
    {
        var obj = new JObject();
        foreach (var kvp in this.body)
            obj[kvp.Key] = ToWireToken(kvp.Value);
        return obj;
    }

    internal string DebugBodyJson()
    {
        if (this.body.Count == 1)
            return ToWireToken(this.body.Values.First()).ToString(Newtonsoft.Json.Formatting.None);
        return BuildBodyContainer().ToString(Newtonsoft.Json.Formatting.None);
    }

    private static JToken ToWireToken(object value)
    {
        if (value is null)
            return JValue.CreateNull();
        // Emit guid strings (not JTokenType.Guid) to match the JSON a real
        // client sends and to exercise the production string-bind paths.
        var valueType = value.GetType();
        if (valueType.IsSubClassOfGeneric(typeof(EastFive.IRef<>)))
            return new JValue(((EastFive.IReferenceable)value).id.ToString());
        if (valueType.IsSubClassOfGeneric(typeof(EastFive.IRefOptional<>)))
        {
            var referenceableOptional = (EastFive.IReferenceableOptional)value;
            return referenceableOptional.HasValue
                ? new JValue(referenceableOptional.id!.Value.ToString())
                : JValue.CreateNull();
        }
        if (valueType.IsSubClassOfGeneric(typeof(EastFive.IRefs<>)) || value is EastFive.IReferences)
            return new JArray(((EastFive.IReferences)value).ids.Select(id => (object)id.ToString()).ToArray());
        return JToken.FromObject(value, WireSerializer);
    }

    private static readonly Newtonsoft.Json.JsonSerializer WireSerializer =
        Newtonsoft.Json.JsonSerializer.Create(new Newtonsoft.Json.JsonSerializerSettings
        {
            Converters = { new EastFive.Serialization.Json.Converter() },
        });

    private bool TryGetTyped(BindingRequirement requirement, out object value)
    {
        var source = requirement.Source;
        var path = requirement.Path ?? string.Empty;

        if ((source & (BindingSource.Query | BindingSource.Path)) != 0
            && !string.IsNullOrEmpty(path)
            && this.query.TryGetValue(path, out value!))
            return true;

        if ((source & BindingSource.Body) != 0)
        {
            var bodyKey = string.IsNullOrEmpty(path)
                ? requirement.Parameter.Name
                : path;
            if (bodyKey != null && this.body.TryGetValue(bodyKey, out value!))
                return true;
        }

        value = null!;
        return false;
    }

    private static bool TryProduceRaw(object typedValue, Type rawType, out object raw)
    {
        if (typedValue != null && rawType.IsInstanceOfType(typedValue))
        {
            raw = typedValue;
            return true;
        }

        if (rawType == typeof(JContainer) || rawType == typeof(JToken))
        {
            if (typedValue == null)
            {
                raw = null!;
                return false;
            }
            raw = JToken.FromObject(typedValue);
            return true;
        }

        if (rawType == typeof(string))
        {
            raw = typedValue?.ToString() ?? string.Empty;
            return true;
        }

        raw = null!;
        return false;
    }
}
