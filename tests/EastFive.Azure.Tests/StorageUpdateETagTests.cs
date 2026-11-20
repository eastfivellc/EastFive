using System;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

using EastFive;
using EastFive.Persistence;
using EastFive.Persistence.Azure.StorageTables;
using EastFive.Azure.Persistence.AzureStorageTables;

namespace EastFive.Azure.Tests;

/// <summary>A table with a public ETag member: its updates are conditional.</summary>
[StorageTable]
public class UpdateETagPublicProbe : IReferenceable
{
    public Guid id => probeRef.id;

    [RowKey]
    [RowKeyPrefix(Characters = 2)]
    public IRef<UpdateETagPublicProbe> probeRef;

    [ETag]
    public string eTag;

    [Storage]
    public string note;
}

/// <summary>A table with no ETag member: its updates are saved unconditionally.</summary>
#pragma warning disable E5001 // The test depends on the unconditional save the warning describes.
[StorageTable]
public class UpdateETagMissingProbe : IReferenceable
{
    public Guid id => probeRef.id;

    [RowKey]
    [RowKeyPrefix(Characters = 2)]
    public IRef<UpdateETagMissingProbe> probeRef;

    [Storage]
    public string note;
}

/// <summary>A table whose ETag member is not public, which the driver does not find.</summary>
[StorageTable]
public class UpdateETagInternalProbe : IReferenceable
{
    public Guid id => probeRef.id;

    [RowKey]
    [RowKeyPrefix(Characters = 2)]
    public IRef<UpdateETagInternalProbe> probeRef;

    [ETag]
    internal string eTag;

    [Storage]
    public string note;
}
#pragma warning restore E5001

/// <summary>
/// What <c>StorageUpdateAsync</c> does when a second caller changes the row while the first is
/// inside its mutation. The save is conditional only when the table type has a PUBLIC member
/// carrying <see cref="ETagAttribute"/>; otherwise the driver replaces with <c>*</c> and the last
/// writer wins. These are the reason for the E5001 warning in EastFive.Generators.
/// </summary>
public class StorageUpdateETagTests
{
    private static readonly TimeSpan GateTimeout = TimeSpan.FromSeconds(30);

    public StorageUpdateETagTests() => TestConfiguration.Ensure();

    private static TaskCompletionSource NewGate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Caller A reads the row and is held inside its mutation; caller B sets the note to "B";
    /// A is released and appends "+A" to the note it read. Returns how many times A's mutation
    /// ran and the stored note.
    /// </summary>
    private static async Task<(int runs, string note)> RaceAsync<TProbe>(
        Func<Guid, string, TProbe> create,
        Func<TProbe, string> noteOf,
        Func<TProbe, string, TProbe> withNote)
        where TProbe : IReferenceable
    {
        var id = Guid.NewGuid();
        Assert.True(await create(id, "created").StorageCreateAsync(
            onCreated: _ => true,
            onAlreadyExists: () => false));
        var probeRef = id.AsRef<TProbe>();

        var inside = NewGate();
        var release = NewGate();
        var runs = 0;

        var first = probeRef.StorageUpdateAsync<TProbe, bool>(
            async (probe, save) =>
            {
                if (Interlocked.Increment(ref runs) == 1)
                {
                    inside.SetResult();
                    await release.Task;
                }
                await save(withNote(probe, noteOf(probe) + "+A"));
                return true;
            });

        try
        {
            await inside.Task.WaitAsync(GateTimeout);
            Assert.True(await probeRef.StorageUpdateAsync<TProbe, bool>(
                async (probe, save) =>
                {
                    await save(withNote(probe, "B"));
                    return true;
                }));
        }
        finally
        {
            release.TrySetResult();
        }
        Assert.True(await first.WaitAsync(GateTimeout));

        var stored = await probeRef.StorageGetAsync(
            noteOf,
            () => throw new InvalidOperationException("The probe row is missing."));
        return (runs, stored);
    }

    [Fact]
    public async Task PublicETagMember_ConflictRerunsTheMutationOnTheNewRow()
    {
        var (runs, note) = await RaceAsync<UpdateETagPublicProbe>(
            (id, text) => new() { probeRef = id.AsRef<UpdateETagPublicProbe>(), note = text },
            probe => probe.note,
            (probe, text) => { probe.note = text; return probe; });

        Assert.Equal(2, runs);
        Assert.Equal("B+A", note);
    }

    [Fact]
    public async Task NoETagMember_LastWriterWinsAndTheMutationRunsOnce()
    {
        var (runs, note) = await RaceAsync<UpdateETagMissingProbe>(
            (id, text) => new() { probeRef = id.AsRef<UpdateETagMissingProbe>(), note = text },
            probe => probe.note,
            (probe, text) => { probe.note = text; return probe; });

        Assert.Equal(1, runs);
        Assert.Equal("created+A", note);
    }

    [Fact]
    public async Task InternalETagMember_BehavesAsNoETagMember()
    {
        var (runs, note) = await RaceAsync<UpdateETagInternalProbe>(
            (id, text) => new() { probeRef = id.AsRef<UpdateETagInternalProbe>(), note = text },
            probe => probe.note,
            (probe, text) => { probe.note = text; return probe; });

        Assert.Equal(1, runs);
        Assert.Equal("created+A", note);
    }
}
