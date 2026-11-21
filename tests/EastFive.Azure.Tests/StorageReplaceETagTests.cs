using System;
using System.Threading.Tasks;

using Xunit;

using EastFive;
using EastFive.Persistence.Azure.StorageTables;
using EastFive.Azure.Persistence.AzureStorageTables;
using EastFive.Azure.Persistence.StorageTables;

namespace EastFive.Azure.Tests;

/// <summary>
/// What <c>StorageReplaceAsync</c> does with the ETag an instance carries, for a table that has a
/// public ETag member. An instance that was not read from storage carries none, and the driver
/// replaces with <c>*</c>: the save is unconditional. An instance that was read carries the ETag
/// it was read with, and the replace is refused once the row has changed.
/// </summary>
public class StorageReplaceETagTests
{
    public StorageReplaceETagTests() => TestConfiguration.Ensure();

    private static async Task<IRef<UpdateETagPublicProbe>> CreateAsync(string note)
    {
        var probeRef = Guid.NewGuid().AsRef<UpdateETagPublicProbe>();
        Assert.True(await new UpdateETagPublicProbe { probeRef = probeRef, note = note }
            .StorageCreateAsync(created => true, () => false));
        return probeRef;
    }

    private static Task<UpdateETagPublicProbe> ReadAsync(IRef<UpdateETagPublicProbe> probeRef)
        => probeRef.StorageGetAsync(probe => probe, () => throw new InvalidOperationException("The row is missing."));

    private static Task<string> ReplaceAsync(UpdateETagPublicProbe probe)
        => probe.StorageReplaceAsync(() => "saved", (code, why) => $"refused:{code}");

    [Fact]
    public async Task AnInstanceNotReadFromStorage_ReplacesTheRowUnconditionally()
    {
        var probeRef = await CreateAsync("created");

        var result = await ReplaceAsync(new UpdateETagPublicProbe { probeRef = probeRef, note = "unread" });

        Assert.Equal("saved", result);
        Assert.Equal("unread", (await ReadAsync(probeRef)).note);
    }

    [Fact]
    public async Task AnInstanceReadBeforeTheRowChanged_IsRefused_AndTheRowKeepsTheLaterWrite()
    {
        var probeRef = await CreateAsync("created");
        var stale = await ReadAsync(probeRef);
        Assert.Equal("saved", await ReplaceAsync(new UpdateETagPublicProbe { probeRef = probeRef, note = "later" }));

        stale.note = "stale";
        var result = await ReplaceAsync(stale);

        Assert.Equal($"refused:{ExtendedErrorInformationCodes.UpdateConditionNotSatisfied}", result);
        Assert.Equal("later", (await ReadAsync(probeRef)).note);
    }

    [Fact]
    public async Task AnInstanceReadAndNotChangedSince_Replaces()
    {
        var probeRef = await CreateAsync("created");
        var current = await ReadAsync(probeRef);

        current.note = "current";
        var result = await ReplaceAsync(current);

        Assert.Equal("saved", result);
        Assert.Equal("current", (await ReadAsync(probeRef)).note);
    }
}
