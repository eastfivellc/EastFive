using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

using EastFive.Reflection;

namespace EastFive.Serialization.Parquet
{
    /// <summary>
    /// Maps the members of a resource carrying an <see cref="IMapParquetProperty"/> attribute for this
    /// mapping's <see cref="ScopedMapParquetAttribute.Scope"/> — fields first, then properties, each
    /// in declaration order. The bindings are built once per resource type, attribute and scope.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    public class ParquetMappingAttribute : ScopedMapParquetAttribute
    {
        public override IBindParquetColumn<TResource>[] GetColumnBindings<TResource>() =>
            BindingCache<TResource>.Bindings.GetOrAdd((GetType(), Scope ?? string.Empty), _ => BuildBindings<TResource>());

        protected virtual IBindParquetColumn<TResource>[] BuildBindings<TResource>() =>
            typeof(TResource)
                .GetPropertyOrFieldMembers()
                .OrderBy(member => member is PropertyInfo)
                .ThenBy(member => member.MetadataToken)
                .Select(member => (member, mapper: SelectMapper(member)))
                .Where(tpl => tpl.mapper is not null)
                .Select(tpl => tpl.mapper.GetColumnBinding<TResource>(tpl.member))
                .ToArray();

        private IMapParquetProperty SelectMapper(MemberInfo member) =>
            member
                .GetAttributesInterface<IMapParquetProperty>()
                .Where(attr => attr.DoesMap(this.Scope))
                .OrderByDescending(attr => attr is ScopedMapParquetPropertyAttribute { IsScoped: true })
                .FirstOrDefault();

        private static class BindingCache<TResource>
        {
            public static readonly ConcurrentDictionary<(Type mapping, string scope), IBindParquetColumn<TResource>[]> Bindings = new();
        }
    }
}
