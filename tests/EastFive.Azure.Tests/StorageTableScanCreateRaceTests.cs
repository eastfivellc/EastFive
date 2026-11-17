using System;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

using Microsoft.Azure.Cosmos.Table;

using EastFive.Linq.Async;
using EastFive.Persistence.Azure.StorageTables.Driver;

namespace EastFive.Azure.Tests;

/// <summary>
/// A table scan whose query is answered "table not found" holds the answer "no rows", even when
/// the table is created a moment later. The readers used to re-check existence after the failure
/// and rethrow the 404 when the table had appeared in between, so the first scan of a table that
/// overlapped the first write to it threw <see cref="StorageException"/> "Not Found".
///
/// The emulator persists between runs, so every test uses a table name of its own and drops it.
/// </summary>
public class StorageTableScanCreateRaceTests
{
    public StorageTableScanCreateRaceTests() => TestConfiguration.Ensure();

    private static AzureTableDriverDynamic Driver() => AzureTableDriverDynamic.FromSettings();

    private static CloudTable NewTable() =>
        Driver().TableClient.GetTableReference($"scanrace{Guid.NewGuid():N}");

    private static TableQuery<DynamicTableEntity> All() => new();

    private static async Task DropAsync(CloudTable table)
    {
        try { await table.DeleteIfExistsAsync(); }
        catch (StorageException) { } // best effort: a leftover table only costs emulator space
    }

    /// <summary>Creates the table when the reader asks whether it exists: the moment the race
    /// opens, after the query was answered "not found" and before the reader re-checks.</summary>
    private sealed class CreatedOnExistenceCheck(CloudTable table)
        : CloudTable(table.StorageUri, table.ServiceClient.Credentials)
    {
        public override async Task<bool> ExistsAsync()
        {
            await table.CreateIfNotExistsAsync();
            return await base.ExistsAsync();
        }
    }

    [Fact]
    public async Task FindAllInternal_TableCreatedAfterTheFirstQueryWasAnswered_YieldsNoRows()
    {
        var table = NewTable();
        try
        {
            // FindAllInternal sends its first request now, not when it is enumerated.
            var scan = AzureTableDriverDynamic.FindAllInternal(All(), table);

            // A probe of the same missing table, answered 404, orders the scan's own answer first.
            await Assert.ThrowsAsync<StorageException>(
                () => table.ExecuteQuerySegmentedAsync(All(), default));
            await table.CreateIfNotExistsAsync();

            Assert.Empty(await scan.ToArrayAsync());
        }
        finally { await DropAsync(table); }
    }

    [Fact]
    public async Task FindAllInternal_TableCreatedBeforeTheExistenceCheck_YieldsNoRows()
    {
        var table = NewTable();
        try
        {
            var rows = await AzureTableDriverDynamic
                .FindAllInternal(All(), new CreatedOnExistenceCheck(table))
                .ToArrayAsync();
            Assert.Empty(rows);
        }
        finally { await DropAsync(table); }
    }

    [Fact]
    public async Task FindAllSegmented_TableCreatedBeforeTheExistenceCheck_YieldsNoRows()
    {
        var table = NewTable();
        try
        {
            var rows = await AzureTableDriverDynamic
                .FindAllSegmented(All(), default, new CreatedOnExistenceCheck(table))
                .ToArrayAsync();
            Assert.Empty(rows);
        }
        finally { await DropAsync(table); }
    }

    [Fact]
    public async Task FindQuerySegmentAsync_TableCreatedBeforeTheExistenceCheck_ReturnsNoRows()
    {
        var table = NewTable();
        try
        {
            var (rows, token) = await AzureTableDriverDynamic
                .FindQuerySegmentAsync(All(), new CreatedOnExistenceCheck(table), default);
            Assert.Empty((DynamicTableEntity[])rows);
            Assert.Null(token);
        }
        finally { await DropAsync(table); }
    }

    [Fact]
    public async Task TableThatNeverExists_EveryReaderYieldsNoRows()
    {
        var table = NewTable();

        Assert.Empty(await AzureTableDriverDynamic.FindAllInternal(All(), table).ToArrayAsync());
        Assert.Empty(await AzureTableDriverDynamic.FindAllSegmented(All(), default, table).ToArrayAsync());
        var (rows, token) = await AzureTableDriverDynamic.FindQuerySegmentAsync(All(), table, default);
        Assert.Empty((DynamicTableEntity[])rows);
        Assert.Null(token);
        Assert.False(await table.ExistsAsync());
    }

    [Fact]
    public async Task TableWithRows_ReadersYieldThemAcrossSegments()
    {
        var table = NewTable();
        try
        {
            await table.CreateAsync();
            var keys = Enumerable.Range(0, 5).Select(i => $"row{i}").ToArray();
            foreach (var key in keys)
                await table.ExecuteAsync(TableOperation.Insert(new DynamicTableEntity("p", key)));

            // Two rows per segment: five rows take three requests and two continuation tokens.
            var paged = new TableQuery<DynamicTableEntity>().Take(2);

            var all = await AzureTableDriverDynamic.FindAllInternal(paged, table).ToArrayAsync();
            Assert.Equal(keys, all.Select(row => row.RowKey).OrderBy(key => key));

            var segmented = await AzureTableDriverDynamic.FindAllSegmented(paged, default, table).ToArrayAsync();
            Assert.Equal(keys, segmented.Select(row => row.Item1.RowKey).OrderBy(key => key));

            var (first, token) = await AzureTableDriverDynamic.FindQuerySegmentAsync(paged, table, default);
            Assert.Equal(2, ((DynamicTableEntity[])first).Length);
            Assert.NotNull(token);
        }
        finally { await DropAsync(table); }
    }
}
