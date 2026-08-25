using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EastFive;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Collections.Generic;

namespace EastFive.Collections.Generic
{
    public class RecursiveDictionary<TKey> : Dictionary<TKey, RecursiveDictionary<TKey>>
    {
    }

    public class RecursiveDictionary<TKey, TValue> : Dictionary<TKey, RecursiveDictionary<TKey, TValue>>
    {
        public TValue Value { get; set; }
    }

    public static class RecursiveDictionaryExtensions
    {
        public static RecursiveDictionary<TKey, TValue> Add<TKey, TValue>(this RecursiveDictionary<TKey, TValue> dictionary, TKey key, TValue value)
        {
            var next = new RecursiveDictionary<TKey, TValue>();
            next.Value = value;
            dictionary.Add(key, next);
            return dictionary;
        }

        public static RecursiveDictionary<TKey, TValue> AddIfMissing<TKey, TValue>(this RecursiveDictionary<TKey, TValue> dictionary, TKey key, TValue value)
        {
            if (dictionary.ContainsKey(key))
                return dictionary;
            return dictionary.Add(key, value);
        }

        public static RecursiveDictionary<TKey> AddWithRecursion<TKey>(this RecursiveDictionary<TKey> dictionary,
            TKey key,
            Action<TKey, Action<TKey>> callback)
        {
            if (dictionary.ContainsKey(key))
                return dictionary;
            dictionary.Add(key, new RecursiveDictionary<TKey>());
            var x = dictionary[key];
            x.WithRecursion(key, callback);
            return dictionary;
        }

        private static void WithRecursion<TKey>(this RecursiveDictionary<TKey> dictionary, TKey added,
            Action<TKey, Action<TKey>> callback)
        {
            callback(added,
                (key) => dictionary.AddWithRecursion(key, callback));
        }

        public static RecursiveDictionary<TKey, TValue> AddWithRecursion<TKey, TValue>(this RecursiveDictionary<TKey, TValue> dictionary,
            TKey key, TValue value,
            Action<RecursiveDictionary<TKey, TValue>, Action<TKey, TValue>> callback)
        {
            if (dictionary.ContainsKey(key))
                return dictionary;
            dictionary.Add(key, value);
            var x = dictionary[key];
            x.WithRecursion(callback);
            return dictionary;
        }

        private static void WithRecursion<TKey, TValue>(this RecursiveDictionary<TKey, TValue> dictionary,
            Action<RecursiveDictionary<TKey, TValue>, Action<TKey, TValue>> callback)
        {
            callback(dictionary,
                (key, value) => dictionary.AddWithRecursion(key, value, callback));
        }

        public static IEnumerable<T> Flatten<T>(this RecursiveDictionary<T> dictionary)
        {
            return dictionary
                .SelectMany(
                    kvp =>
                    {
                        if (kvp.Value.IsDefault())
                            return new T[] { kvp.Key };
                        return kvp.Value.Flatten().Append(kvp.Key);
                    });
        }

        public static IEnumerable<T> Bottom<T>(this RecursiveDictionary<T> dictionary)
        {
            return dictionary
                .SelectMany(n =>
                    n.Value.IsDefault()?
                    n.Key.AsEnumerable()
                    :
                    n.Value.Bottom());
        }

        public static RecursiveDictionary<TResult> SelectRecursive<TSource, TResult>(this RecursiveDictionary<TSource> dictionary,
            Func<TSource, TResult> selector)
        {
            if (dictionary.IsDefault())
                return default(RecursiveDictionary<TResult>);
            return dictionary
                .Select(
                    kvp =>
                    {
                        return selector(kvp.Key)
                            .PairWithValue(
                                //kvp.Value.IsDefault()?
                                //    default(RecursiveDictionary<TResult>)
                                //:
                                    kvp.Value.SelectRecursive(selector));
                    })
                .AsRecursive();
        }

        public static RecursiveDictionary<TSource> WhereRecursive<TSource>(this RecursiveDictionary<TSource> dictionary,
            Func<TSource, bool> predicate)
        {
            if (dictionary.IsDefault())
                return default(RecursiveDictionary<TSource>);
            return dictionary
                .Select(
                    kvp =>
                    {
                        var next = kvp.Value.WhereRecursive(predicate);

                        // Said more clearly, if the predicate is false, AND no children were returned
                        // just return an empty or null set (written backwards so the predicate is not called
                        // if child values DO EXIST since that would orphan them in the tree).
                        var childValuesDoNotExists = next.IsDefault() || (!next.Any());
                        if (childValuesDoNotExists && (!predicate(kvp.Key)))
                            return default(KeyValuePair<TSource, RecursiveDictionary<TSource>>?);

                        return kvp.Key.PairWithValue(next);
                    })
                .SelectWhereHasValue()
                .AsRecursive();
        }

        public delegate TResult SelectRecursiveDelegate<TSource, TResult>(int depth,
            KeyValuePair<TSource, TResult> parent, KeyValuePair<TSource, TResult>[] left, TSource value);
        public static RecursiveDictionary<TResult> SelectRecursive<TSource, TResult>(this RecursiveDictionary<TSource> dictionary,
            SelectRecursiveDelegate<TSource, TResult> selector)
        {
            if (dictionary.IsDefault())
                return default(RecursiveDictionary<TResult>);
            return SelectRecursive_(0, 
                    default(TSource), default(TResult),
                dictionary,
                selector);
        }

        private static RecursiveDictionary<TResult> SelectRecursive_<TSource, TResult>(int depth, 
                TSource parent, TResult parentResult,
                RecursiveDictionary<TSource> dictionary,
            SelectRecursiveDelegate<TSource, TResult> selector)
        {
            var parentKvp = parent.PairWithValue(parentResult);
            var value = dictionary
                .Aggregate(
                    new KeyValuePair<
                        KeyValuePair<TResult, RecursiveDictionary<TResult>>,
                        KeyValuePair<TSource, TResult>>[] { },
                    (left, v) =>
                    {
                        var result = selector(depth, parentKvp, 
                            left.SelectValues().ToArray(),
                            v.Key);
                        var subset = SelectRecursive_(depth + 1, v.Key, result, v.Value, selector);
                        var resultSet = result.PairWithValue(subset);
                        return left.Append(resultSet.PairWithValue(v.Key.PairWithValue(result))).ToArray();
                    })
                .SelectKeys()
                .AsRecursive();
            return value;
        }

        public static RecursiveDictionary<T> AsRecursive<T>(this IDictionary<T, RecursiveDictionary<T>> dictionary)
        {
            var recursive = new RecursiveDictionary<T>();
            foreach (var kvp in dictionary)
                recursive.Add(kvp.Key, kvp.Value);
            return recursive;
        }

        public static RecursiveDictionary<T> AsRecursive<T>(this IEnumerable<KeyValuePair<T, RecursiveDictionary<T>>> kvps)
        {
            var recursive = new RecursiveDictionary<T>();
            foreach (var kvp in kvps)
                recursive.Add(kvp.Key, kvp.Value);
            return recursive;
        }
    }

}
