using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Linq.Async
{
    public interface IGroupingAsync<out TKey, out TElement> : IEnumerableAsync<TElement>
    {
        TKey Key { get; }
    }
}
