using EastFive.Extensions;
using EastFive.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EastFive.Collections.Generic
{
    public static class DictionaryExtensions
    {
        public static IDictionary<TKey, TValue> AddOrReplace<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value)
        {
            if(dictionary.IsDefaultOrNull())
            {
                return new Dictionary<TKey, TValue>()
                {
                    { key, value }
                };
            }
            if (dictionary.ContainsKey(key))
            {
                dictionary[key] = value;
                return dictionary;
            }
            dictionary.Add(key, value);
            return dictionary;
        }

        public static IDictionary<TKey, TValue> AddOrReplace<TKey, TValue>(this IDictionary<TKey, TValue> dictionary,
            TKey key, TValue value,
            out bool replaced, out TValue oldValue)
        {
            if (dictionary.IsDefaultOrNull())
            {
                replaced = false;
                oldValue = default(TValue);
                return new Dictionary<TKey, TValue>()
                {
                    { key, value }
                };
            }

            if (dictionary.ContainsKey(key))
            {
                replaced = true;
                oldValue = dictionary[key];
                dictionary[key] = value;
                return dictionary;
            }

            dictionary.Add(key, value);
            replaced = false;
            oldValue = default(TValue);
            return dictionary;
        }

        public static IDictionary<TValue, TKey> SwapKeyValue<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
        {
            return dictionary.Select(kvp => kvp.Value.PairWithValue(kvp.Key)).ToDictionary();
        }

        public static TResult ToDictionary<TKey, TValue, TKeyDictionary, TValueDictionary, TResult>(this IEnumerable<KeyValuePair<TKey, TValue>> kvpItems,
            Func<KeyValuePair<TKey, TValue>, TKeyDictionary> selectKey,
            Func<KeyValuePair<TKey, TValue>, TValueDictionary> selectValue,
            Func<Dictionary<TKeyDictionary, TValueDictionary>, KeyValuePair<TKey, TValue>[], TResult> dictionaryAndDuplicates)
        {
            var hashSet = new HashSet<TKey>();
            var duplicates = new KeyValuePair<TKey, TValue>[] { };
            var dictionary = kvpItems
                .Select(
                    kvp =>
                    {
                        if (hashSet.Contains(kvp.Key))
                        {
                            duplicates = duplicates.Append(kvp).ToArray();
                            return default(KeyValuePair<TKeyDictionary, TValueDictionary>?);
                        }
                        hashSet.Add(kvp.Key);
                        return selectKey(kvp).PairWithValue(selectValue(kvp));
                    })
                .SelectWhereHasValue()
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            return dictionaryAndDuplicates(dictionary, duplicates);
        }

        public static TResult ToDictionary<TKey, TValue, TResult>(this IEnumerable<KeyValuePair<TKey, TValue>> kvpItems,
            Func<Dictionary<TKey, TValue>, KeyValuePair<TKey, TValue>[], TResult> dictionaryAndDuplicates)
        {
            return kvpItems.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value,
                dictionaryAndDuplicates);
        }

        public static IDictionary<TKey, TValue> Append<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value)
        {
            dictionary.Add(key, value);
            return dictionary;
        }

        public static Dictionary<TKey, TValue[]> ToDictionaryDistinct<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> kvpItems,
            Func<TKey, int> hash = default(Func<TKey, int>))
            where TKey : IComparable<TKey>
        {
            Func<KeyValuePair<TKey, TValue>, KeyValuePair<TKey, TValue>, int> comparison =
                (kvp1, kvp2) => kvp1.Key.CompareTo(kvp2.Key);
            return kvpItems
                .Distinct(comparison.ToEqualityComparer())
                .SelectKeys()
                .Select(key => key.PairWithValue(
                    kvpItems
                        .Where(kvp => key.CompareTo(kvp.Key) == 0)
                        .Select(kvp => kvp.Value)
                        .ToArray()))
                .ToDictionary();
        }
        
        public static Dictionary<TKey, TValue[]> ToDictionaryCollapsed<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> kvpItems,
            Func<TKey, TKey, bool> areEqual,
            Func<TKey, int> hash = default(Func<TKey, int>))
        {
            Func<KeyValuePair<TKey, TValue>, KeyValuePair<TKey, TValue>, int> fullComparison =
                (kvp1, kvp2) => areEqual(kvp1.Key, kvp2.Key)? 0 : -1;
            return kvpItems
                .Distinct(fullComparison.ToEqualityComparer())
                .SelectKeys()
                .Select(key => key.PairWithValue(
                    kvpItems
                        .Where(kvp => areEqual(key, kvp.Key))
                        .Select(kvp => kvp.Value)
                        .ToArray()))
                .ToDictionary();
        }

        public static IEnumerable<TValue> SelectValues<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> dictionary)
        {
            return dictionary.NullToEmpty().Select(kvp => kvp.Value);
        }

        public static IEnumerable<TResult> SelectValues<TKey, TValue, TResult>(this IEnumerable<KeyValuePair<TKey, TValue>> dictionary,
            Func<TValue, TResult> selector)
        {
            return dictionary.Select(kvp => selector(kvp.Value));
        }

        public static IEnumerable<TKey> SelectKeys<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> dictionary)
        {
            return dictionary.NullToEmpty().Select(kvp => kvp.Key);
        }

        public static IEnumerable<TResult> SelectKeys<TKey, TValue, TResult>(this IEnumerable<KeyValuePair<TKey, TValue>> dictionary,
            Func<TKey, TResult> selector)
        {
            return dictionary.Select(kvp => selector(kvp.Key));
        }
        
        public static IDictionary<TKey, TValue> IntersectKeys<TKey, TValue>(this IDictionary<TKey, TValue> dictionary,
                IEnumerable<TKey> keys)
        {
            return dictionary.Where(kvp => keys.Contains(kvp.Key)).ToDictionary();
        }
        
        public static IDictionary<TKey, TValueResult> IntersectKeys<TKey, TValue1, TValue2, TValueResult>(this IDictionary<TKey, TValue1> dictionary1,
                IDictionary<TKey, TValue2> dictionary2,
                Func<TValue1, TValue2, TValueResult> selector)
        {
            return dictionary1
                .Where(kvp => dictionary2.ContainsKey(kvp.Key))
                .Select(kvp => kvp.Key.PairWithValue(selector(kvp.Value, dictionary2[kvp.Key])))
                .ToDictionary();
        }

        public static TResult WhereKey<TKey, TValue, TResult>(this IDictionary<TKey, TValue> dictionary,
                Func<KeyValuePair<TKey, TValue>, bool> predicate,
            Func<KeyValuePair<TKey, TValue>, TResult> onKeyFound,
            Func<TResult> onKeyNotFound)
        {
            var matching = dictionary.Where(predicate).ToArray();
            if (matching.Length > 0)
                return onKeyFound(matching[0]);
            return onKeyNotFound();
        }

        public static HashSet<T> AsHashSet<T>(this IEnumerable<T> items)
        {
            return new HashSet<T>(items);
        }

        public static HashSet<T> AsHashSet<T>(this IEnumerable<T> items, IEqualityComparer<T> equalityComparer)
        {
            return new HashSet<T>(items, equalityComparer);
        }

        public static Func<T, bool> AsPredicate<T>(this HashSet<T> lookups)
        {
            return (v) => lookups.Contains(v);
        }

        public static Func<TPredicate, bool> AsPredicate<T, TPredicate>(this HashSet<T> lookups,
            Func<TPredicate, T> translation)
        {
            return (v) => lookups.Contains(translation(v));
        }

        public static IDictionary<string, string> AsDictionary(this System.Collections.Specialized.NameValueCollection nameValueCollection)
        {
            return nameValueCollection.AllKeys
                .Where(key => key != null)
                .Select(key => key.PairWithValue(nameValueCollection[key]))
                .ToDictionary();
        }

        public static IEnumerable<(string key, string value)> ToEnumerable(this System.Collections.Specialized.NameValueCollection nameValueCollection)
        {
            if (nameValueCollection.IsDefaultOrNull())
                return new (string, string)[] { };

            if(nameValueCollection.AllKeys.IsDefaultOrNull())
                return new (string, string)[] { };

            return nameValueCollection.AllKeys
                .Where(key => key != null)
                .Select(key => (key, nameValueCollection[key]));
        }

        /// <summary>
        /// If <paramref name="key"/> is contained in <paramref name="dictionary"/> then call <paramref name="onDictionaryHasValue"/>.
        /// Otherwise, call <paramref name="onAddValueSinceMissing"/> to add the key.
        /// </summary>
        /// <typeparam name="TKey">Type of keys in IDictionary</typeparam>
        /// <typeparam name="TValue">Type of values in IDictionary</typeparam>
        /// <typeparam name="TResult">Return type for all cases.</typeparam>
        /// <param name="dictionary">Dictionary to check if contains <paramref name="key"/></param>
        /// <param name="key">Key to check for in <paramref name="dictionary"/></param>
        /// <param name="onAddValueSinceMissing">Is called if <paramref name="key"/> is not found in <paramref name="dictionary"/>. A callback is provided to optionally
        /// create a value for <paramref name="key"/> in <paramref name="dictionary"/>.If the callback is invoked it will invoke <paramref name="onDictionaryHasValue"/> and return its TResult.</param>
        /// <param name="onDictionaryHasValue">Invoked if <paramref name="key"/> is found in <paramref name="dictionary"/> or added to <paramref name="dictionary"/> by <paramref name="onAddValueSinceMissing"/>.
        /// Arguments are (TValue valueForKey, IDictionary[TKey, TValue], bool wasAdded, dictionaryUpdated).</param>
        /// <returns>TResult from callabcks</returns>
        /// <example>
        /// <code>
        /// var result = await fieldInfoCache.AddIfMissing(property.Key,
        ///   async (add) => await await ooContext.ProductProperties.GetByIdAsync(property.Key,
        ///       (productPropertyFound) => add(productPropertyFound),
        ///       () => skip(fieldInfoCache));
        ///   (added, fieldInfo, fieldInfoCacheUpdated) => next(fieldInfo, cache));
        /// </code>
        /// </example>
        public static TResult AddIfMissing<TKey, TValue, TResult>(this IDictionary<TKey, TValue> dictionary, TKey key,
            Func<Func<TValue, TResult>, TResult> onAddValueSinceMissing,
            Func<TValue, IDictionary<TKey, TValue>, bool, TResult> onDictionaryHasValue)
        {
            if (dictionary.ContainsKey(key))
                return onDictionaryHasValue(dictionary[key], dictionary, false);
            return onAddValueSinceMissing(
                (value) =>
                {
                    dictionary.Add(key, value);
                    return onDictionaryHasValue(value, dictionary, true);
                });
        }

        public static TValueAs IfKeyOrValueAs<TKey, TValueAs>(this IDictionary<TKey, object> dictionary, TKey key, TValueAs defaultValue)
            where TValueAs : class
        {
            if (!dictionary.ContainsKey(key))
                return defaultValue;
            var value = dictionary[key];
            if (!(value is TValueAs))
                return defaultValue;
            return value as TValueAs;
        }

        public static TResult IfKeyOrValueAs<TKey, TValueAs, TResult>(this IDictionary<TKey, object> dictionary, TKey key, 
            Func<TValueAs, TResult> ifKeyAndValueAs,
            TResult ifNotKeyOrValueAs)
            where TValueAs : class
        {
            if (!dictionary.ContainsKey(key))
                return ifNotKeyOrValueAs;
            var value = dictionary[key];
            if (!(value is TValueAs))
                return ifNotKeyOrValueAs;
            var valueAs = value as TValueAs;
            return ifKeyAndValueAs(valueAs);
        }

        public static TResult Compute<TItem, TResult>(this IEnumerable<TItem> items,
            Func<IEnumerable<TItem>, TResult> compute)
        {
            return compute(items);
        }

        
    }
}
