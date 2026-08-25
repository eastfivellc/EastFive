using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using EastFive.Linq;
using EastFive;
using EastFive.Extensions;

namespace EastFive.Persistence
{
    public class RollbackAsync<TFailure>
    {
        public class RollbackResult
        {
            public TFailure FailureResult { get; private set; }
            
            public bool IsFailure { get; private set; }

            private Func<Task> rollback = default(Func<Task>);

            private RollbackResult()
            {

            }

            internal static RollbackResult Success(Func<Task> rollback)
            {
                return new RollbackResult()
                {
                    rollback = rollback,
                    IsFailure = false,
                };
            }

            internal static RollbackResult Failure(TFailure failureResult)
            {
                return new RollbackResult()
                {
                    rollback = default(Func<Task>),
                    FailureResult = failureResult,
                    IsFailure = true,
                };
            }

            internal Task Rollback()
            {
                if (default(Func<Task>) != rollback)
                    return rollback();
                return true.AsTask();
            }
        }

        public delegate Task<RollbackResult> RollbackTaskDelegate(
            Func<Func<Task>, RollbackResult> success,
            Func<TFailure, RollbackResult> failure);

        public RollbackAsync()
        {
            this.Tasks = new List<RollbackTaskDelegate>();
        }

        public List<RollbackTaskDelegate> Tasks;

        public void AddTask(RollbackTaskDelegate task)
        {
            this.Tasks.Add(task);
        }

        public async Task<TResult> ExecuteAsync<TResult>(
            Func<TResult> success,
            Func<TFailure, TResult> failed)
        {
            var results = await Tasks
                .Select(async task =>
                {
                    var rollbackResult = await task.Invoke(
                        (rollback) => RollbackResult.Success(rollback),
                        (failureResultReturned) => RollbackResult.Failure(failureResultReturned));
                    return rollbackResult;
                })
                .WhenAllAsync();

            var resultGlobal = await results.FirstOrDefault(
                result => result.IsFailure,
                async (failedResult) =>
                {
                    await results.Select(result => result.Rollback()).RunAllAsync();
                    return failed(failedResult.FailureResult);
                },
                async () =>
                {
                    return await success().AsTask();
                });
            return resultGlobal;
        }
    }

    public class RollbackAsync<TSuccess, TFailure>
    {
        public class RollbackResult
        {
            public TFailure FailureResult { get; private set; }

            internal TSuccess SuccessResult { get; set; }

            public bool IsFailure { get; private set; }

            private Func<Task> rollback = default(Func<Task>);

            private RollbackResult()
            {

            }

            internal static RollbackResult Success(TSuccess successResult, Func<Task> rollback)
            {
                return new RollbackResult()
                {
                    rollback = rollback,
                    SuccessResult = successResult,
                    IsFailure = false,
                };
            }

            internal static RollbackResult Failure(TFailure failureResult)
            {
                return new RollbackResult()
                {
                    rollback = default(Func<Task>),
                    FailureResult = failureResult,
                    IsFailure = true,
                };
            }

            internal Task Rollback()
            {
                if (default(Func<Task>) != rollback)
                    return rollback();
                return true.AsTask();
            }
        }

        public delegate Task<RollbackResult> RollbackTaskDelegate(Func<TSuccess, Func<Task>, RollbackResult> success, Func<TFailure, RollbackResult> failure);

        public RollbackAsync()
        {
            this.Tasks = new List<RollbackTaskDelegate>();
        }
        
        public List<RollbackTaskDelegate> Tasks;

        public void AddTask(RollbackTaskDelegate task)
        {
            this.Tasks.Add(task);
        }

        public async Task<TResult> ExecuteAsync<TResult>(
            Func<TSuccess[], TResult> success,
            Func<TFailure, TResult> failed)
        {
            var results = await Tasks
                .Select(async task =>
                {
                    var rollbackResult = await task.Invoke(
                        (successResultReturned, rollback) =>
                            RollbackResult.Success(successResultReturned, rollback),
                        (failureResultReturned) => RollbackResult.Failure(failureResultReturned));
                    return rollbackResult;
                })
                .WhenAllAsync();

            var resultGlobal = await results.FirstOrDefault(
                result => result.IsFailure,
                async (failedResult) =>
                {
                    await results.Select(result => result.Rollback()).RunAllAsync();
                    return failed(failedResult.FailureResult);
                },
                async () =>
                {
                    var successes = results.Select(result => result.SuccessResult).ToArray();
                    return await success(successes).AsTask();
                });
            return resultGlobal;
        }
    }
}
