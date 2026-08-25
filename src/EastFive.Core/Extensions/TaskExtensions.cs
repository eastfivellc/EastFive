using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using EastFive.Extensions;
using EastFive.Linq;

namespace EastFive
{
    public static class TaskExtensions
    {
        public static async Task<IDictionary<TKey, TValue>> CastToGeneric<TKey, TValue>(this Task<Dictionary<TKey, TValue>> task)
        {
            var cast = await task;
            return cast;
        }

        public static async Task<T[]> WhenAllAsync2<T>(this IEnumerable<Task<T>> tasks, int parallelLimit = 0)
        {
            if (parallelLimit <= 0)
                return await Task.WhenAll(tasks);

            // TODO: Change this to use a semaphore and linq Aggregate; also move to EastFive namespace
            var results = await tasks.Aggregate((
                    new
                    {
                        queue = new List<Task<T>>(parallelLimit),
                        results = new List<T>(),
                    }).AsTask(),
                async (aggrTask, current, next) =>
                {
                    var aggr = await aggrTask;
                    aggr.queue.Add(current);
                    if (aggr.queue.Count >= parallelLimit)
                    {
                        var completedTask = await Task.WhenAny(aggr.queue);
                        aggr.queue.Remove(completedTask);
                        aggr.results.Add(await completedTask);
                    }
                    return await next(aggr.AsTask());
                },
                async (aggrTask) =>
                {
                    var aggr = await aggrTask;
                    aggr.results.AddRange(await Task.WhenAll(aggr.queue));
                    return aggr.results.ToArray();
                });

            return results;
        }

        public static async Task<T[]> WhenAllAsync<T>(this Task<IEnumerable<Task<T>>> tasksTask, int parallelLimit = 0)
        {
            var tasks = await tasksTask;
            return await TaskExtensions.WhenAllAsync(tasks, parallelLimit);
        }

        public static async Task RunAllAsync(this IEnumerable<Task> tasks, int parallelLimit = 0)
        {
            if (parallelLimit <= 0)
            {
                await Task.WhenAll(tasks);
                return;
            }

            var queue = new List<Task>(parallelLimit);
            foreach (var task in tasks)
            {
                queue.Add(task);
                if (queue.Count >= parallelLimit)
                {
                    var completedTask = await Task.WhenAny(queue.ToArray());
                    queue.Remove(completedTask);
                }
            }
            await Task.WhenAll(queue);
        }

        public static async Task<T[]> WhenAllAsync<T>(this IEnumerable<Task<T>> tasks, int parallelLimit = 0)
        {
            if (parallelLimit <= 0)
                return await Task.WhenAll(tasks);

            var results = new List<T>();
            var queue = new List<Task<T>>(parallelLimit);
            foreach (var task in tasks)
            {
                queue.Add(task);
                if (queue.Count >= parallelLimit)
                {
                    var completedTask = await Task.WhenAny(queue.ToArray());
                    queue.Remove(completedTask);
                    results.Add(await completedTask);
                }
            }
            results.AddRange(await Task.WhenAll(queue));
            return results.ToArray();
        }

        public static async Task<IEnumerable<T>> WhenAll<T>(this IEnumerable<Task<T>> tasks, int maxParallel)
        {
            var lockObject = new object();
            var taskEnumerator = tasks.GetEnumerator();
            var pullTasks = Enumerable
                .Range(0, maxParallel)
                .Select((i) => PullTasks(taskEnumerator, lockObject))
                .ToArray();

            return (await Task.WhenAll(pullTasks)).SelectMany(task => task);
        }

        private static async Task<IEnumerable<T>> PullTasks<T>(IEnumerator<Task<T>> taskEnumerator, object lockObject)
        {
            var result = new List<T>();
            while (true)
            {
                Task<T> current;
                lock (lockObject)
                {
                    if (!taskEnumerator.MoveNext())
                        break;
                    current = taskEnumerator.Current;
                }
                var currentResult = await current;
                result.Add(currentResult);
            }
            return result;
        }

        public static async Task WhenAll(this IEnumerable<Task> tasks, int maxParallel)
        {
            var lockObject = new object();
            var taskEnumerator = tasks.GetEnumerator();
            var pullTasks = Enumerable
                .Range(0, maxParallel)
                .Select((i) => PullTasks(taskEnumerator, lockObject));

            await Task.WhenAll(pullTasks);
        }

        private static async Task PullTasks(IEnumerator<Task> taskEnumerator, object lockObject)
        {
            while (true)
            {
                Task current;
                lock (lockObject)
                {
                    if (!taskEnumerator.MoveNext())
                        break;
                    current = taskEnumerator.Current;
                }
                await current;
            }
        }

        public static async Task<IEnumerable<Task<T2>>> WhereParallelAsync<T1, T2>(
            this IEnumerable<T1> items,
            Func<T1, Task<bool>> condition,
            Func<T1, Task<T2>> next)
        {
            var itemTasks = new ConcurrentBag<Task<T2>>();
            var iterationTasks = items.Select(item =>
                PeformWhereCheck(item, condition, next, (resultTask) => itemTasks.Add(resultTask)));
            await Task.WhenAll(iterationTasks);
            return itemTasks;
        }

        private static async Task PeformWhereCheck<T1, T2>(T1 item,
            Func<T1, Task<bool>> condition,
            Func<T1, Task<T2>> next,
            Action<Task<T2>> ifSatisfied)
        {
            if (await condition(item))
                ifSatisfied(next(item));
        }
    }
}
