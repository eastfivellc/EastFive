using System;
using System.Threading;

using Newtonsoft.Json;

using EastFive.Persistence;
using EastFive.Persistence.Azure.StorageTables;

namespace EastFive.Azure.Auth
{
    /// <summary>
    /// Records which consumer exchanged an <see cref="Authorization"/> id for credentials.
    /// Keyed by the authorization id, so the first insert wins and every later exchange by a
    /// different consumer is refused.
    /// </summary>
    [StorageTable]
    public struct AuthorizationRedemption : IReferenceable
    {
        public const string ConsumerKindSession = "session";
        public const string ConsumerKindOAuthAuthorize = "oauth-authorize";

        [JsonIgnore]
        public Guid id => redemptionRef.id;

        [RowKey]
        [StandardParititionKey]
        [JsonIgnore]
        public IRef<AuthorizationRedemption> redemptionRef;

        [ETag]
        [JsonIgnore]
        public string eTag;

        [Storage]
        public string consumerKind;

        [Storage]
        public Guid consumer;

        [Storage]
        public DateTime redeemedOn;

        private static readonly AsyncLocal<Func<DateTime>> clock = new AsyncLocal<Func<DateTime>>();

        /// <summary>The current UTC time, overridable per async flow by tests.</summary>
        internal static DateTime UtcNow => clock.Value is { } now ? now() : DateTime.UtcNow;

        /// <summary>Overrides <see cref="UtcNow"/> for the current async flow until disposed.</summary>
        internal static IDisposable UseClock(Func<DateTime> now)
        {
            var previous = clock.Value;
            clock.Value = now;
            return new ClockScope(previous);
        }

        private sealed class ClockScope : IDisposable
        {
            private readonly Func<DateTime> previous;

            public ClockScope(Func<DateTime> previous) => this.previous = previous;

            public void Dispose() => clock.Value = previous;
        }
    }
}
