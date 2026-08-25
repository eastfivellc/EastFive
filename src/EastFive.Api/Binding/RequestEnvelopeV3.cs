using System;
using System.Collections.Generic;

namespace EastFive.Api.Binding
{
    /// <summary>
    /// Concrete <see cref="IRequestEnvelopeV3"/> built by the dispatcher at
    /// selection time. Composes a per-deserializer
    /// <see cref="IRequestEnvelopeBody"/> with the route/query/request data
    /// that is only known after route matching.
    /// </summary>
    public sealed class RequestEnvelopeV3 : IRequestEnvelopeV3
    {
        private readonly IRequestEnvelopeBody body;

        public RequestEnvelopeV3(
            IRequestEnvelopeBody body,
            IReadOnlyDictionary<string, string[]> query,
            IReadOnlyDictionary<string, string> route,
            IHttpRequest request)
        {
            this.body = body;
            this.Query = query ?? EmptyQuery;
            this.Route = route ?? EmptyRoute;
            this.Request = request ?? throw new ArgumentNullException(nameof(request));
        }

        public bool TryGetBody<TBody>(out TBody value)
        {
            if (this.body is null) { value = default; return false; }
            return this.body.TryGetBody(out value);
        }

        public IReadOnlyDictionary<string, string[]> Query { get; }
        public IReadOnlyDictionary<string, string> Route { get; }
        public IHttpRequest Request { get; }

        private static readonly IReadOnlyDictionary<string, string[]> EmptyQuery
            = new Dictionary<string, string[]>(0);
        private static readonly IReadOnlyDictionary<string, string> EmptyRoute
            = new Dictionary<string, string>(0);
    }
}
