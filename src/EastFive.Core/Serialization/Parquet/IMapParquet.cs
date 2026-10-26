using System;
using System.Linq;

namespace EastFive.Serialization.Parquet
{
    /// <summary>
    /// Implemented by type attributes that say which members of a resource map to parquet columns.
    /// The read and write algorithms live in <see cref="ParquetSerializationExtensions"/>; a mapping
    /// only supplies the column bindings.
    /// </summary>
    public interface IMapParquet
    {
        bool DoesParse(string scope);

        /// <summary>The resource's column bindings, in column order. Cached by implementations.</summary>
        IBindParquetColumn<TResource>[] GetColumnBindings<TResource>();
    }

    public abstract class ScopedMapParquetAttribute : Attribute, IMapParquet
    {
        public string Scope { get; set; }

        public string Scopes { get; set; }

        /// <summary>An attribute with no scope parses every scope; a scoped one is preferred over it.</summary>
        public virtual bool DoesParse(string scope) => ParquetScopes.Matches(Scope, Scopes, scope);

        internal bool IsScoped => ParquetScopes.IsScoped(Scope, Scopes);

        public abstract IBindParquetColumn<TResource>[] GetColumnBindings<TResource>();
    }
}
