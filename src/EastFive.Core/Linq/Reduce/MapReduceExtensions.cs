using EastFive.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EastFive.Linq
{
    public static class MapReduceExtensions
    {
        public static TResult FlatMap<TItem, TSelect, TResult>(this IEnumerable<TItem> items,
            Func<TItem, Func<TSelect, TResult>, TResult> select,
            Func<IEnumerable<TSelect>, TResult> reduce)
        {
            return items.FlatMap(
                (item, next, skip) => select(item, next),
                reduce);
        }

        public static TResult FlatMapPartition<TItem, TSelect1, TSelect2, TResult>(this IEnumerable<TItem> items,
            Func<
                TItem,
                Func<TSelect1, TResult>,  // next
                Func<TSelect2, TResult>, // skip
                TResult> callback,
            Func<TSelect1[], TSelect2[], TResult> complete)
        {
            return items.FlatMap<TItem, TSelect2[], TSelect1, TResult>(
                new TSelect2[] { },
                (item, t1, next, skip) => callback(item,
                    (select1) => next(select1, t1),
                    (select2) => skip(t1.Append(select2).ToArray())),
                (selections, t1) => complete(selections, t1));
        }

        public static TResult FlatMap<TItem, TSelect, TResult>(this IEnumerable<TItem> items,
            Func<
                TItem,
                Func<TSelect, TResult>,  // next
                Func<TResult>, // skip
                TResult> callback,
            Func<IEnumerable<TSelect>, TResult> complete)
        {
            return items.FlatMap<TItem, int, TSelect, TResult>(
                1,
                (item, t1, next, skip) => callback(item,
                    (select) => next(select, t1),
                    () => skip(t1)),
                (selections, t1) => complete(selections));
        }

        public static TResult FlatMap<TItem, T1, T2, TSelect, TResult>(this IEnumerable<TItem> items,
                T1 item1, T2 item2,
            Func<
                TItem, T1, T2,
                Func<TSelect, T1, T2, TResult>,  // next
                Func<T1, T2, TResult>, // skip
                TResult> callback,
            Func<TSelect[], T1, T2, TResult> complete)
        {
            return items.FlatMap<TItem, KeyValuePair<T1, T2>, TSelect, TResult>(
                item1.PairWithValue(item2),
                (item, kvp, next, skip) =>
                    callback(item, kvp.Key, kvp.Value,
                        (select, item1Next, item2Next) => next(select, item1Next.PairWithValue(item2Next)),
                        (item1Next, item2Next) => skip(item1Next.PairWithValue(item2Next))),
                (selects, kvp) => complete(selects, kvp.Key, kvp.Value));
        }

        public static TResult FlatMap<TItem, T1, T2, T3, TSelect, TResult>(this IEnumerable<TItem> items,
                T1 item1, T2 item2, T3 item3,
            Func<
                TItem, T1, T2, T3,
                Func<TSelect, T1, T2, T3, TResult>,  // next
                Func<T1, T2, T3, TResult>, // skip
                TResult> callback,
            Func<TSelect[], T1, T2, T3, TResult> complete)
        {
            return items.FlatMap<TItem, T1, KeyValuePair<T2, T3>, TSelect, TResult>(
                item1, item2.PairWithValue(item3),
                (item, item1Carry, item23CarryKvp, next, skip) =>
                    callback(item, item1Carry, item23CarryKvp.Key, item23CarryKvp.Value,
                        (select, item1Next, item2Next, item3Next) => next(select, item1Next, item2Next.PairWithValue(item3Next)),
                        (item1Next, item2Next, item3Next) => skip(item1Next, item2Next.PairWithValue(item3Next))),
                (selects, item1Final, item23FinalKvp) => complete(selects, item1Final, item23FinalKvp.Key, item23FinalKvp.Value));
        }

        public static TResult FlatMap<TItem, T1, TSelect, TResult>(this IEnumerable<TItem> items,
                T1 item1,
            Func<
                TItem, T1,
                Func<TSelect, T1, TResult>,  // next
                Func<T1, TResult>, // skip
                TResult> callback,
            Func<TSelect[], T1, TResult> complete)
        {
            if (typeof(TResult).IsGenericType && typeof(TResult).GetGenericTypeDefinition() == typeof(Task<>))
            {
                // Call it this way because we need to remap TResult from Task<TR> to TR
                var method = typeof(MapReduceExtensions).GetMethod(nameof(MapReduceExtensions.FlatMapGenericAsync), BindingFlags.NonPublic | BindingFlags.Static);
                var generic = method.MakeGenericMethod(typeof(TItem), typeof(T1), typeof(TSelect), typeof(TResult).GenericTypeArguments.First());
                var r = generic.Invoke(null, new object[] { items.NullToEmpty(), item1, callback, complete });
                var tr = (TResult)r;
                return tr;
            }

            return items
                .NullToEmpty()
                .Aggregate(
                    item1.PairWithValue(new TSelect[] { }),
                    (aggr, item) =>
                    {
                        var block = new ManualResetEvent(false);
                        var resultToDiscard = callback(
                            item, aggr.Key,
                            (selection, item1Next) =>
                            {
                                aggr = item1Next.PairWithValue(aggr.Value.Append(selection).ToArray());
                                block.Set();
                                return default(TResult);
                            },
                            (item1Next) =>
                            {
                                aggr = item1Next.PairWithValue(aggr.Value);
                                block.Set();
                                return default(TResult);
                            });
                        block.WaitOne();
                        return aggr;
                    },
                    aggr => complete(aggr.Value, aggr.Key));
        }

        public static Task<object> FlatMapAsync<TItem, T1, TSelect>(this IEnumerable<TItem> items,
                T1 item1,
            Func<
                TItem, T1,
                Func<TSelect, T1, Task<object>>,  // next
                Func<T1, Task<object>>, // skip
                Task<object>> callback,
            Func<TSelect[], T1, Task<object>> complete)
        {
            var globalSelection = new TSelect[] { };

            return items.Aggregate(
                (new ManualResetEvent(true)).PairWithValue(default(object).AsTask()).AsTask(),
                async (blockAndTaskAsync, item) =>
                {
                    var blockAndTask = await blockAndTaskAsync;
                    var block = new ManualResetEvent(false);
                    blockAndTask.Key.WaitOne(); // wait here so Item1 is updated

                    var completeBlock = new ManualResetEvent(false);
                    var nextTask = default(Task<object>);
                    var completeCallback = Task<object>.Run(
                        () =>
                        {
                            completeBlock.WaitOne();
                            return nextTask;
                        });

                    nextTask = await blockAndTask.Value.ContinueWith(
                        (lastTask) =>
                        {
                            return callback(
                                item, item1,
                                (selection, item1Next) =>
                                {
                                    globalSelection = globalSelection.Append(selection).ToArray();
                                    item1 = item1Next;
                                    block.Set();
                                    return completeCallback;
                                },
                                (item1Next) =>
                                {
                                    item1 = item1Next;
                                    block.Set();
                                    return completeCallback;
                                });
                        });
                    completeBlock.Set();
                    return block.PairWithValue(nextTask);
                },
                async (blockAndTaskAsync) =>
                {
                    var blockAndTask = await blockAndTaskAsync;
                    blockAndTask.Key.WaitOne(); // wait here so Item1 is updated
                    await blockAndTask.Value;
                    return await complete(globalSelection, item1);
                });
        }

        /// <summary>
        /// This method is tail optimized mean that calling methods should not use the return values from callback's next/skip
        /// </summary>
        /// <typeparam name="TItem"></typeparam>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="TSelect"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="items"></param>
        /// <param name="item1"></param>
        /// <param name="callback"></param>
        /// <param name="complete"></param>
        /// <returns></returns>
        private static async Task<TResult> FlatMapGenericAsync<TItem, T1, TSelect, TResult>(this IEnumerable<TItem> items,
                T1 item1,
            Func<
                TItem, T1,
                Func<TSelect, T1, Task<TResult>>,  // next
                Func<T1, Task<TResult>>, // skip
                Task<TResult>> callback,
            Func<TSelect[], T1, Task<TResult>> complete)
        {
            var block = new ManualResetEvent(false);
            var selections = new TSelect[] { };
            var itemHistory = new T1[] { item1 };

            var callComplete = Task<TResult>.Run(
                () =>
                {
                    block.WaitOne();
                    return complete(selections, itemHistory[0]);
                });

            var blankSpace = default(TResult).AsTask();
            return await await items
                .Aggregate(
                    blankSpace,
                    async (aggr, item) =>
                    {
                        return await await aggr.ContinueWith(
                            async last =>
                            {
                                return await callback(
                                    item, itemHistory[0],
                                    (selection, item1Next) =>
                                    {
                                        itemHistory[0] = item1Next;
                                        selections = selections.Append(selection).ToArray();
                                        return blankSpace;
                                    },
                                    (item1Next) =>
                                    {
                                        itemHistory[0] = item1Next;
                                        return blankSpace;
                                    });
                            });
                    })
                .ContinueWith(
                    final =>
                    {
                        block.Set();
                        return callComplete;
                    });
        }

        public static TResult Bucket<TItem1, TItem2, Selector1, Selector2, TResult>(this IEnumerable<TItem1> items1, IEnumerable<TItem2> items2,
            Func<TItem1, Selector1> selector1,
            Func<TItem2, Selector2> selector2,
            Func<Selector1, Selector2, bool> matchPredicate,
            Func<KeyValuePair<TItem1[], TItem2[]>[], TItem1[], TItem2[], TResult> onGetResult)
        {
            var items1Grouped = items1.GroupBy(selector1);
            var items2Grouped = items2.GroupBy(selector2).ToArray();
            return items1Grouped.FlatMap<IGrouping<Selector1, TItem1>, TItem1[], IGrouping<Selector2, TItem2>[], KeyValuePair<TItem1[], TItem2[]>, Func<TResult>>(
                    new TItem1[] { }, items2Grouped,
                (item1, items1Unmatched, items2Unmatched, next, skip) =>
                {
                    return items2Unmatched.SplitReduce(
                        item2 => matchPredicate(item1.Key, item2.Key),
                        (items2Matching, items2UnmatchedNew) =>
                        {
                            if (!items2Matching.Any())
                                return skip(items1Unmatched.Concat(item1).ToArray(), items2Unmatched);
                            return next(
                                item1.ToArray().PairWithValue(items2Matching.Select(grp => grp).SelectMany().ToArray()),
                                items1Unmatched,
                                items2UnmatchedNew.ToArray());
                        });
                },
                (KeyValuePair<TItem1[], TItem2[]>[] itemsMatched, TItem1[] items1Unmatched, IGrouping<Selector2, TItem2>[] items2Unmatched) =>
                {
                    // return fuction to not trigger an async FlatMap
                    return () => onGetResult(itemsMatched, items1Unmatched, items2Unmatched.Select(grp => grp.ToArray()).SelectMany().ToArray());
                })();
        }
    }
}
