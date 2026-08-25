using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Async
{
    public interface ITransactionResult<T>
    {
        TResult Success<TResult>(
            Func<TResult> onSuccess,
            Func<T, TResult> onFailure);

        Task RollbackAsync();
    }
}
