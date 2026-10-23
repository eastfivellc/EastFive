using EastFive.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Azure.Login
{
    [Config]
    public static class AppSettings
    {
        /// <summary>Client ID for login</summary>
        [System.Obsolete("Read EastFiveLoginConfiguration.ClientKey through typed configuration; its key is EastFiveLoginConfiguration.ClientKeyKey.")]
        public const string ClientKey = EastFive.Azure.Auth.EastFiveLoginConfiguration.ClientKeyKey;

    }
}
