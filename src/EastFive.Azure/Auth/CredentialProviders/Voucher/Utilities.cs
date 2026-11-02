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
                    ?? throw new EastFive.Web.ConfigurationException(EastFive.Security.VoucherConfiguration.ProviderIdKey, typeof(Uri), "The voucher provider is not configured."),
                issue => throw new EastFive.Web.ConfigurationException(issue.Key, typeof(Uri), issue.ToString()));
        }

        public static string GenerateToken(Guid authId, DateTime validUntilUtc)
        {
            byte[] signatureData;
            var hashedData = ComputeHashData(authId, validUntilUtc, out signatureData);
            return EastFive.Configuration.StartupConfiguration.Get<EastFive.Security.VoucherConfiguration, string>(
                configuration =>
                {
                    using var trustedVoucherPrivateKey = configuration.SigningKey.CreateRsa();
                    var signature = trustedVoucherPrivateKey.SignHash(hashedData, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    var tokenBytes = signatureData.Concat(signature).ToArray();
                    return Convert.ToBase64String(tokenBytes);
                },
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
                configuration =>
                {
                    using var trustedVoucher = configuration.SigningKey.CreateRsa();
                    if (!trustedVoucher.VerifyHash(hashedData, providedSignature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                        return invalidSignature("Cannot verify hash - authId: " + authId +
                            "   validUntilUtc: " + validUntilUtc +
                            "   hashedData: " + hashedData +
                            "   providedSignature: " + providedSignature);

                    return success(authId);
                },
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
