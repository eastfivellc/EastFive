using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using EastFive.Api.Binding;

namespace EastFive.Api.Routing.Envelopes
{
    /// <summary>
    /// URL-encoded form envelope — claims requests with Content-Type
    /// <c>application/x-www-form-urlencoded</c>. Default <c>Priority = 10.0</c>.
    /// Produces <see cref="IFormCollection"/> (whole) and <c>string</c>
    /// (keyed) at <see cref="BindingSource.Body"/>; <c>string</c> at
    /// <see cref="BindingSource.Query"/>/<see cref="BindingSource.Path"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class FormRequestEnvelopeAttribute : Attribute, IDeserializeRequestEnvelope
    {
        public double Priority { get; set; } = 10.0;

        public bool CanClassify(IHttpRequest request)
        {
            if (!EnvelopeHelpers.HasBodyByHeaders(request))
                return false;
            return EnvelopeHelpers.ContentTypeMatches(request, "application/x-www-form-urlencoded");
        }

        public Task<IRequestEnvelope> CreateEnvelopeAsync(IHttpRequest request)
        {
            var form = request.Form;
            var query = EnvelopeHelpers.ParseQuery(request);
            IRequestEnvelope envelope = new FormEnvelope(form, query);
            return Task.FromResult(envelope);
        }

        private sealed class FormEnvelope : IRequestEnvelope, IRequestEnvelopeBody
        {
            private readonly IFormCollection form;
            private readonly IReadOnlyDictionary<string, string> query;

            public FormEnvelope(IFormCollection form, IReadOnlyDictionary<string, string> query)
            {
                this.form = form;
                this.query = query;
            }

            public bool TryGetBody<TBody>(out TBody value)
            {
                if (this.form is TBody match) { value = match; return true; }
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
                if (rawType == typeof(IFormCollection))
                {
                    if ((source & BindingSource.Body) != 0)
                        return TryProduceBody(key, rawType, out producer);
                    producer = null;
                    return false;
                }

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
                if (this.form == null)
                {
                    producer = null;
                    return false;
                }

                if (rawType == typeof(IFormCollection))
                {
                    var captured = this.form;
                    producer = () => captured;
                    return true;
                }

                if (string.IsNullOrEmpty(key) || !this.form.ContainsKey(key))
                {
                    producer = null;
                    return false;
                }
                if (rawType == typeof(string) || rawType == typeof(object))
                {
                    var value = this.form[key].ToString();
                    producer = () => value;
                    return true;
                }
                producer = null;
                return false;
            }
        }
    }
}
