using EastFive.Security;
using System;
using System.Configuration;
using System.Linq;
using System.Security.Cryptography;

namespace EastFive.Azure.Auth.Voucher
{
    public static class Utilities
    {
        public static Uri GetTrustedProviderId()
        {
            // Was System.Configuration's AppSettings, always empty on .NET Core (so this threw).
            return EastFive.Configuration.StartupConfiguration.Get<EastFive.Security.VoucherConfiguration, Uri>(
                configuration => configuration.ProviderId
                    ?? throw new EastFive.Web.ConfigurationException(EastFive.Security.AppSettings.CredentialProviderVoucherProviderId, typeof(Uri), "The voucher provider is not configured."),
                issue => throw new EastFive.Web.ConfigurationException(issue.Key, typeof(Uri), issue.ToString()));
        }

        public static string GenerateToken(Guid authId, DateTime validUntilUtc)
        {
            byte[] signatureData;
            var hashedData = ComputeHashData(authId, validUntilUtc, out signatureData);
            return EastFive.Configuration.StartupConfiguration.Get<EastFive.Security.VoucherConfiguration, string>(
                configuration => configuration.SigningKey.RSAFromSecret(
                (trustedVoucherPrivateKey) =>
                {
                    using (trustedVoucherPrivateKey)
                    {
                        var signature = trustedVoucherPrivateKey.SignHash(hashedData, CryptoConfig.MapNameToOID("SHA256"));
                        var tokenBytes = signatureData.Concat(signature).ToArray();
                        return Convert.ToBase64String(tokenBytes);
                    }
                },
                (why) => throw new EastFive.Web.ConfigurationException(nameof(EastFive.Security.VoucherConfiguration.SigningKey), typeof(EastFive.Security.VoucherConfiguration), why)),
                issue => throw new EastFive.Web.ConfigurationException(issue.Key, typeof(EastFive.Security.VoucherConfiguration), issue.ToString()));
        }

        public static T ValidateToken<T>(string accessToken,
            Func<Guid, T> success,
            Func<string, T> tokenExpired,
            Func<string, T> invalidToken,
            Func<string, T> invalidSignature,
            Func<string, T> unspecifiedConfiguration)
        {
            #region Parse token

            long validUntilTicks = 0;
            var authId = default(Guid);
            var validUntilUtc = default(DateTime);
            var providedSignature = new byte[] { };
            try
            {
                var tokenBytes = Convert.FromBase64String(accessToken);

                var guidSize = Guid.NewGuid().ToByteArray().Length;
                const int dateTimeSize = sizeof(long);

                var authIdData = tokenBytes.Take(guidSize).ToArray();
                var validUntilUtcData = tokenBytes.Skip(guidSize).Take(dateTimeSize).ToArray();
                validUntilTicks = BitConverter.ToInt64(validUntilUtcData, 0);

                authId = new Guid(authIdData);
                validUntilUtc = new DateTime(validUntilTicks, DateTimeKind.Utc);
                providedSignature = tokenBytes.Skip(guidSize + dateTimeSize).ToArray();
            }
            catch (Exception ex)
            {
                invalidSignature("Cannot parse access token: " + ex.Message);
            }

            #endregion

            if (validUntilTicks < DateTime.UtcNow.Ticks)
                return tokenExpired("Token has expired");

            byte[] signatureData;
            var hashedData = ComputeHashData(authId, validUntilUtc, out signatureData);
            return EastFive.Configuration.StartupConfiguration.Get<EastFive.Security.VoucherConfiguration, T>(
                configuration => configuration.SigningKey.RSAFromSecret(
                (trustedVoucher) =>
                {
                    using (trustedVoucher)
                    {
                        if (!trustedVoucher.VerifyHash(hashedData, CryptoConfig.MapNameToOID("SHA256"), providedSignature))
                            return invalidSignature("Cannot verify hash - authId: " + authId +
                                "   validUntilUtc: " + validUntilUtc +
                                "   hashedData: " + hashedData +
                                "   providedSignature: " + providedSignature);

                        return success(authId);
                    }
                },
                (why) => unspecifiedConfiguration($"{nameof(EastFive.Security.VoucherConfiguration)}: {why}")),
                issue => unspecifiedConfiguration(issue.ToString()));
        }

        private static byte [] ComputeHashData(Guid authId, DateTime validUntilUtc, out byte [] signatureData)
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
