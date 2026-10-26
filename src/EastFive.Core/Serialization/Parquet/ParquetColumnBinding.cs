using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using global::Parquet;
using global::Parquet.Schema;

using EastFive.Reflection;

namespace EastFive.Serialization.Parquet
{
    /// <summary>Builds the column binding an <see cref="IMapParquetProperty"/> attribute returns.</summary>
    public static class ParquetColumnBinding
    {
        /// <summary>
        /// Binds <paramref name="member"/> to a column named <paramref name="columnName"/> holding
        /// <typeparamref name="TColumn"/> values — <c>string</c>, <c>byte[]</c>, or a struct (or
        /// nullable struct) Parquet.Net stores natively. The converters run once per value, through
        /// compiled member accessors: no per-row reflection and no boxing.
        /// </summary>
        public static IBindParquetColumn<TResource> Create<TResource, TMember, TColumn>(MemberInfo member,
                string columnName,
                Func<TMember, TColumn> toColumn,
                Func<TColumn, TMember> fromColumn,
                StringComparison nameComparison = StringComparison.Ordinal) =>
            new ParquetMemberColumn<TResource, TMember, TColumn>(member, columnName, toColumn, fromColumn, nameComparison);
    }

    internal sealed class ParquetMemberColumn<TResource, TMember, TColumn> : IBindParquetColumn<TResource>
    {
        private delegate void Assign(ref TResource resource, TMember value);

        private readonly MemberInfo member;
        private readonly Func<TResource, TMember> get;
        private readonly Assign set;
        private readonly Func<TMember, TColumn> toColumn;
        private readonly Func<TColumn, TMember> fromColumn;
        private readonly ParquetColumnCodec<TColumn> codec;
        private readonly StringComparison nameComparison;

        public ParquetMemberColumn(MemberInfo member, string columnName,
            Func<TMember, TColumn> toColumn, Func<TColumn, TMember> fromColumn, StringComparison nameComparison)
        {
            if (member.GetPropertyOrFieldType() != typeof(TMember))
                throw new ArgumentException(
                    $"{Describe(member)} is {member.GetPropertyOrFieldType().FullName}, not {typeof(TMember).FullName}.",
                    nameof(member));
            this.member = member;
            this.toColumn = toColumn;
            this.fromColumn = fromColumn;
            this.nameComparison = nameComparison;
            this.codec = ParquetColumnCodec<TColumn>.Instance
                ?? throw new NotSupportedException(
                    $"{Describe(member)} maps to {typeof(TColumn).FullName}, which is not a parquet column type.");
            try
            {
                this.Field = codec.CreateField(columnName);
            }
            catch (NotSupportedException ex)
            {
                throw new NotSupportedException(
                    $"{Describe(member)} maps to {typeof(TColumn).FullName}, which Parquet.Net cannot store: {ex.Message}", ex);
            }
            this.get = CompileGetter(member);
            this.set = CompileSetter(member);
        }

        public DataField Field { get; }

        public bool IsColumn(DataField fileField) => string.Equals(fileField.Name, Field.Name, nameComparison);

        public Task WriteAsync(ParquetRowGroupWriter rowGroup, IReadOnlyList<TResource> resources,
            CancellationToken cancellationToken)
        {
            var values = new TColumn[resources.Count];
            for (var index = 0; index < values.Length; index++)
                values[index] = toColumn(get(resources[index]));
            return codec.WriteAsync(rowGroup, Field, values, cancellationToken);
        }

        public TResult BindReader<TResult>(DataField fileField,
            Func<IReadParquetColumn<TResource>, TResult> onBound,
            Func<string, TResult> onIncompatible)
        {
            if (set is null)
                return onIncompatible($"{Describe(member)} cannot be assigned.");
            var fileValueType = ParquetColumnCodec.ValueTypeOf(fileField);
            if (fileValueType is null)
                return onIncompatible($"Column '{fileField.Name}' is not a flat column of values.");
            var reader = (IReadParquetColumn<TResource>)typeof(ParquetMemberColumn<TResource, TMember, TColumn>)
                .GetMethod(nameof(CreateReader), BindingFlags.NonPublic | BindingFlags.Instance)
                .MakeGenericMethod(fileValueType)
                .Invoke(this, new object[] { fileField });
            return reader is null
                ? onIncompatible(
                    $"Column '{fileField.Name}' holds {fileValueType.Name} values, which cannot be read into {Describe(member)} ({typeof(TMember).Name}).")
                : onBound(reader);
        }

        private IReadParquetColumn<TResource> CreateReader<TFile>(DataField fileField)
        {
            var fileCodec = ParquetColumnCodec<TFile>.Instance;
            var convert = ParquetValueConversion.Create<TFile, TColumn>();
            return fileCodec is null || convert is null
                ? null
                : new ColumnReader<TFile>(this, fileField, fileCodec, convert);
        }

        private sealed class ColumnReader<TFile> : IReadParquetColumn<TResource>
        {
            private readonly ParquetMemberColumn<TResource, TMember, TColumn> binding;
            private readonly DataField fileField;
            private readonly ParquetColumnCodec<TFile> fileCodec;
            private readonly Func<TFile, TColumn> convert;

            public ColumnReader(ParquetMemberColumn<TResource, TMember, TColumn> binding, DataField fileField,
                ParquetColumnCodec<TFile> fileCodec, Func<TFile, TColumn> convert)
            {
                this.binding = binding;
                this.fileField = fileField;
                this.fileCodec = fileCodec;
                this.convert = convert;
            }

            public async Task ReadAsync(ParquetRowGroupReader rowGroup, TResource[] resources, int firstRow,
                ICollection<UnparsedRow> unparsed, CancellationToken cancellationToken)
            {
                var values = await fileCodec.ReadAsync(rowGroup, fileField, cancellationToken);
                for (var index = 0; index < values.Length; index++)
                {
                    // A value that fails to convert is reported with its row, not thrown.
                    try
                    {
                        binding.set(ref resources[firstRow + index], binding.fromColumn(convert(values[index])));
                    }
                    catch (Exception ex)
                    {
                        unparsed.Add(new UnparsedRow(firstRow + index, fileField.Name, values[index], ex.Message));
                    }
                }
            }
        }

        private static string Describe(MemberInfo member) => $"{member.DeclaringType?.FullName}.{member.Name}";

        private static Func<TResource, TMember> CompileGetter(MemberInfo member)
        {
            if (member is PropertyInfo { CanRead: false })
                throw new NotSupportedException($"{Describe(member)} cannot be read.");
            var resource = Expression.Parameter(typeof(TResource), "resource");
            return Expression.Lambda<Func<TResource, TMember>>(Expression.MakeMemberAccess(resource, member), resource)
                .Compile();
        }

        private static Assign CompileSetter(MemberInfo member)
        {
            if (member is PropertyInfo { CanWrite: false } || member is FieldInfo { IsInitOnly: true })
                return null;
            var resource = Expression.Parameter(typeof(TResource).MakeByRefType(), "resource");
            var value = Expression.Parameter(typeof(TMember), "value");
            return Expression.Lambda<Assign>(
                    Expression.Assign(Expression.MakeMemberAccess(resource, member), value), resource, value)
                .Compile();
        }
    }
}
