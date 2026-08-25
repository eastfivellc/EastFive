using System;
using System.Threading;
using System.Threading.Tasks;

namespace EastFive.Async;

public static class SemaphoreSlimExtensions
{
    public static async Task<IDisposable> LockAsync(this SemaphoreSlim semaphore)
    {
        await semaphore.WaitAsync();
        return new SemaphoreReleaser(semaphore);
    }

    private sealed class SemaphoreReleaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;

        public SemaphoreReleaser(SemaphoreSlim semaphore) => _semaphore = semaphore;

        public void Dispose() => _semaphore.Release();
    }
}
