using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Auth
{
    public class ClaimEnableSessionAttribute : Attribute, IDeclareClaim
    {
        public const string Type = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/privatepersonalidentifier"; // System.Security.Claims.ClaimTypes.PPID;

        public Uri ClaimType => new Uri(Type);

        public string ClaimDescription => "Allows users to create sessions.";

        public string ClaimName => "Session";
    }
}
