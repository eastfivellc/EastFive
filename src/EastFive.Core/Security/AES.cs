using System;
using System.Security.Cryptography;
using EastFive.Web.Configuration;

namespace EastFive.Security
{
	public static class AES
	{
        public static TResult AESFromConfig<TResult>(string keyConfigurationName,
            Func<System.Security.Cryptography.Aes, TResult> onSuccess,
            Func<TResult> onNotSpecified,
            Func<string, TResult> onFailure)
        {
            return keyConfigurationName.ConfigurationBase64Bytes(
                (keyBytes) => 
                {
                    var keySize = keyBytes.Length * 8;
                    var aes = System.Security.Cryptography.Aes.Create();
                    aes.Padding = PaddingMode.ISO10126;
                    aes.KeySize = keySize;
                    aes.Key = keyBytes;
                    return onSuccess(aes);
                },
                onFailure,
                onNotSpecified);
        }

        public static TResult GenerateGuidKey<TResult>(
            Func<Guid, // key
                TResult> onSuccess)
        {
            using (var provider = System.Security.Cryptography.Aes.Create())
            {
                provider.KeySize = 128; // other key sizes won't fit in a GUID
                return onSuccess(
                    new Guid(provider.Key));
            }
        }

        public static TResult GenerateKey<TResult>(
        Func<string, // key
            TResult> onSuccess,
            int keySize = 256)
        {
            using (var provider = System.Security.Cryptography.Aes.Create())
            {
                provider.KeySize = keySize;
                return onSuccess(
                    Convert.ToBase64String(provider.Key));
            }
        }
    }
}

