using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Azure.Cosmos.Table;
using Xunit;

using EastFive.Persistence;
using EastFive.Persistence.Azure.StorageTables;

namespace EastFive.Azure.Tests;

/// <summary>
/// Reading a row sets each of the entity's storage members from the row's columns. Which members
/// those are, and which of a column's value type's members are stored, are facts about the types,
/// so they are worked out once per type and not again for every row.
/// </summary>
public class StorageRowMembersTests
{
    /// <summary>
    /// Reflection builds a member's attributes each time they are read, so the number of these
    /// built is the number of scans that reached the entity's members.
    /// </summary>
    private sealed class CountedStorageAttribute : StorageAttribute
    {
        private static int constructed;

        public static int Constructed => Volatile.Read(ref constructed);

        public CountedStorageAttribute() => Interlocked.Increment(ref constructed);
    }

    /// <summary>The same, for the members of a struct stored as one of the entity's columns.</summary>
    private sealed class CountedInnerStorageAttribute : StorageAttribute
    {
        private static int constructed;

        public static int Constructed => Volatile.Read(ref constructed);

        public CountedInnerStorageAttribute() => Interlocked.Increment(ref constructed);
    }

    public struct CountedInner
    {
        [CountedInnerStorage]
        public string label;
    }

    public struct CountedRow
    {
        [RowKey]
        [RowKeyPrefix(Characters = 2)]
        public Guid id;

        [ETag]
        public string eTag;

        [LastModified]
        public DateTimeOffset lastModified;

        [CountedStorage]
        public string name;

        [Storage]
        public CountedInner inner;
    }

    public struct Target : IReferenceable
    {
        public Guid id => default;
    }

    public struct Inner
    {
        [Storage]
        public string label;

        [Storage]
        public int count;
    }

    /// <summary>One member of each shape the wide probe in the allocation budget test uses.</summary>
    public struct EveryShape
    {
        [RowKey]
        [RowKeyPrefix(Characters = 2)]
        public Guid id;

        [ETag]
        public string eTag;

        [LastModified]
        public DateTimeOffset lastModified;

        [Storage] public string text;
        [Storage] public DateTime? when;
        [Storage] public Guid? other;
        [Storage] public bool? flag;
        [Storage] public IRefOptional<Target> target;
        [Storage] public string[] texts;
        [Storage] public int[] numbers;
        [Storage] public Dictionary<string, string> pairs;
        [Storage] public Inner inner;
        [Storage] public Inner[] inners;
    }

    public struct OtherRow
    {
        [RowKey]
        [RowKeyPrefix(Characters = 2)]
        public Guid id;

        [Storage] public int text;
        [Storage] public string label;
    }

    private static readonly DateTimeOffset stamp = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private static (TEntity read, IDictionary<string, EntityProperty> columns) RoundTrip<TEntity>(TEntity entity, Guid id)
    {
        var table = new StorageTableAttribute();
        var columns = table.GetEntity(entity).WriteEntity(null);
        var read = table.CreateEntityInstance<TEntity>(id.ToString("N"), "ab", columns, "etag", stamp);
        return (read, columns);
    }

    private static EveryShape Populated(int seed)
    {
        var id = Guid.NewGuid();
        return new EveryShape
        {
            id = id,
            text = $"text-{seed}",
            when = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddDays(seed),
            other = Guid.NewGuid(),
            flag = seed % 2 == 0,
            target = Guid.NewGuid().AsRefOptional<Target>(),
            texts = new[] { $"a-{seed}", $"b-{seed}" },
            numbers = new[] { seed, seed + 1 },
            pairs = new Dictionary<string, string> { { "key", $"value-{seed}" } },
            inner = new Inner { label = $"inner-{seed}", count = seed },
            inners = new[]
            {
                new Inner { label = $"first-{seed}", count = seed },
                new Inner { label = $"second-{seed}", count = seed + 1 },
            },
        };
    }

    private static void AssertSame(EveryShape expected, EveryShape actual)
    {
        Assert.Equal(expected.id, actual.id);
        Assert.Equal(expected.text, actual.text);
        Assert.Equal(expected.when, actual.when);
        Assert.Equal(expected.other, actual.other);
        Assert.Equal(expected.flag, actual.flag);
        Assert.Equal(expected.target.id, actual.target.id);
        Assert.Equal(expected.texts, actual.texts);
        Assert.Equal(expected.numbers, actual.numbers);
        Assert.Equal(expected.pairs, actual.pairs);
        Assert.Equal(expected.inner, actual.inner);
        Assert.Equal(expected.inners, actual.inners);
    }

    [Fact]
    public void ReadingMoreRows_DoesNotScanTheEntityOrAColumnsValueTypeForStorageMembersAgain()
    {
        var table = new StorageTableAttribute();
        var id = Guid.NewGuid();
        var row = new CountedRow { id = id, name = "name", inner = new CountedInner { label = "label" } };
        var columns = table.GetEntity(row).WriteEntity(null);
        table.CreateEntityInstance<CountedRow>(id.ToString("N"), "ab", columns, "etag", stamp);
        var entityScans = CountedStorageAttribute.Constructed;
        var valueTypeScans = CountedInnerStorageAttribute.Constructed;

        for (var more = 0; more < 100; more++)
            table.CreateEntityInstance<CountedRow>(id.ToString("N"), "ab", columns, "etag", stamp);

        Assert.Equal(entityScans, CountedStorageAttribute.Constructed);
        Assert.Equal(valueTypeScans, CountedInnerStorageAttribute.Constructed);
    }

    [Fact]
    public void WritingThenReading_ReturnsEveryShapeUnchanged()
    {
        var written = Populated(1);

        var (read, _) = RoundTrip(written, written.id);

        AssertSame(written, read);
    }

    [Fact]
    public void TwoEntityTypesReadAlternately_EachKeepTheirOwnColumns()
    {
        for (var row = 0; row < 3; row++)
        {
            var shape = Populated(row);
            var other = new OtherRow { id = Guid.NewGuid(), text = row, label = $"label-{row}" };

            var (shapeRead, _) = RoundTrip(shape, shape.id);
            var (otherRead, _) = RoundTrip(other, other.id);

            AssertSame(shape, shapeRead);
            Assert.Equal(other, otherRead);
        }
    }

    [Fact]
    public void RowsReadInParallel_EachGetTheirOwnValues()
    {
        var table = new StorageTableAttribute();
        var rows = Enumerable.Range(0, 64)
            .Select(seed => Populated(seed))
            .Select(row => (row, columns: table.GetEntity(row).WriteEntity(null)))
            .ToArray();

        var read = rows
            .AsParallel()
            .WithDegreeOfParallelism(8)
            .Select(written => (written.row,
                read: table.CreateEntityInstance<EveryShape>(written.row.id.ToString("N"), "ab", written.columns, "etag", stamp)))
            .ToArray();

        Assert.Equal(rows.Length, read.Length);
        foreach (var (written, readBack) in read)
            AssertSame(written, readBack);
    }
}
