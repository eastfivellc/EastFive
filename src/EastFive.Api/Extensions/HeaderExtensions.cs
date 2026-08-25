using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Configuration;

using EastFive.Extensions;
using EastFive.Security.Tokens;

namespace EastFive.Web
{
    public static class HeaderExtensions
    {
        private const string BearerTokenPrefix = "bearer ";

        public static TResult GetClaimsJwtString<TResult>(this string jwtString,
            Func<IEnumerable<Claim>, TResult> success,
            Func<string, TResult> failure,
            string issuerConfigSetting = EastFive.Security.AppSettings.TokenIssuer,
            string validationKeyConfigSetting = EastFive.Security.AppSettings.TokenKey)
        {
            try
            {
                var jwtStringPossibleBearer = jwtString;
                var securityClientJwtString = jwtStringPossibleBearer.ToLower().StartsWith(BearerTokenPrefix) ?
                    jwtStringPossibleBearer.Substring(BearerTokenPrefix.Length) :
                    jwtStringPossibleBearer;
                var result = securityClientJwtString.ParseAndValidateToken(
                    (claims) =>
                    {
                        return success(claims);
                    },
                    (why) =>
                    {
                        return failure(why);
                    },
                    (setting) =>
                    {
                        return failure($"Missing config setting [{setting}]");
                    },
                    (setting, why) =>
                    {
                        return failure($"Invalid config setting[{setting}]:{why}");
                    },
                    issuerConfigSetting, validationKeyConfigSetting);
                return result;
            }
            catch (Exception)
            {
                //throw new ArgumentException("Problem getting user id from Authorization header");
                throw;
            }
        }

        public static TResult GetUnvalidatedClaimsJwtString<TResult>(this string jwtString,
            Func<IEnumerable<Claim>, TResult> success,
            Func<string, TResult> onFailure)
        {
            try
            {
                var jwtStringPossibleBearer = jwtString;
                var securityClientJwtString = jwtStringPossibleBearer.ToLower().StartsWith(BearerTokenPrefix) ?
                    jwtStringPossibleBearer.Substring(BearerTokenPrefix.Length) :
                    jwtStringPossibleBearer;
                return securityClientJwtString.ParseJwtString(
                    (token) =>
                    {
                        var claims = token.Claims.ToArray();
                        return success(claims);
                    },
                    (why) =>
                    {
                        return onFailure(why);
                    });
            }
            catch (Exception ex)
            {
                //throw new ArgumentException("Problem getting user id from Authorization header");
                return onFailure(ex.Message);
            }
        }

        public static TResult ParseJwtString<TResult>(this string jwtString,
            Func<System.IdentityModel.Tokens.Jwt.JwtSecurityToken, TResult> onSuccess,
            Func<string, TResult> failure)
        {
            try
            {
                var jwtStringPossibleBearer = jwtString;
                var securityClientJwtString = jwtStringPossibleBearer.ToLower().StartsWith(BearerTokenPrefix) ?
                    jwtStringPossibleBearer.Substring(BearerTokenPrefix.Length) :
                    jwtStringPossibleBearer;
                return securityClientJwtString.ParseToken(
                    (claims) =>
                    {
                        return onSuccess(claims);
                    },
                    (why) =>
                    {
                        return failure(why);
                    });
            }
            catch (Exception)
            {
                //throw new ArgumentException("Problem getting user id from Authorization header");
                throw;
            }
        }


        public static bool IsJson(this HttpContent content)
        {
            if (content.IsDefaultOrNull())
                return false;

            if (content.Headers.IsDefaultOrNull())
                return false;

            if (content.Headers.ContentType.IsDefaultOrNull())
                return false;

            return String.Compare("application/json", content.Headers.ContentType.MediaType.ToLower(), true) == 0;
        }

        public static bool IsXml(this HttpContent content)
        {
            if (content.IsDefaultOrNull())
                return false;

            if (content.Headers.IsDefaultOrNull())
                return false;

            if (content.Headers.ContentType.IsDefaultOrNull())
                return false;

            var mediaTypeStr = content.Headers.ContentType.MediaType.ToLower();
            if (String.Compare("application/xml", mediaTypeStr, true) == 0)
                return true;

            return mediaTypeStr.Contains("xml");
        }

        public static TResult ParseHttpMethod<TResult>(this string methodName,
            Func<HttpMethod, TResult> success,
            Func<TResult> failed)
        {
            if (string.Compare(HttpMethod.Delete.Method, methodName, true) == 0)
                return success(HttpMethod.Delete);
            if (string.Compare(HttpMethod.Get.Method, methodName, true) == 0)
                return success(HttpMethod.Get);
            if (string.Compare(HttpMethod.Head.Method, methodName, true) == 0)
                return success(HttpMethod.Head);
            if (string.Compare(HttpMethod.Options.Method, methodName, true) == 0)
                return success(HttpMethod.Options);
            if (string.Compare(HttpMethod.Post.Method, methodName, true) == 0)
                return success(HttpMethod.Post);
            if (string.Compare(HttpMethod.Put.Method, methodName, true) == 0)
                return success(HttpMethod.Put);
            if (string.Compare(HttpMethod.Trace.Method, methodName, true) == 0)
                return success(HttpMethod.Trace);
            return failed();
        }

        public static IEnumerable<HttpMethod> GetOptions(this HttpContentHeaders headers)
        {
            var optionStrings = headers.Allow;
            return optionStrings
                .Where(option => option.ParseHttpMethod((o) => true, () => false))
                .Select(option => option.ParseHttpMethod(
                    (o) => o, // return parse
                    () => HttpMethod.Get)); // never happens because of Where clause

        }

        public static IEnumerable<HttpMethod> GetOptions(this HttpResponseMessage response)
        {
            return response.Content.Headers.GetOptions();
        }
    }
}
