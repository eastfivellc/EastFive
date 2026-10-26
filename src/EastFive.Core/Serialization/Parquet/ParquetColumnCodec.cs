using System;
using System.Threading;
using System.Threading.Tasks;

using global::Parquet;
using global::Parquet.Schema;

namespace EastFive.Serialization.Parquet
{
    /// <summary>
    /// Moves a whole column of <typeparamref name="TValue"/> values in and out of a row group through
    /// the matching typed Parquet.Net overload: <c>string</c>, <c>byte[]</c>, a struct, or a nullable
    /// struct. These are the only column value types Parquet.Net reads and writes.
    /// </summary>
    internal abstract class ParquetColumnCodec<TValue>
    {
        /// <summary>Null when <typeparamref name="TValue"/> is not a column value type.</summary>
        public static readonly ParquetColumnCodec<TValue> Instance =
            (ParquetColumnCodec<TValue>)ParquetColumnCodec.Create(typeof(TValue));

        /// <summary>Throws <see cref="NotSupportedException"/> for a type Parquet.Net cannot store.</summary>
        public abstract DataField CreateField(string name);

        public abstract Task WriteAsync(ParquetRowGroupWriter rowGroup, DataField field, TValue[] values,
            CancellationToken cancellationToken);

        public abstract Task<TValue[]> ReadAsync(ParquetRowGroupReader rowGroup, DataField field,
            CancellationToken cancellationToken);

        protected static int RowCount(ParquetRowGroupReader rowGroup) => checked((int)rowGroup.RowCount);
    }

    internal static class ParquetColumnCodec
    {
        public static object Create(Type valueType)
        {
            if (valueType == typeof(string))
                return new StringColumnCodec();
            if (valueType == typeof(byte[]))
                return new ByteArrayColumnCodec();
            if (Nullable.GetUnderlyingType(valueType) is Type underlying)
                return Activator.CreateInstance(typeof(NullableStructColumnCodec<>).MakeGenericType(underlying));
            if (valueType.IsValueType)
                return Activator.CreateInstance(typeof(StructColumnCodec<>).MakeGenericType(valueType));
            return null;
        }

        /// <summary>
        /// The type a file column's values read as: <c>string</c> for text (which Parquet.Net reports
        /// as <c>ReadOnlyMemory&lt;char&gt;</c>), <c>T?</c> for a nullable primitive, null for a
        /// repeated or nested column.
        /// </summary>
        public static Type ValueTypeOf(DataField field)
        {
            if (field.IsArray || field.MaxRepetitionLevel > 0)
                return null;
            if (field.ClrType == typeof(string) || field.ClrType == typeof(ReadOnlyMemory<char>))
                return typeof(string);
            if (field.ClrType == typeof(byte[]) || field.ClrType == typeof(ReadOnlyMemory<byte>))
                return typeof(byte[]);
            if (!field.ClrType.IsValueType)
                return null;
            return field.IsNullable
                ? typeof(Nullable<>).MakeGenericType(field.ClrType)
                : field.ClrType;
        }

        private sealed class StringColumnCodec : ParquetColumnCodec<string>
        {
            public override DataField CreateField(string name) => new DataField<string>(name);

            public override Task WriteAsync(ParquetRowGroupWriter rowGroup, DataField field, string[] values,
                    CancellationToken cancellationToken) =>
                rowGroup.WriteAsync(field, values);

            public override async Task<string[]> ReadAsync(ParquetRowGroupReader rowGroup, DataField field,
                CancellationToken cancellationToken)
            {
                var values = new string[RowCount(rowGroup)];
                await rowGroup.ReadAsync(field, values.AsMemory(), cancellationToken: cancellationToken);
                return values;
            }
        }

        private sealed class ByteArrayColumnCodec : ParquetColumnCodec<byte[]>
        {
            public override DataField CreateField(string name) => new DataField<byte[]>(name);

            public override Task WriteAsync(ParquetRowGroupWriter rowGroup, DataField field, byte[][] values,
                    CancellationToken cancellationToken) =>
                rowGroup.WriteAsync(field, values);

            public override async Task<byte[][]> ReadAsync(ParquetRowGroupReader rowGroup, DataField field,
                CancellationToken cancellationToken)
            {
                var values = new byte[RowCount(rowGroup)][];
                await rowGroup.ReadAsync(field, values.AsMemory(), cancellationToken: cancellationToken);
                return values;
            }
        }

        private sealed class StructColumnCodec<T> : ParquetColumnCodec<T>
            where T : struct
        {
            public override DataField CreateField(string name) => new DataField<T>(name);

            public override Task WriteAsync(ParquetRowGroupWriter rowGroup, DataField field, T[] values,
                    CancellationToken cancellationToken) =>
                rowGroup.WriteAsync<T>(field, new ReadOnlyMemory<T>(values), cancellationToken: cancellationToken);

            public override async Task<T[]> ReadAsync(ParquetRowGroupReader rowGroup, DataField field,
                CancellationToken cancellationToken)
            {
                var values = new T[RowCount(rowGroup)];
                await rowGroup.ReadAsync<T>(field, values.AsMemory(), cancellationToken: cancellationToken);
                return values;
            }
        }

        private sealed class NullableStructColumnCodec<T> : ParquetColumnCodec<T?>
            where T : struct
        {
            public override DataField CreateField(string name) => new DataField<T?>(name);

            public override Task WriteAsync(ParquetRowGroupWriter rowGroup, DataField field, T?[] values,
                    CancellationToken cancellationToken) =>
                rowGroup.WriteAsync<T>(field, new ReadOnlyMemory<T?>(values), cancellationToken: cancellationToken);

            public override async Task<T?[]> ReadAsync(ParquetRowGroupReader rowGroup, DataField field,
                CancellationToken cancellationToken)
            {
                var values = new T?[RowCount(rowGroup)];
                await rowGroup.ReadAsync<T>(field, values.AsMemory(), cancellationToken: cancellationToken);
                return values;
            }
        }
    }
}
