using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Serialization;
using EastFive.Web.Configuration;
using Newtonsoft.Json;

namespace EastFive.Api
{
    public struct AccessTokenAccount
    {
        public Guid sessionId;
        public Guid accountId;
        public DateTime expirationUtc;
        public IDictionary<string, string> claims;
    }

    public static class AccessTokenAccountExtensions
    {
        public const string QueryParameter = "access_token";

        private static DateTime GetEpoch() => new DateTime(2010, 1, 1);

        public static TResult SignWithAccessTokenAccount<TResult>(this Uri originalUrl,
                Guid sessionId, Guid accountId,
                DateTime expirationUtc,
            Func<Uri, TResult> onSuccess,
            Func<TResult> onSystemNotConfigured = default,
                IDictionary<string,string> claims = default)
        {
            var expiresAfterEpoch = expirationUtc - GetEpoch();
            var secondsAfterEpoch = (uint)expiresAfterEpoch.TotalSeconds;

            return originalUrl.CreateSignature(sessionId, accountId, secondsAfterEpoch,
                (accessToken, signature) =>
                {
                    var accessTokenString = accessToken.ToBase64String(urlSafe:true);
                    var accessTokenUrl = originalUrl.AddQueryParameter(QueryParameter, accessTokenString);
                    return onSuccess(accessTokenUrl);
                },
                onSystemNotConfigured,
                    claims);
        }

        public static TResult ValidateAccessTokenAccount<TResult>(this IHttpRequest request,
                bool shouldSkipValidationForLocalhost,
            Func<AccessTokenAccount, TResult> onSuccess,
            Func<TResult> onAccessTokenNotProvided,
            Func<TResult> onAccessTokenInvalid,
            Func<TResult> onAccessTokenExpired = default,
            Func<TResult> onInvalidSignature = default,
            Func<TResult> onSystemNotConfigured = default)
        {
            var url = request.RequestUri;
            if (!url.TryGetQueryParam(QueryParameter, out string accessTokenString))
                return onAccessTokenNotProvided();
            var originalUrl = url.RemoveQueryParameter(QueryParameter);

            if(!accessTokenString.TryParseBase64String(out byte[] accessTokenBytes))
                return onAccessTokenInvalid();

            if (accessTokenBytes.Length < 74)
                return onAccessTokenInvalid();

            var sessionId = new Guid(accessTokenBytes.Take(16).ToArray());
            var accountId = new Guid(accessTokenBytes.Skip(16).Take(16).ToArray());

            var secondsSinceEpoch = BitConverter.ToUInt32(accessTokenBytes, 32);
            var expiration = GetEpoch() + TimeSpan.FromSeconds(secondsSinceEpoch);
            if (DateTime.UtcNow > expiration)
            {
                if (onAccessTokenExpired.IsDefaultOrNull())
                    return onAccessTokenInvalid();
                return onAccessTokenExpired();
            }
            var claimsLength = (int)BitConverter.ToUInt32(accessTokenBytes, 36);
            var claimsText = Encoding.UTF8.GetString(accessTokenBytes, 40, claimsLength);
            var claims = JsonConvert.DeserializeObject<Dictionary<string, string>>(claimsText); // an empty dictionary is 2 characters: {}
            if (shouldSkipValidationForLocalhost)
            {
                if (request.IsLocalHostRequest())
                    return onSuccess(
                        new AccessTokenAccount()
                        {
                            sessionId = sessionId,
                            accountId = accountId,
                            expirationUtc = expiration,
                            claims = claims,
                        });
            }

            var signature = accessTokenBytes.Skip(40 + claimsLength).ToArray();
            return VerifySignature(signature,
                sessionId, accountId,
                secondsSinceEpoch, originalUrl,
                () =>
                {
                    return onSuccess(
                        new AccessTokenAccount()
                        {
                            sessionId = sessionId,
                            accountId = accountId,
                            expirationUtc = expiration,
                            claims = claims,
                        });
                },
                () =>
                {
                    if (onInvalidSignature.IsDefaultOrNull())
                        return onAccessTokenInvalid();

                    return onInvalidSignature();
                },
                onSystemNotConfigured,
                    claims: claims);
        }

        private static TResult CreateSignature<TResult>(this Uri originalUrl,
                Guid sessionId, Guid accountId,
                uint secondsAfterEpoch, 
            Func<byte[], byte[], TResult> onAccessTokenAndEnvelope,
            Func<TResult> onNotConfigured = default,
                IDictionary<string, string> claims = default)
        {
            return AppSettings.AccessTokenSecret.ConfigurationGuid(
                (apiSecret) =>
                {
                    var originalUrlStr = originalUrl.AbsoluteUri;
                    var urlHash = originalUrlStr.SHAHash();
                    if (claims == null)
                        claims = new Dictionary<string, string>();

                    var claimText = JsonConvert.SerializeObject(claims, Formatting.None);
                    var claimLength = claimText.Length;

                    var securityEnthropy = sessionId.ToByteArray()
                        .Concat(accountId.ToByteArray())
                        .Concat(BitConverter.GetBytes(secondsAfterEpoch))
                        .Concat(BitConverter.GetBytes(claimLength))
                        .Concat(Encoding.UTF8.GetBytes(claimText));
                    var dataToSign = securityEnthropy
                        .Concat(urlHash);
                    var envelopeBytes = dataToSign
                        .Concat(apiSecret.ToByteArray())
                        .ToArray();
                    var envelopeSignature = envelopeBytes.SHA256Hash();
                    var accessToken = securityEnthropy
                        .Concat(envelopeSignature)
                        .ToArray();
                    return onAccessTokenAndEnvelope(accessToken, envelopeSignature);
                },
                (why) =>
                {
                    if (onNotConfigured.IsDefaultOrNull())
                        throw new ConfigurationException(AppSettings.AccessTokenSecret, typeof(Guid), why);

                    return onNotConfigured();
                });
        }

        public static TResult VerifySignature<TResult>(this byte[] signature,
                Guid sessionId, Guid accountId,
                uint secondsAfterEpoch, Uri originalUrl,
            Func<TResult> onIsValid,
            Func<TResult> onIsNotValid,
            Func<TResult> onNotConfigured = default,
                IDictionary<string, string> claims = default)
        {
            return originalUrl.CreateSignature(sessionId, accountId, secondsAfterEpoch,
                (accessToken, envelopeSignature) =>
                {
                    if (envelopeSignature.SequenceEqual(signature))
                        return onIsValid();

                    return onIsNotValid();
                },
                onNotConfigured,
                    claims);
        }
    }
}
