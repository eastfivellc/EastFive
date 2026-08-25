using System;
using System.Collections;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;

using Microsoft.IdentityModel.Tokens;

using EastFive;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Security;
using EastFive.Security.Tokens;
using EastFive.Web.Configuration;

namespace EastFive.Security.Tokens
{
    public static class JwtTools
    {
        public static TResult ParseToken<TResult>(
            this string jwtEncodedString,
            Func<Claim[], TResult> success,
            Func<string, TResult> invalidToken,
            Func<string, TResult> missingConfigurationSetting,
            Func<string, string, TResult> invalidConfigurationSetting,
            string configNameOfIssuerToValidateAgainst = EastFive.Security.AppSettings.TokenIssuer,
            string configNameOfRsaKeyToValidateAgainst = EastFive.Security.AppSettings.TokenKey)
        {
            return configNameOfRsaKeyToValidateAgainst.RSAFromConfig(
                rsaProvider =>
                {
                    using (rsaProvider)
                        return configNameOfIssuerToValidateAgainst.ConfigurationString(
                            issuer =>
                            {
                                if (issuer.IsNullOrEmpty())
                                    return missingConfigurationSetting(configNameOfIssuerToValidateAgainst);

                                var validationParameters = new TokenValidationParameters()
                                {
                                    ValidateAudience = false,
                                    ValidIssuer = issuer,
                                    IssuerSigningKey = new Microsoft.IdentityModel.Tokens.RsaSecurityKey(rsaProvider),
                                    RequireExpirationTime = true,
                                };

                                var handler = new JwtSecurityTokenHandler();

                                var dontValidateToken = EastFive.Security.AppSettings.TokensAllValid.ConfigurationBoolean(
                                    v => v,
                                    onNotSpecified: () => false);

                                try
                                {
                                    if (dontValidateToken)
                                    {
                                        var jwtToken = handler.ReadJwtToken(jwtEncodedString);
                                        return success(jwtToken.Claims.ToArray());
                                    }

                                    if (handler.CanReadToken(jwtEncodedString))
                                    {
                                        var jwtToken = handler.ReadJwtToken(jwtEncodedString);
                                        if (IsTokenExpired(jwtToken, validationParameters.ClockSkew))
                                            return invalidToken("Token is expired.");
                                    }

                                    var principal = handler.ValidateToken(jwtEncodedString, validationParameters,
                                        out SecurityToken validatedToken);

                                    var claims = principal.Claims.ToArray();
                                    return EastFive.Web.Configuration.Settings.GetDateTime(
                                            EastFive.Web.AppSettings.TokenForceRefreshTime,
                                        (notValidBeforeTime) =>
                                        {
                                            if (validatedToken.ValidFrom < notValidBeforeTime)
                                                return EastFive.Web.Configuration.Settings.GetString(
                                                        EastFive.Web.AppSettings.TokenForceRefreshMessage,
                                                    (message) => invalidToken(message),
                                                    (why) => invalidToken(why));
                                            return success(claims);
                                        },
                                        (why) => success(claims));
                                }
                                catch (ArgumentException ex)
                                {
                                    return invalidToken(ex.Message);
                                }
                                catch (Microsoft.IdentityModel.Tokens.SecurityTokenInvalidIssuerException ex)
                                {
                                    return invalidToken(ex.Message);
                                }
                                catch (Microsoft.IdentityModel.Tokens.SecurityTokenExpiredException ex)
                                {
                                    return invalidToken(ex.Message);
                                }
                                catch (Microsoft.IdentityModel.Tokens.SecurityTokenException ex)
                                {
                                    return invalidToken(ex.Message);
                                }
                            },
                            missingConfigurationSetting);
                },
                () => missingConfigurationSetting(configNameOfRsaKeyToValidateAgainst),
                (issue) => invalidConfigurationSetting(
                    configNameOfRsaKeyToValidateAgainst, issue));
        }

        public static TResult CreateToken<TResult>(Uri scope,
            DateTime issued, TimeSpan duration,
            IEnumerable<Claim> claims,
            Func<string, TResult> tokenCreated,
            Func<string, TResult> missingConfigurationSetting,
            Func<string, string, TResult> invalidConfigurationSetting,
            string configNameOfIssuer = EastFive.Security.AppSettings.TokenIssuer,
            string configNameOfRSAKey = EastFive.Security.AppSettings.TokenKey,
            string configNameOfRSAAlgorithm = EastFive.Security.AppSettings.TokenAlgorithm,
            IEnumerable<KeyValuePair<string, string>> tokenHeaders = default)
        {
            return RSA.RSAFromConfig(configNameOfRSAKey,
                (rsaProvider) =>
                {
                    return configNameOfIssuer.ConfigurationString(
                        issuer =>
                        {
                            if (string.IsNullOrWhiteSpace(issuer))
                                return missingConfigurationSetting(configNameOfIssuer);
                            var algorithm = configNameOfRSAAlgorithm.ConfigurationString(
                                algorithm =>
                                {
                                    if (string.IsNullOrWhiteSpace(algorithm))
                                        return Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSha256Signature;
                                    return algorithm;
                                },
                                why => Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSha256Signature);
                            var jwt = rsaProvider.JwtToken(issuer, scope, claims,
                                        issued, duration, algorithm, tokenHeaders);
                            return tokenCreated(jwt);
                        },
                            (why) => missingConfigurationSetting(configNameOfIssuer));
                },
                () => missingConfigurationSetting("missing"),
                (issue) => invalidConfigurationSetting(
                    configNameOfRSAKey, issue));
        }

        public static TResult ParseToken<TResult>(
            this string jwtEncodedString,
            Func<JwtSecurityToken, TResult> onSuccess,
            Func<string, TResult> invalidToken)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(jwtEncodedString);

                return onSuccess(jwtToken);
            }
            catch (ArgumentException ex)
            {
                return invalidToken(ex.Message);
            }
            catch (SecurityTokenInvalidIssuerException ex)
            {
                return invalidToken(ex.Message);
            }
            catch (SecurityTokenExpiredException ex)
            {
                return invalidToken(ex.Message);
            }
            catch (SecurityTokenException ex)
            {
                return invalidToken(ex.Message);
            }
        }

        public static TResult ParseAndValidateToken<TResult>(
            this string jwtEncodedString,
            Func<Claim[], TResult> success,
            Func<string, TResult> invalidToken,
            Func<string, TResult> missingConfigurationSetting,
            Func<string, string, TResult> invalidConfigurationSetting,
            string configNameOfIssuerToValidateAgainst = AppSettings.TokenIssuer,
            string configNameOfRsaKeyToValidateAgainst = AppSettings.TokenKey)
        {
            var result = configNameOfRsaKeyToValidateAgainst.RSAFromConfig(
                rsaProvider =>
                {
                    return configNameOfIssuerToValidateAgainst.ConfigurationString(
                        (issuer) =>
                        {
                            if (string.IsNullOrEmpty(issuer))
                                return missingConfigurationSetting(configNameOfIssuerToValidateAgainst);

                            var validationParameters = new TokenValidationParameters()
                            {
                                ValidateAudience = false,
                                ValidIssuer = issuer,
                                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.RsaSecurityKey(rsaProvider),
                                RequireExpirationTime = true,
                                // TryAllIssuerSigningKeys = true,
                                IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
                                {
                                    // If the token has a key identifier (kid) in its header, you can use it to
                                    // look up the signing key. This example assumes you have a method
                                    // GetSigningKeysForKeyId that retrieves the appropriate keys.
                                    // if (!string.IsNullOrEmpty(kid))
                                    // {
                                    //     var keys = new List<SecurityKey>();
                                    //     var key = validationParameters.IssuerSigningKey;
                                    //     if (key is RsaSecurityKey rsaKey && rsaKey.KeyId == kid)
                                    //     {
                                    //         keys.Add(key);
                                    //     }
                                    //     return keys;
                                    // }

                                    // If no kid is present, return all possible signing keys.
                                    return new List<SecurityKey> { validationParameters.IssuerSigningKey };
                                },
                            };

                            try
                            {
                                var handler = new JwtSecurityTokenHandler();
                                if (handler.CanReadToken(jwtEncodedString))
                                {
                                    var jwtToken = handler.ReadJwtToken(jwtEncodedString);
                                    if (IsTokenExpired(jwtToken, validationParameters.ClockSkew))
                                        return invalidToken("Token is expired.");
                                }

                                //var principal = handler.ReadJwtToken(jwtEncodedString);
                                var principal = handler.ValidateToken(jwtEncodedString, validationParameters,
                                    out SecurityToken validatedToken);

                                var claims = principal.Claims.ToArray();
                                return success(claims);

                                //return EastFive.Web.Configuration.Settings.GetDateTime(
                                //        EastFive.Web.AppSettings.TokenForceRefreshTime,
                                //    (notValidBeforeTime) =>
                                //    {
                                //        if (validatedToken.ValidFrom < notValidBeforeTime)
                                //            return EastFive.Web.Configuration.Settings.GetString(
                                //                    EastFive.Web.AppSettings.TokenForceRefreshMessage,
                                //                (message) => invalidToken(message),
                                //                (why) => invalidToken(why));
                                //        return success(claims);
                                //    },
                                //    (why) => success(claims));
                            }
                            catch (ArgumentException ex)
                            {
                                return invalidToken(ex.Message);
                            }
                            catch (SecurityTokenInvalidIssuerException ex)
                            {
                                return invalidToken(ex.Message);
                            }
                            catch (SecurityTokenExpiredException ex)
                            {
                                return invalidToken(ex.Message);
                            }
                            catch (SecurityTokenException ex)
                            {
                                return invalidToken(ex.Message);
                            }
                        },
                        (why) => missingConfigurationSetting(why));

            //var result = RSA.FromConfig(configNameOfRsaKeyToValidateAgainst,
            //    (rsaProvider) =>
            //    {
            //        using (rsaProvider)
            //        {
            //            var issuer = ConfigurationContext.Instance.AppSettings[configNameOfIssuerToValidateAgainst];
            //            if (string.IsNullOrEmpty(issuer))
            //                return missingConfigurationSetting(configNameOfIssuerToValidateAgainst);

            //            var validationParameters = new TokenValidationParameters()
            //            {
            //                ValidateAudience = false,
            //                ValidIssuer = issuer,
            //                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.RsaSecurityKey(rsaProvider),
            //                RequireExpirationTime = true,
            //            };

            //            try
            //            {
            //                Microsoft.IdentityModel.Tokens.SecurityToken validatedToken;
            //                var handler = new JwtSecurityTokenHandler();
            //                var principal = handler.ValidateToken(jwtEncodedString, validationParameters, out validatedToken);

            //                // TODO: Check if token is still valid at current date / time?
            //                var claims = principal.Claims.ToArray();

            //                return EastFive.Web.Configuration.Settings.GetDateTime(
            //                        EastFive.Web.AppSettings.TokenForceRefreshTime,
            //                    (notValidBeforeTime) =>
            //                    {
            //                        if (validatedToken.ValidFrom < notValidBeforeTime)
            //                            return EastFive.Web.Configuration.Settings.GetString(
            //                                    EastFive.Web.AppSettings.TokenForceRefreshMessage,
            //                                (message) => invalidToken(message),
            //                                (why) => invalidToken(why));
            //                        return success(claims);
            //                    },
            //                    (why) => success(claims));
            //            }
            //            catch (ArgumentException ex)
            //            {
            //                return invalidToken(ex.Message);
            //            }
            //            catch (Microsoft.IdentityModel.Tokens.SecurityTokenInvalidIssuerException ex)
            //            {
            //                return invalidToken(ex.Message);
            //            }
            //            catch (Microsoft.IdentityModel.Tokens.SecurityTokenExpiredException ex)
            //            {
            //                return invalidToken(ex.Message);
            //            }
            //            catch (Microsoft.IdentityModel.Tokens.SecurityTokenException ex)
            //            {
            //                return invalidToken(ex.Message);
            //            }
            //        }
                },
                () => missingConfigurationSetting(configNameOfRsaKeyToValidateAgainst),
                (issue) => invalidConfigurationSetting(
                    configNameOfRsaKeyToValidateAgainst, issue));
            return result;
        }

        public static TResult CreateToken<TResult>(
            this string secretAsRSAXmlBase64,
            string issuer,
            Uri scope,
            DateTime issued, TimeSpan duration,
            IEnumerable<Claim> claims,
            Func<string, TResult> tokenCreated,
            Func<string, TResult> onInvalidSecret)
        {
            return RSA.RSAFromBase64(secretAsRSAXmlBase64,
                (rsaProvider) =>
                {
                    using (rsaProvider)
                    {
                        var token = rsaProvider.JwtToken(issuer, scope, claims,
                            issued, duration,
                            Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSha256Signature);
                        return tokenCreated(token);
                    }
                },
                onInvalidSecret);
        }

        public static string JwtToken(this System.Security.Cryptography.RSACryptoServiceProvider rsaProvider,
            string issuer, Uri scope,
            IEnumerable<Claim> claims,
            DateTime issued, TimeSpan duration,
            string algorithm,
            IEnumerable<KeyValuePair<string, string>> tokenHeaders = default)
        {
            var securityKey = new Microsoft.IdentityModel.Tokens.RsaSecurityKey(rsaProvider);

            var signature = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                securityKey, algorithm);
            var expires = (issued + duration);
            var token = new JwtSecurityToken(issuer, scope.AbsoluteUri, claims, issued, expires, signature);

            foreach (var kvp in tokenHeaders.NullToEmpty())
                token.Header.Add(kvp.Key, kvp.Value);

            if (!token.Header.ContainsKey(JwtHeaderParameterNames.Kid))
            {
                var keyId = GetSecurityKeyId();
                token.Header.Add(JwtHeaderParameterNames.Kid, keyId);
            }
            
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.WriteToken(token);
            return jwt;

            // Generate a stable key identifier from the RSA parameters
            string GetSecurityKeyId()
            {
                if (securityKey.KeyId.HasBlackSpace())
                    return securityKey.KeyId;
                    
                // Create a hash of the RSA parameters to use as a key ID
                var parameters = rsaProvider.ExportParameters(false);
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                var hash = sha256.ComputeHash(parameters.Modulus);
                return Convert.ToBase64String(hash)[..16]; // Use first 16 chars
            }
        }
        
        

        public static TResult CreateToken<TResult>(Uri scope,
            DateTime issued, TimeSpan duration,
            IEnumerable<Claim> claims,
            Func<string, TResult> tokenCreated,
            Func<string, TResult> missingConfigurationSetting,
            Func<string, string, TResult> invalidConfigurationSetting,
            string configNameOfIssuer = EastFive.Security.AppSettings.TokenIssuer,
            string configNameOfRSAKey = EastFive.Security.AppSettings.TokenKey)
        {
            return configNameOfRSAKey.RSAFromConfig(
                (rsaProvider) =>
                {
                    return configNameOfIssuer.ConfigurationString(
                        issuer =>
                        {
                            if (string.IsNullOrWhiteSpace(issuer))
                                return missingConfigurationSetting(configNameOfIssuer);

                            var jwt = rsaProvider.JwtToken(issuer, scope, claims,
                                issued, duration, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSha256Signature);
                            return tokenCreated(jwt);
                        },
                        missingConfigurationSetting);
                },
                () => missingConfigurationSetting(configNameOfRSAKey),
                (issue) => invalidConfigurationSetting(
                    configNameOfRSAKey, issue));
        }

        public static TResult GetGuidValue<TResult>(this Claim[] claims, string key,
            Func<Guid, TResult> found,
            Func<TResult> notFound = default,
            Func<TResult> invalid = default)
        {
            if (default(Func<TResult>) == notFound)
                notFound = () => { throw new Exception("Session not found in claims"); };
            if (default(Func<TResult>) == invalid)
                invalid = notFound;
            return claims
                .Where(claim => claim.Type.DoesEqual(key))
                .FirstOrDefault(
                    (claim) =>
                    {
                        Guid sessionId;
                        if (Guid.TryParse(claim.Value, out sessionId))
                            return found(sessionId);
                        return invalid();
                    },
                    notFound);
        }

        private static bool DoesEqual(this string strA, string strB, bool ignoreCase = false)
        {
            return String.Compare(strA, strB, ignoreCase) == 0;
        }

        private static bool IsTokenExpired(JwtSecurityToken jwtToken, TimeSpan clockSkew)
        {
            if (jwtToken.ValidTo == DateTime.MinValue)
                return false;

            var expiresBefore = DateTime.UtcNow.Subtract(clockSkew);
            return jwtToken.ValidTo < expiresBefore;
        }

    }
}