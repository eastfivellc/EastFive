using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using EastFive.Api.Binding.Scopes;

namespace EastFive.Api.Binding
{
    /// <summary>
    /// V3 attribute that locates a method parameter inside the request body.
    /// Path defaults to the parameter's name; <see cref="Name"/> overrides
    /// (or the empty string to bind the entire body to the parameter).
    /// <para>
    /// Selection succeeds when the body exists in any supported raw shape.
    /// Whether the path resolves to a present value is decided at bind time —
    /// the underlying <see cref="EastFive.Serialization.Binding.IBindingSource"/>
    /// reports <see cref="EastFive.Serialization.Binding.NotPresent"/> which the
    /// bind phase translates into the parameter's C# default value when present,
    /// otherwise into a bind failure.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class BodyAttribute : Attribute, IBindFromRequest, IProvideMemberScope
    {
        /// <summary>Path inside the body. Defaults to the parameter name.</summary>
        public string Name { get; set; }

        Type IProvideMemberScope.MemberScope => typeof(RequestBody);

        public bool TrySelectSource(IRequestEnvelopeV3 envelope, ParameterInfo parameter,
            out BindCall call)
        {
            if (!EnvelopeBodyAccessor.TryGetBodyRoot(envelope, out var root, out var rawBody))
            {
                if (parameter.HasDefaultValue) { call = BindCalls.NotPresent; return true; }
                call = null;
                return false;
            }
            call = BindCalls.FromSource(root, Name ?? parameter.Name);
            return true;
        }
    }

    /// <summary>
    /// V3 attribute that binds the parameter to the <b>entire</b> body — the
    /// canonical "deserialize the request payload into this POCO" case.
    /// Equivalent to <c>[Body(Name = "")]</c> but expresses intent more clearly
    /// at the call site. Selection misses if no body is present.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class ResourceAttribute : Attribute, IBindFromRequest, IProvideMemberScope
    {
        Type IProvideMemberScope.MemberScope => typeof(RequestBody);

        public bool TrySelectSource(IRequestEnvelopeV3 envelope, ParameterInfo parameter,
            out BindCall call)
        {
            if (EnvelopeBodyAccessor.TryGetBodyRoot(envelope, out var root, out var rawBody))
            {
                call = BindCalls.FromSource(root, string.Empty);
                return true;
            }
            if (parameter.HasDefaultValue) { call = BindCalls.NotPresent; return true; }
            call = null;
            return false;
        }
    }

    /// <summary>
    /// V3 attribute that binds a parameter to the <b>raw request body decoded as
    /// text</b>. Unlike <see cref="BodyAttribute"/> / <see cref="ResourceAttribute"/>
    /// (which parse the body into a structured root for property / POCO binding),
    /// this hands the undecoded payload to a <c>string</c> parameter — for endpoints
    /// that parse the body themselves (XML webhooks, signature-validated payloads,
    /// etc.). The body is read in whatever raw shape the envelope produced
    /// (<c>string</c> directly, or <c>byte[]</c> decoded as UTF-8). Selection misses
    /// if no body is present unless the parameter has a C# default value.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class BodyTextAttribute : Attribute, IBindFromRequest, IProvideMemberScope
    {
        Type IProvideMemberScope.MemberScope => typeof(RequestBody);

        public bool TrySelectSource(IRequestEnvelopeV3 envelope, ParameterInfo parameter,
            out BindCall call)
        {
            if (envelope.TryGetBody<string>(out var text) && text is not null)
            {
                call = BindCalls.Scalar(text);
                return true;
            }
            if (envelope.TryGetBody<byte[]>(out var bytes) && bytes is not null)
            {
                call = BindCalls.Scalar(System.Text.Encoding.UTF8.GetString(bytes));
                return true;
            }
            if (parameter.HasDefaultValue) { call = BindCalls.NotPresent; return true; }
            call = null;
            return false;
        }
    }

    /// <summary>
    /// V3 attribute that roots a parameter at the <b>raw request body</b> for XML
    /// binding. Selection is identical to <see cref="BodyTextAttribute"/> (it hands
    /// the undecoded payload to the bind phase as text); the registered
    /// <c>XmlDocumentBinder</c> then parses that text into a loaded
    /// <see cref="System.Xml.XmlDocument"/>. Use on an <see cref="System.Xml.XmlDocument"/>
    /// parameter for XML webhooks / payloads the endpoint walks itself. A malformed
    /// payload surfaces as a bind failure (HTTP 400) rather than reaching the method.
    /// Selection misses if no body is present unless the parameter has a C# default.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class BodyXmlAttribute : Attribute, IBindFromRequest, IProvideMemberScope
    {
        Type IProvideMemberScope.MemberScope => typeof(RequestBody);

        public bool TrySelectSource(IRequestEnvelopeV3 envelope, ParameterInfo parameter,
            out BindCall call)
        {
            if (envelope.TryGetBody<string>(out var text) && text is not null)
            {
                call = BindCalls.Scalar(text);
                return true;
            }
            if (envelope.TryGetBody<byte[]>(out var bytes) && bytes is not null)
            {
                call = BindCalls.Scalar(System.Text.Encoding.UTF8.GetString(bytes));
                return true;
            }
            if (parameter.HasDefaultValue) { call = BindCalls.NotPresent; return true; }
            call = null;
            return false;
        }
    }

    /// <summary>
    /// V3 attribute that locates the parameter in the URL query string.
    /// Case-insensitive key matching (ASP.NET conventional). Multi-valued keys
    /// dispatch via <c>onArray</c>; single values via <c>onString</c>.
    /// Required-but-absent = selection miss; optional-absent =
    /// <see cref="BindCalls.NotPresent"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class QueryAttribute : Attribute, IBindFromRequest, IProvideMemberScope
    {
        public string Name { get; set; }

        Type IProvideMemberScope.MemberScope => typeof(QueryString);

        public bool TrySelectSource(IRequestEnvelopeV3 envelope, ParameterInfo parameter,
            out BindCall call)
        {
            var key = Name ?? parameter.Name;
            if (envelope.Query.TryGetValue(key, out var values) && values is { Length: > 0 })
            {
                call = BindCalls.MultiValue(key, values);
                return true;
            }
            if (parameter.HasDefaultValue) { call = BindCalls.NotPresent; return true; }
            call = null;
            return false;
        }

        public IEnumerable<string> GetConsumedQueryKeys(ParameterInfo parameter)
            => new[] { Name ?? parameter.Name };
    }

    /// <summary>
    /// V3 attribute that locates an OPTIONAL parameter in the URL query string.
    /// Behaves like <see cref="QueryAttribute"/> when the key is present, but is
    /// never a selection miss: an absent value contributes
    /// <see cref="BindCalls.Null"/>, so a <c>Nullable&lt;T&gt;</c> or reference-type
    /// parameter binds to <c>null</c> even without a C# default value. Apply this
    /// to make <c>start</c>/<c>days</c>-style query parameters optional.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class QueryOptionalAttribute : Attribute, IBindFromRequest, IProvideMemberScope
    {
        public string Name { get; set; }

        Type IProvideMemberScope.MemberScope => typeof(QueryString);

        public bool TrySelectSource(IRequestEnvelopeV3 envelope, ParameterInfo parameter,
            out BindCall call)
        {
            var key = Name ?? parameter.Name;
            if (envelope.Query.TryGetValue(key, out var values) && values is { Length: > 0 })
            {
                call = BindCalls.MultiValue(key, values);
                return true;
            }
            call = BindCalls.Null;
            return true;
        }

        public IEnumerable<string> GetConsumedQueryKeys(ParameterInfo parameter)
            => new[] { Name ?? parameter.Name };
    }

    /// <summary>
    /// V3 attribute that locates the parameter in route-template captures
    /// (regex group names, conventional <c>{id}</c> tokens). Route values are
    /// always single-valued strings.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class RouteAttribute : Attribute, IBindFromRequest, IProvideMemberScope
    {
        public string Name { get; set; }

        Type IProvideMemberScope.MemberScope => typeof(QueryString);

        public bool TrySelectSource(IRequestEnvelopeV3 envelope, ParameterInfo parameter,
            out BindCall call)
        {
            var key = Name ?? parameter.Name;
            if (envelope.Route.TryGetValue(key, out var value))
            {
                call = BindCalls.Scalar(value);
                return true;
            }
            if (parameter.HasDefaultValue) { call = BindCalls.NotPresent; return true; }
            call = null;
            return false;
        }
    }

    /// <summary>
    /// V3 attribute that locates the parameter in HTTP request headers. Header
    /// names are case-insensitive per HTTP spec. Multi-valued headers dispatch
    /// via <c>onArray</c>; single values via <c>onString</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class HeaderAttribute : Attribute, IBindFromRequest, IProvideMemberScope
    {
        public string Name { get; set; }

        Type IProvideMemberScope.MemberScope => typeof(QueryString);

        public bool TrySelectSource(IRequestEnvelopeV3 envelope, ParameterInfo parameter,
            out BindCall call)
        {
            var key = Name ?? parameter.Name;
            var values = envelope.Request.GetHeaders(key);
            var arr = values is null ? Array.Empty<string>() : values.ToArray();
            if (arr.Length > 0)
            {
                call = BindCalls.MultiValue(key, arr);
                return true;
            }
            if (parameter.HasDefaultValue) { call = BindCalls.NotPresent; return true; }
            call = null;
            return false;
        }
    }

    /// <summary>
    /// V3 attribute for a caller-supplied resource id — the equivalent of the
    /// legacy <see cref="EastFive.Api.UpdateIdAttribute"/>. Reads the id from the
    /// URL path capture, then the query string, then the request body (in that
    /// precedence), so <c>PUT /api/Resource/{id}</c>, <c>POST /api/Resource?id=…</c>,
    /// and a body-carried id all bind. Use it wherever the client supplies the
    /// resource's identifier — whether updating an existing resource or creating a
    /// new one with a client-generated id (so a retried POST collides on the same
    /// row and is detected as a duplicate rather than creating a second resource).
    /// <para>
    /// Like the legacy attribute it is <b>required</b> (selection misses when no
    /// body/query/path can supply it, unless the parameter has a C# default),
    /// <b>claims its key as a query parameter</b> so an id-bearing route isn't
    /// shadowed by a sibling keyless endpoint on the same route+verb, and
    /// contributes the trailing <c>/{id}</c> route capture so the path-style form
    /// matches. Apply to an <c>IRef&lt;T&gt;</c> / <c>Guid</c> parameter; the bind
    /// phase validates the value's shape (a malformed id surfaces as HTTP 400).
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class UpdateIdAttribute : Attribute,
        IBindFromRequest, IProvideMemberScope, IModifyRoutePattern
    {
        /// <summary>Wire key for the id. Defaults to <c>"id"</c> when unset.</summary>
        public string Name { get; set; }

        Type IProvideMemberScope.MemberScope => typeof(RequestBody);

        private string KeyFor(ParameterInfo parameter)
            => string.IsNullOrEmpty(Name) ? "id" : Name;

        public bool TrySelectSource(IRequestEnvelopeV3 envelope, ParameterInfo parameter,
            out BindCall call)
        {
            var key = KeyFor(parameter);

            // Path capture wins, then query, then body — parity with the legacy
            // BindingSource.Anywhere precedence (a route value outranks a query value).
            if (envelope.Route != null
                && envelope.Route.TryGetValue(key, out var routeVal)
                && !string.IsNullOrEmpty(routeVal))
            {
                call = BindCalls.Scalar(routeVal);
                return true;
            }

            if (envelope.Query != null
                && envelope.Query.TryGetValue(key, out var values)
                && values is { Length: > 0 })
            {
                call = BindCalls.MultiValue(key, values);
                return true;
            }

            if (EnvelopeBodyAccessor.TryGetBodyRoot(envelope, out var root, out _))
            {
                // The body exists; the id is read at bind time. A body that omits
                // the key surfaces as a bind failure (HTTP 400), matching the
                // legacy attribute's "required" semantics.
                call = BindCalls.FromSource(root, key);
                return true;
            }

            if (parameter.HasDefaultValue) { call = BindCalls.NotPresent; return true; }
            call = null;
            return false;
        }

        public IEnumerable<string> GetConsumedQueryKeys(ParameterInfo parameter)
            => new[] { KeyFor(parameter) };

        public string ModifyRoutePattern(MethodInfo method, ParameterInfo parameter, string currentPattern)
            => RoutePattern.AppendTrailingCapture(currentPattern, KeyFor(parameter),
                parameter.ParameterType);
    }
}
