using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EastFive;
using EastFive.Extensions;
using EastFive.Linq.Async;

namespace EastFive.Async
{
    public static class TransactionExtensions
    {
        private class TransactionResultPrivate<T> : ITransactionResult<T>
        {
            private T result;
            private Func<Task> rollbackAsync;
            private bool isSuccess;

            public TransactionResultPrivate(T result)
            {
                this.isSuccess = false;
                this.result = result;
            }

            public TransactionResultPrivate(Func<Task> rollbackAsync)
            {
                this.isSuccess = true;
                this.rollbackAsync = rollbackAsync;
            }

            public async Task RollbackAsync()
            {
                if (isSuccess)
                    await this.rollbackAsync();

            }

            public TResult Success<TResult>(
                Func<TResult> onSuccess,
                Func<T, TResult> onFailure)
            {
                if (this.isSuccess)
                    return onSuccess();
                return onFailure(this.result);
            }
        }
        
        public static ITransactionResult<T> TransactionResultFailure<T>(this T failureValue)
        {
            return new TransactionResultPrivate<T>(failureValue);
        }

        public static ITransactionResult<T> TransactionResultSuccess<T>(this Func<Task> rollback)
        {
            return new TransactionResultPrivate<T>(rollback);
        }

        public static async Task<T> ExecuteAsync<T>(this Task<ITransactionResult<T>>[] transactionResults,
            Func<T> onCompleteSuccess)
        {
            var transactionAsyncResults = transactionResults
                .AsyncEnumerable();
            return await await transactionAsyncResults
                .FirstMatchAsync(
                    (tr, match, next) =>
                    {
                        return tr.Success<EnumerableAsync.IFirstMatchResult<Task<T>>>(
                            () => next(),
                            (result) =>
                            {
                                async Task<T> DoIt()
                                {
                                    var tresult = await await transactionAsyncResults
                                        .Select(
                                            trr =>
                                            {
                                                return trr.RollbackAsync();
                                            })
                                        .ToArrayAsync(
                                            async (rollbacks) =>
                                            {
                                                await rollbacks.RunAllAsync();
                                                return result;
                                            });
                                    return tresult;
                                }
                                return match(DoIt());
                            });
                    },
                    () => onCompleteSuccess().AsTask());
        }
    }
}
