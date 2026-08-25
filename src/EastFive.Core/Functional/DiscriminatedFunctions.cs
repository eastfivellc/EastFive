using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Functional
{
    public static partial class DiscriminatedFunctions
    {
        public static Func<Func<TStep, TResult>, TResult> AsDiscriminatedStep<TStep, TResult>(this TStep stepValue)
        {
            Func<Func<TStep, TResult>, TResult> callback = 
                (nextStep) =>
                {
                    return nextStep(stepValue);
                };

            return callback;
        }

        public static Func<Func<TStep, TResult>, TResult> AsDiscriminatedResult<TStep, TResult>(this Func<TResult> stepValue)
        {
            Func<Func<TStep, TResult>, TResult> callback =
                (nextStep) =>
                {
                    return stepValue();
                };

            return callback;
        }

        public delegate Task<TResult> InvokeDelayedAsyncCallback<TDelayed, TResult>(Func<TDelayed, Task<TResult>> proceedWithDelayed);

        private static Task RunDelayedCallback<TDelayed, TResult>(
            InvokeDelayedAsyncCallback<TDelayed, TResult> callback,
            TaskCompletionSource<(bool proceeded, TDelayed value)> delayedTaskSource,
            TaskCompletionSource<TResult> resultTaskSource)
        {
            return Task.Run(async () =>
            {
                var result = await callback(async (delayed) =>
                {
                    delayedTaskSource.SetResult((true, delayed));
                    return await resultTaskSource.Task;
                });
                
                // If we get here without proceeding, the callback short-circuited
                if (!delayedTaskSource.Task.IsCompleted)
                {
                    delayedTaskSource.SetResult((false, default));
                    resultTaskSource.TrySetResult(result);
                }
            });
        }

        // public static Func<TPrime1, TPrime2, Task<TResult>> InvokeDelayed<TPrime1, TPrime2,
        //  TDelayed1, TDelayed2, TDelayed3, TDelayed4, TResult>(
        //     this Func<TPrime1, TPrime2, TDelayed1, TDelayed2, TDelayed3, TDelayed4, Task<TResult>> func,
        //     InvokeDelayedAsyncCallback<TDelayed1, TResult> delayed1Callback,
        //     InvokeDelayedAsyncCallback<TDelayed2, TResult> delayed2Callback,
        //     InvokeDelayedAsyncCallback<TDelayed3, TResult> delayed3Callback,
        //     InvokeDelayedAsyncCallback<TDelayed4, TResult> delayed4Callback)
        // {
        //         // Create task completion sources to coordinate the callbacks
        //         var delayed1TaskSource = new TaskCompletionSource<(bool proceeded, TDelayed1 value)>();
        //         var delayed2TaskSource = new TaskCompletionSource<(bool proceeded, TDelayed2 value)>();
        //         var delayed3TaskSource = new TaskCompletionSource<(bool proceeded, TDelayed3 value)>();
        //         var delayed4TaskSource = new TaskCompletionSource<(bool proceeded, TDelayed4 value)>();
                
        //         var resultTaskSource = new TaskCompletionSource<TResult>();

        //         // Start all callbacks in parallel using the helper method
        //         var task1 = RunDelayedCallback(delayed1Callback, delayed1TaskSource, resultTaskSource);
        //         var task2 = RunDelayedCallback(delayed2Callback, delayed2TaskSource, resultTaskSource);
        //         var task3 = RunDelayedCallback(delayed3Callback, delayed3TaskSource, resultTaskSource);
        //     var task4 = RunDelayedCallback(delayed4Callback, delayed4TaskSource, resultTaskSource);
                
        //     return async (prime1, prime2) =>
        //     {
        //         // Race between short-circuit result and all delayed values being ready
        //         var allDelayedValuesTask = Task.WhenAll(
        //             delayed1TaskSource.Task,
        //             delayed2TaskSource.Task,
        //             delayed3TaskSource.Task,
        //             delayed4TaskSource.Task);

        //         // Wait for either a short-circuit or all delayed values
        //         await Task.WhenAny(resultTaskSource.Task, allDelayedValuesTask);

        //         // If any callback short-circuited, return that result immediately
        //         if (resultTaskSource.Task.IsCompleted)
        //             return await resultTaskSource.Task;

        //         // All callbacks proceeded - get their values
        //         var delayed1Result = await delayed1TaskSource.Task;
        //         var delayed2Result = await delayed2TaskSource.Task;
        //         var delayed3Result = await delayed3TaskSource.Task;
        //         var delayed4Result = await delayed4TaskSource.Task;

        //         // Call the original function and set the result
        //         var finalResult = await func(prime1, prime2, 
        //             delayed1Result.value, 
        //             delayed2Result.value, 
        //             delayed3Result.value, 
        //             delayed4Result.value);
                
        //         resultTaskSource.SetResult(finalResult);
        //         return finalResult;
        //     };
        // }
    }
}
