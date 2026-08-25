using EastFive.Analytics;
using EastFive.Async;
using EastFive.Collections.Generic;
using EastFive.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EastFive.Linq.Async
{
    public static partial class EnumerableAsync
    {
        public static IEnumerableAsync<T> Where<T>(this IEnumerableAsync<T> enumerable, Func<T, bool> predicate)
        {
            var enumeratorAsync = enumerable.GetEnumerator();
            return Yield<T>(
                   async (yieldReturn, yieldBreak) =>
                   {
                       if (!await enumeratorAsync.MoveNextAsync())
                           return yieldBreak;
                       var current = enumeratorAsync.Current;
                       while (!predicate(current))
                       {
                           if (!await enumeratorAsync.MoveNextAsync())
                               return yieldBreak;
                           current = enumeratorAsync.Current;
                       }
                       return yieldReturn(current);
                   });
        }

        public static IEnumerableAsync<T> AsyncWhere<T>(this IEnumerableAsync<T> enumerable, Func<T, Task<bool>> predicate)
        {
            var enumeratorAsync = enumerable.GetEnumerator();
            return Yield<T>(
                   async (yieldReturn, yieldBreak) =>
                   {
                       if (!await enumeratorAsync.MoveNextAsync())
                           return yieldBreak;
                       var current = enumeratorAsync.Current;
                       while (!await predicate(current))
                       {
                           if (!await enumeratorAsync.MoveNextAsync())
                               return yieldBreak;
                           current = enumeratorAsync.Current;
                       }
                       return yieldReturn(current);
                   });
        }

        public static IEnumerableAsync<T> AsyncWhere<T>(this IEnumerable<T> items, Func<T, Task<bool>> predicate)
        {
            var enumerator = items.GetEnumerator();
            return Yield<T>(
                async (yieldReturn, yieldBreak) =>
                {
                    while (enumerator.MoveNext())
                    {
                        var item = enumerator.Current;
                        var match = await predicate(item);
                        if (match)
                            return yieldReturn(item);
                    }
                    return yieldBreak;
                });
        }

        public delegate IEnumerableAsync<T> RejoinCollectionDelegate<T>(IEnumerableAsync<T> items);

        public static IEnumerableAsync<T> Split<T>(
            this IEnumerableAsync<T> enumerable,
            Func<T, bool> predicate,
            out RejoinCollectionDelegate<T> falseItems)
        {
            var falseItemsLocal = new List<T>();

            var enumeratorAsync = enumerable.GetEnumerator();
            falseItems = (items) =>
            {
                var enumeratorLocal = items.GetEnumerator();
                return EnumerableAsync.Yield<T>(
                    (yieldReturn, yieldBreak) =>
                    {
                        return falseItemsLocal.First(
                            (item, next) =>
                            {
                                return yieldReturn(item).AsTask();
                            },
                            async () =>
                            {
                                if(!await enumeratorLocal.MoveNextAsync())
                                    return yieldBreak;
                                
                                var current = enumeratorLocal.Current;
                                return yieldReturn(current);
                            });
                    });
            };

            return Yield<T>(
                async (yieldReturn, yieldBreak) =>
                {
                    while(await enumeratorAsync.MoveNextAsync())
                    {
                           var current = enumeratorAsync.Current;
                           if (predicate(current))
                               return yieldReturn(current);
                           falseItemsLocal.Add(current);
                    }
                    return yieldBreak;
                });
        }

        public static IEnumerableAsync<T> Rejoin<T>(this IEnumerableAsync<T> enumerable,
            RejoinCollectionDelegate<T> falseItems)
        {
            return falseItems(enumerable);
        }

        public static (IEnumerableAsync<T>, IEnumerableAsync<T>) Clone<T>(this IEnumerableAsync<T> enumerable)
        {
            var buffer1 = new Queue<T>();  // ← Use Queue
            var buffer2 = new Queue<T>();
            var enumeratorAsync = enumerable.GetEnumerator();
            var moveLock = new SemaphoreSlim(1, 1);

            var firstEnumerator = GetBufferedClone(buffer1, buffer2);
            var secondEnumerator = GetBufferedClone(buffer2, buffer1);

            return (firstEnumerator, secondEnumerator);

            IEnumerableAsync<T> GetBufferedClone(Queue<T> readBuffer, Queue<T> writeBuffer)
            {
                return Yield<T>(
                    async (yieldReturn, yieldBreak) =>
                    {
                        while (true)
                        {
                            using (await moveLock.LockAsync())
                            {
                                if (readBuffer.TryDequeue(out var item))  // ← O(1)
                                    return yieldReturn(item);
                                
                                if (!await enumeratorAsync.MoveNextAsync())
                                    return yieldBreak;
                                
                                var current = enumeratorAsync.Current;
                                writeBuffer.Enqueue(current);  // ← O(1)
                                return yieldReturn(current);
                            }
                        }
                    });
            }
        }

        /// <summary>
        /// Mutates a member (field or property) of items in an enumerable and returns items with updated values.
        /// </summary>
        /// <param name="isMatch">True if the mutated member corresponds to the correct item (handles out-of-order results)</param>
        public static IEnumerableAsync<TItem> Property<TItem, TMember>(this IEnumerableAsync<TItem> enumerable,
            Expression<Func<TItem, TMember>> memberExpr,
            Func<IEnumerableAsync<TMember>, IEnumerableAsync<TMember>> mutateMember,
            Func<TItem, TMember, bool> isMatch)
        {
            var (rawItems, rawMembers) = enumerable.Clone();
            var memberExtraction = memberExpr.Compile();
            var updatedMembers = mutateMember(rawMembers.Select(item => memberExtraction(item)));

            // Extract member info (could be property or field)
            var memberInfo = memberExpr.Body switch
            {
                MemberExpression { Member: System.Reflection.PropertyInfo prop } => (System.Reflection.MemberInfo)prop,
                MemberExpression { Member: System.Reflection.FieldInfo field } => field,
                _ => throw new ArgumentException("Expression must reference a property or field", nameof(memberExpr))
            };

            // Create setter delegate based on member type
            Action<object, TMember> setter = memberInfo switch
            {
                System.Reflection.PropertyInfo prop => (item, value) => prop.SetValue(item, value),
                System.Reflection.FieldInfo field => (item, value) => field.SetValue(item, value),
                _ => throw new InvalidOperationException("Unexpected member type")
            };

            var enumeratorItems = rawItems.GetEnumerator();
            var enumeratorMembers = updatedMembers.GetEnumerator();
            var memberBuffer = new Queue<TMember>();

            return Yield<TItem>(
                async (yieldReturn, yieldBreak) =>
                {
                    // Get next item
                    if (!await enumeratorItems.MoveNextAsync())
                        return yieldBreak;

                    var item = enumeratorItems.Current;

                    // First check buffered members
                    foreach (var bufferedMember in memberBuffer.ToArray())
                    {
                        if (!isMatch(item, bufferedMember))
                            continue;

                        // Found match in buffer - remove it and update item
                        memberBuffer = new Queue<TMember>(memberBuffer.Where(m => !EqualityComparer<TMember>.Default.Equals(m, bufferedMember)));
                        
                        if (typeof(TItem).IsValueType)
                        {
                            // Value types require boxing/unboxing
                            object boxedItem = item;
                            setter(boxedItem, bufferedMember);
                            return yieldReturn((TItem)boxedItem);
                        }

                        // Reference types can be modified directly
                        setter(item, bufferedMember);
                        return yieldReturn(item);
                    }

                    // Not in buffer, search through new members
                    while (await enumeratorMembers.MoveNextAsync())
                    {
                        var mutatedMember = enumeratorMembers.Current;

                        if (!isMatch(item, mutatedMember))
                        {
                            // Doesn't match - add to buffer for future items
                            memberBuffer.Enqueue(mutatedMember);
                            continue;
                        }

                        // Found match - update the member
                        if (typeof(TItem).IsValueType)
                        {
                            object boxedItem = item;
                            setter(boxedItem, mutatedMember);
                            return yieldReturn((TItem)boxedItem);
                        }

                        setter(item, mutatedMember);
                        return yieldReturn(item);
                    }

                    return yieldReturn(item);

                });
        }

        public static IEnumerableAsync<TResult> Select<T, TResult>(this IEnumerableAsync<T> enumerable, Func<T, TResult> selection,
            ILogger log = default(ILogger))
        {
            if (enumerable.IsDefaultOrNull())
                return EnumerableAsync.Empty<TResult>();

            var selectId = Guid.NewGuid();
            var logSelect = log.CreateScope($"Select[{selectId}]");
            var eventId = 1;
            var enumeratorAsync = enumerable.GetEnumerator();
            return Yield<TResult>(
                async (yieldReturn, yieldBreak) =>
                {
                    var logItem = logSelect.CreateScope($"Item[{eventId++}]");
                    logItem.Trace($"START BLOCK");

                    if (enumeratorAsync.IsDefaultOrNull())
                        return yieldBreak;

                    logItem.Trace($"Calling MoveNextAsync.");
                    if (!await enumeratorAsync.MoveNextAsync())
                    {
                        enumeratorAsync = default(IEnumeratorAsync<T>);
                        logItem.Trace($"COMPLETE");
                        return yieldBreak;
                    }
                    var current = enumeratorAsync.Current;

                    logItem.Trace($"Begin transform");
                    var next = selection(current);
                    logItem.Trace($"Ended transform");

                    var result = yieldReturn(next);
                    logItem.Trace($"END BLOCK");
                    return result;
                });
        }

        public static IEnumerableAsync<TResult> SelectWithIndex<T, TResult>(this IEnumerableAsync<T> enumerable,
            Func<T, int, TResult> selection,
            ILogger log = default(ILogger))
        {
            var selectId = Guid.NewGuid();
            var logSelect = log.CreateScope($"Select[{selectId}]");
            var eventId = 0;
            var enumeratorAsync = enumerable.GetEnumerator();
            return EnumerableAsync.Yield<TResult>(
                async (moved, ended) =>
                {
                    var index = eventId;
                    var logItem = logSelect.CreateScope($"Item[{eventId++}]");
                    logItem.Trace($"START BLOCK");
                    if (enumeratorAsync.IsDefaultOrNull())
                        return ended;

                    if (!logItem.IsDefaultOrNull())
                        logItem.Trace($"Calling MoveNextAsync.");
                    if (!await enumeratorAsync.MoveNextAsync())
                    {
                        logItem.Trace($"COMPLETE");
                        return ended;
                    }
                    var current = enumeratorAsync.Current;

                    logItem.Trace($"Begin transform");
                    var next = selection(current, index);
                    logItem.Trace($"Ended transform");

                    var result = moved(next);
                    logItem.Trace($"END BLOCK");
                    return result;
                });
        }

        public static IEnumerableAsync<(T, int)> AddIndexes<T>(this IEnumerableAsync<T> enumerable)
        {
            return enumerable.SelectWithIndex((x, i) => (x, i));
        }

        public static IEnumerableAsync<(TResult, TAggr)> SelectWithAggregate<T, TAggr, TResult>(
            this IEnumerableAsync<T> enumerable,
            TAggr aggr,
            Func<T, TAggr, (TResult, TAggr)> selection)
        {
            var enumeratorAsync = enumerable.GetEnumerator();
            return EnumerableAsync.Yield<(TResult, TAggr)>(
                async (moved, ended) =>
                {
                    if (enumeratorAsync.IsDefaultOrNull())
                        return ended;

                    if (!await enumeratorAsync.MoveNextAsync())
                        return ended;

                    var current = enumeratorAsync.Current;

                    var (next, aggrNext) = selection(current, aggr);
                    aggr = aggrNext;
                    return moved((next, aggrNext));
                });
        }

        public static IEnumerableAsync<(TResult, TAggr)> SelectWithAggregateAsync<T, TAggr, TResult>(
            this IEnumerableAsync<T> enumerable,
            TAggr aggr,
            Func<T, TAggr, Task<(TResult, TAggr)>> selection)
        {
            var enumeratorAsync = enumerable.GetEnumerator();
            return EnumerableAsync.Yield<(TResult, TAggr)>(
                async (moved, ended) =>
                {
                    if (enumeratorAsync.IsDefaultOrNull())
                        return ended;

                    if (!await enumeratorAsync.MoveNextAsync())
                        return ended;

                    var current = enumeratorAsync.Current;

                    var (next, aggrNext) = await selection(current, aggr);
                    aggr = aggrNext;
                    return moved((next, aggrNext));
                });
        }

        public static IEnumerableAsync<(TResult, TAggr)> AppendAggregate<T, TAggr, TResult>(
            this IEnumerableAsync<T> enumerable,
            TAggr aggr,
            Func<T, TAggr, (TResult, TAggr)> selection)
        {
            var selectId = Guid.NewGuid();
            var eventId = 0;
            var enumeratorAsync = enumerable.GetEnumerator();
            return EnumerableAsync.Yield<(TResult, TAggr)>(
                async (moved, ended) =>
                {
                    var index = eventId;
                    if (enumeratorAsync.IsDefaultOrNull())
                        return ended;

                    if (!await enumeratorAsync.MoveNextAsync())
                        return ended;

                    var current = enumeratorAsync.Current;

                    var (next, aggrNext) = selection(current, aggr);
                    aggr = aggrNext;
                    return moved((next, aggrNext));
                });
        }

        public static IEnumerableAsync<(T, TWith)> SelectWith<T, TWith>(this IEnumerableAsync<T> enumerable,
            Func<T, TWith> getWith)
        {
            return enumerable.Select(
                item =>
                {
                    var with = getWith(item);
                    return (item, with);
                });
        }

        public static IEnumerableAsync<(T, TWith)> SelectAsyncWith<T, TWith>(this IEnumerableAsync<T> enumerable,
            Func<T, Task<TWith>> getWith,
            int readAhead = -1)
        {
            return enumerable
                .Select(
                    async item =>
                    {
                        var with = await getWith(item);
                        return (item, with);
                    })
                .Await(readAhead: readAhead);
        }

        public static IEnumerableAsync<T> SelectWhereHasValue<T>(this IEnumerableAsync<Nullable<T>> enumerable)
            where T : struct
        {
            return enumerable
                .Where(valueMaybe => valueMaybe.HasValue)
                .Select(valueMaybe => valueMaybe.Value);
        }

        public static IEnumerableAsync<TCast> CastAs<T, TCast>(this IEnumerableAsync<T> enumerable)
            where T : class
            where TCast : class
        {
            return enumerable
                .Select(valueMaybe => valueMaybe as TCast);
        }

        public static IEnumerableAsync<TCast> CastAsBase<T, TCast>(this IEnumerableAsync<T> enumerable)
            where T : TCast
        {
            return enumerable
                .Select(valueMaybe => (TCast)valueMaybe);
        }

        public static IEnumerableAsync<TCast> CastDynamicAs<TCast>(this IEnumerableAsync<dynamic> enumerable)
        {
            return enumerable
                .Select(valueMaybe => (TCast)valueMaybe);
        }

        public static IEnumerableAsync<TCast> CastObjsAs<TCast>(this IEnumerableAsync<object> enumerable)
        {
            return enumerable
                .Select(valueMaybe => (TCast)valueMaybe);
        }

        public static IEnumerableAsync<T> WhenAsyncUnique<T, TUnique>(this IEnumerableAsync<T> enumerable,
            Func<T, Task> whenUnique,
            Func<T, TUnique> uniqueKey)
            where T : struct
        {
            var hashset = new HashSet<TUnique>();
            var enumeratorAsync = enumerable.GetEnumerator();
            return EnumerableAsync.Yield<T>(
                async (moved, ended) =>
                {
                    if (!await enumeratorAsync.MoveNextAsync())
                        return ended;
                    var current = enumeratorAsync.Current;

                    var key = uniqueKey(current);
                    if (!hashset.Contains(key))
                    {
                        hashset.Add(key);
                        await whenUnique(current);
                    }

                    return moved(current);
                });
        }

        public static async Task<long> CountAsync<T>(this IEnumerableAsync<T> enumerable)
        {
            var enumerator = enumerable.GetEnumerator();
            int count = 0;
            while (await enumerator.MoveNextAsync())
            {
                count = count + 1;
            }
            return count;
        }

        public static async Task<int> SumAsync(this IEnumerableAsync<int> enumerable)
        {
            var enumerator = enumerable.GetEnumerator();
            int sum = 0;
            while (await enumerator.MoveNextAsync())
            {
                sum = sum + enumerator.Current;
            }
            return sum;
        }

        public static async Task<long> SumAsync(this IEnumerableAsync<long> enumerable)
        {
            var enumerator = enumerable.GetEnumerator();
            long sum = 0;
            while (await enumerator.MoveNextAsync())
            {
                sum = sum + enumerator.Current;
            }
            return sum;
        }

        public static async Task<double> SumAsync(this IEnumerableAsync<double> enumerable)
        {
            var enumerator = enumerable.GetEnumerator();
            var sum = 0.0d;
            while (await enumerator.MoveNextAsync())
            {
                sum = sum + enumerator.Current;
            }
            return sum;
        }

        public static async Task<(double, double)> SumAsync(this IEnumerableAsync<(double, double)> enumerable)
        {
            var enumerator = enumerable.GetEnumerator();
            double sum1 = 0;
            double sum2 = 0;
            while (await enumerator.MoveNextAsync())
            {
                sum1 = sum1 + enumerator.Current.Item1;
                sum2 = sum2 + enumerator.Current.Item2;
            }
            return (sum1, sum2);
        }

        public static async Task<(double, double, int)> SumAsync(this IEnumerableAsync<(double, double, int)> enumerable)
        {
            var enumerator = enumerable.GetEnumerator();
            double sum1 = 0;
            double sum2 = 0;
            int sum3 = 0;
            while (await enumerator.MoveNextAsync())
            {
                sum1 = sum1 + enumerator.Current.Item1;
                sum2 = sum2 + enumerator.Current.Item2;
                sum3 = sum3 + enumerator.Current.Item3;
            }
            return (sum1, sum2, sum3);
        }

        public static async Task<T> FirstAsync<T>(this IEnumerableAsync<T> enumerable)
        {
            var enumerator = enumerable.GetEnumerator();
            if (await enumerator.MoveNextAsync())
                return enumerator.Current;
            throw new ArgumentException("FirstAsync: enumerable is empty.");
        }

        public static async Task<TResult> FirstAsync<T, TResult>(this IEnumerableAsync<T> enumerable,
            Func<T, TResult> onOne,
            Func<TResult> onNone)
        {
            var enumerator = enumerable.GetEnumerator();
            if (await enumerator.MoveNextAsync())
                return onOne(enumerator.Current);
            return onNone();
        }

        public static async Task<TResult> SingleAsync<T, TResult>(this IEnumerableAsync<T> enumerable,
            Func<T, TResult> onOne,
            Func<T[], TResult> onMany,
            Func<TResult> onNone)
        {
            var enumerator = enumerable.GetEnumerator();
            if (!await enumerator.MoveNextAsync())
                return onNone();
            var first = enumerator.Current;
            if (!await enumerator.MoveNextAsync())
                return onOne(first);
            var rest = new List<T> { first };
            do  
            {
                rest.Add(enumerator.Current);
            } while (await enumerator.MoveNextAsync());
            return onMany(rest.ToArray());
        }

        public static Task<TResult> NextAsync<T, TResult>(this IEnumerableAsync<T> enumerable,
            Func<T, Func<Task<TResult>>, Task<TResult>> onOne,
            Func<TResult> onNone)
        {
            var enumerator = enumerable.GetEnumerator();
            return firstOrNextAsync(enumerator);

            async Task<TResult> firstOrNextAsync(IEnumeratorAsync<T> enumerator)
            {
                if (await enumerator.MoveNextAsync())
                    return await onOne(enumerator.Current,
                        () => firstOrNextAsync(enumerator));
                return onNone();
            }
        }

        public static Task<TResult> NextAsyncAsync<T, TResult>(this IEnumerableAsync<T> enumerable,
            Func<T, Func<Task<TResult>>, Task<TResult>> onOne,
            Func<Task<TResult>> onNone)
        {
            var enumerator = enumerable.GetEnumerator();
            return firstOrNextAsync(enumerator);

            async Task<TResult> firstOrNextAsync(IEnumeratorAsync<T> enumerator)
            {
                if (await enumerator.MoveNextAsync())
                    return await onOne(enumerator.Current,
                        () => firstOrNextAsync(enumerator));
                return await onNone();
            }
        }

        public interface IFirstMatchResult<T>
        {
            bool Matched { get; }
            T Result { get; }
        }

        private class FirstMatchResultMatch<T> : IFirstMatchResult<T>
        {
            public bool Matched { get { return true; } }

            public T Result { get; set; }
        }

        private class FirstMatchResultNext<T> : IFirstMatchResult<T>
        {
            public bool Matched { get { return false; } }

            public T Result => throw new NotImplementedException();
        }

        public static async Task<TResult> FirstMatchAsync<T, TResult>(this IEnumerableAsync<T> enumerable,
            Func<T, Func<TResult, IFirstMatchResult<TResult>>, Func<IFirstMatchResult<TResult>>, IFirstMatchResult<TResult>> onOne,
            Func<TResult> onNone)
        {
            var enumerator = enumerable.GetEnumerator();
            while (await enumerator.MoveNextAsync())
            {
                var item = enumerator.Current;
                var oneResult = onOne(item,
                    (value) => new FirstMatchResultMatch<TResult>
                    {
                        Result = value,
                    },
                    () =>
                    {
                        return new FirstMatchResultNext<TResult>();
                    });
                if (oneResult.Matched)
                    return oneResult.Result;
            }
            return onNone();
        }

        public static Task<TResult> FirstAsyncMatchAsync<T, TResult>(this IEnumerableAsync<T> enumerable,
            Func<T, Func<Task<TResult>>, Task<TResult>> onOne,
            Func<TResult> onNone)
        {
            var enumerator = enumerable.GetEnumerator();
            return FirstAsyncInnerAsync(enumerator, onOne, onNone);
        }

        private static Task<TResult> FirstAsyncInnerAsync<T, TResult>(IEnumeratorAsync<T> enumerator,
            Func<T, Func<Task<TResult>>, Task<TResult>> onOne,
            Func<TResult> onNone)
        {
            Func<Task<TResult>>[] doNexts = new Func<Task<TResult>>[1];
            doNexts[0] =
                async () =>
                {
                    if (!await enumerator.MoveNextAsync())
                        return onNone();
                    return await onOne(enumerator.Current,
                        doNexts[0]);
                };
            return doNexts[0]();
        }

        public static async Task<TResult> FirstMatchAsync<T, TResult>(this IEnumerableAsync<T> enumerable,
            Func<T, bool> predicate,
            Func<T, TResult> onOne,
            Func<T[], TResult> onNone)
        {
            var enumerator = enumerable.GetEnumerator();
            var list = new List<T>();
            while (await enumerator.MoveNextAsync())
            {
                var item = enumerator.Current;
                if (predicate(item))
                    return onOne(item);
                list.Add(item);
            }
            return onNone(list.ToArray());
        }

        public static async Task<TResult> FirstMatchAndPriorsAsync<T, TResult>(this IEnumerableAsync<T> enumerable,
            Func<T, bool> predicate,
            Func<T, T[], TResult> onMatch,
            Func<T[], TResult> onNone)
        {
            var enumerator = enumerable.GetEnumerator();
            var list = new List<T>();
            while (await enumerator.MoveNextAsync())
            {
                var item = enumerator.Current;
                if (predicate(item))
                    return onMatch(item, list.ToArray());
                list.Add(item);
            }
            return onNone(list.ToArray());
        }

        public static async Task<TResult> LastAsync<T, TResult>(this IEnumerableAsync<T> enumerable,
            Func<T, TResult> onOne,
            Func<TResult> onNone)
        {
            var enumerator = enumerable.GetEnumerator();
            bool found = false;
            var item = default(T);
            while (await enumerator.MoveNextAsync())
            {
                found = true;
                item = enumerator.Current;
            }
            if (!found)
                return onNone();
            return onOne(item);
        }

        public static async Task<T> MinAsync<T, TCompare>(this IEnumerableAsync<T> enumerable,
            Func<T, TCompare> compare)
            where TCompare : IComparable
        {
            var enumerator = enumerable.GetEnumerator();
            var item = default(T);
            var value = default(TCompare);
            var isStart = true;
            while (await enumerator.MoveNextAsync())
            {
                var contestedValue = compare(enumerator.Current);
                bool selectCurrent = isStart
                    ||
                    contestedValue.CompareTo(value) < 0;
                if (selectCurrent)
                {
                    isStart = false;
                    value = contestedValue;
                    item = enumerator.Current;
                }
            }
            return item;
        }

        public static IEnumerableAsync<T> Empty<T>()
        {
            return EnumerableAsync.Yield<T>(
                (yeildReturn, yieldBreak) =>
                {
                    return yieldBreak.AsTask();
                });
        }

        public static async Task<IOrderedEnumerable<TItem>> OrderByAsync<TItem, TRank>(this IEnumerableAsync<TItem> enumerable,
            Func<TItem, TRank> ranking)
        {
            var items = await enumerable.Async();
            return items.OrderBy(ranking);
        }

        public static async Task<IOrderedEnumerable<TItem>> OrderByDescendingAsync<TItem, TRank>(this IEnumerableAsync<TItem> enumerable,
            Func<TItem, TRank> ranking)
        {
            var items = await enumerable.Async();
            return items.OrderByDescending(ranking);
        }

        public static async Task<TResult> SelectOneAsync<TItem, TResult>(this IEnumerableAsync<TItem> enumerable,
                Func<TItem, TItem, TItem> select,
            Func<TItem, TResult> onOne,
            Func<TResult> onNone)
        {
            var enumerator = enumerable.GetEnumerator();
            if (!await enumerator.MoveNextAsync())
                return onNone();
            var selected = enumerator.Current;
            while (await enumerator.MoveNextAsync())
            {
                var challenger = enumerator.Current;
                selected = select(selected, challenger);
            }
            return onOne(selected);
        }

        public static IEnumerableAsync<T> Take<T>(this IEnumerableAsync<T> enumerable, int count)
        {
            var enumerator = enumerable.GetEnumerator();
            var countdown = count + 1;
            return EnumerableAsync.Yield<T>(
                async (yieldCont, yieldBreak) =>
                {
                    countdown--;
                    if (countdown <= 0)
                        return yieldBreak;
                    if (await enumerator.MoveNextAsync())
                        return yieldCont(enumerator.Current);
                    return yieldBreak;
                });
        }

        public static IEnumerableAsync<T> Skip<T>(this IEnumerableAsync<T> enumerable, int count)
        {
            var enumerator = enumerable.GetEnumerator();
            var countdown = count;
            return EnumerableAsync.Yield<T>(
                async (yieldCont, yieldBreak) =>
                {
                    while (countdown > 0)
                    {
                        if (!await enumerator.MoveNextAsync())
                            return yieldBreak;
                        countdown--;
                    }
                    if (!await enumerator.MoveNextAsync())
                        return yieldBreak;
                    return yieldCont(enumerator.Current);
                });
        }

        public static IEnumerableAsync<T> Distinct<T>(this IEnumerableAsync<T> enumerable)
        {
            var accumulation = new HashSet<T>(); // TODO: Should be a hash
            var enumerator = enumerable.GetEnumerator();
            return Yield<T>(
                async (yieldAsync, yieldBreak) =>
                {
                    while (true)
                    {
                        if (!await enumerator.MoveNextAsync())
                            return yieldBreak;

                        var current = enumerator.Current;
                        if (accumulation.Contains(current))
                            continue;

                        accumulation.Add(current);
                        return yieldAsync(current);
                    }
                });
        }

        public static IEnumerableAsync<T> Distinct<T, TKey>(this IEnumerableAsync<T> enumerable, Func<T, TKey> selectKey)
        {
            var accumulation = new HashSet<TKey>();
            var enumerator = enumerable.GetEnumerator();
            return Yield<T>(
                async (yieldAsync, yieldBreak) =>
                {
                    while (true)
                    {
                        if (!await enumerator.MoveNextAsync())
                            return yieldBreak;
                        var current = enumerator.Current;

                        var currentKey = selectKey(current);
                        if (accumulation.Contains(currentKey))
                            continue;
                        accumulation.Add(currentKey);

                        return yieldAsync(current);
                    }
                });
        }

        public static IEnumerableAsync<T[]> Segments<T>(this IEnumerableAsync<T> enumerable, int segmentSize)
        {
            var enumerator = enumerable.GetEnumerator();
            return Yield<T[]>(
                async (yieldReturn, yieldBreak) =>
                {
                    // Works with nullification below to prevent double failed moveNexts
                    if (enumerator.IsDefaultOrNull())
                        return yieldBreak;

                    var segmentIndex = 0;
                    var segment = new T[segmentSize];

                    while (segmentIndex < segmentSize)
                    {
                        var hasMoreData = await enumerator.MoveNextAsync();
                        if (hasMoreData)
                        {
                            segment[segmentIndex] = enumerator.Current;
                            segmentIndex++;
                            continue;
                        }

                        var subseg = segment.Take(segmentIndex).ToArray();
                        if (subseg.None())
                            return yieldBreak;

                        enumerator = null; // yieldBreak will be used on the next call so  ensure failed MoveNext is only called once.
                        return yieldReturn(subseg);
                    }

                    return yieldReturn(segment);
                });
        }

        // NOTE: Old Prespool implementation removed. See EnumerableAsync.ChannelExtensions.cs for new Channel-based implementation.

        /// <summary>
        /// Batches items using Channel-based buffering. See EnumerableAsync.ChannelExtensions.cs for implementation.
        /// This is a compatibility shim that delegates to the new Channel-based implementation.
        /// </summary>
        public static IEnumerableAsync<T[]> Batch<T>(this IEnumerableAsync<T> enumerable,
            EastFive.Analytics.ILogger diagnostics = default(EastFive.Analytics.ILogger))
        {
            return enumerable.BatchWithChannels(bufferSize: 1000, diagnostics);
        }

        /// <summary>
        /// Executes tasks from an async enumerable in parallel, yielding results as they complete.
        /// Reimplemented using Channel-based buffering for cleaner async/await semantics.
        /// </summary>
        /// <param name="maintainOrder">If true, yields results in original order. If false, yields results as they complete.</param>
        public static IEnumerableAsync<T> Parallel<T>(this IEnumerableAsync<Task<T>> enumerable,
            bool maintainOrder = false,
            ILogger diagnostics = default(ILogger))
        {
            var pendingTasks = new List<Task<T>>();

            return enumerable
                .BatchWithChannels(bufferSize: 100, diagnostics)
                .SelectAsyncMany(batch =>
                {
                    pendingTasks.AddRange(batch);

                    return Yield<T>(async (yieldReturn, yieldBreak) =>
                    {
                        if (!pendingTasks.Any())
                            return yieldBreak;

                        Task<T> taskToAwait;

                        if (maintainOrder)
                        {
                            // Maintain order: yield first pending task when complete
                            taskToAwait = pendingTasks.First();
                            pendingTasks.RemoveAt(0);
                        }
                        else
                        {
                            // Yield as completed: yield whichever task completes first
                            taskToAwait = await Task.WhenAny(pendingTasks);
                            pendingTasks.Remove(taskToAwait);
                        }

                        var result = await taskToAwait;
                        return yieldReturn(result);
                    });
                });
        }

        /// <summary>
        /// Prespools items using Channel-based buffering. See EnumerableAsync.ChannelExtensions.cs for implementation.
        /// This is a compatibility shim that delegates to the new Channel-based implementation.
        /// </summary>
        public static IEnumerableAsync<T> Prespool<T>(this IEnumerableAsync<T> items, ILogger diagnosticsTag = default(ILogger))
        {
            return items.PrespoolWithChannels(bufferSize: 100, diagnosticsTag);
        }

        public static IEnumerableAsync<TResult> Range<TItem, TRange, TResult>(this IEnumerableAsync<TItem> enumerables,
            Func<TItem, TRange> min,
            Func<TItem, TRange> max,
            Func<TRange, TRange, IEnumerable<TResult>> range)
        {

            throw new NotImplementedException();

            //var enumerators = enumerables
            //    .Select((enumerable, index) => index.PairWithValue(enumerable.GetEnumerator()))
            //    .ToDictionary();

            //var tasks = enumerators
            //    .Select(enumerator => enumerator.Key.PairWithValue(enumerator.Value.MoveNextAsync()))
            //    .ToArray();

            //return Yield<T>(
            //    async (yieldReturn, yieldBreak) =>
            //    {
            //        while (true)
            //        {
            //            var finishedTaskKvp = await GetCompletedTaskIndex<bool>(tasks);
            //            var finishedTaskIndex = finishedTaskKvp.Key;
            //            var moved = finishedTaskKvp.Value;
            //            var enumerator = enumerators[finishedTaskIndex];
            //            tasks = tasks.Where(task => task.Key != finishedTaskIndex).ToArray();
            //            if (moved)
            //            {
            //                var current = enumerator.Current;
            //                tasks = tasks.Append(finishedTaskIndex.PairWithValue(enumerators[finishedTaskIndex].MoveNextAsync())).ToArray();
            //                return yieldReturn(current);
            //            }
            //            if (!tasks.Any())
            //                return yieldBreak;
            //        }
            //    });
        }

        public static IEnumerableAsync<TItem> Compress<TItem>(this IEnumerableAsync<TItem> enumerables,
            Func<TItem, TItem, IEnumerableAsync<TItem>> compressor)
        {
            throw new NotImplementedException();

            //var enumerators = enumerables
            //    .Select((enumerable, index) => index.PairWithValue(enumerable.GetEnumerator()))
            //    .ToDictionary();

            //var tasks = enumerators
            //    .Select(enumerator => enumerator.Key.PairWithValue(enumerator.Value.MoveNextAsync()))
            //    .ToArray();

            //return Yield<T>(
            //    async (yieldReturn, yieldBreak) =>
            //    {
            //        while (true)
            //        {
            //            var finishedTaskKvp = await GetCompletedTaskIndex<bool>(tasks);
            //            var finishedTaskIndex = finishedTaskKvp.Key;
            //            var moved = finishedTaskKvp.Value;
            //            var enumerator = enumerators[finishedTaskIndex];
            //            tasks = tasks.Where(task => task.Key != finishedTaskIndex).ToArray();
            //            if (moved)
            //            {
            //                var current = enumerator.Current;
            //                tasks = tasks.Append(finishedTaskIndex.PairWithValue(enumerators[finishedTaskIndex].MoveNextAsync())).ToArray();
            //                return yieldReturn(current);
            //            }
            //            if (!tasks.Any())
            //                return yieldBreak;
            //        }
            //    });
        }

        public static IEnumerableAsync<T> SelectMany<T>(this IEnumerable<IEnumerableAsync<T>> enumerables,
            bool sequential = true)
        {
            if (sequential)
                return SelectManySequential(enumerables);
            return SelectManyNonSequential(enumerables);
        }

        private static IEnumerableAsync<T> SelectManySequential<T>(IEnumerable<IEnumerableAsync<T>> enumerables)
        {
            var enumerator = enumerables.GetEnumerator();

            IEnumeratorAsync<T> enumeratorInner;
            while (true)
            {
                if (!enumerator.MoveNext())
                    return EnumerableAsync.Empty<T>();
                // Catch unlikely case where returned EnumerableAsync is null or default;
                if (enumerator.Current.IsDefaultOrNull())
                    continue;

                enumeratorInner = enumerator.Current.GetEnumerator();
                break;
            }

            return Yield<T>(
                async (yieldReturn, yieldBreak) =>
                {
                    while (true)
                    {
                        if (await enumeratorInner.MoveNextAsync())
                            return yieldReturn(enumeratorInner.Current);

                        while (true)
                        {
                            if (!enumerator.MoveNext())
                                return yieldBreak;

                            // Catch unlikely case where returned EnumerableAsync is null or default;
                            if (enumerator.Current.IsDefaultOrNull())
                                continue;

                            enumeratorInner = enumerator.Current.GetEnumerator();
                            break;
                        }
                    }
                });
        }

        private static IEnumerableAsync<T> SelectManyNonSequential<T>(IEnumerable<IEnumerableAsync<T>> enumerables)
        {
            var allTask = new List<Task<KeyValuePair<bool, T>>>();
            var allTaskLock = new object();
            var taskGenerator = enumerables
                .Select(
                    enumerable =>
                    {
                        var enumerator = enumerable.GetEnumerator();
                        async Task<KeyValuePair<bool, T>> NextTask()
                        {
                            var moved = await enumerator.MoveNextAsync();
                            if (!moved)
                                return default(T).PairWithKey(false);

                            lock (allTaskLock)
                            {
                                allTask.Add(NextTask());
                            }
                            var current = enumerator.Current;
                            return current.PairWithKey(true);
                        }
                        lock (allTaskLock)
                        {
                            allTask.Add(NextTask());
                        }
                        return enumerator;
                    })
                 .GetEnumerator();

            var generating = true;
            return Yield<T>(
                async (yieldReturn, yieldBreak) =>
                {
                    async Task<KeyValuePair<bool, T>> YieldValueAsync(Task<KeyValuePair<bool, T>> task)
                    {
                        var result = await task;
                        lock (allTaskLock)
                        {
                            allTask.Remove(task);
                        }
                        return result;
                    }

                    while (true)
                    {
                        Task<KeyValuePair<bool, T>>[] tasks;
                        lock (allTaskLock)
                        {
                            tasks = allTask.ToArray();
                        }

                        foreach (var task in tasks)
                        {
                            if (task.Status != TaskStatus.RanToCompletion)
                                continue;

                            var result = await YieldValueAsync(task);
                            if (result.Key)
                                return yieldReturn(result.Value);
                        }

                        if (generating)
                        {
                            if (!taskGenerator.MoveNext())
                                generating = false;
                            continue;
                        }

                        if (!tasks.Any())
                            return yieldBreak;

                        var nextTask = tasks.GetFirstSuccessfulTask();
                        var next = await YieldValueAsync(nextTask);
                        if (next.Key)
                            return yieldReturn(next.Value);
                    }
                });
        }

        public static IEnumerableAsync<TItem> SelectMany<TItem>(
            this IEnumerableAsync<IEnumerable<TItem>> enumerables,
            ILogger logger = default,
            CancellationToken cancellationToken = default)
        {
            return enumerables.SelectMany(x => x,
                logger: logger, cancellationToken: cancellationToken);
        }

        public static IEnumerableAsync<TResult> SelectMany<T, TResult>(
            this IEnumerableAsync<T> enumerables,
            Func<T, IEnumerable<TResult>> selectMany,
            ILogger logger = default,
            System.Threading.CancellationToken cancellationToken = default)
        {
            var enumerator = enumerables.GetEnumerator();
            var enumeratorInner = default(IEnumerator<TResult>);
            var scopedLogger = logger.CreateScope($"SelectMany:{enumerables.GetHashCode()}");
            
            return Yield<TResult>(
                async (yieldReturn, yieldBreak) =>
                {
                    // Check cancellation
                    if (!cancellationToken.IsDefault() && cancellationToken.IsCancellationRequested)
                    {
                        enumeratorInner?.Dispose();
                        return yieldBreak;
                    }

                    // Try to advance current inner enumerator
                    if (enumeratorInner != null && enumeratorInner.MoveNext())
                    {
                        scopedLogger.Trace("Yielding value");
                        return yieldReturn(enumeratorInner.Current);
                    }

                    // Inner exhausted or null - dispose and find next non-empty inner
                    enumeratorInner?.Dispose();
                    enumeratorInner = null;

                    while (await enumerator.MoveNextAsync())
                    {
                        scopedLogger.Trace("Moved Outer");

                        var current = enumerator.Current;
                        var many = selectMany(current);
                        if (many.IsDefaultOrNull())
                        {
                            scopedLogger.Trace("FAILURE:selectMany returned null IEnumerable");
                            continue;
                        }

                        enumeratorInner = many.GetEnumerator();
                        if (enumeratorInner.MoveNext())
                        {
                            scopedLogger.Trace("Yielding value");
                            return yieldReturn(enumeratorInner.Current);
                        }
                        
                        // Dispose immediately if empty
                        enumeratorInner.Dispose();
                        enumeratorInner = null;
                    }

                    // Outer exhausted
                    scopedLogger.Trace("Complete");
                    return yieldBreak;
                });
        }

        public static IEnumerableAsync<TResult> SelectAsyncMany<T, TResult>(this IEnumerableAsync<T> enumerables, Func<T, IEnumerableAsync<TResult>> selectMany)
        {
            var enumerator = enumerables.GetEnumerator();
            var enumeratorInner = default(IEnumeratorAsync<TResult>);
            return Yield<TResult>(
                async (yieldReturn, yieldBreak) =>
                {
                    while (true)
                    {
                        if (enumeratorInner.IsDefaultOrNull())
                        {
                            if (!await enumerator.MoveNextAsync())
                                return yieldBreak;
                            enumeratorInner = selectMany(enumerator.Current).GetEnumerator();
                            continue;
                        }

                        if (!await enumeratorInner.MoveNextAsync())
                        {
                            if (!await enumerator.MoveNextAsync())
                                return yieldBreak;
                            enumeratorInner = selectMany(enumerator.Current).GetEnumerator();
                            continue;
                        }

                        return yieldReturn(enumeratorInner.Current);
                    }
                });
        }

        public static IEnumerableAsync<T> SelectAsyncMany<T>(this IEnumerableAsync<IEnumerableAsync<T>> enumerables)
        {
            return enumerables.SelectAsyncMany(items => items);
        }

        public static IEnumerableAsync<T> Concat<T>(this IEnumerableAsync<T> enumerable1, IEnumerableAsync<T> enumerable2)
        {
            return (new IEnumerableAsync<T>[]
            {
                enumerable1,
                enumerable2,
            })
            .AsyncConcat();
        }

        public static IEnumerableAsync<T> Concat<T>(this IEnumerableAsync<T> enumerable1, IEnumerable<T> enumerable2)
        {
            var enumerable2Async = enumerable2.Select(item => item.AsTask()).AsyncEnumerable();
            return enumerable1.Concat(enumerable2Async);
        }

        public static IEnumerableAsync<T> AsyncConcat<T>(this IEnumerable<IEnumerableAsync<T>> enumerables)
        {
            var enumerator = enumerables.GetEnumerator();
            var enumeratorInner = default(IEnumeratorAsync<T>);
            return Yield<T>(
                async (yieldReturn, yieldBreak) =>
                {
                    while (true)
                    {
                        if (enumeratorInner.IsDefaultOrNull() || (!await enumeratorInner.MoveNextAsync()))
                        {
                            if (!enumerator.MoveNext())
                            {
                                enumerator.Dispose();
                                return yieldBreak;
                            }

                            enumeratorInner = enumerator.Current.GetEnumerator();
                            if (!await enumeratorInner.MoveNextAsync())
                                continue;
                        }
                        return yieldReturn(enumeratorInner.Current);
                    }
                });
        }

        public static IEnumerableAsync<T> Append<T>(this IEnumerableAsync<T> enumerable, T item)
        {
            var enumerator = enumerable.GetEnumerator();
            bool needToAppend = true;
            return Yield<T>(
                async (yieldReturn, yieldBreak) =>
                {
                    if (await enumerator.MoveNextAsync())
                        return yieldReturn(enumerator.Current);

                    if (needToAppend)
                    {
                        needToAppend = false;
                        return yieldReturn(item);
                    }

                    return yieldBreak;
                });
        }

        public static IEnumerableAsync<T> AsyncAppend<T>(this IEnumerableAsync<T> enumerable1, Task<T> enumerable2)
        {
            return (new IEnumerableAsync<T>[]
            {
                enumerable1,
                enumerable2.AsEnumerable(),
            })
            .AsyncConcat();
        }

        public static IEnumerableAsync<T> AppendOptional<T>(this IEnumerableAsync<T> enumerableAsync,
            Func<Func<T, IEnumerableAsync<T>>, Func<IEnumerableAsync<T>>, IEnumerableAsync<T>> appendOptional)
        {
            return appendOptional(
                (item) => enumerableAsync.Append(item),
                () => enumerableAsync);
        }

        public static IEnumerableAsync<T> AppendAsyncOptional<T>(this IEnumerableAsync<T> enumerableAsync,
            Func<Func<T, IEnumerableAsync<T>>, Func<IEnumerableAsync<T>>, Task<IEnumerableAsync<T>>> appendOptionalAsync)
        {
            return appendOptionalAsync(
                (item) => enumerableAsync.Append(item),
                () => enumerableAsync)
                .FoldTask();
        }

        public static IEnumerableAsync<TItem> ConcatIfAny<TItem>(this IEnumerableAsync<TItem> enumerable1,
            Func<IEnumerableAsync<TItem>> onSome)
        {
            return enumerable1.ConcatWithTotal(
                total =>
                {
                    if (total > 0)
                        return onSome();
                    return Empty<TItem>();
                });
        }

        public static IEnumerableAsync<TItem> ConcatIfNone<TItem>(this IEnumerableAsync<TItem> enumerable1,
            Func<IEnumerableAsync<TItem>> onSome)
        {
            return enumerable1.ConcatWithTotal(
                total =>
                {
                    if (total == 0)
                        return onSome();
                    return Empty<TItem>();
                });
        }

        public static IEnumerableAsync<TItem> ConcatWithTotal<TItem>(this IEnumerableAsync<TItem> enumerable1,
            Func<int, IEnumerableAsync<TItem>> onSome)
        {
            bool hasExecuted = false;
            var iterator = default(IEnumeratorAsync<TItem>);
            var enumerator1 = enumerable1.GetEnumerator();
            bool enumerator1Terminated = false;
            int total = 0;
            return EnumerableAsync.Yield<TItem>(
                async (yieldContinue, yieldBreak) =>
                {
                    if (!enumerator1Terminated)
                    {
                        var next = await enumerator1.MoveNextAsync();
                        if (next)
                        {
                            total++;
                            return yieldContinue(enumerator1.Current);
                        }
                        enumerator1Terminated = true;
                    }

                    if (!hasExecuted)
                    {
                        var some = onSome(total);
                        iterator = some.GetEnumerator();
                        hasExecuted = true;
                    }

                    if (await iterator.MoveNextAsync())
                        return yieldContinue(iterator.Current);
                    return yieldBreak;
                });
        }

        public static Task<KeyValuePair<int, T>> GetCompletedTaskIndex<T>(this IEnumerable<KeyValuePair<int, Task<T>>> tasks)
        {
            var tcs = new TaskCompletionSource<KeyValuePair<int, T>>();
            int remainingTasks = tasks.Count();
            foreach (var task in tasks)
            {
                task.Value.ContinueWith(t =>
                {
                    if (task.Value.Status == TaskStatus.RanToCompletion)
                        tcs.TrySetResult(task.Key.PairWithValue(task.Value.Result));
                    else if (System.Threading.Interlocked.Decrement(ref remainingTasks) == 0)
                        tcs.SetException(new AggregateException(
                            tasks.SelectMany(t2 => t2.Value.Exception?.InnerExceptions ?? Enumerable.Empty<Exception>())));
                });
            }
            return tcs.Task;
        }

        public static Task<T> GetFirstSuccessfulTask<T>(this IEnumerable<Task<T>> tasks)
        {
            var tcs = new TaskCompletionSource<T>();
            int remainingTasks = tasks.Count();
            foreach (var task in tasks)
            {
                task.ContinueWith(
                    t =>
                    {
                        if (task.Status == TaskStatus.RanToCompletion)
                            tcs.TrySetResult(t.Result);
                        else if (System.Threading.Interlocked.Decrement(ref remainingTasks) == 0)
                            tcs.SetException(new AggregateException(
                                tasks.SelectMany(t2 => t2.Exception?.InnerExceptions ?? Enumerable.Empty<Exception>())));
                    });
            }
            return tcs.Task;
        }



        public static IEnumerableAsync<TResult> Zip<T1, T2, TResult>(this IEnumerableAsync<T1> items1,
            IEnumerableAsync<T2> items2, Func<T1, T2, TResult> zipper)
        {
            var enumerator1 = items1.GetEnumerator();
            var enumerator2 = items2.GetEnumerator();
            return Yield<TResult>(
                async (yieldReturn, yieldBreak) =>
                {
                    var moved1Task = enumerator1.MoveNextAsync();
                    var moved2Task = enumerator2.MoveNextAsync();
                    var moved1 = await moved1Task;
                    if (!moved1)
                        return yieldBreak;

                    var moved2 = await moved2Task;
                    if (!moved2)
                        return yieldBreak;

                    var value1 = enumerator1.Current;
                    var value2 = enumerator2.Current;
                    var zipped = zipper(value1, value2);
                    return yieldReturn(zipped);
                });
        }

        public static IEnumerableAsync<T> FoldTask<T>(this Task<IEnumerableAsync<T>> enumerable, string tag = default(string))
        {
            var enumerator = default(IEnumeratorAsync<T>);
            return Yield<T>(
                async (yieldReturn, yieldBreak) =>
                {
                    if (enumerator.IsDefaultOrNull())
                        enumerator = (await enumerable).GetEnumerator();

                    if (!await enumerator.MoveNextAsync())
                        return yieldBreak;

                    var next = enumerator.Current;
                    return yieldReturn(next);
                });
        }

        public static IEnumerableAsync<T> FoldTasks<T>(this Task<Task<IEnumerableAsync<T>>> enumerable)
        {
            var enumerator = default(IEnumeratorAsync<T>);
            return Yield<T>(
                async (yieldReturn, yieldBreak) =>
                {
                    if (enumerator.IsDefaultOrNull())
                        enumerator = (await await enumerable).GetEnumerator();

                    if (!await enumerator.MoveNextAsync())
                        return yieldBreak;

                    var next = enumerator.Current;
                    return yieldReturn(next);
                });
        }

        public static IEnumerableAsync<T> Await<T>(this IEnumerableAsync<Task<T>> enumerable,
            string tag = default(string))
        {
            var enumerator = enumerable.GetEnumerator();
            return Yield<T>(
                async (yieldReturn, yieldBreak) =>
                {
                    if (!tag.IsNullOrWhiteSpace())
                        Console.WriteLine($"Await[{tag}]:Requesting next");

                    if (!await enumerator.MoveNextAsync())
                    {
                        if (!tag.IsNullOrWhiteSpace())
                            Console.WriteLine($"Await[{tag}]:Completed");
                        return yieldBreak;
                    }

                    if (!tag.IsNullOrWhiteSpace())
                        Console.WriteLine($"Await[{tag}]:Awaiting value next");
                    var next = await enumerator.Current;

                    if (!tag.IsNullOrWhiteSpace())
                        Console.WriteLine($"Await[{tag}]:Yielding value");
                    return yieldReturn(next);
                });
        }

        /// <summary>
        /// Awaits tasks with read-ahead buffering. See EnumerableAsync.ChannelExtensions.cs for implementation.
        /// For readAhead &lt;= 1, uses simple sequential awaiting to avoid Channel complexity.
        /// </summary>
        public static IEnumerableAsync<TItem> Await<TItem>(this IEnumerableAsync<Task<TItem>> enumerable,
            int readAhead)
        {
            // For sequential processing (readAhead <= 1), use simple implementation
            // This avoids Channel-based complexity that can cause debugger issues
            if (readAhead <= 1)
                return enumerable.Await();
            
            return enumerable.AwaitWithChannels(readAhead);
        }

        /// <summary>
        /// Awaits tasks with adaptive read-ahead that automatically scales based on consumption rate.
        /// Starts with sequential processing and scales up when the consumer is waiting for items.
        /// This is ideal when the optimal concurrency level is unknown.
        /// </summary>
        /// <param name="maxReadAhead">Maximum concurrency level (default 10)</param>
        public static IEnumerableAsync<TItem> AwaitAdaptive<TItem>(this IEnumerableAsync<Task<TItem>> enumerable,
            int maxReadAhead = 10)
        {
            return enumerable.AwaitAdaptive(maxReadAhead, diagnostics: default);
        }

        public static IEnumerableAsync<TItem> AsSingleItemEnumerableAsync<TItem>(this TItem item)
        {
            return EnumerableAsyncStart(item);
        }

        public static IEnumerableAsync<TItem> AsEnumerableAsync<TItem>(this IEnumerable<TItem> items)
        {
            var enumerator = items.GetEnumerator();
            return Yield<TItem>(
                async (yieldReturn, yieldBreak) =>
                {
                    if(!enumerator.MoveNext())
                    {
                        enumerator.Dispose();
                        return yieldBreak;
                    }

                    var next = enumerator.Current;
                    return yieldReturn(next);
                });
        }

        public static IEnumerableAsync<TItem> ToEnumerableAsync<TItem>(this IAsyncEnumerable<TItem> items)
        {
            var enumerator = items.GetAsyncEnumerator();
            return Yield<TItem>(
                async (yieldReturn, yieldBreak) =>
                {
                    if (!await enumerator.MoveNextAsync())
                    {
                        await enumerator.DisposeAsync();
                        return yieldBreak;
                    }

                    var next = enumerator.Current;
                    return yieldReturn(next);
                });
        }

    }
}
