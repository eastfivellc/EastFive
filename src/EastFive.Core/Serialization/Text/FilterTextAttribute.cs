using System;
using System.Linq;
using System.Reflection;

using EastFive;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Reflection;

namespace EastFive.Serialization.Text
{
    public class FilterTextAttribute : Attribute, IFilterText
    {
        public string Field { get; set; }

        public string Value { get; set; }

        public StringComparison KeyComparisonType { get; set; } = StringComparison.Ordinal;

        public StringComparison ValueComparisonType { get; set; } = StringComparison.Ordinal;

        public ComparisonRelationship Relationship { get; set; } = ComparisonRelationship.equals;

        public bool Required { get; set; } = false;

        public string Scope { get; set; }

        public string Scopes { get; set; }

        public FilterTextAttribute()
        {

        }

        public FilterTextAttribute(string field, ComparisonRelationship relationship, string value)
        {
            this.Field = field;
            this.Relationship = relationship;
            this.Value = value;
        }

        public virtual bool DoesFilter(string scope)
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

        public bool Where((string key, string value)[] rowValues)
        {
            return rowValues
                .Where(kvp => String.Equals(kvp.key, this.Field, KeyComparisonType))
                .First(
                    (kvp, next) =>
                    {
                        var comparision = String.Compare(kvp.value, this.Value, ValueComparisonType);
                        var areEqual = comparision == 0;
                        if (this.Relationship == ComparisonRelationship.equals)
                            return areEqual;
                        if (this.Relationship == ComparisonRelationship.notEquals)
                            return !areEqual;
                        if (this.Relationship == ComparisonRelationship.greaterThan)
                            return comparision > 1;
                        if (this.Relationship == ComparisonRelationship.lessThan)
                            return comparision < 1;
                        if (this.Relationship == ComparisonRelationship.greaterThanOrEquals)
                            return comparision >= 0;
                        if (this.Relationship == ComparisonRelationship.lessThanOrEquals)
                            return comparision <= 0;
                        throw new Exception("Unrecongized comparision type.");
                    },
                    () =>
                    {
                        if (this.Required)
                            return false;
                        return true;
                    });
        }
    }
}

