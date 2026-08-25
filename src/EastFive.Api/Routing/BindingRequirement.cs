using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using EastFive;
using EastFive.Api.Bindings;

namespace EastFive.Api
{
    /// <summary>
    /// Where a <see cref="BindingRequirement"/> expects to find its value.
    /// Bit-flags so a single requirement may declare multiple acceptable
    /// sources — <see cref="Fulfillment.From"/> iterates set bits in priority
    /// order Path → Query → Body → BodyBytes and the first hit wins.
    /// </summary>
    [Flags]
    public enum BindingSource
    {
        None = 0,

        /// <summary>URL query string parameter.</summary>
        Query = 1 << 0,

        /// <summary>Named capture from the route regex (e.g. <c>(?&lt;id&gt;…)</c>).</summary>
        Path = 1 << 1,

        /// <summary>Named property in the request body — the envelope decides
        /// whether that means a JSON property, multipart field, or form field.
        /// Binding-class attributes use this source regardless of wire format;
        /// abstracting body shape across deserializers is the point of the
        /// envelope.</summary>
        Body = 1 << 2,

        /// <summary>Raw body bytes, addressed without a key.</summary>
        BodyBytes = 1 << 3,

        /// <summary>The request as a whole — headers, full URI, etc. The
        /// requirement is always considered fulfilled; the converter reads
        /// what it needs from the <see cref="IHttpRequest"/> handed to its
        /// <see cref="BindCallback{TRaw}"/>. Used by <c>[Header]</c>,
        /// <c>[Accepts]</c>, <c>[HashedFile]</c> — parameters whose value
        /// is a function of the request itself rather than any single
        /// query/path/body slot.</summary>
        Request = 1 << 4,

        /// <summary>Convenience union — try Path, then Query, then Body, then BodyBytes.</summary>
        Anywhere = Query | Path | Body | BodyBytes,
    }

    /// <summary>
    /// Outcome of a single <see cref="BindCallback{TRaw}"/> invocation. Pure
    /// value: either <see cref="Ok"/> with a converted <see cref="Value"/>, or
    /// not-ok with an <see cref="Error"/>. The framework projects this back
    /// out to the caller through <see cref="Fulfillment.ExtractAsync{TResult}"/>.
    /// </summary>
    public readonly struct BindResult
    {
        private BindResult(bool ok, object value, string error)
        {
            this.Ok = ok;
            this.Value = value;
            this.Error = error;
        }

        public bool Ok { get; }
        public object Value { get; }
        public string Error { get; }

        public static BindResult Parsed(object value) => new BindResult(true, value, null);
        public static BindResult Failed(string error) => new BindResult(false, null, error);
    }

    /// <summary>
    /// Callback registered by a binding attribute via
    /// <see cref="BindingRequirement.AddConverter{TRaw}"/>. Receives a raw
    /// value of <typeparamref name="TRaw"/> produced by an envelope and is
    /// responsible for the attribute-specific bind step (typically delegating
    /// to <c>httpApp.Bind</c> with the parameter's declared target type).
    /// </summary>
    public delegate BindResult BindCallback<TRaw>(TRaw raw, ParameterInfo parameter,
        IApplication httpApp, IHttpRequest request,
        Func<object, BindResult> onParsed,
        Func<string, BindResult> onFailure);

    /// <summary>
    /// Erased view of a <see cref="BindingConverter{TRaw}"/>, indexed by
    /// <see cref="RawType"/> in the requirement's converter table.
    /// </summary>
    public abstract class BindingConverter
    {
        /// <summary>The raw payload type this converter accepts.</summary>
        public abstract Type RawType { get; }

        /// <summary>
        /// Run the converter against an envelope-produced raw value. The
        /// dynamic type of <paramref name="raw"/> must be assignment-compatible
        /// with <see cref="RawType"/>.
        /// </summary>
        public abstract BindResult Convert(object raw, ParameterInfo parameter,
            IApplication httpApp, IHttpRequest request);
    }

    /// <summary>
    /// Typed converter — wraps a <see cref="BindCallback{TRaw}"/> and performs
    /// the single cast from <see cref="object"/> to <typeparamref name="TRaw"/>
    /// inside <see cref="Convert"/>.
    /// </summary>
    public sealed class BindingConverter<TRaw> : BindingConverter
    {
        private readonly BindCallback<TRaw> bind;

        public BindingConverter(BindCallback<TRaw> bind)
        {
            this.bind = bind;
        }

        public override Type RawType => typeof(TRaw);

        public override BindResult Convert(object raw, ParameterInfo parameter,
            IApplication httpApp, IHttpRequest request)
        {
            var typed = (TRaw)raw;
            return this.bind(typed, parameter, httpApp, request,
                onParsed: BindResult.Parsed,
                onFailure: BindResult.Failed);
        }
    }

    /// <summary>
    /// Per-request, attribute-side description of what a binding-class
    /// parameter wants from the envelope. Pure value: the requirement carries
    /// no runtime state. The set of conversions the attribute is willing to
    /// accept lives in <see cref="Converters"/>, keyed by the raw payload
    /// type. The envelope picks any compatible <c>TRaw</c> and dispatches
    /// through that converter.
    /// </summary>
    public sealed class BindingRequirement
    {
        private readonly Dictionary<Type, BindingConverter> converters
            = new Dictionary<Type, BindingConverter>();

        private readonly Func<ParameterInfo, object> optionalDefault;

        public BindingRequirement(string path, BindingSource source,
            ParameterInfo parameter, bool isOptional = false,
            Func<ParameterInfo, object> optionalDefault = null)
        {
            this.Path = path;
            this.Source = source;
            this.Parameter = parameter;
            this.IsOptional = isOptional;
            this.optionalDefault = optionalDefault ?? DefaultOptionalDefault;
        }

        /// <summary>
        /// Lookup key — query parameter name, JSON property name, form field
        /// name, etc. May be empty for <see cref="BindingSource.BodyBytes"/>
        /// requirements that address the body as a whole.
        /// </summary>
        public string Path { get; }

        /// <summary>Where the value is expected to come from.</summary>
        public BindingSource Source { get; }

        /// <summary>
        /// If <c>true</c>, the method still matches when the envelope cannot
        /// fulfill this requirement; the requirement supplies a default value
        /// at extraction time via <see cref="GetOptionalDefault"/>.
        /// </summary>
        public bool IsOptional { get; }

        /// <summary>The parameter this requirement was generated for.</summary>
        public ParameterInfo Parameter { get; }

        /// <summary>
        /// Read-only view of the registered converter table. The envelope
        /// iterates these keys to find a producible raw type.
        /// </summary>
        public IReadOnlyDictionary<Type, BindingConverter> Converters => this.converters;

        /// <summary>
        /// Register a converter for raw payload type <typeparamref name="TRaw"/>.
        /// Fluent — returns <c>this</c> for chaining.
        /// </summary>
        public BindingRequirement AddConverter<TRaw>(BindCallback<TRaw> bind)
        {
            this.converters[typeof(TRaw)] = new BindingConverter<TRaw>(bind);
            return this;
        }

        /// <summary>
        /// Produce the optional-default value for the bound parameter — used
        /// when the envelope could not fulfill an optional requirement.
        /// </summary>
        public object GetOptionalDefault() => this.optionalDefault(this.Parameter);

        private static object DefaultOptionalDefault(ParameterInfo parameter)
        {
            var t = parameter.ParameterType;
            if (t.IsSubClassOfGeneric(typeof(IRefOptional<>)))
                return RefOptionalHelper.CreateEmpty(t.GenericTypeArguments.First());
            return t.GetDefault();
        }
    }

    /// <summary>
    /// Closure produced by <see cref="IRequestEnvelope.TryFulfill"/>. When
    /// invoked, runs the matching converter against an envelope-produced raw
    /// value and yields a <see cref="BindResult"/>.
    /// </summary>
    public delegate Task<BindResult> ExtractAsyncDelegate(IApplication httpApp, IHttpRequest request);

    /// <summary>
    /// Per-method-match dispatch surface: a stack of raw-payload producers
    /// that resolve a <see cref="BindingRequirement"/> to an
    /// <see cref="ExtractAsyncDelegate"/>. Two producers wrap the inner
    /// <see cref="IRequestEnvelope"/>:
    /// <list type="bullet">
    /// <item><c>Request</c> — serves whole-request converters from a sentinel.</item>
    /// <item><c>Path</c> — serves regex-named captures registered for this candidate.</item>
    /// </list>
    /// The inner envelope handles wire-format sources (<c>Query</c>,
    /// <c>Body</c>, <c>BodyBytes</c>). The dispatch pipeline asks the
    /// <see cref="RouteEnvelope"/> — never the raw envelope — to fulfil each
    /// requirement; from <see cref="Fulfillment.From"/>'s perspective the
    /// wrapper is the only envelope that exists.
    /// </summary>
    public sealed class RouteEnvelope
    {
        private readonly IRequestEnvelope envelope;
        private readonly IReadOnlyDictionary<string, string> captures;

        public RouteEnvelope(IRequestEnvelope envelope,
            IReadOnlyDictionary<string, string> captures = null)
        {
            this.envelope = envelope ?? throw new ArgumentNullException(nameof(envelope));
            this.captures = captures;
        }

        /// <summary>The raw envelope (for tests / advanced fall-through).</summary>
        public IRequestEnvelope Envelope => this.envelope;

        /// <summary>
        /// Try each producer in turn — Request, then Path, then the inner
        /// envelope — and return the first one that owns the requirement's
        /// declared <see cref="BindingRequirement.Source"/> flags and can
        /// satisfy a registered converter.
        /// </summary>
        public bool TryFulfill(BindingRequirement requirement, out ExtractAsyncDelegate extract)
        {
            if (TryProduceFromRequest(requirement, out extract))
                return true;
            if (TryProduceFromCaptures(requirement, out extract))
                return true;
            return this.envelope.TryFulfill(requirement, out extract);
        }

        private static bool TryProduceFromRequest(BindingRequirement requirement,
            out ExtractAsyncDelegate extract)
        {
            extract = null;
            if ((requirement.Source & BindingSource.Request) == 0)
                return false;

            // The converter reads from the IHttpRequest directly; raw value is
            // a sentinel (empty string, since string is the only required
            // converter type for Request-sourced requirements).
            if (!requirement.Converters.TryGetValue(typeof(string), out var stringConv))
                return false;

            extract = (app, request) => Task.FromResult(
                stringConv.Convert(string.Empty, requirement.Parameter, app, request));
            return true;
        }

        private bool TryProduceFromCaptures(BindingRequirement requirement,
            out ExtractAsyncDelegate extract)
        {
            extract = null;
            if ((requirement.Source & BindingSource.Path) == 0)
                return false;
            if (this.captures == null || string.IsNullOrEmpty(requirement.Path))
                return false;
            if (!this.captures.TryGetValue(requirement.Path, out var raw) || raw == null)
                return false;

            BindingConverter chosen = null;
            if (requirement.Converters.TryGetValue(typeof(string), out var stringConv))
                chosen = stringConv;
            else if (requirement.Converters.TryGetValue(typeof(object), out var objConv))
                chosen = objConv;
            if (chosen == null)
                return false;

            var captured = raw;
            extract = (app, request) => Task.FromResult(
                chosen.Convert(captured, requirement.Parameter, app, request));
            return true;
        }
    }

    /// <summary>
    /// Pairs a <see cref="BindingRequirement"/> with the envelope's response.
    /// Selection caches one of these per binding-class parameter; binding
    /// later runs <see cref="ExtractAsync{TResult}"/> on each.
    /// </summary>
    public readonly struct Fulfillment
    {
        private Fulfillment(BindingRequirement requirement, ExtractAsyncDelegate extract,
            bool isFulfilled, bool isValid)
        {
            this.Requirement = requirement;
            this.extract = extract;
            this.IsFulfilled = isFulfilled;
            this.IsValid = isValid;
        }

        private readonly ExtractAsyncDelegate extract;

        public BindingRequirement Requirement { get; }

        /// <summary>Whether the envelope produced a converter match.</summary>
        public bool IsFulfilled { get; }

        /// <summary>
        /// <c>true</c> when this fulfilment does not block route selection.
        /// Optional requirements are valid regardless of fulfilment;
        /// non-optional requirements are valid only when fulfilled.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Build a fulfilment for <paramref name="requirement"/>. The
        /// <paramref name="routeEnvelope"/> consults captures, request-level
        /// metadata, and body-shape sources together — selection never reaches
        /// past it.
        /// </summary>
        public static Fulfillment From(BindingRequirement requirement, RouteEnvelope routeEnvelope)
        {
            var fulfilled = routeEnvelope.TryFulfill(requirement, out var extract);
            var valid = fulfilled || requirement.IsOptional;
            return new Fulfillment(requirement, fulfilled ? extract : null, fulfilled, valid);
        }

        /// <summary>
        /// Project the bind outcome into <typeparamref name="TResult"/> via
        /// caller-supplied callbacks. When the requirement is optional and
        /// unfulfilled, <paramref name="onParsed"/> is invoked with the
        /// requirement's optional-default value. When the requirement is
        /// required and unfulfilled, <paramref name="onFailure"/> is invoked.
        /// </summary>
        public async Task<TResult> ExtractAsync<TResult>(
            IApplication httpApp, IHttpRequest request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            if (this.extract == null)
            {
                if (this.Requirement.IsOptional)
                    return onParsed(this.Requirement.GetOptionalDefault());
                return onFailure(
                    $"requirement '{this.Requirement.Path}' ({this.Requirement.Source}) was not fulfilled by the request envelope");
            }

            BindResult result;
            try
            {
                result = await this.extract(httpApp, request);
            }
            catch (Exception ex)
            {
                return onFailure($"converter threw while extracting '{this.Requirement.Path}': {ex.Message}");
            }

            return result.Ok ? onParsed(result.Value) : onFailure(result.Error);
        }
    }

    /// <summary>
    /// Per-parameter wrapper around one or more <see cref="Fulfillment"/>s.
    /// Selection caches one of these per binding-class parameter; binding
    /// runs <see cref="ExtractAsync{TResult}"/> on each, which gathers the
    /// sub-values and invokes the attribute's
    /// <see cref="AssembleParameter"/> closure to produce the final
    /// <c>(value, context)</c> pair handed to the controller method and the
    /// validator chain.
    /// </summary>
    public readonly struct ParameterFulfillment
    {
        private readonly Fulfillment[] sub;
        private readonly AssembleParameter assemble;
        private readonly string parameterPath;

        private ParameterFulfillment(ParameterInfo parameter, Fulfillment[] sub,
            AssembleParameter assemble, string parameterPath)
        {
            this.Parameter = parameter;
            this.sub = sub;
            this.assemble = assemble;
            this.parameterPath = parameterPath;
        }

        public ParameterInfo Parameter { get; }

        public IReadOnlyList<Fulfillment> SubFulfillments => this.sub;

        /// <summary>Path used in error messages — joined sub-paths.</summary>
        public string Path => this.parameterPath;

        /// <summary>All sub-fulfilments are valid (or optional).</summary>
        public bool IsValid
        {
            get
            {
                foreach (var f in this.sub)
                    if (!f.IsValid) return false;
                return true;
            }
        }

        /// <summary>Joined error string across sub-fulfilments.</summary>
        public string ErrorMessage
        {
            get
            {
                var bits = new System.Collections.Generic.List<string>(this.sub.Length);
                foreach (var f in this.sub)
                    if (!f.IsValid)
                        bits.Add($"{f.Requirement.Path}({f.Requirement.Source})");
                return bits.Count == 0 ? null : "unfulfilled: " + string.Join(", ", bits);
            }
        }

        /// <summary>
        /// Build a fulfilment for <paramref name="parameter"/>: one
        /// <see cref="Fulfillment"/> per requirement plus the supplied
        /// <see cref="AssembleParameter"/> closure that produces the final
        /// <c>(value, context)</c> pair from the bound sub-values.
        /// </summary>
        public static ParameterFulfillment From(ParameterInfo parameter,
            IReadOnlyList<BindingRequirement> requirements,
            AssembleParameter assemble,
            RouteEnvelope routeEnvelope)
        {
            var subs = new Fulfillment[requirements.Count];
            for (int i = 0; i < requirements.Count; i++)
                subs[i] = Fulfillment.From(requirements[i], routeEnvelope);
            var path = subs.Length == 1
                ? subs[0].Requirement.Path
                : string.Join("+", System.Linq.Enumerable.Select(subs, s => s.Requirement.Path));
            return new ParameterFulfillment(parameter, subs, assemble, parameterPath: path);
        }

        /// <summary>
        /// Extract every sub-value, then invoke the attribute's
        /// <see cref="AssembleParameter"/> closure to produce the
        /// <c>(value, context)</c> pair, projecting through caller-supplied
        /// callbacks. The second argument to <paramref name="onParsed"/> is
        /// the context the closure published (or <c>null</c>).
        /// </summary>
        public async System.Threading.Tasks.Task<TResult> ExtractAsync<TResult>(
            IApplication httpApp, IHttpRequest request,
            System.Func<object, object, TResult> onParsed,
            System.Func<string, TResult> onFailure)
        {
            var values = new object[this.sub.Length];
            for (int i = 0; i < this.sub.Length; i++)
            {
                var idx = i;
                string failure = null;
                var ok = await this.sub[i].ExtractAsync<bool>(httpApp, request,
                    onParsed: v => { values[idx] = v; return true; },
                    onFailure: msg => { failure = msg; return false; });
                if (!ok)
                    return onFailure(failure);
            }

            try
            {
                var (value, context) = this.assemble(values);
                return onParsed(value, context);
            }
            catch (System.Exception ex)
            {
                return onFailure($"assemble threw while building '{this.parameterPath}': {ex.Message}");
            }
        }
    }
}
