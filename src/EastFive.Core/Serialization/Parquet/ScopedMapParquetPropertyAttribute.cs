using System;
using System.Linq;
using System.Reflection;

namespace EastFive.Serialization.Parquet
{
    public abstract class ScopedMapParquetPropertyAttribute : Attribute, IMapParquetProperty
    {
        public string Scope { get; set; }

        public string Scopes { get; set; }

        /// <summary>An attribute with no scope maps every scope; a scoped one is preferred over it.</summary>
        public virtual bool DoesMap(string scope) => ParquetScopes.Matches(Scope, Scopes, scope);

        internal bool IsScoped => ParquetScopes.IsScoped(Scope, Scopes);

        public abstract IBindParquetColumn<TResource> GetColumnBinding<TResource>(MemberInfo member);
    }

    internal static class ParquetScopes
    {
        public static bool IsScoped(string scope, string scopes) => scope.HasBlackSpace() || scopes.HasBlackSpace();

        public static bool Matches(string scope, string scopes, string requested)
        {
            if (scope.HasBlackSpace() && string.Equals(scope, requested, StringComparison.Ordinal))
                return true;
            if (scopes.HasBlackSpace())
                return scopes
                    .Split(',')
                    .Select(candidate => candidate.Trim())
                    .Any(candidate => string.Equals(candidate, requested, StringComparison.Ordinal));
            return scope.IsNullOrWhiteSpace();
        }
    }
}
