using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using EastFive;
using EastFive.Linq;
using EastFive.Extensions;
using EastFive.Collections.Generic;

namespace EastFive.Linq.Async
{
    public static class AsyncExtensions
    {
        public static async Task<IEnumerable<T>> WhereAsync<T>(this IEnumerable<T> items, Func<T, Task<bool>> predicate)
        {
            return await TaskExtensions.WhenAllAsync(items
                .Select(async item => (await predicate(item)) ?
                    item.PairWithValue(true)
                    :
                    default(KeyValuePair<T, bool>?)))
                .SelectWhereHasValueAsync()
                .SelectAsync(kvp => kvp.Key);
        }
        
        public static async Task<IEnumerable<T>> AwaitAsync<T>(this Task<T[]> itemsTask, Func<IEnumerable<T>, IEnumerable<T>> callback)
        {
            var items = await itemsTask;
            return callback(items);
        }

        public static async Task<IEnumerable<T>> AwaitAsync<T>(this Task<IEnumerable<T>> itemsTask, Func<IEnumerable<T>, IEnumerable<T>> callback)
        {
            var items = await itemsTask;
            return callback(items);
        }

        public static Task<IEnumerable<T>> DistinctAsync<T>(this Task<T[]> itemsTask)
        {
            return itemsTask.AwaitAsync(Enumerable.Distinct);
            //var items = await itemsTask;
            //return items.Distinct();
        }

        public static async Task<IEnumerable<T>> DistinctAsync<T>(this Task<IEnumerable<T>> itemsTask, Func<T, Guid> uniqueProp)
        {
            var items = await itemsTask;
            return items.Distinct(uniqueProp);
        }
        public static async Task<IEnumerable<T>> SelectManyAsync<T>(this Task<IEnumerable<IEnumerable<T>>> itemsTask)
        {
            var items = await itemsTask;
            return items.SelectMany();
        }

        public static async Task<IEnumerable<T>> SelectManyAsync<T>(this Task<IEnumerable<T[]>> itemsTask)
        {
            var items = await itemsTask;
            return items.SelectMany();
        }

        public static async Task<IEnumerable<T>> SelectManyAsync<T>(this Task<T[][]> itemsTask)
        {
            var items = await itemsTask;
            return items.SelectMany();
        }

        public static async Task<IEnumerable<T>> SelectManyAsync<T>(this Task<IEnumerable<T>[]> itemsTask)
        {
            var items = await itemsTask;
            return items.SelectMany();
        }

        public static async Task<IEnumerable<T>> SelectManyAsync<T>(this Task<Task<T[]>[]> itemsTasksTask)
        {
            var itemsTasks = await itemsTasksTask;
            var items = await TaskExtensions.WhenAllAsync(itemsTasks);
            return items.SelectMany();
        }

        public static async Task<IDictionary<TKey, TValue>> ToDictionaryAsync<TKey, TValue>(this Task<IEnumerable<KeyValuePair<TKey, TValue>>> itemsTask)
        {
            var items = await itemsTask;
            return items.ToDictionary();
        }

        public static async Task<IEnumerable<T>> DistinctAsync<T>(this Task<IEnumerable<T>> itemsTask)
        {
            var items = await itemsTask;
            return items.Distinct();
        }

        public static async Task<T> AwaitAsync<T>(this Task<Task<T>> taskTask)
        {
            var task = await await taskTask;
            return task;
        }

        public static async Task<IEnumerable<T>> WhereAsync<T>(this Task<IEnumerable<T>> itemsTask, Func<T, bool> predicate)
        {
            var items = await itemsTask;
            return items.Where(predicate);
        }

        public static async Task<IEnumerable<T>> WhereAsync<T>(this Task<T[]> itemsTask, Func<T, bool> predicate)
        {
            var items = await itemsTask;
            return items.Where(predicate);
        }

        public static async Task<IEnumerable<TResult>> SelectAsync<T1, TResult>(this Task<IEnumerable<T1>> itemsTask, Func<T1, TResult> selector)
        {
            var items = await itemsTask;
            return items.Select(selector);
        }

        public static async Task<IEnumerable<T>> SelectWhereHasValueAsync<T>(this Task<IEnumerable<T?>> itemsTask)
            where T : struct
        {
            var items = await itemsTask;
            return items.SelectWhereHasValue();
        }

        public static async Task<T> LastAsync<T>(this Task<IEnumerable<T>> itemsTask)
        {
            var items = await itemsTask;
            return items.Last();
        }

        public static async Task<T> LastAsync<T>(this Task<T[]> itemsTask)
        {
            var items = await itemsTask;
            return items.Last();
        }

        public static async Task<T> LastOrDefaultAsync<T>(this Task<T[]> itemsTask)
        {
            var items = await itemsTask;
            return items.LastOrDefault();
        }

        public static async Task<T> FirstAsync<T>(this Task<T[]> itemsTask)
        {
            var items = await itemsTask;
            return items.First();
        }

        public static async Task<IDictionary<TKey, TValue>> ToDictionaryAsync<TKey, TValue>(this Task<KeyValuePair<TKey, TValue>[]> kvpsTask)
        {
            var kvps = await kvpsTask;
            return kvps.ToDictionary();
        }

        public static async Task<bool> AllAsync<T>(this Task<T[]> itemsTask, Func<T, bool> predicate)
        {
            var items = await itemsTask;
            return items.All(predicate);
        }

        public static async Task<bool> AllAsync<T>(this Task<IEnumerable<T>> itemsTask, Func<T, bool> predicate)
        {
            var items = await itemsTask;
            return items.All(predicate);
        }

        public static async Task<T[]> ToArrayAsync<T>(this Task<IEnumerable<T>> itemsTask)
        {
            var items = await itemsTask;
            return items.ToArray();
        }

        public static async Task<IEnumerable<T>> SelectWhereHasValueAsync<T>(this Task<T?[]> itemsTask)
            where T : struct
        {
            var items = await itemsTask;
            return items.SelectWhereHasValue();
        }
    }
}
