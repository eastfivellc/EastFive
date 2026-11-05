using System;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using EastFive.Azure.Persistence.AzureStorageTables;
using EastFive.Extensions;
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

        /// <summary>
        /// Binds <paramref name="authorizationRef"/> to one consumer. A client exchange
        /// (<paramref name="rebind"/> false) is refused when the authorization is deleted,
        /// expired, not tied to an account, older than <see cref="Authorization.RedemptionWindow"/>,
        /// or already redeemed by a different consumer; repeating the same consumer's exchange is
        /// accepted. A trusted caller that minted or re-verified the authorization rebinds it
        /// (overwriting any earlier consumer) and is never refused.
        /// </summary>
        /// <remarks>Refusal reasons are ASCII: callers put them in a response header.</remarks>
        internal static async Task<TResult> RedeemAsync<TResult>(
                IRef<Authorization> authorizationRef,
                string consumerKind,
                Guid consumer,
                bool rebind,
            Func<TResult> onRedeemed,
            Func<string, TResult> onRefused)
        {
            var redemptionRef = authorizationRef.id.AsRef<AuthorizationRedemption>();
            if (rebind)
                return await redemptionRef.StorageCreateOrUpdateAsync(
                    async (created, redemption, saveAsync) =>
                    {
                        redemption.redemptionRef = redemptionRef;
                        redemption.consumerKind = consumerKind;
                        redemption.consumer = consumer;
                        redemption.redeemedOn = UtcNow;
                        await saveAsync(redemption);
                        return onRedeemed();
                    });

            return await await authorizationRef.StorageGetAsync(
                async authorization =>
                {
                    if (authorization.deleted.HasValue)
                        return onRefused("Authorization was revoked.");
                    if (authorization.expired)
                        return onRefused("Authorization has expired.");
                    if (!authorization.accountIdMaybe.HasValue)
                        return onRefused("Authorization is not tied to an account.");
                    if (UtcNow - authorization.lastModified > Authorization.RedemptionWindow)
                        return onRefused("Authorization is too old to redeem. Sign in again.");

                    var redemption = new AuthorizationRedemption
                    {
                        redemptionRef = redemptionRef,
                        consumerKind = consumerKind,
                        consumer = consumer,
                        redeemedOn = UtcNow,
                    };
                    return await await redemption.StorageCreateAsync(
                        created => onRedeemed().AsTask(),
                        onAlreadyExists: () => redemptionRef.StorageGetAsync(
                            existing => existing.consumerKind == consumerKind && existing.consumer == consumer
                                ? onRedeemed()
                                : onRefused("Authorization was already redeemed."),
                            () => onRefused("Authorization was already redeemed.")));
                },
                () => onRefused("Authorization not found.").AsTask());
        }

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
