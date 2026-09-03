using System;

using EastFive;
using EastFive.Api;
using EastFive.Api.Binding;

namespace EastFive.Api.Tests.Probes;

/// <summary>Referenced-entity stand-in for IRefOptional binding probes.</summary>
public struct ProbeLink : IReferenceable
{
    public Guid id { get; set; }
}

/// <summary>
/// Regression probe for the [BodyText] JSON envelope fix: a JSON request
/// body must reach a [BodyText] parameter as raw text (the envelope
/// previously discarded raw text for application/json, so the method never
/// matched).
/// </summary>
[FunctionViewController(Route = "body-text-probe")]
public static class BodyTextProbe
{
    [HttpPost]
    public static IHttpResponse Receive(
            [BodyText] string payload,
        ContentTypeResponse<string> onReceived) => onReceived(payload);
}

/// <summary>
/// Regression probe for the PropertyBinder PATCH fix: an omitted key must
/// bind as unspecified (previously it bound as specified+empty and cleared
/// the stored ref), while an explicit null must bind as specified+empty.
/// </summary>
[FunctionViewController(Route = "property-patch-probe")]
public static class PropertyPatchProbe
{
    [HttpPatch]
    public static IHttpResponse Update(
            [PropertyOptional(Name = "linked")] Property<IRefOptional<ProbeLink>> linked,
            [PropertyOptional(Name = "name")] Property<string> name,
        ContentTypeResponse<string> onResult)
    {
        var linkedState = !linked.specified ? "unspecified"
            : linked.value is null ? "specified:null-ref"
            : linked.value.HasValue ? $"specified:{linked.value.id}"
            : "specified:empty";
        return onResult(linkedState);
    }
}

/// <summary>
/// Regression probe for object-target binding: [Body] parameters typed
/// <c>IDictionary&lt;string, object&gt;</c> / <c>object</c> must bind from any
/// JSON native. Before ObjectBinder, nothing claimed <c>typeof(object)</c>, so
/// dictionary values fell to PocoBinder — which supplies only onNull/onObject —
/// and a JSON string 400'd with WrongSourceType("object", "String").
/// </summary>
[FunctionViewController(Route = "object-binding-probe")]
public static class ObjectBindingProbe
{
    public static bool LastValuesSpecified;
    public static System.Collections.Generic.IDictionary<string, object>? LastValues;
    public static object? LastSingle;

    [HttpPost]
    public static IHttpResponse Receive(
            [Body(Name = "values")] Property<System.Collections.Generic.IDictionary<string, object>> values,
            [Body(Name = "single")] object single,
        ContentTypeResponse<string> onReceived)
    {
        LastValuesSpecified = values.specified;
        LastValues = values.value;
        LastSingle = single;
        return onReceived("bound");
    }
}

/// <summary>
/// Simple controller the TestHarnessGenerator can fully model — proves the
/// parameterized generator emits wrappers from build-property configuration.
/// </summary>
[FunctionViewController(Route = "generator-probe")]
public static class GeneratorProbe
{
    [HttpGet]
    public static IHttpResponse Fetch(
            [QueryParameter(Name = "who")] string who,
        ContentTypeResponse<string> onFound,
        NotFoundResponse onNotFound)
    {
        if (string.IsNullOrEmpty(who))
            return onNotFound();
        return onFound($"hello {who}");
    }
}
