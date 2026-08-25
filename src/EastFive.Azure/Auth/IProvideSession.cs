using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Azure.Auth
{
    public interface IProvideSession
    {
        Task<bool> SupportsSessionAsync(Session session);
    }
}
