using System;
using System.Linq;
using System.Reflection;

namespace EastFive.Serialization.Text
{
    public abstract class ScopedMapTextPropertyAttribute : Attribute, IMapTextProperty
    {
        public string Scope { get; set; }

        public string Scopes { get; set; }

        public virtual bool DoesMap(string scope)
        {
            if (this.Scope.HasBlackSpace())
                if (String.Equals(Scope, scope, StringComparison.Ordinal))
                    return true;

            if (this.Scopes.HasBlackSpace())
                return this.Scopes
                    .Split(',')
                    .Select(scopeCandidate => scopeCandidate.Trim())
                    .Where(scopeCandidate => String.Equals(scopeCandidate, scope, StringComparison.Ordinal))
                    .Any();

            return this.Scope.IsNullOrWhiteSpace();
        }

        abstract public TResource ParseRow<TResource>(TResource resource, MemberInfo member, (string key, string value)[] rowValues);
    }
}

