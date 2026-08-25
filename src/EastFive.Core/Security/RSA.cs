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
        public static TResult RSAFromConfig<TResult>(this string configSettingName,
            Func<RSACryptoServiceProvider, TResult> success,
            Func<TResult> missingConfigurationSetting,
            Func<string, TResult> invalidConfigurationSetting)
        {
            return configSettingName.ConfigurationBase64Bytes(
                bytes =>
                {
                    var xml = Encoding.ASCII.GetString(bytes);
                    return xml.RSAFromXml(success, (why) => invalidConfigurationSetting(why));
                },
                onFailure: (why) => invalidConfigurationSetting(why),
                onNotSpecified: () => missingConfigurationSetting());
        }

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
        
        public static TResult FromConfig<TResult>(string configSettingName,
            Func<RSACryptoServiceProvider, TResult> success,
            Func<string, TResult> missingConfigurationSetting,
            Func<string, string, TResult> invalidConfigurationSetting)
        {
            return configSettingName.ConfigurationBase64Bytes(
                secretAsRSAXmlBytes =>
                {
                    var xml = Encoding.ASCII.GetString(secretAsRSAXmlBytes);
                    var rsaProvider = new RSACryptoServiceProvider();
                    try
                    {
                        rsaProvider.FromXmlString(xml);
                        return success(rsaProvider);
                    }
                    catch (CryptographicException ex)
                    {
                        return invalidConfigurationSetting(
                            configSettingName, ex.Message);
                    }
                },
                onFailure: why => invalidConfigurationSetting(
                    configSettingName, why),
                () => missingConfigurationSetting(configSettingName));
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
