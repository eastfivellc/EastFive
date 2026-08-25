using System;
using System.Threading.Tasks;

namespace EastFive.Azure.Synchronization
{
    public class Mutexes
    {
        public static Task<TResult> SynchronizeLockedAsync<TResult>(Guid integrationId, string resourceKey, string action, //integrationId (const guid), resourceKEy (3char), action (const string), (hash over to make unique)make new file, ignore custome partition (use deefault -12 - 12)
            Func<TimeSpan?, // time since last sync
                Func<TResult, Task<Persistence.MutexDocument.ILockResult<TResult>>>,
                Func<TResult, Task<Persistence.MutexDocument.ILockResult<TResult>>>,
                Task<Persistence.MutexDocument.ILockResult<TResult>>> onLockAquired,
            Func<int,       // retry count
                TimeSpan,   // retry duration
                TimeSpan?,  // time since last sync
                TimeSpan?,  // time locked
                Func<Task<TResult>>,
                Func<Task<TResult>>,
                Task<TResult>> onAlreadyLocked)
        {
            return Persistence.MutexDocument.SynchronizeLockedAsync<TResult>(integrationId, resourceKey, action,
                async (lastSyncDuration, unlockAndSave, unlock) =>
                {
                    var result = await onLockAquired(lastSyncDuration,
                        (t) => unlockAndSave(t),
                        (t) => unlock(t));
                    return result;
                },
                (retryCount, retryDuration, lastSyncDuration, lockedDuration, continueAquiring, force) =>
                {
                    return onAlreadyLocked(retryCount, retryDuration, lastSyncDuration, lockedDuration, continueAquiring, force);
                });
        }

        public static Task<TResult> DeleteAsync<TResult>(Guid integrationId, string resourceKey, string action,
            Func<TResult> onDeleted,
            Func<TResult> onNotFound)
        {
            return Persistence.MutexDocument.DeleteAsync(integrationId, resourceKey, action, onDeleted, onNotFound);
        }
    }
}