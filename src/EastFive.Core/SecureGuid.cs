using System;

namespace EastFive.Security
{
    public class SecureGuid
    {
        public static Guid Generate()
        {
            var guidData = new byte[0x10];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(guidData);
            }
            return new Guid(guidData);
        }
    }
}
