using EastFive.Extensions;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EastFive.Collections;
using EastFive.Collections.Generic;
using EastFive.Linq;
using BlackBarLabs;
using EastFive.Analytics;
using System.Diagnostics;

namespace EastFive.Linq.Async
{
    public interface IManagePerformance<TResult>
    {
        Task<TResult> Manage(Func<Task<TResult>> criticalSection);
    }
    
    public static partial class EnumerableAsync
    {
        private class PerformanceManager<TResult> : IManagePerformance<TResult>
        {
            private List<Timing> timings = new List<Timing>();
            private Queue<ManualResetEvent> workQueue = new Queue<ManualResetEvent>();
            private List<ManualResetEvent> concurrentThreads = new List<ManualResetEvent>();
            private DateTime epoch = DateTime.UtcNow;
            //private double predictor = 0.0;
            private Random rand = new Random();
            private ILogger logger;


            public PerformanceManager(int desiredRunCount, ILogger logger)
            {
                this.logger = logger;
                this.desiredRunCount = desiredRunCount;
            }
            
            private int manageRequestsTotal = 0;
            private int manageRequestsProcessed = 0;
            private long totalDuration;
            private int desiredConcurrency = 1;
            private int desiredRunCount;
            

            public async Task<TResult> Manage(Func<Task<TResult>> criticalSection)
            {
                var taskLogger = logger.CreateScope($"Manage[{manageRequestsTotal++}]");
                taskLogger.Information($"BEGIN");
                var stopWatch = new Stopwatch();
                stopWatch.Start();
                ManualResetEvent mutex;
                bool runImmediately;
                lock (timings)
                {
                    runImmediately = concurrentThreads.Count < desiredConcurrency;
                    mutex = new ManualResetEvent(runImmediately);
                }

                if (!runImmediately)
                    workQueue.Enqueue(mutex);

                mutex.WaitOne();
                lock (timings)
                {
                    concurrentThreads.Add(mutex);
                }

                stopWatch.Stop();
                taskLogger.Information($"ACTIVATED after {stopWatch.Elapsed.Milliseconds / 1000.0f} second wait.");
                
                var when = (DateTime.UtcNow - epoch);
                stopWatch.Restart();

                void launchTasks()
                {
                    while (true)
                    {
                        ManualResetEvent nextEntry;
                        lock (timings)
                        {
                            if (!workQueue.Any())
                            {
                                desiredRunCount++;
                                taskLogger.Information($"Empty work Queue, increased desiredRunCount to {desiredRunCount}.");
                                break;
                            }

                            if (concurrentThreads.Count >= desiredConcurrency)
                            {
                                taskLogger.Information($"Desired thread concurrency exceeded {concurrentThreads.Count} >= {desiredConcurrency}.");
                                if (desiredRunCount > 1 && manageRequestsTotal % (desiredRunCount + concurrentThreads.Count) == 0)
                                    desiredRunCount--;
                                break;
                            }

                            nextEntry = workQueue.Dequeue();
                        }
                        taskLogger.Information($"Launching next task.");
                        nextEntry.Set();
                        Thread.Sleep(0);
                    }
                }
                
                try
                {
                    var task = criticalSection();
                    var result = await task;
                    stopWatch.Stop();
                    return result;
                }
                catch (Exception ex)
                {
                    taskLogger.Information($"EXCEPTION: {ex.Message}");
                    return default(TResult);
                }
                finally
                {
                    taskLogger.Information($"COMPLETED after {stopWatch.Elapsed.Milliseconds / 1000.0f} second processing.");
                    string infoLog;
                    lock (timings)
                    {
                        var concurrentEnd = concurrentThreads.Count;
                        if (concurrentEnd <= 0)
                            concurrentEnd = 1;
                        concurrentThreads.Remove(mutex);
                        var durationTicks = stopWatch.Elapsed.Ticks / concurrentEnd;

                        var averageDuration = manageRequestsProcessed <= 0 ?
                            long.MaxValue
                            :
                            totalDuration / manageRequestsProcessed;
                        totalDuration += durationTicks;
                        manageRequestsProcessed++;

                        var isImproved = (durationTicks <= averageDuration);
                        if (isImproved)
                            desiredConcurrency++;
                        else
                        {
                            if (desiredConcurrency > 1)
                                desiredConcurrency--;
                        }
                        infoLog = $"{concurrentThreads.Count} / {desiredConcurrency} => {isImproved} = {averageDuration} > {durationTicks} = {stopWatch.Elapsed.Ticks} / {concurrentEnd}";
                    }
                    taskLogger.Information(infoLog);

                    launchTasks();

                    taskLogger.Information($"END");
                }
            }

            private EventWaitHandle gateway = new AutoResetEvent(false);
            private int runCount = 0;
            internal async Task<TTask> RunTask<TTask>(Func<Task<TTask>> getNextTask)
            {
                EventWaitHandle blocker;
                string infoLog = "EXCEPTION";
                lock (timings)
                {
                    infoLog = $"Runlist = {runCount} / {this.desiredRunCount}";
                    blocker = (runCount >= this.desiredRunCount) ?
                        gateway
                        :
                        new ManualResetEvent(true);
                    runCount++;
                }
                this.logger.Information(infoLog);

                blocker.WaitOne();
                this.logger.Information($"Passed GATEWAY {blocker}");
                try
                {
                    var nextTask = Task.Run<TTask>(getNextTask);
                    var result = await nextTask;
                    return result;
                }
                catch (Exception ex)
                {
                    this.logger.Information($"EXCEPTION:{ex.Message}");
                    return default(TTask);
                }
                finally
                {
                    lock (timings)
                    {
                        runCount--;
                        this.logger.Information($"Runcount:{runCount}");
                        gateway.Set();
                    }
                }
                
            }
        }

        private class UnthrottledPerformanceManager<TResult> : IManagePerformance<TResult>
        {
            public Task<TResult> Manage(Func<Task<TResult>> criticalSection)
            {
                return criticalSection();
            }
        }

        public static IManagePerformance<T> GetDefaultIfNull<T>(this IManagePerformance<T> performanceManagerMaybe)
        {
            if (performanceManagerMaybe.IsDefaultOrNull())
                return new UnthrottledPerformanceManager<T>();

            return performanceManagerMaybe;
        }

        private class CastedPerformanceManager<TSource, TConverted> : IManagePerformance<TConverted>
        {
            private IManagePerformance<TSource> sourceManager;
            private Func<TConverted, TSource> cast;

            public CastedPerformanceManager(IManagePerformance<TSource> sourceManager, Func<TConverted, TSource> cast)
            {
                this.sourceManager = sourceManager;
                this.cast = cast;
            }

            public async Task<TConverted> Manage(Func<Task<TConverted>> criticalSection)
            {
                var storage = default(TConverted);
                var result2 = await sourceManager.Manage(
                    async () =>
                    {
                        storage = await criticalSection();
                        var r1 = cast(storage);
                        return r1;
                    });
                return storage;
            }
        }
        
        private struct Timing
        {
            //public TimeSpan when;
            //public TimeSpan duration;
        }
        
        public static IEnumerableAsync<TResult> Throttle<TSource, TResult, TThrottle>(this IEnumerable<TSource> enumerable,
            Func<TSource, IManagePerformance<TThrottle>, Task<TResult>> selectKey,
            int desiredRunCount = 1,
            ILogger log = default(ILogger))
        {
            var throttler = new PerformanceManager<TThrottle>(desiredRunCount, log);
            var runList = new List<Task>();
            return enumerable
                .Select(
                    item => throttler.RunTask(() => selectKey(item, throttler)))
                .AsyncEnumerable();
        }

        /// <summary>
        /// Read remarks.
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <typeparam name="TThrottle"></typeparam>
        /// <param name="enumerable"></param>
        /// <param name="selectKey"></param>
        /// <param name="initialBandwidth"></param>
        /// <param name="tag"></param>
        /// <returns></returns>
        /// <remarks>
        /// Throttling can only slow down iteration. Throttling cannot accelerate/force iteration. Therefore, unless the 
        /// calling method requests the tasks in the returned enumeration faster than the tasks' Results are awaited,
        /// the iteration will be sequention and Throttle will have a negligable effect. More specifically, 
        /// be sure to call a method such as Prespool, Batch, Array, etc before calling Await on the throttled IEnumerableAsync<Task<TResult>>.
        /// </remarks>
        public static IEnumerableAsync<Task<TResult>> Throttle<TSource, TResult, TThrottle>(this IEnumerableAsync<TSource> enumerable,
            Func<TSource, IManagePerformance<TThrottle>, Task<TResult>> selectKey,
            int desiredRunCount = 1,
            ILogger log = default(ILogger))
        {
            var logScope = log.CreateScope($"Throttle[{Guid.NewGuid()}]");
            var throttler = new PerformanceManager<TThrottle>(desiredRunCount, logScope);
            return enumerable
                .Batch()
                .SelectMany(
                    items =>
                    {
                        return items
                            .Select(item => throttler.RunTask(() => selectKey(item, throttler)));
                    });
        }
        
        public static IEnumerableAsync<TItem> Throttle<TItem>(this IEnumerableAsync<Task<TItem>> enumerable,
            int desiredRunCount = 1,
            ILogger log = default(ILogger))
        {
            var logScope = log.CreateScope($"Throttle");
            var taskList = new List<Task<TItem>>();
            var locker = new AutoResetEvent(true);
            var enumerator = enumerable.GetEnumerator();
            var moving = true;
            return Yield<TItem>(
                async (yieldReturn, yieldBreak) =>
                {
                    while (true)
                    {
                        locker.WaitOne();

                        if (taskList.Count >= desiredRunCount)
                        {
                            Task<TItem>[] tasks = taskList.ToArray();
                            var finishedTask = await Task.WhenAny(tasks);
                            taskList.Remove(finishedTask);
                            locker.Set();
                            var item = await finishedTask;
                            return yieldReturn(item);
                        }

                        if (!moving)
                        {
                            if (!taskList.Any())
                            {
                                locker.Set();
                                return yieldBreak;
                            }

                            Task<TItem>[] tasks = taskList.ToArray();
                            var finishedTask = await Task.WhenAny(tasks);
                            taskList.Remove(finishedTask);
                            locker.Set();
                            var item = await finishedTask;
                            return yieldReturn(item);
                        }

                        locker.Set();

                        if (!await enumerator.MoveNextAsync())
                        {
                            moving = false;
                            continue;
                        }
                        var nextTask = enumerator.Current;
                        locker.WaitOne();
                        taskList.Add(nextTask);
                        locker.Set();
                    }
                });
        }
    }
}
