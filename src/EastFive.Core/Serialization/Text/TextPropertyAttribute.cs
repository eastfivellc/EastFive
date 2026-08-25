using System;
using System.Linq;
using System.Reflection;

using EastFive.Linq;
using EastFive.Reflection;

namespace EastFive.Serialization.Text
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class TextPropertyAttribute : ScopedMapTextPropertyAttribute
    {
        public string Name { get; set; }

        public StringComparison ComparisonType { get; set; }

        public bool IgnoreWhitespace { get; set; } = false;

        public TextPropertyAttribute()
        {

        }

        public TextPropertyAttribute(string name)
        {
            this.Name = name;
        }

        public override TResource ParseRow<TResource>(TResource resource,
            MemberInfo member, (string key, string value)[] rowValues)
        {
            var type = member.GetPropertyOrFieldType();

            var assignment = rowValues
                .Where(
                    tpl =>
                    {
                        return IsMatch(tpl.key, tpl.value);
                    })
                .First(
                    (rowKeyValue, next) =>
                    {
                        var (rowKey, rowValue) = rowKeyValue;
                        var assign = member.ParseTextAsAssignment<TResource>(type, rowValue, this.ComparisonType);
                        return assign;
                    },
                    () =>
                    {
                        Func<TResource, TResource> assign = (res) => res;
                        return assign;
                    });
            return assignment(resource);
        }

        public virtual bool IsMatch(string key, string value)
        {
            if (!this.IgnoreWhitespace)
                return String.Equals(this.Name, key, ComparisonType);

            var nameNoWhitespace = this.Name.RemoveWhitespace();
            var keyNoWhitespace = key.RemoveWhitespace();
            return String.Equals(nameNoWhitespace, keyNoWhitespace, ComparisonType);
        }

        public virtual Func<TResource, TResource> ParseAsAssignment<TResource>(MemberInfo member,
            Type type, string rowValue, StringComparison comparisonType)
        {
            return member.ParseTextAsAssignment<TResource>(type, rowValue, comparisonType);
        }

    }
}

