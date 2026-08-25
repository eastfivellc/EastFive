using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Serialization
{
    public interface ISerialize<TToFrom> : ICast<TToFrom>, IBind<TToFrom>
    {
    }
}
