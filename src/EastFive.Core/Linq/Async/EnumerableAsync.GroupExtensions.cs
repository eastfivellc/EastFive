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

        private class GroupingAsync<TKey, TElement> : IGroupingAsync<TKey, TElement>
        {
            public TKey Key { get; private set; }
            
            private List<TElement> cache;
            private Func<Task<bool>> moveNextAsync;

            public IEnumeratorAsync<TElement> GetEnumerator()
            {
                int index = 0;
                var enumerator = Yield<TElement>(
                    async (yieldReturn, yieldBreak) =>
                    {
                        while (true)
                        {
                            var element = default(TElement);
                            bool found = false;
                            lock (cache)
                            {
                                if (index < cache.Count)
                                {
                                    found = true;
                                    element = cache[index];
                                    index++;
                                }
                            }
                            if (found)
                                return yieldReturn(element);

                            if (!await moveNextAsync())
                                return yieldBreak;
                        }
                    });

                return enumerator.GetEnumerator();
            }

            public void AddItem(TElement element)
            {
                lock(cache)
                {
                    cache.Add(element);
                }
            }

            public GroupingAsync(TKey key,
                Func<Task<bool>> moveNextAsync)
            {
                this.Key = key;
                this.moveNextAsync = moveNextAsync;
                this.cache = new List<TElement>();
            }
        }

        public static IEnumerableAsync<IGroupingAsync<TKey, TSource>> GroupBy<TSource, TKey>(this IEnumerableAsync<TSource> enumerable,
            Func<TSource, TKey> keySelector)
        {
            var accumulation = new Dictionary<TKey, GroupingAsync<TKey, TSource>>();
            var enumeratorAsync = enumerable.GetEnumerator();
            
            var keyQueue = new Queue<TKey>();

            var mutex = new AutoResetEvent(true);
            return Yield<IGroupingAsync<TKey, TSource>>(
                async (yieldReturn, yieldBreak) =>
                {
                    async Task<bool> MoveNextAsync()
                    {
                        mutex.WaitOne();
                        var current = default(TSource);
                        try
                        {
                            if (!await enumeratorAsync.MoveNextAsync())
                                return false;
                            current = enumeratorAsync.Current;
                        }
                        finally
                        {
                            mutex.Set();
                        }
                        
                        var key = keySelector(current);
                        lock (accumulation)
                        {
                            if (!accumulation.ContainsKey(key))
                            {
                                lock (keyQueue)
                                {
                                    keyQueue.Enqueue(key);
                                }
                                var grouping = new GroupingAsync<TKey, TSource>(
                                    key,
                                    async () =>
                                    {
                                        var didMove = await MoveNextAsync();
                                        return didMove;
                                    });
                                accumulation.Add(key, grouping);
                            }
                            accumulation[key].AddItem(current);
                        }
                        return true;
                    }

                    while(true)
                    {
                        var key = default(TKey);
                        bool any = false;
                        lock(keyQueue)
                        {
                            any = keyQueue.Any();
                            if (any)
                                key = keyQueue.Dequeue();
                        }
                        if (any)
                        {
                            lock (accumulation)
                            {
                                return yieldReturn(accumulation[key]);
                            }
                        }

                        var moved = await MoveNextAsync();
                        if (moved)
                            continue;
                        return yieldBreak;
                    }
                });
        }
    }
}
