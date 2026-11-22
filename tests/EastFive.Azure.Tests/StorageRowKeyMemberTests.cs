using System;
using System.Threading;

using Xunit;

using EastFive.Persistence.Azure.StorageTables;

namespace EastFive.Azure.Tests;

/// <summary>
/// Reading a row hands its row key, partition key, timestamp and ETag to the entity. Which member
/// takes each one is a fact about the entity type, so it is worked out once per type and not again
/// for every row.
/// </summary>
public class StorageRowKeyMemberTests
{
    /// <summary>
    /// Reflection builds a member's attributes each time they are read, so the number of these
    /// built is the number of scans that reached the row-key member.
    /// </summary>
    private sealed class CountedRowKeyAttribute : RowKeyAttribute
    {
        private static int constructed;

        public static int Constructed => Volatile.Read(ref constructed);

        public CountedRowKeyAttribute() => Interlocked.Increment(ref constructed);
    }

    public struct CountedRow
    {
        [CountedRowKey]
        [RowKeyPrefix(Characters = 2)]
        public Guid id;

        [ETag]
        public string eTag;

        [LastModified]
        public DateTimeOffset lastModified;
    }

    /// <summary>No ETag member and no last-modified member: both lookups find nothing.</summary>
    public struct NamedRow
    {
        [RowKey]
        [RowKeyPrefix(Characters = 2)]
        public string name;
    }

    /// <summary>
    /// The wrapper a row is read into is protected. This sets a row's keys on it in the order
    /// <see cref="StorageTableAttribute.CreateEntityInstance{TEntity}"/> does, without the columns.
    /// </summary>
    private sealed class Rows : StorageTableAttribute
    {
        public TEntity Keyed<TEntity>(string rowKey, string partitionKey, DateTimeOffset timestamp, string eTag)
        {
            var wrapper = CreateWrapper<TEntity>();
            wrapper.PartitionKey = partitionKey;
            wrapper.RowKey = rowKey;
            wrapper.Timestamp = timestamp;
            wrapper.ETag = eTag;
            return wrapper.Entity;
        }
    }

    private static readonly DateTimeOffset firstStamp = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
    private static readonly DateTimeOffset secondStamp = new(2026, 6, 7, 8, 9, 10, TimeSpan.Zero);

    [Fact]
    public void ReadingMoreRows_DoesNotScanTheEntityForItsKeyMembersAgain()
    {
        var rows = new Rows();
        rows.Keyed<CountedRow>(Guid.NewGuid().ToString("N"), "ab", firstStamp, "etag");
        var afterTheFirstRow = CountedRowKeyAttribute.Constructed;

        for (var row = 0; row < 100; row++)
            rows.Keyed<CountedRow>(Guid.NewGuid().ToString("N"), "ab", firstStamp, "etag");

        Assert.Equal(afterTheFirstRow, CountedRowKeyAttribute.Constructed);
    }

    [Fact]
    public void EachRowTakesItsOwnKeys()
    {
        var rows = new Rows();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();

        var first = rows.Keyed<CountedRow>(firstId.ToString("N"), "ab", firstStamp, "etag-1");
        var second = rows.Keyed<CountedRow>(secondId.ToString("N"), "cd", secondStamp, "etag-2");

        Assert.Equal((firstId, "etag-1", firstStamp), (first.id, first.eTag, first.lastModified));
        Assert.Equal((secondId, "etag-2", secondStamp), (second.id, second.eTag, second.lastModified));
    }

    [Fact]
    public void TwoEntityTypes_EachKeepTheirOwnKeyMembers_IncludingTheOnesTheyLack()
    {
        var rows = new Rows();
        var id = Guid.NewGuid();

        var counted = rows.Keyed<CountedRow>(id.ToString("N"), "ab", firstStamp, "etag");
        var named = rows.Keyed<NamedRow>("first-name", "fi", firstStamp, "etag");
        var namedAgain = rows.Keyed<NamedRow>("second-name", "se", secondStamp, "etag");

        Assert.Equal(id, counted.id);
        Assert.Equal("first-name", named.name);
        Assert.Equal("second-name", namedAgain.name);
    }
}
