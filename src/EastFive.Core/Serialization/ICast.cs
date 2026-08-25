using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Serialization
{
    public interface ICast<TTo>
    {
        TResult Cast<TResult>(object value, 
                Type valueType, string path, MemberInfo member,
            Func<TTo, TResult> onValue,
            Func<TResult> onNoCast);
    }
}
