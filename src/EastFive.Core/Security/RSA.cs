using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using EastFive.Security.Crypto;
using EastFive.Web.Configuration;

namespace EastFive.Security
{
    public static class RSA
    {
        /// <summary>An RSA key held as a secret: base64 of the RSA key XML. Configuration types read
        /// it from the secret store; there is no overload that takes a configuration key.</summary>
        public static TResult RSAFromSecret<TResult>(this EastFive.Configuration.Secret secret,
            Func<RSACryptoServiceProvider, TResult> success,
            Func<string, TResult> invalidKey)
            => secret.Reveal().RSAFromBase64(success, invalidKey);

        public static TResult RSAFromBase64<TResult>(this string secretAsRSAXmlBase64,
            Func<RSACryptoServiceProvider, TResult> success,
            Func<string, TResult> invalidToken)
        {
            try
            {
                var bytes = Convert.FromBase64String(secretAsRSAXmlBase64);
                var xml = Encoding.ASCII.GetString(bytes);
                return xml.RSAFromXml(success, invalidToken);
            }
            catch (FormatException ex)
            {
                return invalidToken(ex.Message);
            }
        }

        public static TResult RSAFromXml<TResult>(this string xml,
            Func<RSACryptoServiceProvider, TResult> success,
            Func<string, TResult> invalidToken)
        {
            var rsaProvider = new RSACryptoServiceProvider();
            try
            {
                rsaProvider.FromXmlString(xml);
                return success(rsaProvider);
            }
            catch (CryptographicException ex)
            {
                return invalidToken(ex.Message);
            }
        }
        
        public static TResult FromBase64String<TResult>(string secretAsRSAXmlBase64,
            Func<RSACryptoServiceProvider, TResult> success,
            Func<string, TResult> invalidToken)
        {
            var rsaProvider = new RSACryptoServiceProvider();
            try
            {
                rsaProvider.FromXmlString(secretAsRSAXmlBase64);
                return success(rsaProvider);
            }
            catch (CryptographicException ex)
            {
                return invalidToken(ex.Message);
            }
        }

        public static TResult Generate<TResult>(Func<string, string, TResult> success)
        {
            if (!OperatingSystem.IsWindows())
                throw new NotSupportedException("OS not supported");
            
            var cspParams = new CspParameters()
            {
                ProviderType = 1, // PROV_RSA_FULL
                Flags = CspProviderFlags.UseArchivableKey,
                KeyNumber = (int)KeyNumber.Exchange,
            };
            using (var rsaProvider = new RSACryptoServiceProvider(2048, cspParams))
            {
                // Export public key
                var publicKey = Convert.ToBase64String(
                    Encoding.ASCII.GetBytes(
                        rsaProvider.ToXmlString(false)));

                // Export private/public key pair
                var privateKey = Convert.ToBase64String(
                    Encoding.ASCII.GetBytes(
                        rsaProvider.ToXmlString(true)));

                return success(publicKey, privateKey);
            }
        }
    }
}
