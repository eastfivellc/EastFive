using System;
using System.Linq;
using System.Security.Cryptography;
using EastFive.Extensions;
using EastFive.Security;
using EastFive.Serialization;

namespace EastFive.Security
{
    public static class VoucherTools
    {
        public static TResult GenerateToken<TResult>(Guid authId, DateTime validUntilUtc,
            Func<string, TResult> success,
            Func<string, TResult> missingConfigurationSetting,
            Func<string, string, TResult> invalidConfigurationSetting)
        {
            return GenerateBytes(authId, validUntilUtc,
                tokenBytes =>
                {
                    var token = Convert.ToBase64String(tokenBytes);
                    return success(token);
                },
                missingConfigurationSetting,
                invalidConfigurationSetting);
        }

        public static TResult GenerateUrlToken<TResult>(Guid authId, DateTime validUntilUtc,
            Func<string, TResult> success,
            Func<string, TResult> missingConfigurationSetting,
            Func<string, string, TResult> invalidConfigurationSetting)
        {
            byte[] signatureData;
            var hashedData = ComputeHashData(authId, validUntilUtc, out signatureData);

            return VoucherConfiguration.WithKey(
                (trustedVoucherPrivateKey) =>
                {
                    using (trustedVoucherPrivateKey)
                    {
                        var signature = trustedVoucherPrivateKey.SignHash(
                        hashedData, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                        var compactHash = signature.SHA256Hash();
                        var tokenBytes = signatureData.Concat(compactHash).ToArray();
                        var token = tokenBytes.Base64UrlEncode(); //  System.Web.HttpServerUtility.UrlTokenEncode();
                        return success(token);
                    }
                },
                () => missingConfigurationSetting(nameof(VoucherConfiguration)),
                (issue) => invalidConfigurationSetting(
                    nameof(VoucherConfiguration), issue));
        }

        /// <summary>A URL token signed with a caller-supplied key, in any encoding <see cref="RsaPrivateKey"/> reads.</summary>
        public static TResult GenerateUrlToken<TResult>(Guid authId, DateTime validUntilUtc,
                string trustedVoucherPrivateKey,
            Func<string, TResult> success,
            Func<string, TResult> onInvalidKey)
            => RsaPrivateKey.Parse(trustedVoucherPrivateKey,
                key => success(GenerateUrlToken(authId, validUntilUtc, key)),
                onInvalidKey);

        public static string GenerateUrlToken(Guid authId, DateTime validUntilUtc, RsaPrivateKey trustedVoucherPrivateKey)
        {
            byte[] signatureData;
            var hashedData = ComputeHashData(authId, validUntilUtc, out signatureData);

            using var rsa = trustedVoucherPrivateKey.CreateRsa();
            var signature = rsa.SignHash(hashedData, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var compactHash = signature.SHA256Hash();
            var tokenBytes = signatureData.Concat(compactHash).ToArray();
            return tokenBytes.Base64UrlEncode(); // System.Web.HttpServerUtility.UrlTokenEncode(tokenBytes);
        }

        public static TResult GenerateBytes<TResult>(Guid authId, DateTime validUntilUtc,
            Func<byte[], TResult> success,
            Func<string, TResult> missingConfigurationSetting,
            Func<string, string, TResult> invalidConfigurationSetting)
        {
            byte[] signatureData;
            var hashedData = ComputeHashData(authId, validUntilUtc, out signatureData);

            return VoucherConfiguration.WithKey(
                (trustedVoucherPrivateKey) =>
                {
                    using (trustedVoucherPrivateKey)
                    {
                        var signature = trustedVoucherPrivateKey.SignHash(hashedData, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                        var tokenBytes = signatureData.Concat(signature).ToArray();
                        return success(tokenBytes);
                    }
                },
                () => missingConfigurationSetting(nameof(VoucherConfiguration)),
                (issue) => invalidConfigurationSetting(
                    nameof(VoucherConfiguration), issue));
        }

        public static TResult ValidateToken<TResult>(string accessToken,
            Func<Guid, TResult> success,
            Func<string, TResult> invalidToken,
            Func<string, TResult> tokenExpired,
            Func<string, TResult> invalidSignature,
            Func<string, TResult> missingConfigurationSetting,
            Func<string, string, TResult> invalidConfigurationSetting)
        {
            #region Parse token

            long validUntilTicks = 0;
            var authId = default(Guid);
            var validUntilUtc = default(DateTime);
            var providedSignature = new byte[] {};
            try
            {
                var tokenBytes = Convert.FromBase64String(accessToken);

                var guidSize = Guid.NewGuid().ToByteArray().Length;
                var dateTimeSize = sizeof(long);

                var authIdData = tokenBytes.Take(guidSize).ToArray();
                var validUntilUtcData = tokenBytes.Skip(guidSize).Take(dateTimeSize).ToArray();
                validUntilTicks = BitConverter.ToInt64(validUntilUtcData, 0);

                authId = new Guid(authIdData);
                validUntilUtc = new DateTime(validUntilTicks, DateTimeKind.Utc);
                providedSignature = tokenBytes.Skip(guidSize + dateTimeSize).ToArray();
            }
            catch (Exception ex)
            {
                return invalidToken(ex.Message);
            }
            #endregion

            if (validUntilTicks < DateTime.UtcNow.Ticks)
                return tokenExpired("Token has expired");

            byte[] signatureData;
            var hashedData = ComputeHashData(authId, validUntilUtc, out signatureData);

            return VoucherConfiguration.WithKey(
                (trustedVoucher) =>
                {
                    using (trustedVoucher)
                    {
                        if (!trustedVoucher.VerifyHash(hashedData, providedSignature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                            return invalidSignature("Cannot verify hash - authId: " + authId +
                               "   validUntilUtc: " + validUntilUtc +
                               "   hashedData: " + hashedData +
                               "   providedSignature: " + providedSignature);

                        return success(authId);
                    }
                },
                () => missingConfigurationSetting(nameof(VoucherConfiguration)),
                (issue) => invalidConfigurationSetting(
                    nameof(VoucherConfiguration), issue));
        }

        public static TResult ValidateUrlToken<TResult>(string accessToken,
            Func<Guid, TResult> success,
            Func<string, TResult> invalidToken,
            Func<string, TResult> tokenExpired,
            Func<string, TResult> invalidSignature,
            Func<string, TResult> missingConfigurationSetting,
            Func<string, string, TResult> invalidConfigurationSetting)
        {
            #region Parse token

            long validUntilTicks = 0;
            var authId = default(Guid);
            var validUntilUtc = default(DateTime);
            var providedSignature = new byte[] { };
            try
            {
                var tokenBytes = accessToken.Base64UrlDecode(); // System.Web.HttpServerUtility.UrlTokenDecode(accessToken);

                var guidSize = Guid.NewGuid().ToByteArray().Length;
                var dateTimeSize = sizeof(long);

                var authIdData = tokenBytes.Take(guidSize).ToArray();
                var validUntilUtcData = tokenBytes.Skip(guidSize).Take(dateTimeSize).ToArray();
                validUntilTicks = BitConverter.ToInt64(validUntilUtcData, 0);

                authId = new Guid(authIdData);
                validUntilUtc = new DateTime(validUntilTicks, DateTimeKind.Utc);
                providedSignature = tokenBytes.Skip(guidSize + dateTimeSize).ToArray();
            }
            catch (Exception ex)
            {
                return invalidToken(ex.Message);
            }
            #endregion

            if (validUntilTicks < DateTime.UtcNow.Ticks)
                return tokenExpired("Token has expired");

            return GenerateUrlToken(authId, validUntilUtc,
                tokenCorrect =>
                {
                    if (accessToken == tokenCorrect)
                        return success(authId);
                    return invalidSignature("Signature is incorrect.");
                },
                missingConfigurationSetting,
                invalidConfigurationSetting);
        }

        private static byte[] ComputeHashData(Guid authId, DateTime validUntilUtc, out byte[] signatureData)
        {
            var authIdData = authId.ToByteArray();
            var validUntilUtcData = BitConverter.GetBytes(validUntilUtc.Ticks);
            signatureData = authIdData.Concat(validUntilUtcData).ToArray();

            using (var algorithm = SHA256.Create())
            {
                var hashedData = algorithm.ComputeHash(signatureData);
                return hashedData;
            }
        }
    }
}
