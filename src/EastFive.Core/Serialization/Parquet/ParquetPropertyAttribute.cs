using System;
using System.Reflection;

namespace EastFive.Serialization.Parquet
{
    /// <summary>
    /// Maps a property or field to the column <see cref="Name"/> (the member's name when unset).
    /// Strings, booleans, numbers, <see cref="DateTime"/> and their nullable forms are stored as
    /// themselves; <see cref="DateTimeOffset"/> as a UTC <see cref="DateTime"/> (INT96, as the 3.x
    /// engine wrote it); <see cref="Guid"/>, <see cref="IRef{TType}"/> and
    /// <see cref="IRefOptional{TType}"/> as their "N" string; enums by name.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class ParquetPropertyAttribute : ScopedMapParquetPropertyAttribute
    {
        public string Name { get; set; }

        /// <summary>How the column name is matched against a file's columns when reading.</summary>
        public StringComparison ComparisonType { get; set; } = StringComparison.Ordinal;

        public ParquetPropertyAttribute()
        {
        }

        public ParquetPropertyAttribute(string name)
        {
            this.Name = name;
        }

        protected virtual string GetColumnName(MemberInfo member) => Name.HasBlackSpace() ? Name : member.Name;

        public override IBindParquetColumn<TResource> GetColumnBinding<TResource>(MemberInfo member) =>
            ParquetMemberMapping.Bind<TResource>(member, GetColumnName(member), ComparisonType);
    }
}
