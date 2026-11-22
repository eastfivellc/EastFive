using System;
using System.Collections.Generic;

using Xunit;
using Xunit.Abstractions;

using EastFive.Persistence;
using EastFive.Persistence.Azure.StorageTables;

namespace EastFive.Azure.Tests;

/// <summary>
/// What reading one row allocates, on an entity with many columns of the common shapes. The
/// counting tests prove each per-type scan happens once; this catches the next per-row scan
/// someone adds, whatever it is.
/// </summary>
public class StorageRowAllocationBudgetTests
{
    /// <summary>
    /// Bytes per row reading <see cref="Wide"/>. Measured at 138,331 in Debug and 136,779 in
    /// Release; the budget is about twice the worse. Before the per-type caches it was about
    /// 1,100,000.
    /// </summary>
    private const long BudgetBytesPerRow = 280_000;

    private readonly ITestOutputHelper output;

    public StorageRowAllocationBudgetTests(ITestOutputHelper output) => this.output = output;

    public struct Target : IReferenceable
    {
        public Guid id => default;
    }

    /// <summary>68 storage members, 70 columns.</summary>
    public struct Wide
    {
        [RowKey]
        [RowKeyPrefix(Characters = 2)]
        public Guid id;

        [ETag]
        public string eTag;

        [LastModified]
        public DateTimeOffset lastModified;

        [Storage] public string f0;
        [Storage] public string f1;
        [Storage] public string f2;
        [Storage] public string f3;
        [Storage] public string f4;
        [Storage] public string f5;
        [Storage] public string f6;
        [Storage] public string f7;
        [Storage] public string f8;
        [Storage] public string f9;
        [Storage] public string f10;
        [Storage] public string f11;
        [Storage] public string f12;
        [Storage] public string f13;
        [Storage] public string f14;
        [Storage] public string f15;
        [Storage] public string f16;
        [Storage] public string f17;
        [Storage] public string f18;
        [Storage] public string f19;
        [Storage] public string f20;
        [Storage] public string f21;
        [Storage] public string f22;
        [Storage] public string f23;
        [Storage] public string f24;
        [Storage] public string f25;
        [Storage] public string f26;
        [Storage] public string f27;
        [Storage] public string f28;
        [Storage] public string f29;
        [Storage] public string f30;
        [Storage] public string f31;
        [Storage] public string f32;
        [Storage] public string f33;
        [Storage] public DateTime? f34;
        [Storage] public DateTime? f35;
        [Storage] public DateTime? f36;
        [Storage] public DateTime? f37;
        [Storage] public DateTime? f38;
        [Storage] public DateTime? f39;
        [Storage] public DateTime? f40;
        [Storage] public DateTime? f41;
        [Storage] public DateTime? f42;
        [Storage] public DateTime? f43;
        [Storage] public DateTime? f44;
        [Storage] public DateTime? f45;
        [Storage] public DateTime? f46;
        [Storage] public Guid? f47;
        [Storage] public Guid? f48;
        [Storage] public Guid? f49;
        [Storage] public Guid? f50;
        [Storage] public Guid? f51;
        [Storage] public bool? f52;
        [Storage] public bool? f53;
        [Storage] public IRefOptional<Target> f54;
        [Storage] public IRefOptional<Target> f55;
        [Storage] public IRefOptional<Target> f56;
        [Storage] public IRefOptional<Target> f57;
        [Storage] public IRefOptional<Target> f58;
        [Storage] public IRefOptional<Target> f59;
        [Storage] public IRefOptional<Target> f60;
        [Storage] public IRefOptional<Target> f61;
        [Storage] public IRefOptional<Target> f62;
        [Storage] public string[] f63;
        [Storage] public string[] f64;
        [Storage] public int[] f65;
        [Storage] public Dictionary<string, string> f66;
        [Storage] public Dictionary<string, string> f67;
    }

    private static Wide Populated()
    {
        var when = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        return new Wide
        {
            id = Guid.NewGuid(),
            f0 = "value",
            f1 = "value",
            f2 = "value",
            f3 = "value",
            f4 = "value",
            f5 = "value",
            f6 = "value",
            f7 = "value",
            f8 = "value",
            f9 = "value",
            f10 = "value",
            f11 = "value",
            f12 = "value",
            f13 = "value",
            f14 = "value",
            f15 = "value",
            f16 = "value",
            f17 = "value",
            f18 = "value",
            f19 = "value",
            f20 = "value",
            f21 = "value",
            f22 = "value",
            f23 = "value",
            f24 = "value",
            f25 = "value",
            f26 = "value",
            f27 = "value",
            f28 = "value",
            f29 = "value",
            f30 = "value",
            f31 = "value",
            f32 = "value",
            f33 = "value",
            f34 = when,
            f35 = when,
            f36 = when,
            f37 = when,
            f38 = when,
            f39 = when,
            f40 = when,
            f41 = when,
            f42 = when,
            f43 = when,
            f44 = when,
            f45 = when,
            f46 = when,
            f47 = Guid.NewGuid(),
            f48 = Guid.NewGuid(),
            f49 = Guid.NewGuid(),
            f50 = Guid.NewGuid(),
            f51 = Guid.NewGuid(),
            f52 = true,
            f53 = true,
            f54 = Guid.NewGuid().AsRefOptional<Target>(),
            f55 = Guid.NewGuid().AsRefOptional<Target>(),
            f56 = Guid.NewGuid().AsRefOptional<Target>(),
            f57 = Guid.NewGuid().AsRefOptional<Target>(),
            f58 = Guid.NewGuid().AsRefOptional<Target>(),
            f59 = Guid.NewGuid().AsRefOptional<Target>(),
            f60 = Guid.NewGuid().AsRefOptional<Target>(),
            f61 = Guid.NewGuid().AsRefOptional<Target>(),
            f62 = Guid.NewGuid().AsRefOptional<Target>(),
            f63 = new[] { "a", "b" },
            f64 = new[] { "a", "b" },
            f65 = new[] { 1, 2 },
            f66 = new Dictionary<string, string> { { "key", "value" } },
            f67 = new Dictionary<string, string> { { "key", "value" } },
        };
    }

    /// <summary>The allocation counter is per thread, so this and its caller stay synchronous.</summary>
    private static long PerRow(Action row, int rows = 300)
    {
        row(); row(); row();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < rows; i++)
            row();
        return (GC.GetAllocatedBytesForCurrentThread() - before) / rows;
    }

    [Fact]
    public void ReadingARow_StaysWithinItsAllocationBudget()
    {
        var table = new StorageTableAttribute();
        var populated = Populated();
        var columns = table.GetEntity(populated).WriteEntity(null);
        var rowKey = populated.id.ToString("N");
        var stamp = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

        var bytesPerRow = PerRow(() => table.CreateEntityInstance<Wide>(rowKey, "ab", columns, "etag", stamp));

        output.WriteLine($"{columns.Count} columns, {bytesPerRow:N0} bytes per row");
        Assert.True(bytesPerRow < BudgetBytesPerRow,
            $"Reading a row allocated {bytesPerRow:N0} bytes; the budget is {BudgetBytesPerRow:N0}.");
    }
}
