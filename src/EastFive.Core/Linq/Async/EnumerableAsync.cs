using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using EastFive.Analytics;
using EastFive.Extensions;

namespace EastFive.Linq.Async
{
    public interface IYieldResult<T>
    {
        T Value { get; }

        Task<bool> HasNext(
            Func<IYieldResult<T>, bool> onMore,
            Func<Exception, bool> onException,
            Func<bool> onEnd);
    }

    public static partial class EnumerableAsync
    {
        public delegate Task<IYieldResult<T>> YieldDelegateAsync<T>(
                Func<T, IYieldResult<T>> yieldReturn,
                IYieldResult<T> yieldBreak);

        private struct YieldEnumerable<T> : IEnumerableAsync<T>
        {
            private class YieldResult : IYieldResult<T>
            {
                private T value;
                private YieldDelegateAsync<T> getNext;
                private Task<IYieldResult<T>> fetch;
                private bool hasFeched;

                internal YieldResult(T value, YieldDelegateAsync<T> next)
                {
                    this.value = value;
                    this.getNext = next;
                    this.hasFeched = false;
                }

                public virtual T Value => value;

                public async Task<bool> HasNext(
                    Func<IYieldResult<T>, bool> onMore,
                    Func<Exception, bool> onException,
                    Func<bool> onEnd)
                {
                    Task<IYieldResult<T>> internalFetch;
                    lock(this)
                    {
                        if (!this.hasFeched)
                        {
                            this.fetch = getNext(
                                v =>
                                {
                                    return new YieldResult(v, this.getNext);
                                },
                                new YieldBreak());
                            this.hasFeched = true;
                        }
                        internalFetch = this.fetch;
                    }

                    try
                    {
                        var yieldResult = await internalFetch;
                        var isTerminal = (yieldResult is YieldBreak);
                        if (isTerminal)
                            return onEnd();
                        return onMore(yieldResult);
                    }catch (Exception ex)
                    {
                        ex.GetType();// suppress warning
                        throw;
                    }
                }

                private struct YieldBreak : IYieldResult<T>
                {
                    public T Value => throw new NullReferenceException();

                    public Task<bool> HasNext(
                        Func<IYieldResult<T>, bool> onMore,
                        Func<Exception, bool> onException,
                        Func<bool> onEnd)
                    {
                        return onEnd().AsTask();
                    }
                }

            }

            private class YieldResultFirst : YieldResult
            {
                internal YieldResultFirst(YieldDelegateAsync<T> next)
                    : base(default(T), next)
                {
                }

                public override T Value => throw new NullReferenceException();
                
            }

            // private YieldDelegateAsync<T> yield;
            private IYieldResult<T> firstStep;

            internal YieldEnumerable(YieldDelegateAsync<T> yield)
            {
                this.firstStep = new YieldResultFirst(yield);
            }
            
            private class YieldEnumerator : IEnumeratorAsync<T>
            {
                private IYieldResult<T> currentStep;
                public YieldEnumerator(IYieldResult<T> firstStep)
                {
                    this.currentStep = firstStep;
                }

                public T Current => currentStep.Value;

                public Task<bool> MoveNextAsync()
                {
                    return this.currentStep.HasNext(
                        (next) =>
                        {
                            this.currentStep = next;
                            return true;
                        },
                        (ex) =>
                        {
                            return true;
                        },
                        () =>
                        {
                            //this.currentStep = null;
                            return false;
                        });
                }
            }

            public IEnumeratorAsync<T> GetEnumerator()
            {
                return new YieldEnumerator(firstStep);
            }
        }

        public static IEnumerableAsync<T> Yield<T>(
            YieldDelegateAsync<T> generateFunction)
        {
            return new YieldEnumerable<T>(generateFunction);
        }

        public static IEnumerableAsync<T> YieldBatch<T>(
            YieldDelegateAsync<T[]> generateFunction,
            CancellationToken cancellationToken = default)
        {
            return Yield<T[]>(generateFunction)
                .SelectMany(cancellationToken:cancellationToken);
        }

        public static IEnumerableAsync<T> Range<T>(int start, int count,
            Func<int, Task<T>> generateFunction)
        {
            return Yield<T>(
                async (yieldReturn, yieldBreak) =>
                {
                    if (count == 0)
                        return yieldBreak;
                    count--;
                    var index = start;
                    start++;
                    var value = await generateFunction(index);
                    return yieldReturn(value);
                });
        }

        public static IEnumerableAsync<T> From<T>(params T[] items)
        {
            var count = -1;
            return Yield<T>(
                async (yieldReturn, yieldBreak) =>
                {
                    count++;
                    if (items.Length == count)
                        return await yieldBreak.AsTask();

                    var item = items[count];
                    return yieldReturn(item);
                });
        }

        public static IEnumerableAsync<TItem> AsyncEnumerable<TItem>(this IEnumerable<Task<TItem>> items)
        {
            var enumerator = items.GetEnumerator();
            return Yield<TItem>(
                async (yieldReturn, yieldBreak) =>
                {
                    if (!enumerator.MoveNext())
                    {
                        enumerator.Dispose();
                        return yieldBreak;
                    }

                    var current = await enumerator.Current;
                    return yieldReturn(current);
                });
        }

        public static IEnumerableAsync<TItem> AsyncEnumerable<TItem>(this IEnumerable<Task<TItem>> items,
            int readAhead = 1)
        {
            // No arg exception here because some items want to read ahead 
            // using a dynamic value which could be 0 and ...
            if (readAhead <= 1)
                return items.AsyncEnumerable();
                // ... the readAhead >= 1 requirement is based off the implementation of this method.

            var enumerator = items.GetEnumerator();
            bool moreDataToRead = enumerator.MoveNext();

            //var batchQueue = new Queue<Task<TItem>>(readAhead);
            var batchQueue = new System.Collections.Concurrent.ConcurrentQueue<Task<TItem>>();
            var batchQueueLock = new object();

            bool ShouldQueueMoreItems()
            {
                if (!moreDataToRead)
                    return false;

                if (batchQueue.Count < readAhead)
                    return true;

                return false;
            }

            while (ShouldQueueMoreItems())
            {
                batchQueue.Enqueue(enumerator.Current);
                moreDataToRead = enumerator.MoveNext();
            }

            return Yield<TItem>(
                async (yieldReturn, yieldBreak) =>
                {
                    Task<TItem> currentTask;

                    lock (batchQueueLock)
                    {
                        while (true)
                        {
                            while (ShouldQueueMoreItems())
                            {
                                batchQueue.Enqueue(enumerator.Current);
                                moreDataToRead = enumerator.MoveNext();
                            }

                            //if (batchQueue.Any())
                            if (batchQueue.TryDequeue(out currentTask))
                                break;

                            if (!moreDataToRead)
                            {
                                enumerator.Dispose();
                                return yieldBreak;
                            }
                        }
                        //currentTask = batchQueue.Dequeue();
                    }

                    var current = await currentTask;
                    return yieldReturn(current);
                });
        }

        public static IEnumerableAsync<TItem> AsyncEnumerable<TItem>(this IEnumerable<Task<TItem>> items,
            bool startAllTasks)
        {
            var tasks = items.ToArray();
            var batchQueue = new System.Collections.Concurrent.ConcurrentQueue<Task<TItem>>(tasks);
            
            return Yield<TItem>(
                async (yieldReturn, yieldBreak) =>
                {
                    if (!batchQueue.TryDequeue(out Task<TItem>  currentTask))
                        return yieldBreak;

                    var current = await currentTask;
                    return yieldReturn(current);
                });
        }

        public static IEnumerableAsync<TItem> AsAsync<TItem>(this IEnumerable<TItem> items)
        {
            var enumerator = items.GetEnumerator();
            return Yield<TItem>(
                async (yieldReturn, yieldBreak) =>
                {
                    if (!enumerator.MoveNext())
                    {
                        enumerator.Dispose();
                        return await yieldBreak.AsTask();
                    }

                    var current = enumerator.Current;
                    return yieldReturn(current);
                });
        }

        public static IEnumerableAsync<TItem> FoldTask<TItem>(this Task<IEnumerable<TItem>> itemsTask)
        {
            var enumerator = default(IEnumerator<TItem>);
            return Yield<TItem>(
                async (yieldReturn, yieldBreak) =>
                {
                    if (enumerator.IsDefault())
                    {
                        var items = await itemsTask;
                        enumerator = items.GetEnumerator();
                    }
                    if (!enumerator.MoveNext())
                    {
                        enumerator?.Dispose();
                        return yieldBreak;
                    }

                    var current = enumerator.Current;
                    return yieldReturn(current);
                });
        }

        public static IEnumerableAsync<TItem> FoldTask<TItem>(this Task<TItem[]> itemsTask)
        {
            var enumerator = default(System.Collections.IEnumerator);
            return Yield<TItem>(
                async (yieldReturn, yieldBreak) =>
                {
                    if (enumerator.IsDefault())
                    {
                        var items = await itemsTask;
                        enumerator = items.GetEnumerator();
                    }
                    if (!enumerator.MoveNext())
                        return yieldBreak;

                    var current = (TItem)enumerator.Current;
                    return yieldReturn(current);
                });
        }

        public static IEnumerableAsync<TItem> EnumerableAsyncStart<TItem>(this TItem item)
        {
            bool yielded = false;
            return Yield<TItem>(
                (yieldReturn, yieldBreak) =>
                {
                    if (!yielded)
                    {
                        yielded = true;
                        return yieldReturn(item).AsTask();
                    }
                    return yieldBreak.AsTask();
                });
        }

        public static IEnumerableAsync<TItem> AsEnumerable<TItem>(this Task<TItem> itemTask)
        {
            bool yielded = false;
            return Yield<TItem>(
                async (yieldReturn, yieldBreak) =>
                {
                    if (!yielded)
                    {
                        yielded = true;
                        var item = await itemTask;
                        return yieldReturn(item);
                    }
                    return yieldBreak;
                });
        }

        public static IEnumerableAsync<T> Parallel<T>(this IEnumerable<Task<T>> enumerable,
            bool maintainOrder = false,
            ILogger diagnostics = default(ILogger))
        {
            var segment = new List<Task<T>>();
            var moved = new AutoResetEvent(false);
            var complete = new AutoResetEvent(false);
            var segmentTask = Task.Run(
                () =>
                {
                    var enumerator = enumerable.GetEnumerator();
                    while(enumerator.MoveNext())
                    {
                        lock(segment)
                        {
                            segment.Add(enumerator.Current);
                        }
                        moved.Set();
                    }
                    complete.Set();
                });
            var taskComplete = false; // Prevents call to WaitOne if task has already completed
            return Yield<T>(
                async (yieldReturn, yieldBreak) =>
                {
                    async Task<bool> CompleteAsync()
                    {
                        if (taskComplete)
                            return true;

                        var taskIsFinished = complete.WaitOne(0);
                        if (!taskIsFinished)
                            return false;

                        taskComplete = true; // prevent hanging on complete.WaitOne(0)
                        await segmentTask; // wrap up batch read-ahead task
                        return true;
                    }

                    while (!await CompleteAsync())
                    {
                        moved.WaitOne(TimeSpan.FromSeconds(5));

                        var yieldKvp = await YieldResultAsync();
                        if (!yieldKvp.Key)
                            continue;
                        return yieldKvp.Value;
                    }

                    var yieldFinalKvp = await YieldResultAsync();
                    if (!yieldFinalKvp.Key)
                    {
                        return yieldBreak;
                    }
                    return yieldFinalKvp.Value;

                    async Task<KeyValuePair<bool, IYieldResult<T>>> YieldResultAsync()
                    {
                        Task<T>[] nextSegment;
                        lock (segment)
                        {
                            nextSegment = segment.ToArray();
                        }
                        if (nextSegment.Any())
                        {
                            var finishedTaskNext = maintainOrder ?
                                nextSegment.First()
                                :
                                await Task.WhenAny<T>(nextSegment);
                            lock (segment)
                            {
                                segment.Remove(finishedTaskNext);
                            }
                            var next = await finishedTaskNext;
                            return yieldReturn(next).PairWithKey(true);
                        }
                        return default(IYieldResult<T>).PairWithKey(false);
                    }
                });
        }

        public static IEnumerableAsync<TItem> Throttle<TItem>(this IEnumerable<Task<TItem>> enumerable,
            int desiredRunCount = 1,
            ILogger log = default(ILogger))
        {
            var logScope = log.CreateScope($"Throttle");
            var taskList = new List<Task<TItem>>();
            var enumerator = enumerable.GetEnumerator();
            var moving = true;
            return Yield<TItem>(
                async (yieldReturn, yieldBreak) =>
                {
                    while (true)
                    {
                        bool DequeueAnotherTask()
                        {
                            if (!moving)
                                return false;

                            lock (taskList)
                            {
                                if (taskList.Count >= desiredRunCount)
                                    return false;
                            }

                            return true;
                        }

                        if(DequeueAnotherTask())
                        {
                            if (!enumerator.MoveNext())
                            {
                                moving = false;
                                continue;
                            }
                            var nextTask = enumerator.Current;
                            lock (taskList)
                            {
                                taskList.Add(nextTask);
                            }
                            continue;
                        }

                        var finishedTaskTask = default(Task<Task<TItem>>);
                        lock (taskList)
                        {
                            if (!taskList.Any())
                                if (!moving)
                                    return yieldBreak;

                            Task<TItem>[] tasks = taskList.ToArray();
                            finishedTaskTask = Task.WhenAny(tasks);
                        }
                        if (finishedTaskTask.IsDefaultOrNull())
                            continue;

                        var finishedTask = await finishedTaskTask;
                        lock (taskList)
                        {
                            taskList.Remove(finishedTask);
                        }
                        var next = await finishedTask;
                        return yieldReturn(next);
                    }
                });
        }

        public static IEnumerableAsync<TItem2> SelectAsyncMany<TItem1, TItem2>(this IEnumerable<TItem1> items,
            Func<TItem1, IEnumerableAsync<TItem2>> callback)
        {
            var enumerator = items.GetEnumerator();
            var enumeratorInner = default(IEnumeratorAsync<TItem2>);
            return Yield<TItem2>(
                async (yieldReturn, yieldBreak) =>
                {
                    while (true)
                    {
                        if (await DidMoveAsync())
                            break;

                        if (!enumerator.MoveNext())
                            return yieldBreak;

                        enumeratorInner = callback(enumerator.Current).GetEnumerator();
                    }

                    return yieldReturn(enumeratorInner.Current);

                    async Task<bool> DidMoveAsync()
                    {
                        if (enumeratorInner.IsDefaultOrNull())
                            return false;

                        var didMove = await enumeratorInner.MoveNextAsync();
                        return didMove;
                    }
                });
        }

        public static IEnumerableAsync<TResult> SelectAsyncWith<TWith, TItem, TResult>(this IEnumerable<TItem> items,
            TWith seed, Func<TWith, TItem, Task<(TResult, TWith)>> callback)
        {
            var carry = seed;
            var enumerator = items.GetEnumerator();
            return Yield<TResult>(
                async (yieldReturn, yieldBreak) =>
                {
                    if (!enumerator.MoveNext())
                        return yieldBreak;
                    var (next, newCarry) = await callback(carry, enumerator.Current);
                    carry = newCarry;
                    return yieldReturn(next);
                });
        }

        public static IEnumerableAsync<(TItem, TWith)> SelectUsingCache<TItem, TWith, TDistinct>(this IEnumerable<TItem> items,
            Func<TItem, TDistinct> identify,
            Func<TItem, Task<TWith>> loadAsync)
        {
            var lookups = new Dictionary<TDistinct, TWith>();
            var lookupsLock = new object();

            var enumerator = items.GetEnumerator();
            return Yield<(TItem, TWith)>(
                async (yieldReturn, yieldBreak) =>
                {
                    if (!enumerator.MoveNext())
                        return yieldBreak;

                    var current = enumerator.Current;
                    var lookup = identify(current);
                    lock (lookupsLock)
                    {
                        if (lookups.TryGetValue(lookup, out TWith value))
                            return yieldReturn((current, value));
                    }

                    var newValue = await loadAsync(current);
                    lock (lookupsLock)
                    {
                        lookups.Add(lookup, newValue);
                    }
                    return yieldReturn((current, newValue));
                });
        }

        #region SelectWhere Tuples

        public static IEnumerableAsync<T3> SelectWhere<T1, T2, T3>(this IEnumerableAsync<(T1, T2)> items,
            Func<(T1, T2), (bool, T3)> isWhere)
        {
            var enumerator = items.GetEnumerator();
            return EnumerableAsync.Yield<T3>(
                async (yieldReturn, yieldBreak) =>
                {
                    while (true)
                    {
                        if (!await enumerator.MoveNextAsync())
                            return yieldBreak;

                        var current = enumerator.Current;
                        var (isSelected, r) = isWhere(current);
                        if (isSelected)
                            return yieldReturn(r);
                    }
                });
        }

        public static IEnumerableAsync<T> SelectWhere<T>(this IEnumerableAsync<(bool, T)> items)
        {
            return items.SelectWhere(
                item => (item.Item1, item.Item2));
        }

        public static IEnumerableAsync<(T4, T5)> SelectWhere<T1, T2, T3, T4, T5>(this IEnumerableAsync<(T1, T2, T3)> items,
            Func<(T1, T2, T3), (bool, T4, T5)> isWhere)
        {
            return items
                .Select(item => isWhere(item))
                .Select(tpl => (tpl.Item1, (tpl.Item2, tpl.Item3)))
                .SelectWhere();
        }

        public static IEnumerableAsync<(T2, T3)> SelectWhere<T2, T3>(this IEnumerableAsync<(bool, T2, T3)> items)
        {
            return items.SelectWhere(
                item => (item.Item1, item.Item2, item.Item3));
        }

        public static IEnumerableAsync<(T4, T5, T6)> SelectWhere<T1, T2, T3, T4, T5, T6>(this IEnumerableAsync<(T1, T2, T3, T4)> items,
            Func<(T1, T2, T3, T4), (bool, T4, T5, T6)> isWhere)
        {
            return items
                .Select(item => isWhere(item))
                .Select(tpl => (tpl.Item1, (tpl.Item2, tpl.Item3, tpl.Item4)))
                .SelectWhere();
        }

        public static IEnumerableAsync<(T2, T3, T4)> SelectWhere<T2, T3, T4>(this IEnumerableAsync<(bool, T2, T3, T4)> items)
        {
            return items
                .Where(item => item.Item1)
                .Select(tpl => (tpl.Item2, tpl.Item3, tpl.Item4));
        }

        public static IEnumerableAsync<(T2, T3, T4, T5)> SelectWhere<T2, T3, T4, T5>(this IEnumerableAsync<(bool, T2, T3, T4, T5)> items)
        {
            return items
                .Where(item => item.Item1)
                .Select(tpl => (tpl.Item2, tpl.Item3, tpl.Item4, tpl.Item5));
        }

        #endregion

        public static IEnumerableAsync<(TItem, TWith)> SelectUsingCache<TItem, TWith, TDistinct>(this IEnumerableAsync<TItem> items,
            Func<TItem, TDistinct> identify,
            Func<TItem, Task<TWith>> loadAsync)
        {
            var lookups = new Dictionary<TDistinct, TWith>();
            var lookupsLock = new object();

            var enumerator = items.GetEnumerator();
            return Yield<(TItem, TWith)>(
                async (yieldReturn, yieldBreak) =>
                {
                    if (!await enumerator.MoveNextAsync())
                        return yieldBreak;

                    var current = enumerator.Current;
                    var lookup = identify(current);
                    lock(lookupsLock)
                    {
                        if (lookups.TryGetValue(lookup, out TWith value))
                            return yieldReturn((current, value));
                    }

                    var newValue = await loadAsync(current);
                    lock (lookupsLock)
                    {
                        lookups.Add(lookup, newValue);
                    }
                    return yieldReturn((current, newValue));
                });
        }

        public static IEnumerableAsync<(TItem, TWith)> SelectMaybeUsingCache<TItem, TWith, TDistinct>(this IEnumerableAsync<TItem> items,
            Func<TItem, TDistinct> identify,
            Func<TItem, Task<(bool, TWith)>> loadAsync)
        {
            var lookups = new Dictionary<TDistinct, TWith>();
            var skips = new HashSet<TDistinct>();
            var lookupsLock = new object();

            var enumerator = items.GetEnumerator();
            return Yield<(TItem, TWith)>(
                async (yieldReturn, yieldBreak) =>
                {
                    while (true)
                    {
                        if (!await enumerator.MoveNextAsync())
                            return yieldBreak;

                        var current = enumerator.Current;
                        var lookup = identify(current);
                        lock (lookupsLock)
                        {
                            if (lookups.TryGetValue(lookup, out TWith value))
                                return yieldReturn((current, value));
                            if (skips.Contains(lookup))
                                continue;
                        }

                        var (loaded, newValue) = await loadAsync(current);
                        if (!loaded)
                        {
                            lock(lookupsLock)
                            {
                                skips.Add(lookup);
                            }
                            continue;
                        }

                        lock (lookupsLock)
                        {
                            lookups.Add(lookup, newValue);
                        }

                        return yieldReturn((current, newValue));
                    }
                });
        }

        public static IEnumerableAsync<(TItem1, TItem2, TWith)> SelectMaybeUsingCache<TItem1, TItem2, TWith, TDistinct>(
                this IEnumerableAsync<(TItem1, TItem2)> items,
            Func<TItem1, TItem2, TDistinct> identify,
            Func<TItem1, TItem2, Task<(bool, TWith)>> loadAsync)
        {
            return items
                .SelectMaybeUsingCache(
                    tpl => identify(tpl.Item1, tpl.Item2),
                    tpl => loadAsync(tpl.Item1, tpl.Item2))
                .Select(tpl => (tpl.Item1.Item1, tpl.Item1.Item2, tpl.Item2));
        }

        public static IEnumerableAsync<(TItem1, TItem2, TItem3, TWith)> SelectMaybeUsingCache<TItem1, TItem2, TItem3, TWith, TDistinct>(
                this IEnumerableAsync<(TItem1, TItem2, TItem3)> items,
            Func<TItem1, TItem2, TItem3, TDistinct> identify,
            Func<TItem1, TItem2, TItem3, Task<(bool, TWith)>> loadAsync)
        {
            return items
                .SelectMaybeUsingCache(
                    tpl => identify(tpl.Item1, tpl.Item2, tpl.Item3),
                    tpl => loadAsync(tpl.Item1, tpl.Item2, tpl.Item3))
                .Select(tpl => (tpl.Item1.Item1, tpl.Item1.Item2, tpl.Item1.Item3, tpl.Item2));
        }

        public static IEnumerableAsync<(TItem1, TItem2, TItem3, TItem4, TWith)> SelectMaybeUsingCache<TItem1, TItem2, TItem3, TItem4, TWith, TDistinct>(
                this IEnumerableAsync<(TItem1, TItem2, TItem3, TItem4)> items,
            Func<TItem1, TItem2, TItem3, TItem4, TDistinct> identify,
            Func<TItem1, TItem2, TItem3, TItem4, Task<(bool, TWith)>> loadAsync)
        {
            return items
                .SelectMaybeUsingCache(
                    tpl => identify(tpl.Item1, tpl.Item2, tpl.Item3, tpl.Item4),
                    tpl => loadAsync(tpl.Item1, tpl.Item2, tpl.Item3, tpl.Item4))
                .Select(tpl => (tpl.Item1.Item1, tpl.Item1.Item2, tpl.Item1.Item3, tpl.Item1.Item4, tpl.Item2));
        }

        public static IEnumerableAsync<(TItem, TWith)> SelectMaybeFlattenedUsingCache<TItem, TWith, TDistinct>(this IEnumerableAsync<TItem> items,
            Func<TItem, IEnumerable<TDistinct>> identify,
            Func<TItem, TDistinct, Task<(bool, TWith)>> loadAsync)
        {
            var lookups = new Dictionary<TDistinct, TWith>();
            var skips = new HashSet<TDistinct>();
            var lookupsLock = new object();

            var enumerator = items.GetEnumerator();
            var lookupEnumerator = default(IEnumerator<TDistinct>);
            return Yield<(TItem, TWith)>(
                async (yieldReturn, yieldBreak) =>
                {
                    while (true)
                    {
                        if (IsLookupFullyEnumerated())
                        {
                            if (!await enumerator.MoveNextAsync())
                                return yieldBreak;
                            var lookups = identify(enumerator.Current).ToArray();
                            lookupEnumerator = ((IEnumerable<TDistinct>)lookups).GetEnumerator();
                            continue;
                        }

                        var current = enumerator.Current;
                        var lookup = lookupEnumerator.Current;
                        lock (lookupsLock)
                        {
                            if (lookups.TryGetValue(lookup, out TWith value))
                                return yieldReturn((current, value));
                            if (skips.Contains(lookup))
                                continue;
                        }

                        var (loaded, newValue) = await loadAsync(current, lookup);
                        if (!loaded)
                        {
                            lock (lookupsLock)
                            {
                                skips.Add(lookup);
                            }
                            continue;
                        }

                        lock (lookupsLock)
                        {
                            lookups.Add(lookup, newValue);
                        }

                        return yieldReturn((current, newValue));

                        bool IsLookupFullyEnumerated()
                        {
                            if (lookupEnumerator.IsDefaultOrNull())
                                return true;

                            return !lookupEnumerator.MoveNext();
                        }
                    }
                });
        }

        public static IEnumerableAsync<(TItem1, TItem2, TItem3, TWith)> SelectMaybeFlattenedUsingCache<TItem1, TItem2, TItem3, TWith, TDistinct>(
                this IEnumerableAsync<(TItem1, TItem2, TItem3)> items,
            Func<TItem1, TItem2, TItem3, TDistinct[]> identify,
            Func<TItem1, TItem2, TItem3, TDistinct, Task<(bool, TWith)>> loadAsync)
        {
            return items
                .SelectMaybeFlattenedUsingCache(
                    tpl => identify(tpl.Item1, tpl.Item2, tpl.Item3),
                    (tpl, lookupKey) => loadAsync(tpl.Item1, tpl.Item2, tpl.Item3, lookupKey))
                .Select(tpl => (tpl.Item1.Item1, tpl.Item1.Item2, tpl.Item1.Item3, tpl.Item2));
        }

        public static IEnumerableAsync<(TItem, TWith[])> SelectMaybeUsingCache<TItem, TWith, TDistinct>(this IEnumerableAsync<TItem> items,
            Func<TItem, IEnumerable<TDistinct>> identify,
            Func<TItem, TDistinct, Task<(bool, TWith)>> loadAsync)
        {
            var lookups = new Dictionary<TDistinct, TWith>();
            var skips = new HashSet<TDistinct>();
            var lookupsLock = new object();

            var enumerator = items.GetEnumerator();
            return Yield<(TItem, TWith[])>(
                async (yieldReturn, yieldBreak) =>
                {
                    if (!await enumerator.MoveNextAsync())
                        return yieldBreak;

                    var current = enumerator.Current;
                    var withs = await identify(current)
                        .Select(
                            async lookup =>
                            {
                                lock (lookupsLock)
                                {
                                    if (lookups.TryGetValue(lookup, out TWith value))
                                        return (true, value);
                                    if (skips.Contains(lookup))
                                        return (false, default);
                                }

                                var (loaded, newValue) = await loadAsync(current, lookup);
                                if (!loaded)
                                {
                                    lock (lookupsLock)
                                    {
                                        skips.Add(lookup);
                                    }
                                    return (false, default);
                                }

                                lock (lookupsLock)
                                {
                                    lookups.Add(lookup, newValue);
                                }

                                return (true, newValue);
                            })
                        .AsyncEnumerable()
                        .SelectWhere()
                        .ToArrayAsync();

                    return yieldReturn((current, withs));
                });
        }

        public static IEnumerableAsync<(TItem1, TItem2, TItem3, TWith[])> SelectMaybeUsingCache<TItem1, TItem2, TItem3, TWith, TDistinct>(
                this IEnumerableAsync<(TItem1, TItem2, TItem3)> items,
            Func<TItem1, TItem2, TItem3, IEnumerable<TDistinct>> identify,
            Func<TItem1, TItem2, TItem3, TDistinct, Task<(bool, TWith)>> loadAsync)=>  items
                .SelectMaybeUsingCache(
                    tpl => identify(tpl.Item1, tpl.Item2, tpl.Item3),
                    (tpl, lookupKey) => loadAsync(tpl.Item1, tpl.Item2, tpl.Item3, lookupKey))
                .Select(tpl => (tpl.Item1.Item1, tpl.Item1.Item2, tpl.Item1.Item3, tpl.Item2));

        public static IEnumerableAsync<T2> SelectIfElse<T1, T2>(this IEnumerableAsync<T1> value,
            Func<T1, bool> ifCondition,
            Func<T1, T2> ifOperation,
            Func<T1, T2> elseOperation)
        {
            return value
                .Select(
                    item =>
                    {
                        if (ifCondition(item))
                            return ifOperation(item);

                        return elseOperation(item);
                    });
        }

        public static IEnumerableAsync<T> IfThenSelect<T>(this IEnumerableAsync<T> values,
                bool ifCondition,
            Func<T, T> thenOperation)
        {
            if (ifCondition)
                return values.Select(thenOperation);
            return values;
        }

        public static IEnumerableAsync<TResult> IfThenElseSelect<T, TResult>(this IEnumerableAsync<T> values,
                bool ifCondition,
            Func<T, TResult> thenOperation,
            Func<T, TResult> elseOperation)
        {
            if (ifCondition)
                return values.Select(thenOperation);
            return values.Select(elseOperation);
        }

        public static async Task<T[]> RandomSubsetAsync<T>(this IEnumerableAsync<T> items, int total, Random rand = null)
        {
            if (rand == null)
                rand = new Random();
            
            var index = 0;
            var results = new T[total];
            var enumerator = items.GetEnumerator();
            while (await enumerator.MoveNextAsync() && index < total)
            {
                results[index] = enumerator.Current;
                index++;
            }

            while(await enumerator.MoveNextAsync())
            {
                var j = rand.Next(0, index+1);
                if (j < total)
                    results[j] = enumerator.Current;
                index++;
            }
            return results;
        }

        public interface ISelected<T>
        {
            bool HasValue { get; }
            T Value { get; }
        }

        private struct SelectedValue<T> : ISelected<T>
        {
            public SelectedValue(bool x)
            {
                this.HasValue = false;
                this.Value = default(T);
            }

            public SelectedValue(T nextItem)
            {
                this.HasValue = true;
                this.Value = nextItem;
            }

            public bool HasValue { get; private set; }

            public T Value { get; private set; }
        }
        
    }
}
