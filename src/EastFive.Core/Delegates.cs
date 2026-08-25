using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlackBarLabs
{
    public delegate TResult DiscriminatedDelegate<TParam1, TResult>(Func<TParam1, TResult> callback);
    public delegate TResult DiscriminatedDelegate<TParam1, TParam2, TResult>(
        Func<TParam1, TResult> callback1,
        Func<TParam2, TResult> callback2);
    public delegate Task<TResult> DiscriminatedDelegateAsync<TParam1, TResult>(Func<TParam1, Task<TResult>> callback);
    public delegate Task<TResult> DiscriminatedDelegateAsync<TParam1, TParam2, TResult>(
        Func<TParam1, Task<TResult>> callback1,
        Func<TParam2, Task<TResult>> callback2);
}
