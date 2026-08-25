using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using EastFive.Api.Binding;

namespace EastFive.Api.Routing.Envelopes
{
    /// <summary>
    /// JSON request envelope — claims requests whose Content-Type is
    /// <c>application/json</c>, ends in <c>+json</c>, or starts with
    /// <c>x-application/</c>. Default <c>Priority = 10.0</c>.
    ///
    /// Reads the body once during <see cref="CreateEnvelopeAsync"/> and parses
    /// it into a <see cref="JContainer"/>. Produces <see cref="JContainer"/>
    /// (whole body), <see cref="JToken"/> (keyed), <c>string</c>
    /// (re-serialized), and <c>object</c> at <see cref="BindingSource.Body"/>;
    /// <c>string</c>/<c>object</c> at <see cref="BindingSource.Query"/> and
    /// <see cref="BindingSource.Path"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class JsonRequestEnvelopeAttribute : Attribute, IDeserializeRequestEnvelope
    {
        public double Priority { get; set; } = 10.0;

        public bool CanClassify(IHttpRequest request)
        {
            if (!EnvelopeHelpers.HasBodyByHeaders(request))
                return false;
            if (EnvelopeHelpers.ContentTypeMatches(request, "application/json"))
                return true;
            if (EnvelopeHelpers.ContentTypeEndsWith(request, "+json"))
                return true;
            if (EnvelopeHelpers.ContentTypeStartsWith(request, "x-application/"))
                return true;
            return false;
        }

        public async Task<IRequestEnvelope> CreateEnvelopeAsync(IHttpRequest request)
        {
            var body = await request.ReadContentAsStringAsync();
            JContainer parsed = null;
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    parsed = JsonConvert.DeserializeObject(body) as JContainer;
                }
                catch (JsonException)
                {
                    parsed = null;
                }
            }
            var query = EnvelopeHelpers.ParseQuery(request);
            return new JsonEnvelope(parsed, body, query);
        }

        private sealed class JsonEnvelope : IRequestEnvelope, IRequestEnvelopeBody
        {
            private readonly JContainer body;
            private readonly string rawText;
            private readonly IReadOnlyDictionary<string, string> query;

            public JsonEnvelope(JContainer body, string rawText,
                IReadOnlyDictionary<string, string> query)
            {
                this.body = body;
                this.rawText = rawText;
                this.query = query;
            }

            public bool TryGetBody<TBody>(out TBody value)
            {
                // The raw text is retained alongside the parsed container so
                // [BodyText]-style raw-payload endpoints (signature-validated JSON
                // webhooks that parse the body themselves) can select on JSON
                // content types — previously only non-JSON envelopes served string.
                if (typeof(TBody) == typeof(string) && !string.IsNullOrEmpty(this.rawText))
                {
                    value = (TBody)(object)this.rawText;
                    return true;
                }
                if (this.body is null) { value = default; return false; }
                if (this.body is TBody match) { value = match; return true; }
                value = default;
                return false;
            }

            public bool TryFulfill(BindingRequirement requirement, out ExtractAsyncDelegate extract)
            {
                foreach (var kvp in requirement.Converters)
                {
                    if (TryProduceRaw(requirement.Source, requirement.Path ?? string.Empty, kvp.Key, out var producer))
                    {
                        var converter = kvp.Value;
                        extract = (httpApp, request) =>
                            Task.FromResult(converter.Convert(producer(), requirement.Parameter, httpApp, request));
                        return true;
                    }
                }
                extract = null;
                return false;
            }

            private bool TryProduceRaw(BindingSource source, string key, Type rawType, out Func<object> producer)
            {
                // Structured body types always come from the body, regardless of which other
                // flags the requirement declares.
                if (rawType == typeof(JContainer) || rawType == typeof(JToken))
                {
                    if ((source & BindingSource.Body) != 0)
                        return TryProduceBody(key, rawType, out producer);
                    producer = null;
                    return false;
                }

                // Scalars: prefer query, fall back to body when both flags are set.
                if ((source & BindingSource.Query) != 0
                    && TryProduceQuery(key, rawType, out producer))
                    return true;
                if ((source & BindingSource.Body) != 0
                    && TryProduceBody(key, rawType, out producer))
                    return true;
                producer = null;
                return false;
            }

            private bool TryProduceQuery(string key, Type rawType, out Func<object> producer)
            {
                if (!this.query.TryGetValue(key, out var value))
                {
                    producer = null;
                    return false;
                }
                if (rawType == typeof(string) || rawType == typeof(object))
                {
                    producer = () => value;
                    return true;
                }
                producer = null;
                return false;
            }

            private bool TryProduceBody(string key, Type rawType, out Func<object> producer)
            {
                if (this.body == null)
                {
                    producer = null;
                    return false;
                }

                // Whole-body access — JContainer is the canonical type for [Property]/[Resource] etc.
                // These converters receive the entire JContainer and extract their own key
                // internally; for *selection*, though, a keyed [Property] must actually be present
                // in the body. Without this gate, two POST overloads on the same route+verb that
                // differ only by body keys (e.g. Authorization.CreateAsync's
                // `location_authentication_return` vs CreateAuthorizedAsync's `parameters`) both
                // fulfill, and the dispatcher reports an ambiguous match. [Resource] and other
                // whole-body binders use an empty key and are unaffected.
                if (rawType == typeof(JContainer))
                {
                    if (!string.IsNullOrEmpty(key)
                        && this.body is JObject keyedBody
                        && keyedBody.Property(key, StringComparison.OrdinalIgnoreCase) == null)
                    {
                        producer = null;
                        return false;
                    }
                    var captured = this.body;
                    producer = () => captured;
                    return true;
                }

                JToken token;
                if (string.IsNullOrEmpty(key))
                {
                    token = this.body;
                }
                else if (this.body is JObject obj)
                {
                    var prop = obj.Property(key, StringComparison.OrdinalIgnoreCase);
                    if (prop == null)
                    {
                        producer = null;
                        return false;
                    }
                    token = prop.Value;
                }
                else
                {
                    producer = null;
                    return false;
                }

                if (rawType == typeof(JToken))
                {
                    producer = () => token;
                    return true;
                }
                if (rawType == typeof(string))
                {
                    producer = () => token.Type == JTokenType.String
                        ? token.Value<string>()
                        : token.ToString(Formatting.None);
                    return true;
                }
                if (rawType == typeof(object))
                {
                    producer = () => token;
                    return true;
                }
                producer = null;
                return false;
            }
        }
    }
}
