using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Serialization
{
    public interface IBind<TFrom>
    {
        TResult Bind<TResult>(TFrom value, Type type, string path, MemberInfo member,
            Func<object, TResult> onBound,
            Func<TResult> onFailedToBind);
    }
}
