using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

using global::Parquet;
using global::Parquet.Schema;

using EastFive.Reflection;

namespace EastFive.Serialization.Parquet
{
    /// <summary>
    /// The columnar core: reads and writes whole columns through the bindings an
    /// <see cref="IMapParquet"/> attribute supplies, on Parquet.Net's low-level row-group API.
    /// A file is held in memory one column at a time, as the 3.x Table API held it.
    /// </summary>
    public static class ParquetSerializationExtensions
    {
        /// <summary>
        /// Parses every row of <paramref name="parquetData"/> (which must be seekable) into
        /// <typeparamref name="TResource"/>. Rows with a value that could not be converted are left
        /// out of the parsed rows and reported, cell by cell, in the same callback, so a caller
        /// cannot see one without the other. A column the mapping names but the file lacks leaves
        /// the member as constructed; columns the mapping does not name are ignored.
        /// </summary>
        /// <param name="onUnreadable">The file is not parquet, is corrupt, or holds a mapped column
        /// whose values cannot be converted to the member at all.</param>
        public static async Task<TResult> ParseParquetAsync<TResource, TResult>(this Stream parquetData,
            Func<TResource[], UnparsedRow[], TResult> onParsed,
            Func<string, TResult> onUnreadable,
            string scope = default,
            CancellationToken cancellationToken = default)
        {
            var bindings = GetMapping<TResource>(scope).GetColumnBindings<TResource>();
            if (ResourceFactory<TResource>.Create is null)
                throw new NotSupportedException(
                    $"{typeof(TResource).FullName} needs a public parameterless constructor to be parsed from parquet.");
            var (resources, unparsed, unreadable) = await ReadAsync(parquetData, bindings, cancellationToken);
            if (unreadable is not null)
                return onUnreadable(unreadable);
            var failedRows = unparsed.Select(cell => cell.Row).ToHashSet();
            var parsed = resources.Where((resource, index) => !failedRows.Contains(index)).ToArray();
            return onParsed(parsed, unparsed.OrderBy(cell => cell.Row).ToArray());
        }

        private static async Task<(TResource[], UnparsedRow[], string)> ReadAsync<TResource>(Stream parquetData,
            IBindParquetColumn<TResource>[] bindings, CancellationToken cancellationToken)
        {
            // Opening and reading the file is where a corrupt or foreign file throws; that is an
            // outcome (unreadable), not an exception for the caller.
            try
            {
                await using var reader = await ParquetReader.CreateAsync(parquetData, leaveStreamOpen: true,
                    cancellationToken: cancellationToken);
                var fileFields = reader.Schema.DataFields;
                var columnReaders = bindings
                    .Select(binding => fileFields.FirstOrDefault(binding.IsColumn) is DataField fileField
                        ? binding.BindReader(fileField,
                            columnReader => (columnReader, reason: default(string)),
                            reason => (columnReader: default(IReadParquetColumn<TResource>), reason))
                        : (columnReader: default(IReadParquetColumn<TResource>), reason: default(string)))
                    .ToArray();
                if (columnReaders.FirstOrDefault(bound => bound.reason is not null).reason is string incompatible)
                    return (null, null, incompatible);

                var rowCount = reader.RowGroups.Sum(rowGroup => rowGroup.RowCount);
                if (rowCount > int.MaxValue)
                    return (null, null, $"The file holds {rowCount} rows; at most {int.MaxValue} can be read.");
                var create = ResourceFactory<TResource>.Create;
                var resources = Enumerable.Range(0, (int)rowCount).Select(_ => create()).ToArray();
                var unparsed = new List<UnparsedRow>();
                var firstRow = 0;
                for (var index = 0; index < reader.RowGroupCount; index++)
                {
                    using var rowGroup = reader.OpenRowGroupReader(index);
                    foreach (var (columnReader, _) in columnReaders.Where(bound => bound.columnReader is not null))
                        await columnReader.ReadAsync(rowGroup, resources, firstRow, unparsed, cancellationToken);
                    firstRow += (int)rowGroup.RowCount;
                }
                return (resources, unparsed.ToArray(), null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return (null, null, ex.Message);
            }
        }

        /// <summary>Writes <paramref name="resources"/> as one row group; zero resources write a schema-only file.</summary>
        public static async Task WriteToParquetStreamAsync<TResource>(this IEnumerable<TResource> resources,
            Stream stream, string scope = default, CancellationToken cancellationToken = default)
        {
            var bindings = GetMapping<TResource>(scope).GetColumnBindings<TResource>();
            var rows = resources as IReadOnlyList<TResource> ?? resources.ToArray();
            await using var writer = await ParquetWriter.CreateAsync(
                new ParquetSchema(bindings.Select(binding => (Field)binding.Field)), stream,
                cancellationToken: cancellationToken);
            if (rows.Count == 0)
                return;
            using var rowGroup = writer.CreateRowGroup();
            foreach (var binding in bindings)
                await binding.WriteAsync(rowGroup, rows, cancellationToken);
        }

        public static ParquetSchema GetParquetSchema<TResource>(string scope = default) =>
            new(GetMapping<TResource>(scope).GetColumnBindings<TResource>().Select(binding => (Field)binding.Field));

        /// <summary>
        /// Writes rows of (column, type, value) cells against an explicit schema, for tables whose
        /// columns are only known at run time. Cells are matched to columns by name; a column a row
        /// does not mention, a null, or a <see cref="DBNull"/> writes the column's default.
        /// </summary>
        public static async Task WriteToParquetStreamAsync(this IEnumerable<(string name, Type type, object value)[]> rows,
            ParquetSchema schema, Stream stream, CancellationToken cancellationToken = default)
        {
            var table = rows.ToArray();
            var fields = schema.DataFields;
            var columns = fields
                .Select(field => TupleColumn.Create(field, table.Length))
                .ToArray();
            var columnIndexes = fields
                .Select((field, index) => (field.Name, index))
                .ToDictionary(tpl => tpl.Name, tpl => tpl.index, StringComparer.Ordinal);

            foreach (var (row, rowIndex) in table.Select((row, rowIndex) => (row, rowIndex)))
                foreach (var cell in row)
                    if (columnIndexes.TryGetValue(cell.name, out var columnIndex))
                        columns[columnIndex].Set(rowIndex, cell.value);

            await using var writer = await ParquetWriter.CreateAsync(schema, stream, cancellationToken: cancellationToken);
            if (table.Length == 0)
                return;
            using var rowGroup = writer.CreateRowGroup();
            foreach (var column in columns)
                await column.WriteAsync(rowGroup, cancellationToken);
        }

        private static IMapParquet GetMapping<TResource>(string scope) =>
            typeof(TResource)
                .GetAttributesInterface<IMapParquet>()
                .Where(mapping => mapping.DoesParse(scope))
                .OrderByDescending(mapping => mapping is ScopedMapParquetAttribute { IsScoped: true })
                .FirstOrDefault()
            ?? throw new NotSupportedException(
                $"{typeof(TResource).FullName} does not have an attribute implementing {nameof(IMapParquet)} for scope '{scope}'.");

        private static class ResourceFactory<TResource>
        {
            /// <summary>Null when <typeparamref name="TResource"/> cannot be constructed.</summary>
            public static readonly Func<TResource> Create =
                typeof(TResource).IsValueType || typeof(TResource).GetConstructor(Type.EmptyTypes) is not null
                    ? Expression.Lambda<Func<TResource>>(Expression.New(typeof(TResource))).Compile()
                    : null;
        }

        private abstract class TupleColumn
        {
            public static TupleColumn Create(DataField field, int rowCount)
            {
                var valueType = ParquetColumnCodec.ValueTypeOf(field)
                    ?? throw new NotSupportedException($"Column '{field.Name}' is not a flat column of values.");
                return (TupleColumn)Activator.CreateInstance(
                    typeof(TupleColumn<>).MakeGenericType(valueType), field, rowCount);
            }

            public abstract void Set(int row, object value);

            public abstract Task WriteAsync(ParquetRowGroupWriter rowGroup, CancellationToken cancellationToken);
        }

        private sealed class TupleColumn<TValue> : TupleColumn
        {
            private static readonly Type Core = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);
            private readonly DataField field;
            private readonly TValue[] values;

            public TupleColumn(DataField field, int rowCount)
            {
                this.field = field;
                this.values = new TValue[rowCount];
            }

            public override void Set(int row, object value) => values[row] = value switch
            {
                null or DBNull => default,
                TValue typed => typed,
                DateTimeOffset offset when Core == typeof(DateTime) => (TValue)(object)offset.UtcDateTime,
                _ => (TValue)System.Convert.ChangeType(value, Core, CultureInfo.InvariantCulture),
            };

            public override Task WriteAsync(ParquetRowGroupWriter rowGroup, CancellationToken cancellationToken) =>
                ParquetColumnCodec<TValue>.Instance.WriteAsync(rowGroup, field, values, cancellationToken);
        }
    }
}
