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
        public const string ClientKey = "EastFive.Azure.Login.ClientKey";

        /// <summary>Client Key for login</summary>
        public const string ClientSecret = "EastFive.Azure.Login.ClientSecret";
    }
}
