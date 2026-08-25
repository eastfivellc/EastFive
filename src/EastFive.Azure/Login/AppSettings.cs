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
        [ConfigKey("Client ID for login", DeploymentOverrides.Suggested,
            DeploymentSecurityConcern = false,
            Location = "Stored in AST for given environment",
            PrivateRepositoryOnly = false,
            MoreInfo = "Should be of type GUID")]
        public const string ClientKey = "EastFive.Azure.Login.ClientKey";

        [ConfigKey("Client Key for login", DeploymentOverrides.Mandatory,
            DeploymentSecurityConcern = true,
            Location = "Stored in AST for given environment.",
            PrivateRepositoryOnly = true,
            MoreInfo = "Base64 string")]
        public const string ClientSecret = "EastFive.Azure.Login.ClientSecret";
    }
}
