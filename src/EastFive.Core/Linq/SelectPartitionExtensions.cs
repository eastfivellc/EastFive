using EastFive.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EastFive.Linq
{
    public static class SelectPartitionExtensions
    {
        private class Reduction<TSelect1, TSelect2, TResult>
        {
            internal ManualResetEvent selected;
            internal ManualResetEvent reduced;
            internal ManualResetEvent trigger;
            internal TSelect1[] p1s;
            internal TSelect2[] p2s;
            internal TResult result;
        }

        public static TResult SelectPartition<TItem, TSelect1, TSelect2, TResult>(this IEnumerable<TItem> items,
            Func<TItem, Func<TSelect1, TResult>, Func<TSelect2, TResult>, TResult> select,
            Func<TSelect1[], TSelect2[], TResult> reduce)
        {
            var enumerator = items.GetEnumerator();
            return enumerator.SelectPartition(new TSelect1[] { }, new TSelect2[] { }, select,
                (p1s, p2s) =>
                {
                    return reduce(p1s, p2s);
                });
        }

        public static TResult SelectPartitionParallel<TItem, TSelect1, TSelect2, TResult>(this IEnumerable<TItem> items,
            Func<TItem, Func<TSelect1, TResult>, Func<TSelect2, TResult>, TResult> select,
            Func<TSelect1[], TSelect2[], TResult> reduce,
            int depthLimit = 100)
        {
            return items
                .Split(index => depthLimit)
                .Aggregate(
                    new Reduction<TSelect1, TSelect2, TResult>[] { },
                    (Reduction<TSelect1, TSelect2, TResult>[] reductions, IEnumerable<TItem> split) =>
                    {
                        var reduction = new Reduction<TSelect1, TSelect2, TResult>
                        {
                            selected = new ManualResetEvent(false),
                            reduced = new ManualResetEvent(false),
                            trigger = new ManualResetEvent(false),
                        };
                        var thread = new Thread(() =>
                        {
                            var enumerator = split.GetEnumerator();
                            reduction.result = enumerator.SelectPartition(new TSelect1[] { }, new TSelect2[] { }, select,
                                (p1s, p2s) =>
                                {
                                    reduction.p1s = p1s;
                                    reduction.p2s = p2s;
                                    reduction.selected.Set();
                                    reduction.trigger.WaitOne();
                                    return reduction.result;
                                });
                            reduction.reduced.Set();
                        });
                        thread.Start();
                        return reductions.Append(reduction).ToArray();
                    },
                    (Reduction<TSelect1, TSelect2, TResult>[] reductions) =>
                    {
                        var allComplete = reductions.All(reduction => reduction.selected.WaitOne());
                        var p1s = reductions.SelectMany(reduction => reduction.p1s.NullToEmpty()).ToArray();
                        var p2s = reductions.SelectMany(reduction => reduction.p2s.NullToEmpty()).ToArray();
                        return reductions
                            .Reverse()
                            .Aggregate(
                                reduce(p1s, p2s),
                                (result, reduction) =>
                                {
                                    reduction.result = result;
                                    reduction.trigger.Set();
                                    reduction.reduced.WaitOne();
                                    return reduction.result;
                                });
                    });
        }

        private static TResult SelectPartition<TItem, TSelect1, TSelect2, TResult>(this IEnumerator<TItem> items,
            TSelect1[] selections1, TSelect2[] selections2,
            Func<TItem, Func<TSelect1, TResult>, Func<TSelect2, TResult>, TResult> select,
            Func<TSelect1[], TSelect2[], TResult> reduce)
        {
            if (!items.MoveNext())
                return reduce(selections1, selections2);

            return select(items.Current,
                (r) =>
                {
                    return items.SelectPartition(selections1.Append(r).ToArray(), selections2, select, reduce);
                },
                (r) =>
                {
                    return items.SelectPartition(selections1, selections2.Append(r).ToArray(), select, reduce);
                });
        }

        public static TResult SelectPartitionOptimized<TItem, TSelect1, TSelect2, TResult>(this IEnumerable<TItem> items,
            Func<TItem, Func<TSelect1, TResult>, Func<TSelect2, TResult>, TResult> select,
            Func<TSelect1[], TSelect2[], TResult> reduce)
        {
            var space = default(TResult);
            return items
                .Aggregate(
                    (new TSelect1[] { }).PairWithValue(new TSelect2[] { }),
                    (reductions, item) =>
                    {
                        var update = reductions;
                        select(item,
                            (t1) =>
                            {
                                update = reductions.Key.Append(t1).ToArray().PairWithValue(reductions.Value);
                                return space;
                            },
                            (t2) =>
                            {
                                update = reductions.Key.PairWithValue(reductions.Value.Append(t2).ToArray());
                                return space;
                            });
                        return update;
                    },
                    (reductions) =>
                    {
                        return reduce(reductions.Key, reductions.Value);
                    });
        }

        public static TResult SplitReduce<TItem, TResult>(this TItem[] items,
            Func<TItem, bool> isKey,
            Func<IEnumerable<TItem>, IEnumerable<TItem>, TResult> reduce)
        {
            int start = 0;
            int end = items.Length - 1;
            while(true)
            {
                if (start >= end)
                    return reduce(new ArraySegment<TItem>(items, 0, start - 1).Array, new ArraySegment<TItem>(items, start, items.Length - start).Array);
                while (isKey(items[start]))
                {
                    start++;
                    if (start >= end)
                        return reduce(new ArraySegment<TItem>(items, 0, start - 1).Array, new ArraySegment<TItem>(items, start, items.Length - start).Array);
                }
                while (!isKey(items[end]))
                {
                    end--;
                    if (start >= end)
                        return reduce(new ArraySegment<TItem>(items, 0, start), new ArraySegment<TItem>(items, start, items.Length - start));
                }
                var t = items[start];
                items[start] = items[end];
                items[end] = t;
            }
        }

    }
}
