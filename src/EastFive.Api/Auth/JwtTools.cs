using EastFive.Extensions;
using EastFive.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace EastFive.Api.Auth
{
    public static class JwtTools
    {
        public static TResult CreateToken<TResult>(Guid? sessionIdMaybe,
                Uri scope, TimeSpan duration, IDictionary<string, string> claims,
            Func<string, DateTime, TResult> tokenCreated,
            Func<string, TResult> missingConfigurationSetting,
            Func<string, string, TResult> invalidConfigurationSetting,
            string configNameOfIssuer = EastFive.Security.AppSettings.TokenIssuer,
            string configNameOfRSAKey = EastFive.Security.AppSettings.TokenKey,
            string configNameOfRSAAlgorithm = EastFive.Security.AppSettings.TokenAlgorithm,
            IEnumerable<KeyValuePair<string, string>> tokenHeaders = default)
        {
            IEnumerable<Claim> claimsAuth = new Claim[] { };
            if (sessionIdMaybe.HasValue)
                claimsAuth = new Claim(ClaimEnableSessionAttribute.Type, sessionIdMaybe.Value.ToString()).AsArray();
            
            var claimsCrypt = claims.NullToEmpty()
                .Select(kvp => new Claim(kvp.Key, kvp.Value));

            var issued = DateTime.UtcNow;
            var result = EastFive.Security.Tokens.JwtTools.CreateToken(scope,
                issued, duration, claimsAuth.Concat(claimsCrypt),
                (token) => tokenCreated(token, issued), missingConfigurationSetting, invalidConfigurationSetting,
                configNameOfIssuer, configNameOfRSAKey, configNameOfRSAAlgorithm, tokenHeaders);
            return result;
        }

        public static TResult CreateToken<TResult>(Guid sessionId, Uri scope,
            TimeSpan duration,
            Func<string, TResult> tokenCreated,
            Func<string, TResult> missingConfigurationSetting,
            Func<string, string, TResult> invalidConfigurationSetting,
            string configNameOfIssuer = EastFive.Security.AppSettings.TokenIssuer,
            string configNameOfRSAKey = EastFive.Security.AppSettings.TokenKey,
            string configNameOfRSAAlgorithm = EastFive.Security.AppSettings.TokenAlgorithm,
            IEnumerable<KeyValuePair<string, string>> tokenHeaders = default)
        {
            return CreateToken(sessionId, scope, duration, default(IDictionary<string, string>),
                (token, whenIssued) => tokenCreated(token), missingConfigurationSetting, invalidConfigurationSetting,
                configNameOfIssuer, configNameOfRSAKey, configNameOfRSAAlgorithm, tokenHeaders);
        }

        public static TResult CreateToken<TResult>(Guid sessionId, Guid authId, Uri scope,
            TimeSpan duration,
            IEnumerable<Claim> claims,
            Func<string, TResult> tokenCreated,
            Func<string, TResult> missingConfigurationSetting,
            Func<string, string, TResult> invalidConfigurationSetting,
            string configNameOfIssuer = EastFive.Security.AppSettings.TokenIssuer,
            string configNameOfRSAKey = EastFive.Security.AppSettings.TokenKey,
            string configNameOfRSAAlgorithm = EastFive.Security.AppSettings.TokenAlgorithm,
            IEnumerable<KeyValuePair<string, string>> tokenHeaders = default)
        {
            var claimsAuth = new[] {
                new Claim(ClaimEnableSessionAttribute.Type, sessionId.ToString()),
                new Claim(ClaimEnableActorAttribute.Type, authId.ToString()) };
            var claimsCrypt = claims.NullToEmpty();

            var issued = DateTime.UtcNow;
            var result = EastFive.Security.Tokens.JwtTools.CreateToken(scope,
                    issued, duration, claimsAuth.Concat(claimsCrypt),
                tokenCreated, 
                missingConfigurationSetting, 
                invalidConfigurationSetting,
                    configNameOfIssuer, configNameOfRSAKey, configNameOfRSAAlgorithm, tokenHeaders);
            return result;
        }

        public static TResult CreateToken<TResult>(Guid sessionId, Guid authId, Uri scope,
                TimeSpan duration,
            Func<string, TResult> tokenCreated,
            Func<string, TResult> missingConfigurationSetting,
            Func<string, string, TResult> invalidConfigurationSetting,
                string configNameOfIssuer = EastFive.Security.AppSettings.TokenIssuer,
                string configNameOfRSAKey = EastFive.Security.AppSettings.TokenKey,
            string configNameOfRSAAlgorithm = EastFive.Security.AppSettings.TokenAlgorithm,
            IEnumerable<KeyValuePair<string, string>> tokenHeaders = default)
        {
            return CreateToken(sessionId, authId, scope, duration, default(IDictionary<string, string>),
                tokenCreated, missingConfigurationSetting, invalidConfigurationSetting,
                configNameOfIssuer, configNameOfRSAKey, configNameOfRSAAlgorithm, tokenHeaders);
        }

        public static TResult CreateToken<TResult>(Guid sessionId, Guid authId, Uri scope,
            TimeSpan duration,
            IDictionary<string, string> claims,
            Func<string, TResult> tokenCreated,
            Func<string, TResult> missingConfigurationSetting,
            Func<string, string, TResult> invalidConfigurationSetting,
            string configNameOfIssuer = EastFive.Security.AppSettings.TokenIssuer,
            string configNameOfRSAKey = EastFive.Security.AppSettings.TokenKey,
            string configNameOfRSAAlgorithm = EastFive.Security.AppSettings.TokenAlgorithm,
            IEnumerable<KeyValuePair<string, string>> tokenHeaders = default)
        {
            var claimsCrypt = claims.NullToEmpty().Select(kvp => new Claim(kvp.Key, kvp.Value));
            return CreateToken(sessionId, authId, scope, duration, claimsCrypt, tokenCreated, missingConfigurationSetting, invalidConfigurationSetting,
                configNameOfIssuer, configNameOfRSAKey, configNameOfRSAAlgorithm, tokenHeaders);
        }
    }
}
