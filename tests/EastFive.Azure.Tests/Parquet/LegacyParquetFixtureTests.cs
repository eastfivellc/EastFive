using Xunit;

using Parquet;

using EastFive.Serialization.Parquet;

namespace EastFive.Azure.Tests.Parquet;

/// <summary>
/// Files written by the Parquet.Net 3.x engine that EastFive.Core vendored until the move to the
/// upstream package (synthetic copies of Rosemary's lake export, huddle cache, REDCap crosswalk and
/// an AffirmHealth risk-contract export). They must stay readable through the mapping layer.
/// Never regenerate them: a file written by the new engine proves nothing about old ones.
/// </summary>
public class LegacyParquetFixtureTests
{
    private static byte[] Read(string fileName) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Parquet", "Fixtures", fileName));

    [ParquetMapping]
    public sealed class HuddleRow
    {
        [ParquetProperty(Name = "source_row")] public string SourceRow = "";
        [ParquetProperty(Name = "provider_npi")] public string ProviderNpi = "";
        [ParquetProperty(Name = "access_time")] public DateTimeOffset AccessTime;
        [ParquetProperty(Name = "access_date")] public DateTimeOffset AccessDate;
        [ParquetProperty(Name = "schedule_date")] public DateTime ScheduleDate;
        [ParquetProperty(Name = "error")] public string Error = "";
    }

    [Fact]
    public async Task HuddleCache_Int96DateTimeOffsetColumns_ReadAsUtc()
    {
        var (rows, unparsed) = await ParquetMappingTests.ParseAsync<HuddleRow>(Read("huddle-cache.parquet"));

        Assert.Empty(unparsed);
        Assert.Equal(2, rows.Length);
        Assert.Equal("11111111111141118111111111111111", rows[0].SourceRow);
        Assert.Equal("", rows[0].ProviderNpi);
        Assert.Equal(new DateTimeOffset(2026, 7, 14, 9, 41, 7, 123, TimeSpan.Zero), rows[0].AccessTime);
        Assert.Equal(TimeSpan.Zero, rows[0].AccessTime.Offset);
        Assert.Equal(new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero), rows[0].AccessDate);
        Assert.Equal(new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc), rows[0].ScheduleDate);
        Assert.Equal(DateTimeKind.Utc, rows[0].ScheduleDate.Kind);
        Assert.Equal("1234567893", rows[1].ProviderNpi);
        Assert.Equal(new DateTimeOffset(2026, 7, 31, 23, 59, 59, TimeSpan.Zero), rows[1].AccessTime);
        Assert.Equal("practice_mismatch", rows[1].Error);
    }

    [ParquetMapping]
    public sealed class CrosswalkRow
    {
        [ParquetProperty(Name = "worksheet")] public string Worksheet = "";
        [ParquetProperty(Name = "source_row")] public int RowNumber;
        [ParquetProperty(Name = "reporting_market")] public string Market = "";
        [ParquetProperty(Name = "status")] public string Status = "";
    }

    [Fact]
    public async Task RedcapsCrosswalk_StringColumns_ParseIntoTypedMembers()
    {
        var (rows, unparsed) = await ParquetMappingTests.ParseAsync<CrosswalkRow>(Read("redcaps-crosswalk.parquet"));

        Assert.Empty(unparsed);
        Assert.Equal(
            new[] { ("Roster", 4, "Atlanta", "mapped"), ("Roster", 5, "", "unmapped"), ("Other", 2, "__ignored__", "ignored") },
            rows.Select(row => (row.Worksheet, row.RowNumber, row.Market, row.Status)));
    }

    [Fact]
    public async Task RedcapsCrosswalk_SchemaOnlyFile_ReadsAsNoRows()
    {
        // The 3.x engine wrote this file but threw reading it back.
        var (rows, unparsed) = await ParquetMappingTests.ParseAsync<CrosswalkRow>(Read("redcaps-crosswalk-empty.parquet"));

        Assert.Empty(rows);
        Assert.Empty(unparsed);
    }

    [ParquetMapping]
    public sealed class RiskContract
    {
        [ParquetProperty(Name = "patientIdEmr")] public string? PatientIdEmr;
        [ParquetProperty(Name = "practiceIdEmr")] public string? PracticeIdEmr;
        [ParquetProperty(Name = "RequestedRiskContract")] public string? RequestedRiskContract;
    }

    [Fact]
    public async Task RiskContracts_UnmappedColumnsOfOtherTypes_AreIgnored()
    {
        var (rows, unparsed) = await ParquetMappingTests.ParseAsync<RiskContract>(Read("datalake-to-emr-risk-contracts.parquet"));

        Assert.Empty(unparsed);
        Assert.Equal(
            new (string?, string?, string?)[] { ("E-1001", "195900", "12"), ("E-1002", "195900", null), ("E-2001", "1959", "61") },
            rows.Select(row => (row.PatientIdEmr, row.PracticeIdEmr, row.RequestedRiskContract)));
    }

    [ParquetMapping]
    public sealed class LakeRow
    {
        [ParquetProperty(Name = "row_number")] public int RowNumber;
        [ParquetProperty(Name = "record_id")] public string? RecordId;
        [ParquetProperty(Name = "payer")] public string? Payer;
        [ParquetProperty(Name = "email")] public string? Email;
        [ParquetProperty(Name = "practice_id")] public Guid PracticeId;
        [ParquetProperty(Name = "committed_at")] public DateTime CommittedAt;
        [ParquetProperty(Name = "create_patients")] public bool CreatePatients;
    }

    [Fact]
    public async Task ImportLakeExport_AllStringSnappyFile_ParsesIntoTypedMembers()
    {
        var (rows, unparsed) = await ParquetMappingTests.ParseAsync<LakeRow>(Read("import-lake-export.parquet"));

        Assert.Empty(unparsed);
        Assert.Equal(new[] { 2, 3 }, rows.Select(row => row.RowNumber));
        Assert.Equal(new[] { "MBI-TEST-1", "MBI-TEST-2" }, rows.Select(row => row.RecordId));
        Assert.Equal(new[] { "", null }, rows.Select(row => row.Payer));
        Assert.All(rows, row => Assert.Null(row.Email));
        Assert.All(rows, row => Assert.Equal(Guid.Parse("6f1c2a0e-3b7d-4e55-9a51-0c6f2b8d1e01"), row.PracticeId));
        Assert.All(rows, row => Assert.Equal(new DateTime(2026, 10, 26, 15, 30, 45, DateTimeKind.Utc), row.CommittedAt));
        Assert.All(rows, row => Assert.True(row.CreatePatients));
    }

    [Fact]
    public async Task LegacyFiles_OpenWithTheUpstreamReader()
    {
        foreach (var name in new[] { "huddle-cache.parquet", "import-lake-export.parquet", "redcaps-crosswalk.parquet", "datalake-to-emr-risk-contracts.parquet" })
        {
            await using var reader = await ParquetReader.CreateAsync(new MemoryStream(Read(name)));
            Assert.Equal(1, reader.RowGroupCount);
        }
    }
}
