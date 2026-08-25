using System;
using System.Linq;
using System.Reflection;
using EastFive.Extensions;
using EastFive.Reflection;
using Parquet.Data;

namespace EastFive.Serialization.Parquet
{
    public abstract class ScopedMapParquetPropertyAttribute : Attribute, IMapParquetProperty
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

        protected virtual string GetMemberName(MemberInfo memberInfo)
        {
            return memberInfo.Name;
        }

        protected virtual Type GetMemberType(MemberInfo memberInfo)
        {
            var propertyOrFieldType = memberInfo.GetPropertyOrFieldType();
            return MapType(propertyOrFieldType);

            Type MapType(Type type)
            {
                if (type == typeof(DateTime))
                    return typeof(DateTime?);
                if (type == typeof(long))
                    return typeof(long?);
                if (type == typeof(int))
                    return typeof(int?);
                if (type == typeof(bool))
                    return typeof(bool?);
                if (type == typeof(float))
                    return typeof(float?);
                if (type == typeof(double))
                    return typeof(double?);
                if (type == typeof(decimal))
                    return typeof(decimal?);
                if (type.IsSubClassOfGeneric(typeof(IReferenceable)))
                    return typeof(string);
                if (type.IsSubClassOfGeneric(typeof(IReferenceableOptional)))
                    return typeof(string);
                if (type == typeof(string))
                    return typeof(string);
                if (type == typeof(Guid))
                    return typeof(string);
                if (type.IsEnum)
                    return typeof(string);

                return type.IsNullable(
                    a => MapType(a),
                    () =>
                    {
                        throw new Exception($"Could not map type `{type.FullName}` to a parquet schema");
                    });
            }
        }

        public DataField GetParquetDataField(MemberInfo memberInfo)
        {
            var name = GetMemberName(memberInfo);
            var type = GetMemberType(memberInfo);
            return new global::Parquet.Data.DataField(name, type);
        }

        public (string name, Type type, object value) GetParquetDataValue<TEntity>(TEntity entity, MemberInfo memberInfo)
        {
            var name = GetMemberName(memberInfo);
            var type = GetMemberType(memberInfo);
            var value = memberInfo.GetPropertyOrFieldValue(entity);
            if(value.IsNotDefaultOrNull())
            {
                var rawType = value.GetType();
                if (rawType.IsAssignableTo(typeof(IReferenceable)))
                    value = ((IReferenceable)value).id.ToString("N");
                if (rawType.IsAssignableTo(typeof(IReferenceableOptional)))
                {
                    var castValue = (IReferenceableOptional)value;
                    if (castValue.HasValue)
                        value = castValue.id.Value.ToString("N");
                    else
                        value =string.Empty;
                }
                if (rawType.IsAssignableTo(typeof(Guid)))
                    value = ((Guid)value).ToString("N");
                if (rawType.IsEnum)
                    value = Enum.GetName(rawType, value);
            }
            return (name, type, value);
        }

        abstract public TResource ParseMemberValueFromRow<TResource>(
            TResource resource, MemberInfo member, (global::Parquet.Data.Field key, object value)[] rowValues);

        
    }
}

