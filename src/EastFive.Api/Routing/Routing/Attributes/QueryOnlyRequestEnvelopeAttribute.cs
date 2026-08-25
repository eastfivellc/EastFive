using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using EastFive.Api.Binding;

namespace EastFive.Api.Routing.Envelopes
{
    /// <summary>
    /// Body-less request envelope — claims requests whose headers advertise no
    /// body. Default <c>Priority = 5.0</c>. Produces <c>string</c> and
    /// <c>object</c> at <see cref="BindingSource.Query"/>,
    /// <see cref="BindingSource.Path"/>, and <see cref="BindingSource.Anywhere"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class QueryOnlyRequestEnvelopeAttribute : Attribute, IDeserializeRequestEnvelope
    {
        public double Priority { get; set; } = 5.0;

        public bool CanClassify(IHttpRequest request)
            => EnvelopeHelpers.LacksBodyByHeaders(request);

        public Task<IRequestEnvelope> CreateEnvelopeAsync(IHttpRequest request)
        {
            var query = EnvelopeHelpers.ParseQuery(request);
            IRequestEnvelope envelope = new QueryOnlyEnvelope(query);
            return Task.FromResult(envelope);
        }

        private sealed class QueryOnlyEnvelope : IRequestEnvelope, IRequestEnvelopeBody
        {
            private readonly IReadOnlyDictionary<string, string> query;

            public QueryOnlyEnvelope(IReadOnlyDictionary<string, string> query)
            {
                this.query = query;
            }

            public bool TryGetBody<TBody>(out TBody value)
            {
                value = default;
                return false;
            }

            public bool TryFulfill(BindingRequirement requirement, out ExtractAsyncDelegate extract)
            {
                if ((requirement.Source & BindingSource.Query) == 0)
                {
                    extract = null;
                    return false;
                }

                if (!this.query.TryGetValue(requirement.Path ?? string.Empty, out var value))
                {
                    extract = null;
                    return false;
                }

                foreach (var kvp in requirement.Converters)
                {
                    if (kvp.Key == typeof(string) || kvp.Key == typeof(object))
                    {
                        var converter = kvp.Value;
                        extract = (httpApp, request) =>
                            Task.FromResult(converter.Convert(value, requirement.Parameter, httpApp, request));
                        return true;
                    }
                }
                extract = null;
                return false;
            }
        }
    }
}
