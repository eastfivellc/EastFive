using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.Azure.Cosmos.Table;
using Xunit;

using EastFive;
using EastFive.Linq.Async;
using EastFive.Persistence;
using EastFive.Persistence.Azure.StorageTables;
using EastFive.Persistence.Azure.StorageTables.Driver;
using EastFive.Azure.Persistence.AzureStorageTables;

namespace EastFive.Azure.Tests;

/// <summary>
/// A delete modifier that runs between the driver's read of a row and its delete. It records the
/// note each delete attempt saw, and runs the hook registered for the row (once) to play the
/// second writer.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class DeleteRaceHookAttribute : Attribute, IModifyAzureStorageTableSave
{
    private static readonly ConcurrentDictionary<Guid, Func<Task>> hooks = new();
    private static readonly ConcurrentDictionary<Guid, ConcurrentQueue<string>> seen = new();

    /// <summary>Runs <paramref name="hook"/> inside the next delete attempt of the row.</summary>
    public static void OnNextDelete(Guid id, Func<Task> hook) => hooks[id] = hook;

    /// <summary>The note each delete attempt of the row saw, in order.</summary>
    public static string[] NotesSeen(Guid id) =>
        seen.TryGetValue(id, out var notes) ? notes.ToArray() : Array.Empty<string>();

    public async Task<TResult> ExecuteDeleteAsync<TEntity, TResult>(MemberInfo memberInfo,
            string rowKeyRef, string partitionKeyRef,
            TEntity value, IDictionary<string, EntityProperty> dictionary,
            AzureTableDriverDynamic repository,
        Func<Func<Task>, TResult> onSuccessWithRollback,
        Func<TResult> onFailure)
    {
        var id = ((IReferenceable)value).id;
        seen.GetOrAdd(id, _ => new ConcurrentQueue<string>())
            .Enqueue(dictionary.TryGetValue("note", out var note) ? note.StringValue : null);
        if (hooks.TryRemove(id, out var hook))
            await hook();
        return onSuccessWithRollback(() => Task.CompletedTask);
    }

    public Task<TResult> ExecuteCreateAsync<TEntity, TResult>(MemberInfo memberInfo,
            string rowKeyRef, string partitionKeyRef,
            TEntity value, IDictionary<string, EntityProperty> dictionary,
            AzureTableDriverDynamic repository,
        Func<Func<Task>, TResult> onSuccessWithRollback,
        Func<TResult> onFailure) => Task.FromResult(onSuccessWithRollback(() => Task.CompletedTask));

    public Task<TResult> ExecuteInsertOrReplaceAsync<TEntity, TResult>(MemberInfo memberInfo,
            string rowKeyRef, string partitionKeyRef,
            TEntity value, IDictionary<string, EntityProperty> dictionary,
            AzureTableDriverDynamic repository,
        Func<Func<Task>, TResult> onSuccessWithRollback,
        Func<TResult> onFailure) => Task.FromResult(onSuccessWithRollback(() => Task.CompletedTask));

    public Task<TResult> ExecuteUpdateAsync<TEntity, TResult>(MemberInfo memberInfo,
            IAzureStorageTableEntity<TEntity> updatedEntity,
            IAzureStorageTableEntity<TEntity> existingEntity,
            AzureTableDriverDynamic repository,
        Func<Func<Task>, TResult> onSuccessWithRollback,
        Func<TResult> onFailure) => Task.FromResult(onSuccessWithRollback(() => Task.CompletedTask));

    public IEnumerable<IBatchModify> GetBatchCreateModifier<TEntity>(MemberInfo member,
        string rowKey, string partitionKey,
        TEntity entity, IDictionary<string, EntityProperty> serializedEntity) => Enumerable.Empty<IBatchModify>();

    public IEnumerable<IBatchModify> GetBatchDeleteModifier<TEntity>(MemberInfo member,
        string rowKey, string partitionKey,
        TEntity entity, IDictionary<string, EntityProperty> serializedEntity) => Enumerable.Empty<IBatchModify>();
}

/// <summary>A table with a public ETag member: its deletes are conditional on the ETag read.</summary>
[StorageTable]
public class DeleteRaceProbe : IReferenceable
{
    public Guid id => probeRef.id;

    [RowKey]
    [RowKeyPrefix(Characters = 2)]
    public IRef<DeleteRaceProbe> probeRef;

    [ETag]
    public string eTag;

    [Storage]
    [DeleteRaceHook]
    public string note;
}

/// <summary>
/// A table with a public ETag member and a lookup. The hook is declared before the lookup so the
/// second writer moves the lookup before the first attempt's lookup modifier runs.
/// </summary>
[StorageTable]
public class DeleteRaceLookupProbe : IReferenceable
{
    public Guid id => probeRef.id;

    [RowKey]
    [RowKeyPrefix(Characters = 2)]
    public IRef<DeleteRaceLookupProbe> probeRef;

    [ETag]
    public string eTag;

    [Storage]
    [DeleteRaceHook]
    public string note;

    [Storage]
    [IdHashXX32Lookup]
    public IRef<LookupSubject> subject;
}

/// <summary>A table with no ETag member: every delete is unconditional.</summary>
#pragma warning disable E5001 // The test depends on the unconditional delete the warning describes.
[StorageTable]
public class DeleteRaceNoETagProbe : IReferenceable
{
    public Guid id => probeRef.id;

    [RowKey]
    [RowKeyPrefix(Characters = 2)]
    public IRef<DeleteRaceNoETagProbe> probeRef;

    [Storage]
    [DeleteRaceHook]
    public string note;
}
#pragma warning restore E5001

/// <summary>
/// What a delete does when another write lands between the driver's read of the row and its
/// delete, on a table whose ETag member makes that delete conditional. The write is made by a
/// delete modifier (<see cref="DeleteRaceHookAttribute"/>), which the driver runs at exactly
/// that point.
/// </summary>
public class StorageDeleteETagTests
{
    public StorageDeleteETagTests() => TestConfiguration.Ensure();

    private static async Task<IRef<DeleteRaceProbe>> CreateAsync(string note)
    {
        var probeRef = Guid.NewGuid().AsRef<DeleteRaceProbe>();
        Assert.True(await new DeleteRaceProbe { probeRef = probeRef, note = note }
            .StorageCreateAsync(created => true, () => false));
        return probeRef;
    }

    private static Task<DeleteRaceProbe> ReadAsync(IRef<DeleteRaceProbe> probeRef)
        => probeRef.StorageGetAsync(probe => probe, () => throw new InvalidOperationException("The row is missing."));

    private static Task<bool> ExistsAsync<TProbe>(IRef<TProbe> probeRef) where TProbe : IReferenceable
        => probeRef.StorageGetAsync(probe => true, () => false);

    /// <summary>The second writer: sets the note, conditionally on the row it reads.</summary>
    private static void WriteBetweenReadAndDelete(IRef<DeleteRaceProbe> probeRef, string note)
        => DeleteRaceHookAttribute.OnNextDelete(probeRef.id,
            () => probeRef.StorageUpdateAsync<DeleteRaceProbe, bool>(
                async (probe, save) =>
                {
                    probe.note = note;
                    await save(probe);
                    return true;
                }));

    [Fact]
    public async Task ByReference_AWriteBetweenReadAndDelete_DeletesTheRow_AndReportsTheLaterWrite()
    {
        var probeRef = await CreateAsync("created");
        WriteBetweenReadAndDelete(probeRef, "later");

        var deleted = await probeRef.StorageDeleteAsync(
            onDeleted: probe => probe.note,
            onNotFound: () => "missing");

        Assert.Equal("later", deleted);
        Assert.False(await ExistsAsync(probeRef));
        Assert.Equal(new[] { "created", "later" }, DeleteRaceHookAttribute.NotesSeen(probeRef.id));
    }

    [Fact]
    public async Task HeldInstance_WithNoOnModified_AWriteBetweenReadAndDelete_DeletesTheRow()
    {
        var probeRef = await CreateAsync("created");
        var held = await ReadAsync(probeRef);
        WriteBetweenReadAndDelete(probeRef, "later");

        var deleted = await held.StorageDeleteAsync(onDeleted: () => "deleted");

        Assert.Equal("deleted", deleted);
        Assert.False(await ExistsAsync(probeRef));
        Assert.Equal(new[] { "created", "later" }, DeleteRaceHookAttribute.NotesSeen(probeRef.id));
    }

    [Fact]
    public async Task HeldInstance_WithOnModified_IsCalledOnceWithTheFreshRow_AndItsDeleteDeletes()
    {
        var probeRef = await CreateAsync("created");
        var held = await ReadAsync(probeRef);
        WriteBetweenReadAndDelete(probeRef, "later");
        var modified = new List<string>();

        var deleted = await held.StorageDeleteAsync(
            onDeleted: () => "deleted",
            onModified: (fresh, deleteAsync) =>
            {
                modified.Add(fresh.note);
                return deleteAsync();
            });

        Assert.Equal("deleted", deleted);
        Assert.Equal(new[] { "later" }, modified);
        Assert.False(await ExistsAsync(probeRef));
    }

    [Fact]
    public async Task DeleteIf_AWriteBetweenReadAndDelete_RerunsTheDecisionOnTheFreshRow_AndKeepsADeclinedRow()
    {
        var probeRef = await CreateAsync("created");
        WriteBetweenReadAndDelete(probeRef, "keep");
        var decided = new List<string>();

        var result = await probeRef.StorageDeleteIfAsync<DeleteRaceProbe, string>(
            async (probe, deleteAsync) =>
            {
                decided.Add(probe.note);
                if (probe.note == "keep")
                    return "declined";
                await deleteAsync();
                return "deleted";
            },
            onNotFound: () => "missing");

        Assert.Equal("declined", result);
        Assert.Equal(new[] { "created", "keep" }, decided);
        Assert.Equal("keep", (await ReadAsync(probeRef)).note);
    }

    [Fact]
    public async Task DeleteIf_AWriteBetweenReadAndDelete_DeletesWhenTheFreshRowIsStillWanted()
    {
        var probeRef = await CreateAsync("created");
        WriteBetweenReadAndDelete(probeRef, "later");
        var decided = new List<string>();

        var result = await probeRef.StorageDeleteIfAsync<DeleteRaceProbe, string>(
            async (probe, deleteAsync) =>
            {
                decided.Add(probe.note);
                await deleteAsync();
                return $"deleted:{probe.note}";
            },
            onNotFound: () => "missing");

        Assert.Equal("deleted:later", result);
        Assert.Equal(new[] { "created", "later" }, decided);
        Assert.False(await ExistsAsync(probeRef));
    }

    [Fact]
    public async Task ByReference_WithALookup_TheRetriedDeleteRemovesTheLaterValuesIndexEntry()
    {
        var before = Guid.NewGuid().AsRef<LookupSubject>();
        var after = Guid.NewGuid().AsRef<LookupSubject>();
        var probeRef = Guid.NewGuid().AsRef<DeleteRaceLookupProbe>();
        Assert.True(await new DeleteRaceLookupProbe { probeRef = probeRef, note = "created", subject = before }
            .StorageCreateAsync(created => true, () => false));
        DeleteRaceHookAttribute.OnNextDelete(probeRef.id,
            () => probeRef.StorageUpdateAsync<DeleteRaceLookupProbe, bool>(
                async (probe, save) =>
                {
                    probe.note = "later";
                    probe.subject = after;
                    await save(probe);
                    return true;
                }));

        var deleted = await probeRef.StorageDeleteAsync(
            onDeleted: probe => probe.subject.id,
            onNotFound: () => Guid.Empty);

        Assert.Equal(after.id, deleted);
        Assert.False(await ExistsAsync(probeRef));
        Assert.Empty(await after.StorageGetByIdProperty((DeleteRaceLookupProbe p) => p.subject).ToArrayAsync());
        Assert.Empty(await before.StorageGetByIdProperty((DeleteRaceLookupProbe p) => p.subject).ToArrayAsync());
    }

    [Fact]
    public async Task NoETagMember_AWriteBetweenReadAndDelete_DeletesUnconditionally_AsBefore()
    {
        var probeRef = Guid.NewGuid().AsRef<DeleteRaceNoETagProbe>();
        Assert.True(await new DeleteRaceNoETagProbe { probeRef = probeRef, note = "created" }
            .StorageCreateAsync(created => true, () => false));
        DeleteRaceHookAttribute.OnNextDelete(probeRef.id,
            () => probeRef.StorageUpdateAsync<DeleteRaceNoETagProbe, bool>(
                async (probe, save) =>
                {
                    probe.note = "later";
                    await save(probe);
                    return true;
                }));

        var deleted = await probeRef.StorageDeleteAsync(
            onDeleted: probe => probe.note,
            onNotFound: () => "missing");

        Assert.Equal("created", deleted);
        Assert.False(await ExistsAsync(probeRef));
        Assert.Equal(new[] { "created" }, DeleteRaceHookAttribute.NotesSeen(probeRef.id));
    }
    private static IRef<DeleteRaceProbe> Missing() => Guid.NewGuid().AsRef<DeleteRaceProbe>();

    /// <summary>An instance of a row that is not there, carrying the ETag a read would have given it.</summary>
    private static DeleteRaceProbe MissingInstance() => new()
    {
        probeRef = Missing(),
        eTag = "W/\"datetime'2020-01-01T00%3A00%3A00.0000000Z'\"",
    };

    [Fact]
    public async Task AMissingRow_WithAHandler_CallsIt_OnEveryOverload()
    {
        Assert.Equal("missing", await Missing().StorageDeleteAsync(probe => "deleted", () => "missing"));
        Assert.Equal("missing", await MissingInstance().StorageDeleteAsync(() => "deleted", onAlreadyDeleted: () => "missing"));
        Assert.Equal("missing", await Missing().StorageDeleteIfAsync<DeleteRaceProbe, string>(
            async (probe, deleteAsync) => { await deleteAsync(); return "deleted"; },
            () => "missing"));
        Assert.Equal("missing", await Missing().StorageDeleteAsync<DeleteRaceProbe, string>(
            query => query, probe => "deleted", () => "missing"));
    }

    [Fact]
    public async Task AMissingRow_WithNoHandler_ThrowsInvalidOperation_OnEveryOverload()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Missing().StorageDeleteAsync(probe => "deleted"));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => MissingInstance().StorageDeleteAsync(() => "deleted"));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Missing().StorageDeleteIfAsync<DeleteRaceProbe, string>(
                async (probe, deleteAsync) => { await deleteAsync(); return "deleted"; }));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Missing().StorageDeleteAsync<DeleteRaceProbe, string>(query => query, probe => "deleted"));
    }
}
