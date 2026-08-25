using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using EastFive.Api.Binding;

namespace EastFive.Api.Routing.Envelopes
{
    /// <summary>
    /// Catch-all body envelope — claims any body-bearing request not claimed
    /// by a higher-priority deserializer. Default <c>Priority = -100.0</c>.
    /// At <see cref="BindingSource.BodyBytes"/>, <see cref="BindingSource.Body"/>,
    /// and <see cref="BindingSource.Anywhere"/>: produces <c>byte[]</c>,
    /// <see cref="Stream"/>, <c>string</c> (UTF-8), and <c>object</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class RawRequestEnvelopeAttribute : Attribute, IDeserializeRequestEnvelope
    {
        public double Priority { get; set; } = -100.0;

        public bool CanClassify(IHttpRequest request)
            => EnvelopeHelpers.HasBodyByHeaders(request);

        public async Task<IRequestEnvelope> CreateEnvelopeAsync(IHttpRequest request)
        {
            var bytes = await request.ReadContentAsync();
            var query = EnvelopeHelpers.ParseQuery(request);
            return new RawEnvelope(bytes ?? Array.Empty<byte>(), query);
        }

        private sealed class RawEnvelope : IRequestEnvelope, IRequestEnvelopeBody
        {
            private readonly byte[] body;
            private readonly IReadOnlyDictionary<string, string> query;

            public RawEnvelope(byte[] body, IReadOnlyDictionary<string, string> query)
            {
                this.body = body;
                this.query = query;
            }

            public bool TryGetBody<TBody>(out TBody value)
            {
                if (this.body is TBody bytes) { value = bytes; return true; }
                if (typeof(TBody) == typeof(System.IO.Stream))
                {
                    object stream = new System.IO.MemoryStream(this.body, writable: false);
                    value = (TBody)stream;
                    return true;
                }
                if (typeof(TBody) == typeof(string))
                {
                    object str = System.Text.Encoding.UTF8.GetString(this.body);
                    value = (TBody)str;
                    return true;
                }
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
                // Body-shape types always come from the body.
                if (rawType == typeof(byte[]) || rawType == typeof(Stream))
                {
                    if ((source & (BindingSource.Body | BindingSource.BodyBytes)) != 0)
                        return TryProduceBody(rawType, out producer);
                    producer = null;
                    return false;
                }

                if ((source & BindingSource.Query) != 0
                    && TryProduceQuery(key, rawType, out producer))
                    return true;
                if ((source & (BindingSource.Body | BindingSource.BodyBytes)) != 0
                    && TryProduceBody(rawType, out producer))
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

            private bool TryProduceBody(Type rawType, out Func<object> producer)
            {
                if (rawType == typeof(byte[]))
                {
                    var captured = this.body;
                    producer = () => captured;
                    return true;
                }
                if (rawType == typeof(Stream))
                {
                    var captured = this.body;
                    producer = () => new MemoryStream(captured, writable: false);
                    return true;
                }
                if (rawType == typeof(string))
                {
                    var captured = this.body;
                    producer = () => Encoding.UTF8.GetString(captured);
                    return true;
                }
                if (rawType == typeof(object))
                {
                    var captured = this.body;
                    producer = () => captured;
                    return true;
                }
                producer = null;
                return false;
            }
        }
    }
}
