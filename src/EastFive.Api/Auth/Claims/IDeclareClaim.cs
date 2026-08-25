using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Auth
{
    public interface IDeclareClaim
    {
        string ClaimName { get; }
        Uri ClaimType { get; }
        string ClaimDescription { get; }
    }
}
