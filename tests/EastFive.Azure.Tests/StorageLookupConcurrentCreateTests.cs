using System;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

using EastFive;
using EastFive.Linq.Async;
using EastFive.Persistence;
using EastFive.Persistence.Azure.StorageTables;
using EastFive.Azure.Persistence.AzureStorageTables;

namespace EastFive.Azure.Tests;

/// <summary>Lookup target type; exists only to parameterize IRef.</summary>
public struct LookupSubject : IReferenceable
{
    public Guid id { get; set; }
}

/// <summary>Minimal entity carrying an [IdHashXX32Lookup]-indexed reference.</summary>
[StorageTable]
public struct LookupProbe : IReferenceable
{
    public Guid id => probeRef.id;

    [RowKey]
    [RowKeyPrefix(Characters = 2)]
    public IRef<LookupProbe> probeRef;

    [ETag]
    public string eTag;

    [Storage]
    [IdHashXX32Lookup]
    public IRef<LookupSubject> subject;

    [Storage]
    public string note;
}

/// <summary>
/// Regression: concurrent StorageCreateAsync calls on the SAME deterministic
/// row id each pre-write the lookup bucket; a losing racer's already-exists
/// rollback used to strip the winner's index entry (set-based Except defeated
/// the bucket's deliberate duplicate retention) — the row existed but
/// lookup-by-property missed it. The fix is multiset rollback semantics:
/// each rollback removes only its own instances.
/// </summary>
public class StorageLookupConcurrentCreateTests
{
    public StorageLookupConcurrentCreateTests() => TestConfiguration.Ensure();

    [Fact]
    public async Task ContestedCreateLeavesWinnersLookupIntact()
    {
        // Race-dependent bug: iterate to give losers a real chance to
        // interleave their pre-write/rollback with the winner's.
        for (var round = 0; round < 10; round++)
        {
            var subjectRef = Guid.NewGuid().AsRef<LookupSubject>();
            var rowId = Guid.NewGuid();
            var entity = new LookupProbe
            {
                probeRef = rowId.AsRef<LookupProbe>(),
                subject = subjectRef,
                note = $"round {round}",
            };

            var racers = Enumerable.Range(0, 8)
                .Select(_ => Task.Run(() => entity.StorageCreateAsync(
                    onCreated: _ => true,
                    onAlreadyExists: () => false)))
                .ToArray();
            var outcomes = await Task.WhenAll(racers);

            Assert.Equal(1, outcomes.Count(won => won));

            var found = await subjectRef
                .StorageGetByIdProperty((LookupProbe p) => p.subject)
                .ToArrayAsync();

            Assert.Single(found);
            Assert.Equal(rowId, found[0].id);
        }
    }

    [Fact]
    public async Task UncontestedCreateIsFoundByLookup()
    {
        var subjectRef = Guid.NewGuid().AsRef<LookupSubject>();
        var rowId = Guid.NewGuid();
        var entity = new LookupProbe
        {
            probeRef = rowId.AsRef<LookupProbe>(),
            subject = subjectRef,
            note = "solo",
        };

        var created = await entity.StorageCreateAsync(
            onCreated: _ => true,
            onAlreadyExists: () => false);
        Assert.True(created);

        var found = await subjectRef
            .StorageGetByIdProperty((LookupProbe p) => p.subject)
            .ToArrayAsync();

        Assert.Single(found);
        Assert.Equal(rowId, found[0].id);
    }
}
