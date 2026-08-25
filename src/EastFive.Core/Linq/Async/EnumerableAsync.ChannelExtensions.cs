using EastFive.Analytics;
using EastFive.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace EastFive.Linq.Async
{
    /// <summary>
    /// Channel-based extensions for IEnumerableAsync that enable efficient producer-consumer patterns
    /// while maintaining functional composition style.
    /// </summary>
    public static class EnumerableAsyncChannelExtensions
    {
        /// <summary>
        /// Converts IEnumerableAsync to Channel-based buffering for producer-consumer scenarios.
        /// Yields batches of items as they become available in the buffer.
        /// Maintains functional composition while enabling efficient buffering.
        /// </summary>
        /// <param name="bufferSize">Maximum number of items to buffer before blocking producer</param>
        /// <param name="diagnostics">Optional logger for debugging</param>
        public static IEnumerableAsync<T[]> BatchWithChannels<T>(this IEnumerableAsync<T> enumerable,
            int bufferSize = 1000,
            ILogger diagnostics = default)
        {
            var channel = Channel.CreateBounded<T>(new BoundedChannelOptions(bufferSize)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true
            });

            var producerTask = ProduceAsync(enumerable, channel.Writer, diagnostics);

            return ConsumeInBatches(channel.Reader, producerTask, diagnostics);
        }

        /// <summary>
        /// Converts IEnumerableAsync to fixed-size batches using Channel-based buffering.
        /// Yields batches when they reach the specified size, or when the producer completes.
        /// </summary>
        /// <param name="batchSize">Number of items per batch</param>
        /// <param name="bufferSize">Maximum number of items to buffer before blocking producer</param>
        /// <param name="diagnostics">Optional logger for debugging</param>
        public static IEnumerableAsync<T[]> BatchFixedWithChannels<T>(this IEnumerableAsync<T> enumerable,
            int batchSize,
            int bufferSize = 1000,
            ILogger diagnostics = default)
        {
            if (batchSize < 1)
                throw new ArgumentException("Batch size must be at least 1", nameof(batchSize));

            var channel = Channel.CreateBounded<T>(new BoundedChannelOptions(bufferSize)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true
            });

            var producerTask = ProduceAsync(enumerable, channel.Writer, diagnostics);

            return ConsumeFixedBatches(channel.Reader, producerTask, batchSize, diagnostics);
        }

        /// <summary>
        /// Enhanced Prespool that returns IEnumerableAsync with configurable buffering.
        /// Uses Channel-based buffering for efficient read-ahead while maintaining lazy pull semantics.
        /// </summary>
        /// <param name="bufferSize">Number of items to buffer ahead</param>
        /// <param name="diagnostics">Optional logger for debugging</param>
        public static IEnumerableAsync<T> PrespoolWithChannels<T>(this IEnumerableAsync<T> items,
            int bufferSize = 100,
            ILogger diagnostics = default)
        {
            return items
                .BatchWithChannels(bufferSize, diagnostics)
                .SelectMany();
        }

        /// <summary>
        /// Awaits tasks from an async enumerable with read-ahead buffering using Channels.
        /// Buffers up to 'readAhead' tasks before blocking the producer.
        /// </summary>
        /// <param name="readAhead">Number of tasks to buffer ahead (minimum 1)</param>
        /// <param name="diagnostics">Optional logger for debugging</param>
        public static IEnumerableAsync<TItem> AwaitWithChannels<TItem>(
            this IEnumerableAsync<Task<TItem>> enumerable,
            int readAhead,
            ILogger diagnostics = default)
        {
            if (readAhead < 1)
                readAhead = 1;

            var channel = Channel.CreateBounded<Task<TItem>>(new BoundedChannelOptions(readAhead)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true
            });

            var producerTask = ProduceTasksAsync(enumerable, channel.Writer, diagnostics);

            return ConsumeAndAwaitTasks(channel.Reader, producerTask, diagnostics);
        }

        /// <summary>
        /// Awaits tasks from an async enumerable with adaptive read-ahead that scales based on consumption rate.
        /// Starts with minimal buffering and increases when the consumer is waiting for items.
        /// </summary>
        /// <param name="maxReadAhead">Maximum number of tasks to buffer ahead</param>
        /// <param name="diagnostics">Optional logger for debugging</param>
        public static IEnumerableAsync<TItem> AwaitAdaptive<TItem>(
            this IEnumerableAsync<Task<TItem>> enumerable,
            int initialConcurrency = 2,
            int maxReadAhead = 10,
            ILogger diagnostics = default)
        {
            if (maxReadAhead < 1)
                maxReadAhead = 1;

            // Use unbounded channel so producer never blocks
            var channel = Channel.CreateUnbounded<Task<TItem>>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true
            });

            // Semaphore controls how many items can be in-flight
            // Start with 1 (sequential) and adapt based on demand
            var state = new AdaptiveState { CurrentConcurrency = initialConcurrency, MaxReadAhead = maxReadAhead };
            var inflightSemaphore = new SemaphoreSlim(initialConcurrency, maxReadAhead);
            
            var producerTask = ProduceAdaptiveAsync(enumerable, channel.Writer, inflightSemaphore, diagnostics);

            return ConsumeAdaptive(channel.Reader, producerTask, inflightSemaphore, state, diagnostics: diagnostics);
        }

        private class AdaptiveState
        {
            public int CurrentConcurrency;
            public int MaxReadAhead;
            public readonly object Lock = new object();
        }

        private static async Task ProduceAdaptiveAsync<T>(
            IEnumerableAsync<Task<T>> source,
            ChannelWriter<Task<T>> writer,
            SemaphoreSlim inflightSemaphore,
            ILogger diagnostics)
        {
            try
            {
                diagnostics?.Trace("Adaptive producer starting");
                var enumerator = source.GetEnumerator();
                
                while (await enumerator.MoveNextAsync())
                {
                    // Wait for permission to add another in-flight task
                    await inflightSemaphore.WaitAsync();
                    diagnostics?.Trace($"Writing task to channel (inflight slots: {inflightSemaphore.CurrentCount})");
                    await writer.WriteAsync(enumerator.Current);
                }
                
                diagnostics?.Trace("Adaptive producer completed");
                writer.Complete();
            }
            catch (Exception ex)
            {
                diagnostics?.Trace($"Adaptive producer exception: {ex.Message}");
                writer.Complete(ex);
                throw;
            }
        }

        private static IEnumerableAsync<T> ConsumeAdaptive<T>(
            ChannelReader<Task<T>> reader,
            Task producerTask,
            SemaphoreSlim inflightSemaphore,
            AdaptiveState state,
            int fastReadThresholdMs = 10, // If read takes < 10ms, consumer is waiting
            int readsBeforeScaleUp = 3,   // Scale up after 3 consecutive fast reads
            ILogger diagnostics = null)
        {
            var lastReadTime = DateTime.UtcNow;
            var consecutiveFastReads = 0;
            
            return EnumerableAsync.Yield<T>(
                async (yieldReturn, yieldBreak) =>
                {
                    if (await reader.WaitToReadAsync())
                    {
                        if (reader.TryRead(out var task))
                        {
                            var waitTime = (DateTime.UtcNow - lastReadTime).TotalMilliseconds;
                            
                            // Check if we should scale up concurrency
                            if (waitTime < fastReadThresholdMs)
                            {
                                consecutiveFastReads++;
                                if (consecutiveFastReads >= readsBeforeScaleUp)
                                {
                                    lock (state.Lock)
                                    {
                                        if (state.CurrentConcurrency < state.MaxReadAhead)
                                        {
                                            state.CurrentConcurrency++;
                                            inflightSemaphore.Release(); // Allow one more in-flight
                                            diagnostics?.Trace($"Scaled up concurrency to {state.CurrentConcurrency}");
                                        }
                                    }
                                    consecutiveFastReads = 0;
                                }
                            }
                            else
                            {
                                consecutiveFastReads = 0;
                            }
                            
                            diagnostics?.Trace("Awaiting task from channel");
                            var result = await task;
                            
                            // Release the semaphore slot so producer can queue the next item
                            inflightSemaphore.Release();
                            
                            lastReadTime = DateTime.UtcNow;
                            diagnostics?.Trace("Task completed, yielding result");
                            return yieldReturn(result);
                        }
                    }

                    // Channel is complete
                    await producerTask;
                    diagnostics?.Trace("Adaptive consumer completed");
                    return yieldBreak;
                });
        }

        #region Private Implementation

        private static async Task ProduceAsync<T>(
            IEnumerableAsync<T> source,
            ChannelWriter<T> writer,
            ILogger diagnostics)
        {
            try
            {
                diagnostics?.Trace("Producer starting");
                var enumerator = source.GetEnumerator();
                
                while (await enumerator.MoveNextAsync())
                {
                    diagnostics?.Trace("Writing item to channel");
                    await writer.WriteAsync(enumerator.Current);
                }
                
                diagnostics?.Trace("Producer completed");
                writer.Complete();
            }
            catch (Exception ex)
            {
                diagnostics?.Trace($"Producer exception: {ex.Message}");
                writer.Complete(ex);
                throw;
            }
        }

        private static async Task ProduceTasksAsync<T>(
            IEnumerableAsync<Task<T>> source,
            ChannelWriter<Task<T>> writer,
            ILogger diagnostics)
        {
            try
            {
                diagnostics?.Trace("Task producer starting");
                var enumerator = source.GetEnumerator();
                
                while (await enumerator.MoveNextAsync())
                {
                    diagnostics?.Trace("Writing task to channel");
                    await writer.WriteAsync(enumerator.Current);
                }
                
                diagnostics?.Trace("Task producer completed");
                writer.Complete();
            }
            catch (Exception ex)
            {
                diagnostics?.Trace($"Task producer exception: {ex.Message}");
                writer.Complete(ex);
                throw;
            }
        }

        private static IEnumerableAsync<T> ConsumeAndAwaitTasks<T>(
            ChannelReader<Task<T>> reader,
            Task producerTask,
            ILogger diagnostics)
        {
            return EnumerableAsync.Yield<T>(
                async (yieldReturn, yieldBreak) =>
                {
                    if (await reader.WaitToReadAsync())
                    {
                        if (reader.TryRead(out var task))
                        {
                            diagnostics?.Trace("Awaiting task from channel");
                            var result = await task;
                            diagnostics?.Trace("Task completed, yielding result");
                            return yieldReturn(result);
                        }
                    }

                    // Channel is complete
                    await producerTask;
                    diagnostics?.Trace("Consumer completed");
                    return yieldBreak;
                });
        }

        private static IEnumerableAsync<T[]> ConsumeInBatches<T>(
            ChannelReader<T> reader,
            Task producerTask,
            ILogger diagnostics)
        {
            var currentBatch = new List<T>();

            return EnumerableAsync.Yield<T[]>(
                async (yieldReturn, yieldBreak) =>
                {
                    // Try to read a batch of items
                    while (await reader.WaitToReadAsync())
                    {
                        // Read all immediately available items
                        while (reader.TryRead(out var item))
                        {
                            currentBatch.Add(item);
                        }

                        if (currentBatch.Any())
                        {
                            var batch = currentBatch.ToArray();
                            currentBatch.Clear();
                            diagnostics?.Trace($"Yielding batch of {batch.Length} items");
                            return yieldReturn(batch);
                        }
                    }

                    // Channel is complete, return any remaining items
                    if (currentBatch.Any())
                    {
                        var finalBatch = currentBatch.ToArray();
                        currentBatch.Clear();
                        diagnostics?.Trace($"Yielding final batch of {finalBatch.Length} items");
                        return yieldReturn(finalBatch);
                    }

                    // Ensure producer task completed successfully
                    await producerTask;
                    
                    diagnostics?.Trace("Consumer completed");
                    return yieldBreak;
                });
        }

        private static IEnumerableAsync<T[]> ConsumeFixedBatches<T>(
            ChannelReader<T> reader,
            Task producerTask,
            int batchSize,
            ILogger diagnostics)
        {
            var currentBatch = new List<T>(batchSize);

            return EnumerableAsync.Yield<T[]>(
                async (yieldReturn, yieldBreak) =>
                {
                    while (currentBatch.Count < batchSize)
                    {
                        if (await reader.WaitToReadAsync())
                        {
                            if (reader.TryRead(out var item))
                            {
                                currentBatch.Add(item);
                                continue;
                            }
                        }

                        // Channel closed - yield partial batch if any
                        if (currentBatch.Any())
                        {
                            var finalBatch = currentBatch.ToArray();
                            currentBatch.Clear();
                            diagnostics?.Trace($"Yielding partial batch of {finalBatch.Length} items");
                            return yieldReturn(finalBatch);
                        }

                        await producerTask;
                        diagnostics?.Trace("Consumer completed");
                        return yieldBreak;
                    }

                    // Batch is full
                    var batch = currentBatch.ToArray();
                    currentBatch.Clear();
                    diagnostics?.Trace($"Yielding full batch of {batch.Length} items");
                    return yieldReturn(batch);
                });
        }

        #endregion
    }
}
