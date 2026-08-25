using EastFive.Collections.Generic;
using EastFive.Threading;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EastFive.Analytics;
using EastFive.Extensions;

namespace EastFive.Linq.Async
{
    public static partial class EnumerableAsync
    {
        public static Task<bool> AnyAsync<T>(this IEnumerableAsync<T> enumerable)
        {
            var enumerator = enumerable.GetEnumerator();
            return enumerator.MoveNextAsync();
        }

        public static async Task<TResult> AnyAsync<T, TResult>(this IEnumerableAsync<T> enumerable,
            Func<T, TResult> onOneOrMore,
            Func<TResult> onNone)
        {
            var enumerator = enumerable.GetEnumerator();
            if (!await enumerator.MoveNextAsync())
                return onNone();

            return onOneOrMore(enumerator.Current);
        }

        public static Task<bool> ContainsAsync<T>(this IEnumerableAsync<T> enumerable, Func<T, bool> isMatchFunc)
        {
            return enumerable.FirstMatchAsync(
                (item, match, next) =>
                {
                    if (isMatchFunc(item))
                        return match(true);
                    return next();
                },
                () => false);
        }

        public static async Task<T[]> ToArrayAsync<T>(this IEnumerableAsync<T> enumerableAsync,
            EastFive.Analytics.ILogger logger = default(Analytics.ILogger))
        {
            var items = await enumerableAsync.Async(logger);
            return items.ToArray();
        }

        public static async Task<TResult> ToArrayAsync<T, TResult>(this IEnumerableAsync<T> enumerableAsync,
            Func<T[], TResult> onComplete)
        {
            var enumerable = await enumerableAsync.Async();
            var items = enumerable.ToArray();
            return onComplete(items);
        }

        #region Aggregate

        public static async Task<TAccumulate> AggregateAsync<TAccumulate, TItem>(this IEnumerableAsync<TItem> enumerable,
            TAccumulate seed,
            Func<TAccumulate, TItem, TAccumulate> funcAsync)
        {
            var accumulation = seed;
            var enumeratorAsync = enumerable.GetEnumerator();
            while (await enumeratorAsync.MoveNextAsync())
            {
                var current = enumeratorAsync.Current;
                accumulation = funcAsync(accumulation, current);
            }
            return accumulation;
        }

        public struct Accumulate<TAccumulate1, TAccumulate2>
        {
            public TAccumulate1 accumulation1;
            public TAccumulate2 accumulation2;
        }

        public static async Task<Accumulate<TAccumulate1, TAccumulate2>> AggregateAsync<TAccumulate1, TAccumulate2, TItem>(this IEnumerableAsync<TItem> enumerable,
            TAccumulate1 seed1, TAccumulate2 seed2,
            Func<TAccumulate1, TAccumulate2, 
                TItem,
                Func<TAccumulate1, TAccumulate2, Accumulate<TAccumulate1, TAccumulate2>>,
                Accumulate<TAccumulate1, TAccumulate2>> funcAsync)
        {
            var accumulation = new Accumulate<TAccumulate1, TAccumulate2>
            {
                accumulation1 = seed1,
                accumulation2 = seed2,
            };
            var enumeratorAsync = enumerable.GetEnumerator();
            while (await enumeratorAsync.MoveNextAsync())
            {
                var current = enumeratorAsync.Current;
                accumulation = funcAsync(accumulation.accumulation1, accumulation.accumulation2,
                    current,
                    (acc1, acc2) => new Accumulate<TAccumulate1, TAccumulate2>
                    {
                        accumulation1 = acc1,
                        accumulation2 = acc2,
                    });
            }
            return accumulation;
        }

        #endregion

        #region ToDictionary

        public static async Task<IDictionary<TKey, TValue>> ToDictionaryAsync<TKey, TValue>(this IEnumerableAsync<KeyValuePair<TKey, TValue>> enumerableAsync)
        {
            var enumerable = await enumerableAsync.Async();
            return enumerable.ToDictionary();
        }

        public static Task<TResult> ToDictionaryAsync<TKey, TValue, TResult>(this IEnumerableAsync<KeyValuePair<TKey, TValue>> kvpItems,
            Func<Dictionary<TKey, TValue>, KeyValuePair<TKey, TValue>[], TResult> dictionaryAndDuplicates)
        {
            return kvpItems.ToDictionaryAsync(
                kvp => kvp.Key,
                kvp => kvp.Value,
                dictionaryAndDuplicates);
        }

        public static async Task<TResult> ToDictionaryAsync<TKey, TValue, TKeyDictionary, TValueDictionary, TResult>(this IEnumerableAsync<KeyValuePair<TKey, TValue>> kvpItems,
            Func<KeyValuePair<TKey, TValue>, TKeyDictionary> selectKey,
            Func<KeyValuePair<TKey, TValue>, TValueDictionary> selectValue,
            Func<Dictionary<TKeyDictionary, TValueDictionary>, KeyValuePair<TKey, TValue>[], TResult> dictionaryAndDuplicates)
        {
            var hashSet = new HashSet<TKey>();
            var kvpItemsArray = await kvpItems.ToArrayAsync();
            var duplicates = new KeyValuePair<TKey, TValue>[] { };
            var dictionary = kvpItemsArray
                .Select(
                    kvp =>
                    {
                        if (hashSet.Contains(kvp.Key))
                        {
                            duplicates = duplicates.Append(kvp).ToArray();
                            return default(KeyValuePair<TKeyDictionary, TValueDictionary>?);
                        }
                        hashSet.Add(kvp.Key);
                        return selectValue(kvp).PairWithKey(selectKey(kvp));
                    })
                .SelectWhereHasValue()
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            return dictionaryAndDuplicates(dictionary, duplicates);
        }

        #endregion

        public static async Task<List<T>> ToListAsync<T>(this IEnumerableAsync<T> enumerableAsync)
        {
            var enumerable = await enumerableAsync.Async();
            return enumerable.ToList();
        }

        public static async Task<ILookup<TKey, T>> ToLookupAsync<TKey, T>(this IEnumerableAsync<T> enumerableAsync,
            Func<T, TKey> keySelector)
        {
            var enumerable = await enumerableAsync.Async();
            return enumerable.ToLookup(keySelector);
        }

        public static IEnumerableAsync<T> JoinTask<T>(this IEnumerableAsync<T> enumerableAsync,
            Func<Task> task,
            EastFive.Analytics.ILogger logger = default(ILogger))
        {
            enumerableAsync
                .JoinTask(
                    Task.Run(task),
                    logger);
            return enumerableAsync;
        }

        public static IEnumerableAsync<T> JoinTask<T>(this IEnumerableAsync<T> enumerableAsync,
            Task task,
            EastFive.Analytics.ILogger logger = default(ILogger))
        {
            var scopedLogger = logger.CreateScope($"Join[{task.Id}]");
            var enumerator = enumerableAsync.GetEnumerator();
            return EnumerableAsync.Yield<T>(
                async (next, last) =>
                {
                    //if (!tag.IsNullOrWhiteSpace())
                    //    Console.WriteLine($"Join[{tag}] MoveNextAsync.");
                    if (await enumerator.MoveNextAsync())
                    {
                        //if (!tag.IsNullOrWhiteSpace())
                        //    Console.WriteLine($"Join[{tag}] Passthrough on value.");
                        return next(enumerator.Current);
                    }

                    scopedLogger.Trace($"Joining Task.");
                    await task;
                    scopedLogger.Trace($"Complete.");
                    return last;
                });
        }
        
        public static IEnumerableAsync<T> OnComplete<T>(this IEnumerableAsync<T> enumerableAsync,
            Action<T[]> onComplete,
            EastFive.Analytics.ILogger logger = default(ILogger))
        {
            var enumerator = enumerableAsync.GetEnumerator();
            var stack = new Stack<T>();
            return EnumerableAsync.Yield<T>(
                async (next, last) =>
                {
                    //if (!tag.IsNullOrWhiteSpace())
                    //    Console.WriteLine($"Join[{tag}] MoveNextAsync.");
                    if (await enumerator.MoveNextAsync())
                    {
                        var current = enumerator.Current;
                        //if (!tag.IsNullOrWhiteSpace())
                        //    Console.WriteLine($"Join[{tag}] Passthrough on value.");
                        stack.Push(current);
                        return next(current);
                    }

                    var allValues = stack.ToArray();
                    if (!logger.IsDefaultOrNull())
                        Console.WriteLine($"OnComplete Accumulated `{allValues.Length}` Values.");
                    onComplete(allValues);
                    if (!logger.IsDefaultOrNull())
                        Console.WriteLine($"OnComplete Complete.");

                    return last;
                });
        }

        public static IEnumerableAsync<T> OnCompleteJoin<T>(this IEnumerableAsync<T> enumerableAsync,
            Func<T[], IEnumerableAsync<T>> onComplete)
        {
            var enumerator = enumerableAsync.GetEnumerator();
            var stack = new Stack<T>();
            var called = false;
            return EnumerableAsync.Yield<T>(
                async (next, last) =>
                {
                    if (!called)
                    {
                        if (await enumerator.MoveNextAsync())
                        {
                            var current = enumerator.Current;
                            stack.Push(current);
                            return next(current);
                        }

                        var allValues = stack.ToArray();
                        var completeItems = onComplete(allValues);
                        called = true;
                        enumerator = completeItems.GetEnumerator();
                    }

                    if (await enumerator.MoveNextAsync())
                    {
                        var current = enumerator.Current;
                        return next(current);
                    }

                    return last;
                });
        }

        public static IEnumerableAsync<T> OnCompleteAsync<T>(this IEnumerableAsync<T> enumerableAsync,
            Func<T[], Task> onComplete,
            ILogger logger = default(ILogger))
        {
            var scopedLogger = logger.CreateScope("OnCompleteAsync");
            var enumerator = enumerableAsync.GetEnumerator();
            var stack = new Stack<T>();
            return EnumerableAsync.Yield<T>(
                async (next, last) =>
                {
                    scopedLogger.Trace("MoveNextAsync.");
                    if (await enumerator.MoveNextAsync())
                    {
                        var current = enumerator.Current;
                        scopedLogger.Trace("Passthrough on value.");
                        stack.Push(current);
                        return next(current);
                    }

                    var allValues = stack.ToArray();
                    scopedLogger.Trace($"Accumulated `{allValues.Length}` Values.");
                    await onComplete(allValues);
                    scopedLogger.Trace($"Complete.");

                    return last;
                });
        }

        public static IEnumerableAsync<T> OnCompleteAsyncJoin<T>(this IEnumerableAsync<T> enumerableAsync,
            Func<T[], Task<IEnumerableAsync<T>>> onComplete)
        {
            return enumerableAsync.OnCompleteJoin(
                items => onComplete(items).FoldTask());
        }

        public static IEnumerableAsync<T> OnCompleteAsyncAppend<T>(this IEnumerableAsync<T> enumerableAsync,
            Func<T[], Task<T[]>> onComplete)
        {
            var enumerator = enumerableAsync.GetEnumerator();
            var stack = new Stack<T>();
            var called = false;
            var completeItemsIndex = 0;
            var completeItems = default(T[]);
            return EnumerableAsync.Yield<T>(
                async (next, last) =>
                {
                    if (!called)
                    {
                        if (await enumerator.MoveNextAsync())
                        {
                            var current = enumerator.Current;
                            stack.Push(current);
                            return next(current);
                        }

                        var allValues = stack.ToArray();
                        completeItems = await onComplete(allValues);
                        called = true;
                    }

                    if (completeItemsIndex < completeItems.Length)
                    {
                        var current = completeItems[completeItemsIndex];
                        completeItemsIndex++;
                        return next(current);
                    }

                    return last;
                });
        }

        private class CompleteAllMutex<T>
        {
            public EventWaitHandle mutex;
            public T[] values;
        }

        public static void OnCompleteAll<T>(this IEnumerable<IEnumerableAsync<T>> enumerableAsyncs,
            Action<T[][]> onComplete,
            string tag = default(string))
        {
            var mutexes = enumerableAsyncs
                .Select(
                    enumerableAsync =>
                    {
                        var s = new CompleteAllMutex<T>
                        {
                            mutex = new AutoResetEvent(false),
                            values = default(T[]),
                        };
                        enumerableAsync.OnComplete(
                            (values) =>
                            {
                                s.values = values;
                                s.mutex.Set();
                            });
                        return s;
                    })
                .ToArray();

            mutexes.Select(mutex => mutex.mutex).WaitAll();

            var allValuess = mutexes.Select(mutex => mutex.values).ToArray();
            onComplete(allValuess);
        }

        
    }
}
