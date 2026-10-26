using System.Reflection;

using Xunit;

using Parquet;
using Parquet.Schema;

using EastFive.Serialization.Parquet;

namespace EastFive.Azure.Tests.Parquet;

/// <summary>
/// The attribute-interface parquet layer on Parquet.Net's columnar API: attributes declare the
/// columns and own the member ⇄ column conversion; nothing goes through the class serializer.
/// </summary>
public class ParquetMappingTests
{
    public sealed class Thing : IReferenceable
    {
        public Guid id { get; set; }
    }

    [ParquetMapping]
    public sealed class EveryType
    {
        [ParquetProperty(Name = "text")] public string? Text = "unset";
        [ParquetProperty(Name = "flag")] public bool Flag;
        [ParquetProperty(Name = "count")] public int Count;
        [ParquetProperty(Name = "big")] public long Big;
        [ParquetProperty(Name = "ratio")] public double Ratio;
        [ParquetProperty(Name = "money")] public decimal Money;
        [ParquetProperty(Name = "when")] public DateTime When;
        [ParquetProperty(Name = "at")] public DateTimeOffset At;
        [ParquetProperty(Name = "id")] public Guid Id;
        [ParquetProperty(Name = "day")] public DayOfWeek Day;
        [ParquetProperty(Name = "thing")] public IRef<Thing>? ThingRef;
        [ParquetProperty(Name = "maybe_thing")] public IRefOptional<Thing>? MaybeThing;
        [ParquetProperty(Name = "maybe_flag")] public bool? MaybeFlag;
        [ParquetProperty(Name = "maybe_count")] public int? MaybeCount;
        [ParquetProperty(Name = "maybe_big")] public long? MaybeBig;
        [ParquetProperty(Name = "maybe_ratio")] public double? MaybeRatio;
        [ParquetProperty(Name = "maybe_money")] public decimal? MaybeMoney;
        [ParquetProperty(Name = "maybe_when")] public DateTime? MaybeWhen;
        [ParquetProperty(Name = "maybe_at")] public DateTimeOffset? MaybeAt;
        [ParquetProperty(Name = "maybe_id")] public Guid? MaybeId;
        [ParquetProperty(Name = "maybe_day")] public DayOfWeek? MaybeDay;
        [ParquetProperty] public string? Unnamed { get; set; }
    }

    private static readonly Guid ThingId = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");

    private static EveryType Full() => new()
    {
        Text = "hello", Flag = true, Count = -42, Big = long.MaxValue, Ratio = 0.125, Money = 12345.6789m,
        When = new DateTime(2026, 7, 14, 9, 41, 7, 123, DateTimeKind.Utc),
        At = new DateTimeOffset(2026, 7, 14, 4, 41, 7, 123, TimeSpan.FromHours(-5)),
        Id = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7"), Day = DayOfWeek.Thursday,
        ThingRef = ThingId.AsRef<Thing>(), MaybeThing = ThingId.AsRefOptional<Thing>(),
        MaybeFlag = false, MaybeCount = 7, MaybeBig = -1, MaybeRatio = double.MaxValue, MaybeMoney = -0.01m,
        MaybeWhen = new DateTime(1999, 12, 31, 23, 59, 59, DateTimeKind.Utc),
        MaybeAt = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.FromHours(9)),
        MaybeId = Guid.Parse("a8098c1a-f86e-11da-bd1a-00112444be1e"), MaybeDay = DayOfWeek.Sunday,
        Unnamed = "by member name",
    };

    private static EveryType Sparse() => new()
    {
        Text = null, When = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        At = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        ThingRef = ThingId.AsRef<Thing>(), MaybeThing = default(Guid?).AsRefOptional<Thing>(),
    };

    internal static async Task<byte[]> WriteAsync<T>(IEnumerable<T> rows, string? scope = null)
    {
        using var stream = new MemoryStream();
        await rows.WriteToParquetStreamAsync(stream, scope);
        return stream.ToArray();
    }

    internal static Task<(T[] rows, UnparsedRow[] unparsed)> ParseAsync<T>(byte[] bytes, string? scope = null) =>
        new MemoryStream(bytes).ParseParquetAsync<T, (T[], UnparsedRow[])>(
            (rows, unparsed) => (rows, unparsed),
            reason => throw new Xunit.Sdk.XunitException($"unreadable: {reason}"),
            scope);

    internal static async Task<DataField[]> ReadFieldsAsync(byte[] bytes)
    {
        await using var reader = await ParquetReader.CreateAsync(new MemoryStream(bytes));
        return reader.Schema.DataFields;
    }

    [Fact]
    public async Task EverySupportedType_RoundTrips()
    {
        var (rows, unparsed) = await ParseAsync<EveryType>(await WriteAsync(new[] { Full(), Sparse() }));

        Assert.Empty(unparsed);
        Assert.Equal(2, rows.Length);

        var expected = Full();
        var full = rows[0];
        Assert.Equal(expected.Text, full.Text);
        Assert.Equal(expected.Flag, full.Flag);
        Assert.Equal(expected.Count, full.Count);
        Assert.Equal(expected.Big, full.Big);
        Assert.Equal(expected.Ratio, full.Ratio);
        Assert.Equal(expected.Money, full.Money);
        Assert.Equal(expected.When, full.When);
        Assert.Equal(DateTimeKind.Utc, full.When.Kind);
        Assert.Equal(expected.At, full.At);
        Assert.Equal(expected.Id, full.Id);
        Assert.Equal(expected.Day, full.Day);
        Assert.Equal(ThingId, full.ThingRef!.id);
        Assert.True(full.MaybeThing!.HasValue);
        Assert.Equal(ThingId, full.MaybeThing.id);
        Assert.Equal(expected.MaybeFlag, full.MaybeFlag);
        Assert.Equal(expected.MaybeCount, full.MaybeCount);
        Assert.Equal(expected.MaybeBig, full.MaybeBig);
        Assert.Equal(expected.MaybeRatio, full.MaybeRatio);
        Assert.Equal(expected.MaybeMoney, full.MaybeMoney);
        Assert.Equal(expected.MaybeWhen, full.MaybeWhen);
        Assert.Equal(expected.MaybeAt, full.MaybeAt);
        Assert.Equal(expected.MaybeId, full.MaybeId);
        Assert.Equal(expected.MaybeDay, full.MaybeDay);
        Assert.Equal(expected.Unnamed, full.Unnamed);

        var sparse = rows[1];
        Assert.Null(sparse.Text);
        Assert.False(sparse.MaybeThing!.HasValue);
        Assert.Null(sparse.MaybeFlag);
        Assert.Null(sparse.MaybeCount);
        Assert.Null(sparse.MaybeBig);
        Assert.Null(sparse.MaybeRatio);
        Assert.Null(sparse.MaybeMoney);
        Assert.Null(sparse.MaybeWhen);
        Assert.Null(sparse.MaybeAt);
        Assert.Null(sparse.MaybeId);
        Assert.Null(sparse.MaybeDay);
        Assert.Null(sparse.Unnamed);
    }

    [Fact]
    public async Task DateTimeOffset_IsStoredAsUtcInt96_LikeTheLegacyEngine()
    {
        var row = Full();
        var bytes = await WriteAsync(new[] { row });

        var at = Assert.Single(await ReadFieldsAsync(bytes), field => field.Name == "at");
        var dateTimeField = Assert.IsType<DateTimeDataField>(at);
        Assert.Equal(DateTimeFormat.Impala, dateTimeField.DateTimeFormat);

        var read = Assert.Single((await ParseAsync<EveryType>(bytes)).rows);
        Assert.Equal(row.At.UtcDateTime, read.At.UtcDateTime);
        Assert.Equal(TimeSpan.Zero, read.At.Offset);
    }

    [Fact]
    public async Task Schema_UsesAttributeNames_AndLegacyStringEncodings()
    {
        var fields = await ReadFieldsAsync(await WriteAsync(new[] { Full() }));

        Assert.Equal(
            new[]
            {
                "text", "flag", "count", "big", "ratio", "money", "when", "at", "id", "day", "thing", "maybe_thing",
                "maybe_flag", "maybe_count", "maybe_big", "maybe_ratio", "maybe_money", "maybe_when", "maybe_at",
                "maybe_id", "maybe_day", "Unnamed",
            },
            fields.Select(field => field.Name));
        // Guids, references and enums travel as strings, as the 3.x mapping wrote them.
        Assert.All(new[] { "id", "day", "thing", "maybe_thing", "maybe_id", "maybe_day" },
            name => Assert.Equal(typeof(ReadOnlyMemory<char>), fields.Single(field => field.Name == name).ClrType));
        Assert.False(fields.Single(field => field.Name == "count").IsNullable);
        Assert.True(fields.Single(field => field.Name == "maybe_count").IsNullable);
        Assert.Equal(fields.Select(field => field.Name), ParquetSerializationExtensions.GetParquetSchema<EveryType>().DataFields.Select(field => field.Name));
    }

    [Fact]
    public async Task ZeroRows_WritesASchemaOnlyFile_ThatReadsBackEmpty()
    {
        var bytes = await WriteAsync(Array.Empty<EveryType>());

        Assert.Equal("PAR1", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal(22, (await ReadFieldsAsync(bytes)).Length);
        var (rows, unparsed) = await ParseAsync<EveryType>(bytes);
        Assert.Empty(rows);
        Assert.Empty(unparsed);
    }

    [ParquetMapping]
    public sealed class Counted
    {
        [ParquetProperty(Name = "name")] public string Name = "";
        [ParquetProperty(Name = "count")] public int Count;
        [ParquetProperty(Name = "missing")] public string Missing = "initial";
    }

    private static async Task<byte[]> WriteRawAsync(ParquetSchema schema, params Func<ParquetRowGroupWriter, Task>[] rowGroups)
    {
        using var stream = new MemoryStream();
        await using (var writer = await ParquetWriter.CreateAsync(schema, stream))
            foreach (var rowGroup in rowGroups)
            {
                using var group = writer.CreateRowGroup();
                await rowGroup(group);
            }
        return stream.ToArray();
    }

    [Fact]
    public async Task UnconvertibleValue_IsReportedWithItsRow_NotSilentlyDropped()
    {
        var schema = new ParquetSchema(new DataField<string>("name"), new DataField<string>("count"), new DataField<int>("extra"));
        var bytes = await WriteRawAsync(schema,
            group => WriteRowsAsync(group, new[] { "a", "b" }, new[] { "1", "not a number" }, new[] { 1, 2 }),
            group => WriteRowsAsync(group, new[] { "c", "d" }, new[] { "3", "99999999999" }, new[] { 3, 4 }));

        var (rows, unparsed) = await ParseAsync<Counted>(bytes);

        Assert.Equal(new[] { ("a", 1), ("c", 3) }, rows.Select(row => (row.Name, row.Count)));
        Assert.All(rows, row => Assert.Equal("initial", row.Missing));
        Assert.Equal(new[] { 1, 3 }, unparsed.Select(row => row.Row));
        Assert.All(unparsed, row => Assert.Equal("count", row.Column));
        Assert.Equal(new object?[] { "not a number", "99999999999" }, unparsed.Select(row => row.RawValue));
        Assert.All(unparsed, row => Assert.False(string.IsNullOrWhiteSpace(row.Reason)));

        async Task WriteRowsAsync(ParquetRowGroupWriter group, string[] names, string[] counts, int[] extras)
        {
            await group.WriteAsync(schema.DataFields[0], names);
            await group.WriteAsync(schema.DataFields[1], counts);
            await group.WriteAsync<int>(schema.DataFields[2], extras.AsMemory());
        }
    }

    [Fact]
    public async Task FileColumnTypes_ConvertIntoMemberTypes()
    {
        // A nullable long column into an int member (null → default), and an int column into a string.
        var schema = new ParquetSchema(new DataField<int>("name"), new DataField<long?>("count"));
        var bytes = await WriteRawAsync(schema, async group =>
        {
            await group.WriteAsync<int>(schema.DataFields[0], new[] { 10, 20 }.AsMemory());
            await group.WriteAsync<long>(schema.DataFields[1], new long?[] { 5, null }.AsMemory());
        });

        var (rows, unparsed) = await ParseAsync<Counted>(bytes);

        Assert.Empty(unparsed);
        Assert.Equal(new[] { ("10", 5), ("20", 0) }, rows.Select(row => (row.Name, row.Count)));
    }

    [Fact]
    public async Task IncompatibleColumn_MakesTheFileUnreadable_ForThisMapping()
    {
        var schema = new ParquetSchema(new DataField<byte[]>("count"));
        var bytes = await WriteRawAsync(schema, group => group.WriteAsync(schema.DataFields[0], new[] { new byte[] { 1 } }));

        var reason = await new MemoryStream(bytes).ParseParquetAsync<Counted, string?>(
            (rows, unparsed) => null,
            why => why);

        Assert.Contains("count", reason);
    }

    [Fact]
    public async Task NotParquet_IsUnreadable()
    {
        var reason = await new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 })
            .ParseParquetAsync<Counted, string?>((rows, unparsed) => null, why => why);

        Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    [ParquetMapping]
    public sealed class Unsupported
    {
        [ParquetProperty(Name = "uri")] public Uri? Address;
    }

    [Fact]
    public void UnsupportedMemberType_FailsWhenTheMappingIsBuilt() =>
        Assert.Throws<NotSupportedException>(() => ParquetSerializationExtensions.GetParquetSchema<Unsupported>());

    /// <summary>A custom attribute supplies its own binding: a string array stored as one joined column.</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class PipeJoinedAttribute : Attribute, IMapParquetProperty
    {
        public string Name { get; set; } = "";

        public bool DoesMap(string scope) => true;

        public IBindParquetColumn<TResource> GetColumnBinding<TResource>(MemberInfo member) =>
            ParquetColumnBinding.Create<TResource, string[], string>(member, Name,
                values => string.Join('|', values),
                column => column?.Split('|') ?? Array.Empty<string>());
    }

    [ParquetMapping]
    public struct Tagged
    {
        [ParquetProperty(Name = "key")] public string Key;
        [PipeJoined(Name = "tags")] public string[] Tags { get; set; }
    }

    [Fact]
    public async Task CustomAttribute_SuppliesItsOwnBinding_OnAStruct()
    {
        var bytes = await WriteAsync(new[]
        {
            new Tagged { Key = "one", Tags = new[] { "a", "b" } },
            new Tagged { Key = "two", Tags = Array.Empty<string>() },
        });

        Assert.Equal(typeof(ReadOnlyMemory<char>), Assert.Single(await ReadFieldsAsync(bytes), field => field.Name == "tags").ClrType);
        var (rows, unparsed) = await ParseAsync<Tagged>(bytes);
        Assert.Empty(unparsed);
        Assert.Equal("one", rows[0].Key);
        Assert.Equal(new[] { "a", "b" }, rows[0].Tags);
        Assert.Equal(new[] { "" }, rows[1].Tags);
    }

    [ParquetMapping]
    [ParquetMapping(Scope = "export")]
    public sealed class Scoped
    {
        [ParquetProperty(Name = "default_name")]
        [ParquetProperty(Name = "export_name", Scope = "export")]
        public string Value = "";
    }

    [Fact]
    public async Task Scopes_SelectTheirOwnColumnNames()
    {
        var exported = await WriteAsync(new[] { new Scoped { Value = "v" } }, scope: "export");

        Assert.Equal("export_name", Assert.Single(await ReadFieldsAsync(exported)).Name);
        Assert.Equal("v", Assert.Single((await ParseAsync<Scoped>(exported, scope: "export")).rows).Value);
        Assert.Equal("default_name", Assert.Single(ParquetSerializationExtensions.GetParquetSchema<Scoped>().DataFields).Name);
    }

    [Fact]
    public async Task RowTupleWriter_TransposesRowsIntoColumns()
    {
        var schema = new ParquetSchema(
            new DataField<string>("name"), new DataField<int?>("count"), new DataField<DateTime>("when"));
        var when = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        var tuples = new[]
        {
            new (string, Type, object)[] { ("name", typeof(string), "a"), ("count", typeof(int?), 1), ("when", typeof(DateTime), when) },
            new (string, Type, object)[] { ("name", typeof(string), null!), ("count", typeof(int?), DBNull.Value), ("when", typeof(DateTime), new DateTimeOffset(when)) },
        };

        using var stream = new MemoryStream();
        await tuples.WriteToParquetStreamAsync(schema, stream);

        var (rows, unparsed) = await ParseAsync<WhenCounted>(stream.ToArray());
        Assert.Empty(unparsed);
        Assert.Equal(new (string?, int?, DateTime)[] { ("a", 1, when), (null, null, when) },
            rows.Select(row => (row.Name, row.Count, row.When)));
    }

    [ParquetMapping]
    public sealed class WhenCounted
    {
        [ParquetProperty(Name = "name")] public string? Name;
        [ParquetProperty(Name = "count")] public int? Count;
        [ParquetProperty(Name = "when")] public DateTime When;
    }
}
