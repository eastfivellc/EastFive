using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Linq.Async
{
    public interface IDictionaryAsync<TKey, TValue> : IEnumerableAsync<KeyValuePair<TKey, TValue>>
    {
        Task<TValue> this[TKey key] { get;  }

        IEnumerableAsync<TKey> Keys { get; }

        IEnumerableAsync<TValue> Values { get; }

        Task<bool> ContainsKeyAsync(TKey key);

        Task<TResult> TryGetValueAsync<TResult>(TKey key,
                Func<TValue, TResult> value,
                Func<TResult> onNotFound);
    }
}
