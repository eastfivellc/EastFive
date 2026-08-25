using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace EastFive.Security.Crypto
{
	public static class Aes
	{
        #region GUID data
        public static Guid AesEncrypt(this Guid dataToEncrypt, Guid secretAseKey)
        {
            using (var aes = System.Security.Cryptography.Aes.Create())
            {
		        #pragma warning disable SCS0013 // Potential usage of weak CipherMode
                aes.Mode = CipherMode.ECB;
		        #pragma warning restore SCS0013 // Potential usage of weak CipherMode
                
		        aes.Padding = PaddingMode.None;
                aes.Key = secretAseKey.ToByteArray();

                using (var e = aes.CreateEncryptor())
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, e, CryptoStreamMode.Write))
                    {
                        cs.Write(dataToEncrypt.ToByteArray(), 0, 16);
                    }
                    return new Guid(ms.ToArray());
                }
            }
        }

        public static Guid AesDecrypt(this Guid dataToDecrypt, Guid secretAseKey)
        {
            using (var aes = System.Security.Cryptography.Aes.Create())
            {
		        #pragma warning disable SCS0013 // Potential usage of weak CipherMode
                aes.Mode = CipherMode.ECB;
		        #pragma warning restore SCS0013 // Potential usage of weak CipherMode

                aes.Padding = PaddingMode.None;
                aes.Key = secretAseKey.ToByteArray();

                using (var d = aes.CreateDecryptor())
                using (var ms = new MemoryStream(dataToDecrypt.ToByteArray()))
                using (var cs = new CryptoStream(ms, d, CryptoStreamMode.Read))
                {
                    var bytes = Guid.Empty.ToByteArray();

                    // using .NET 6 approach recommended here: https://github.com/dotnet/runtime/issues/61398
                    int num = 0;
                    while (num < bytes.Length)
                    {
                        int bytesRead = cs.Read(bytes, num, bytes.Length - num);
                        if (bytesRead == 0)
                            break;
                        num += bytesRead;
                    }
                    return new Guid(bytes);
                }
            }
        }
        #endregion GUID data

        #region string data
        // as needed, call aes.GenerateIV() before calling this
        public static (byte[] encrypted, byte[] iv) AesEncrypt(this byte[] bytes, System.Security.Cryptography.Aes aes)
        {
            var iv = aes.IV;
            using (var e = aes.CreateEncryptor())
            using (var ms = new MemoryStream())
            {
                using (var cs = new CryptoStream(ms, e, CryptoStreamMode.Write))
                {
                    cs.Write(bytes, 0, bytes.Length);
                    cs.FlushFinalBlock();
                }
                return (ms.ToArray(), iv);
            }
        }

        // call aes.IV = stuff before calling this
        public static byte[] AesDecrypt(this byte[] encrypted, System.Security.Cryptography.Aes aes)
        {
            using (var d = aes.CreateDecryptor())
            using (var ms = new MemoryStream(encrypted))
            using (var cs = new CryptoStream(ms, d, CryptoStreamMode.Read))
            {
                var output = new byte[encrypted.Length];

                // using .NET 6 approach recommended here: https://github.com/dotnet/runtime/issues/61398
                int num = 0;
                while (num < output.Length)
                {
                    int bytesRead = cs.Read(output, num, output.Length - num);
                    if (bytesRead == 0)
                        break;
                    num += bytesRead;
                    if (num > output.Length)
                        num = output.Length;
                }
                var trimmed = new byte[num];
                Array.Copy(output, trimmed, num);
                return trimmed;
            }
        }
        #endregion string data
    }
}
