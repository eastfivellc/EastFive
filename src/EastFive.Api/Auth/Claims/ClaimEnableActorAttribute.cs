using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Auth
{
    public class ClaimEnableActorAttribute : Attribute, IDeclareClaim
    {
        public const string Type = System.Security.Claims.ClaimTypes.Actor;

        public Uri ClaimType => new Uri(Type);

        public string ClaimDescription => "Allows users to be identified by an actor id.";

        public string ClaimName => "Actor";
    }
}
