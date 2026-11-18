using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

using EastFive;
using EastFive.Persistence;
using EastFive.Persistence.Azure.StorageTables;
using EastFive.Azure.Persistence.AzureStorageTables;

namespace EastFive.Azure.Tests;

/// <summary>A lockable entity stored as a value type.</summary>
[StorageTable]
public struct LockedUpdateStructProbe : IReferenceable
{
    public Guid id => probeRef.id;

    [RowKey]
    [RowKeyPrefix(Characters = 2)]
    public IRef<LockedUpdateStructProbe> probeRef;

    [ETag]
    public string eTag;

    [Storage]
    public DateTime? lockedAt;

    [Storage]
    public string note;
}

/// <summary>The same entity as <see cref="LockedUpdateStructProbe"/>, stored as a reference type.</summary>
[StorageTable]
public class LockedUpdateClassProbe : IReferenceable
{
    public Guid id => probeRef.id;

    [RowKey]
    [RowKeyPrefix(Characters = 2)]
    public IRef<LockedUpdateClassProbe> probeRef;

    [ETag]
    public string eTag;

    [Storage]
    public DateTime? lockedAt;

    [Storage]
    public string note;
}

public class StorageLockedUpdateStructTests : StorageLockedUpdateContract<LockedUpdateStructProbe>
{
    protected override LockedUpdateStructProbe New(Guid id, string note) =>
        new() { probeRef = id.AsRef<LockedUpdateStructProbe>(), note = note };

    protected override Expression<Func<LockedUpdateStructProbe, DateTime?>> Lock => probe => probe.lockedAt;

    protected override string NoteOf(LockedUpdateStructProbe probe) => probe.note;

    protected override LockedUpdateStructProbe WithNote(LockedUpdateStructProbe probe, string note)
    {
        probe.note = note;
        return probe;
    }
}

public class StorageLockedUpdateClassTests : StorageLockedUpdateContract<LockedUpdateClassProbe>
{
    protected override LockedUpdateClassProbe New(Guid id, string note) =>
        new() { probeRef = id.AsRef<LockedUpdateClassProbe>(), note = note };

    protected override Expression<Func<LockedUpdateClassProbe, DateTime?>> Lock => probe => probe.lockedAt;

    protected override string NoteOf(LockedUpdateClassProbe probe) => probe.note;

    protected override LockedUpdateClassProbe WithNote(LockedUpdateClassProbe probe, string note)
    {
        probe.note = note;
        return probe;
    }
}

/// <summary>
/// The contract of <c>StorageLockedUpdateAsync</c>, run once for a struct entity and once for an
/// otherwise identical class entity. The lock and unlock used to be written to a boxed copy of a
/// struct and thrown away, so a struct row was never locked in storage and nothing was refused.
/// Every wait is on a gate and bounded, so a broken lock fails a test instead of hanging it.
/// </summary>
public abstract class StorageLockedUpdateContract<TProbe>
    where TProbe : IReferenceable
{
    private static readonly TimeSpan GateTimeout = TimeSpan.FromSeconds(30);

    protected StorageLockedUpdateContract() => TestConfiguration.Ensure();

    protected abstract TProbe New(Guid id, string note);

    protected abstract Expression<Func<TProbe, DateTime?>> Lock { get; }

    protected abstract string NoteOf(TProbe probe);

    protected abstract TProbe WithNote(TProbe probe, string note);

    private DateTime? LockOf(TProbe probe) => Lock.Compile()(probe);

    private async Task<IRef<TProbe>> CreateAsync(string note = "created")
    {
        var id = Guid.NewGuid();
        var created = await New(id, note).StorageCreateAsync(
            onCreated: _ => true,
            onAlreadyExists: () => false);
        Assert.True(created);
        return id.AsRef<TProbe>();
    }

    private static Task<TProbe> ReadAsync(IRef<TProbe> probeRef) =>
        probeRef.StorageGetAsync(
            probe => probe,
            () => throw new InvalidOperationException("The probe row is missing."));

    private static TaskCompletionSource NewGate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task HeldLockIsStoredAndRefusesASecondCaller()
    {
        var probeRef = await CreateAsync();
        var inside = NewGate();
        var release = NewGate();

        var holder = probeRef.StorageLockedUpdateAsync<TProbe, string>(Lock,
            async (probe, unlockAndSave, unlock) =>
            {
                inside.SetResult();
                await release.Task;
                await unlock();
                return "held";
            },
            onNotFound: () => "missing");

        try
        {
            await inside.Task.WaitAsync(GateTimeout);

            var stored = await ReadAsync(probeRef);
            Assert.NotNull(LockOf(stored));

            var second = await probeRef.StorageLockedUpdateAsync<TProbe, string>(Lock,
                async (probe, unlockAndSave, unlock) =>
                {
                    await unlock();
                    return "ran";
                },
                onNotFound: () => "missing",
                onAlreadyLocked: (retries, elapsed, probe, continueAquiring, force) =>
                    Task.FromResult("refused"));
            Assert.Equal("refused", second);
        }
        finally
        {
            release.TrySetResult();
            Assert.Equal("held", await holder.WaitAsync(GateTimeout));
        }
    }

    [Fact]
    public async Task UnlockClearsTheStoredLock()
    {
        var probeRef = await CreateAsync();
        DateTime? lockedInside = default;

        var result = await probeRef.StorageLockedUpdateAsync<TProbe, string>(Lock,
            async (probe, unlockAndSave, unlock) =>
            {
                lockedInside = LockOf(await ReadAsync(probeRef));
                await unlock();
                return "done";
            },
            onNotFound: () => "missing");

        Assert.Equal("done", result);
        Assert.NotNull(lockedInside);
        Assert.Null(LockOf(await ReadAsync(probeRef)));
    }

    [Fact]
    public async Task UnlockAndSaveClearsTheLockAndSavesTheChange()
    {
        var probeRef = await CreateAsync();
        DateTime? lockedInside = default;

        var result = await probeRef.StorageLockedUpdateAsync<TProbe, string>(Lock,
            async (probe, unlockAndSave, unlock) =>
            {
                lockedInside = LockOf(await ReadAsync(probeRef));
                await unlockAndSave((current, save) => save(WithNote(current, "saved")));
                return "done";
            },
            onNotFound: () => "missing");

        Assert.Equal("done", result);
        Assert.NotNull(lockedInside);
        var stored = await ReadAsync(probeRef);
        Assert.Null(LockOf(stored));
        Assert.Equal("saved", NoteOf(stored));
    }

    [Fact]
    public async Task ThrowingCallbackRethrowsAndClearsTheLock()
    {
        var probeRef = await CreateAsync();
        DateTime? lockedInside = default;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            probeRef.StorageLockedUpdateAsync<TProbe, string>(Lock,
                async (probe, unlockAndSave, unlock) =>
                {
                    lockedInside = LockOf(await ReadAsync(probeRef));
                    throw new InvalidOperationException("callback failed");
                },
                onNotFound: () => "missing"));

        Assert.NotNull(lockedInside);
        Assert.Null(LockOf(await ReadAsync(probeRef)));
    }

    [Fact]
    public async Task ConcurrentCallersOnAnUnlockedRowHaveOneWinner()
    {
        const int callers = 5;
        var probeRef = await CreateAsync();
        var start = NewGate();
        var release = NewGate();
        var settled = NewGate();
        var entered = 0;
        var refused = 0;

        // Every caller either enters or is refused; the gate opens once all have, however many
        // entered, so a lock that is not held fails the count below rather than hanging.
        void Settle()
        {
            if (Volatile.Read(ref entered) + Volatile.Read(ref refused) == callers)
                settled.TrySetResult();
        }

        var racers = Enumerable.Range(0, callers)
            .Select(_ => Task.Run(async () =>
            {
                await start.Task;
                return await probeRef.StorageLockedUpdateAsync<TProbe, string>(Lock,
                    async (probe, unlockAndSave, unlock) =>
                    {
                        Interlocked.Increment(ref entered);
                        Settle();
                        await release.Task;
                        await unlock();
                        return "entered";
                    },
                    onNotFound: () => "missing",
                    onAlreadyLocked: (retries, elapsed, probe, continueAquiring, force) =>
                    {
                        Interlocked.Increment(ref refused);
                        Settle();
                        return Task.FromResult("refused");
                    });
            }))
            .ToArray();

        start.SetResult();
        try
        {
            await settled.Task.WaitAsync(GateTimeout);
        }
        finally
        {
            release.TrySetResult();
        }
        var outcomes = await Task.WhenAll(racers).WaitAsync(GateTimeout);

        Assert.Equal(1, outcomes.Count(outcome => outcome == "entered"));
        Assert.Equal(callers - 1, outcomes.Count(outcome => outcome == "refused"));
        Assert.Null(LockOf(await ReadAsync(probeRef)));
    }
}
