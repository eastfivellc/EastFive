using EastFive.Collections.Generic;
using EastFive.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EastFive.Linq.Async
{
    public static partial class EnumerableAsync
    {

        private class DictionaryAsync<TElement, TKey, TValue> : IDictionaryAsync<TKey, TValue>
        {
            // TODO: Use singular enumeration

            private IEnumerableAsync<TElement> enumerable;
            private IEnumeratorAsync<TElement> tryNextEnumerator;
            bool isEnumerating = true;
            private Dictionary<TKey, TValue> dictionary;
            private AutoResetEvent dictionaryLock;
            private Func<TElement, TKey> keySelector;
            private Func<TElement, TValue> valueSelector;
            Func<TKey, TKey, bool> comparison;


            public DictionaryAsync(IEnumerableAsync<TElement> enumerable,
                Func<TElement, TKey> keySelector,
                Func<TElement, TValue> valueSelector,
                Func<TKey, TKey, bool> comparison = default(Func<TKey, TKey, bool>))
            {
                this.enumerable = enumerable;
                this.dictionary = new Dictionary<TKey, TValue>();
                this.keySelector = keySelector;
                this.valueSelector = valueSelector;
                this.tryNextEnumerator = enumerable.GetEnumerator();
                this.dictionaryLock = new AutoResetEvent(true);

                if (comparison.IsDefaultOrNull())
                    comparison = (key1, key2) => key1.Equals(key2);
                this.comparison = comparison;
            }

            public Task<TValue> this[TKey key]
            {
                get
                {
                    return TryGetValueAsync(key,
                        (v) => v,
                        () =>
                        {
                            throw new KeyNotFoundException(); 
                        });
                }
            }

            public IEnumerableAsync<TKey> Keys => enumerable.Select(this.keySelector);

            public IEnumerableAsync<TValue> Values => enumerable.Select(this.valueSelector);

            public Task<bool> ContainsKeyAsync(TKey key)
            {
                return TryGetValueAsync(key,
                    (v) => true,
                    () => false);
            }

            public IEnumeratorAsync<KeyValuePair<TKey, TValue>> GetEnumerator()
                => enumerable
                    .Select(
                        item => this.valueSelector(item).PairWithKey(this.keySelector(item)))
                    .GetEnumerator();

            private async Task<KeyValuePair<bool, TValue>> GetLockedValueAsync(TKey key)
            {
                dictionaryLock.WaitOne();
                try
                {
                    if (dictionary.ContainsKey(key))
                        return dictionary[key].PairWithKey(true);

                    if (!isEnumerating)
                        return default(TValue).PairWithKey(false);

                    while (true)
                    {
                        if (!await tryNextEnumerator.MoveNextAsync())
                        {
                            isEnumerating = false;
                            return default(TValue).PairWithKey(false);
                        }

                        var element = tryNextEnumerator.Current;
                        var elementKey = this.keySelector(element);
                        var elementValue = this.valueSelector(element);
                        dictionary.Add(elementKey, elementValue);
                        if (!comparison(elementKey, key))
                            continue;

                        return elementValue.PairWithKey(true);
                    }
                }
                catch(Exception)
                {
                    throw;
                }
                finally
                {
                    dictionaryLock.Set();
                }
            }

            public async Task<TResult> TryGetValueAsync<TResult>(TKey key, 
                Func<TValue, TResult> onValue,
                Func<TResult> onNotFound)
            {
                var kvp = await GetLockedValueAsync(key);
                if (kvp.Key)
                    return onValue(kvp.Value);

                return onNotFound();
            }
        }


        private class DynamicDictionaryAsync<TKey, TValue> : IDictionaryAsync<TKey, TValue>
        {
            // TODO: Use singular enumeration

            private Dictionary<TKey, TValue> dictionary;
            private Dictionary<TKey, Task<ISelectNextValue<TValue>>> inProgressDictionary;
            private NextValueDelegate<TKey, TValue> nextAsync;

            public DynamicDictionaryAsync(NextValueDelegate<TKey, TValue> nextAsync)
            {
                this.dictionary = new Dictionary<TKey, TValue>();
                this.inProgressDictionary = new Dictionary<TKey, Task<ISelectNextValue<TValue>>>();
                this.nextAsync = nextAsync;
            }

            public Task<TValue> this[TKey key]
            {
                get
                {
                    return TryGetValueAsync(key,
                        (v) => v,
                        () =>
                        {
                            throw new KeyNotFoundException();
                        });
                }
            }

            public IEnumerableAsync<TKey> Keys => this.Select(item => item.Key);

            public IEnumerableAsync<TValue> Values => this.Select(item => item.Value);

            public Task<bool> ContainsKeyAsync(TKey key)
            {
                return TryGetValueAsync(key,
                    (v) => true,
                    () => false);
            }

            public IEnumeratorAsync<KeyValuePair<TKey, TValue>> GetEnumerator()
            {
                KeyValuePair<TKey, TValue>[] dictionaryKvps;
                lock (dictionary)
                {
                    dictionaryKvps = dictionary.ToArray();
                }
                var dictionaryKvpsIndex = 0;
                var kvps = EnumerableAsync.Yield<KeyValuePair<TKey, TValue>>(
                    async (yieldReturn, yieldBreak) =>
                    {
                        if (dictionaryKvpsIndex < dictionaryKvps.Length)
                        {
                            dictionaryKvpsIndex = dictionaryKvpsIndex + 1;
                            var kvp = dictionaryKvps[dictionaryKvpsIndex];
                            return yieldReturn(kvp);
                        }

                        return await yieldBreak.AsTask();
                    });
                return kvps.GetEnumerator();
            }

            public async Task<TResult> TryGetValueAsync<TResult>(TKey key,
                Func<TValue, TResult> onValue,
                Func<TResult> onNotFound)
            {
                var value = default(TValue);
                bool containedKey;
                lock (dictionary)
                {
                    containedKey = dictionary.ContainsKey(key);
                    if (containedKey)
                        value = dictionary[key];
                }
                if (containedKey)
                    return onValue(value);

                var gettingValue = default(Task<ISelectNextValue<TValue>>);
                lock(inProgressDictionary)
                {
                    containedKey = inProgressDictionary.ContainsKey(key);
                    if (containedKey)
                    {
                        gettingValue = inProgressDictionary[key];
                    }
                    else
                    { 
                        gettingValue = this.nextAsync(key,
                            (v) => new SelectedNextValue<TValue>(v),
                            () => new NoNextValue<TValue>());
                        inProgressDictionary.Add(key, gettingValue);
                    }
                }

                var selecter = await gettingValue;
                if (selecter.Selected)
                {
                    lock (dictionary)
                    {
                        dictionary.AddOrReplace(key, selecter.Value);
                    }
                    return onValue(selecter.Value);
                }

                return onNotFound();
            }
        }

        public static IDictionaryAsync<TKey, TValue> ToDictionary<TElement, TKey, TValue>(this IEnumerableAsync<TElement> enumerable,
            Func<TElement, TKey> keySelector,
            Func<TElement, TValue> valueSelector)
        {
            return new DictionaryAsync<TElement, TKey, TValue>(enumerable, keySelector, valueSelector);
        }

        public static IDictionaryAsync<TKey, TValue> ToDictionary<TKey, TValue>(this IEnumerableAsync<KeyValuePair<TKey, TValue>> enumerable)
        {
            return new DictionaryAsync<KeyValuePair<TKey, TValue>, TKey, TValue>(enumerable, 
                (kvp) => kvp.Key,
                (kvp) => kvp.Value);
        }

        public static IEnumerableAsync<TKey> SelectKeys<TKey, TValue>(this IEnumerableAsync<KeyValuePair<TKey, TValue>> enumerable)
        {
            return enumerable.Select(kvp => kvp.Key);
        }

        public static IEnumerableAsync<TValue> SelectValues<TKey, TValue>(this IEnumerableAsync<KeyValuePair<TKey, TValue>> enumerable)
        {
            return enumerable.Select(kvp => kvp.Value);
        }

        public interface ISelectNextValue<T>
        {
            bool Selected { get; }
            T Value { get; }
        }

        private struct SelectedNextValue<T> : ISelectNextValue<T>
        {
            public SelectedNextValue(T v)
            {
                this.Value = v;
            }

            public bool Selected => true;

            public T Value { get; private set; }
        }

        private struct NoNextValue<T> : ISelectNextValue<T>
        {
            public bool Selected => false;

            public T Value => throw new NotImplementedException();
        }

        public delegate Task<ISelectNextValue<TValue>> NextValueDelegate<TKey, TValue>(TKey key,
            Func<TValue, ISelectNextValue<TValue>> onValue,
            Func<ISelectNextValue<TValue>> onValueNotAvailable);


        public static IDictionaryAsync<TKey, TValue> DictionaryAsyncStart<TKey, TValue>(
            NextValueDelegate<TKey, TValue> generate)
        {
            return new DynamicDictionaryAsync<TKey, TValue>(generate);
        }
    }
}
