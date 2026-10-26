using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using global::Parquet;
using global::Parquet.Schema;

namespace EastFive.Serialization.Parquet
{
    /// <summary>
    /// Implemented by member attributes that map a property or field to a parquet column. The
    /// attribute declares the column and owns the conversion between member and column values by
    /// returning a binding; <see cref="ParquetColumnBinding.Create"/> builds one from two converters.
    /// </summary>
    public interface IMapParquetProperty
    {
        bool DoesMap(string scope);

        /// <summary>
        /// Called once per resource type and scope; the result is cached. Throws
        /// <see cref="NotSupportedException"/> when the member's type cannot be mapped, so an
        /// unsupported type fails when the mapping is built rather than part way through a file.
        /// </summary>
        IBindParquetColumn<TResource> GetColumnBinding<TResource>(MemberInfo member);
    }

    /// <summary>A member bound to one parquet column.</summary>
    public interface IBindParquetColumn<TResource>
    {
        /// <summary>The column this binding writes.</summary>
        DataField Field { get; }

        /// <summary>Whether a column found in a file is the one this binding reads.</summary>
        bool IsColumn(DataField fileField);

        /// <summary>Writes this binding's column for every resource, in one call.</summary>
        Task WriteAsync(ParquetRowGroupWriter rowGroup, IReadOnlyList<TResource> resources,
            CancellationToken cancellationToken);

        /// <summary>
        /// Binds a reader for a column found in a file, whose values may be of a different type than
        /// <see cref="Field"/> (an older writer, a nullable column). <paramref name="onIncompatible"/>
        /// when no conversion exists from the file's values to the member.
        /// </summary>
        TResult BindReader<TResult>(DataField fileField,
            Func<IReadParquetColumn<TResource>, TResult> onBound,
            Func<string, TResult> onIncompatible);
    }

    /// <summary>Reads one file column into the members of already-constructed resources.</summary>
    public interface IReadParquetColumn<TResource>
    {
        /// <summary>
        /// Reads the column of <paramref name="rowGroup"/> into
        /// <c>resources[firstRow .. firstRow + rowGroup.RowCount]</c>, adding a value it cannot
        /// convert to <paramref name="unparsed"/> instead of throwing.
        /// </summary>
        Task ReadAsync(ParquetRowGroupReader rowGroup, TResource[] resources, int firstRow,
            ICollection<UnparsedRow> unparsed, CancellationToken cancellationToken);
    }
}
