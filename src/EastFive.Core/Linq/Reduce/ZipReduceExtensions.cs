using EastFive.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EastFive.Linq
{
    public static class ZipReduceExtensions
    {
        public static KeyValuePair<TSelect1, TSelect2>[] ZipReduce<TItem, TSelect1, TSelect2>(this IEnumerable<TItem> items,
            Func<
                TItem,
                Func<TSelect1, TSelect2, KeyValuePair<TSelect1, TSelect2>[]>,
                Func<KeyValuePair<TSelect1, TSelect2>[]>,
                KeyValuePair<TSelect1, TSelect2>[]> select)
        {
            return items.ZipReduce<TItem, TSelect1, TSelect2, KeyValuePair<TSelect1, TSelect2>[]>(
                select,
                kvps => kvps.ToArray());
        }

        public static TResult ZipReduce<TItem, TSelect1, TSelect2, TResult>(this IEnumerable<TItem> items,
            Func<
                TItem,
                Func<TSelect1, TSelect2, TResult>,
                Func<TResult>,
                TResult> select,
            Func<KeyValuePair<TSelect1, TSelect2>[], TResult> reduce)
        {
            return items.SelectReduce<TItem, KeyValuePair<TSelect1, TSelect2>, TResult>(
                (item, next, skip) => select(
                    item,
                    (k, p) => next(k.PairWithValue(p)),
                    skip),
                reduce);
        }
        
        public static Task<TResult> ZipReduceAsync<TItem, TSelect1, TSelect2, TResult>(this IEnumerable<TItem> items,
            Func<
                TItem,
                Func<TSelect1, TSelect2, Task<TResult>>,
                Func<Task<TResult>>,
                Task<TResult>> select,
            Func<KeyValuePair<TSelect1, TSelect2>[], TResult> reduce)
        {
            return items.SelectReduce<TItem, KeyValuePair<TSelect1, TSelect2>, Task<TResult>>(
                (item, next, skip) => select(
                    item,
                    (k, p) => next(k.PairWithValue(p)),
                    skip),
                (kvps) => reduce(kvps).AsTask());
        }
        
    }
}
