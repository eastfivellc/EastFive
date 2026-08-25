using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using EastFive.Api.Binding;

namespace EastFive.Api.Routing.Envelopes
{
    /// <summary>
    /// Multipart envelope — claims requests with Content-Type starting with
    /// <c>multipart/</c> (e.g. <c>multipart/form-data</c>). Default
    /// <c>Priority = 10.0</c>.
    ///
    /// At <see cref="BindingSource.Body"/> produces <see cref="IFormCollection"/>
    /// (whole), <c>string</c> (text field), <c>byte[]</c> and
    /// <see cref="Stream"/> (file part). At
    /// <see cref="BindingSource.Query"/>/<see cref="BindingSource.Path"/>
    /// produces <c>string</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class MultipartRequestEnvelopeAttribute : Attribute, IDeserializeRequestEnvelope
    {
        public double Priority { get; set; } = 10.0;

        public bool CanClassify(IHttpRequest request)
        {
            if (!EnvelopeHelpers.HasBodyByHeaders(request))
                return false;
            return EnvelopeHelpers.ContentTypeStartsWith(request, "multipart/");
        }

        public Task<IRequestEnvelope> CreateEnvelopeAsync(IHttpRequest request)
        {
            var form = request.Form;
            var query = EnvelopeHelpers.ParseQuery(request);
            IRequestEnvelope envelope = new MultipartEnvelope(form, query);
            return Task.FromResult(envelope);
        }

        private sealed class MultipartEnvelope : IRequestEnvelope, IRequestEnvelopeBody
        {
            private readonly IFormCollection form;
            private readonly IReadOnlyDictionary<string, string> query;

            public MultipartEnvelope(IFormCollection form, IReadOnlyDictionary<string, string> query)
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
                    if (TryProduceRaw(requirement.Source, requirement.Path ?? string.Empty, kvp.Key,
                        out var asyncProducer))
                    {
                        var converter = kvp.Value;
                        extract = async (httpApp, request) =>
                        {
                            var raw = await asyncProducer();
                            return converter.Convert(raw, requirement.Parameter, httpApp, request);
                        };
                        return true;
                    }
                }
                extract = null;
                return false;
            }

            private bool TryProduceRaw(BindingSource source, string key, Type rawType,
                out Func<Task<object>> producer)
            {
                // Body-shape types always come from the body.
                if (rawType == typeof(IFormCollection)
                    || rawType == typeof(byte[])
                    || rawType == typeof(Stream))
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

            private bool TryProduceQuery(string key, Type rawType, out Func<Task<object>> producer)
            {
                if (!this.query.TryGetValue(key, out var value))
                {
                    producer = null;
                    return false;
                }
                if (rawType == typeof(string) || rawType == typeof(object))
                {
                    producer = () => Task.FromResult<object>(value);
                    return true;
                }
                producer = null;
                return false;
            }

            private bool TryProduceBody(string key, Type rawType, out Func<Task<object>> producer)
            {
                if (this.form == null)
                {
                    producer = null;
                    return false;
                }

                if (rawType == typeof(IFormCollection))
                {
                    var captured = this.form;
                    producer = () => Task.FromResult<object>(captured);
                    return true;
                }

                if (string.IsNullOrEmpty(key))
                {
                    producer = null;
                    return false;
                }

                // File parts for byte[] / Stream.
                if (rawType == typeof(byte[]) || rawType == typeof(Stream))
                {
                    var file = this.form.Files.GetFile(key);
                    if (file == null)
                    {
                        producer = null;
                        return false;
                    }
                    if (rawType == typeof(byte[]))
                    {
                        producer = async () =>
                        {
                            using var ms = new MemoryStream();
                            await file.CopyToAsync(ms);
                            return ms.ToArray();
                        };
                        return true;
                    }
                    producer = () => Task.FromResult<object>(file.OpenReadStream());
                    return true;
                }

                if (rawType == typeof(string) || rawType == typeof(object))
                {
                    if (!this.form.ContainsKey(key))
                    {
                        producer = null;
                        return false;
                    }
                    var value = this.form[key].ToString();
                    producer = () => Task.FromResult<object>(value);
                    return true;
                }

                producer = null;
                return false;
            }
        }
    }
}
