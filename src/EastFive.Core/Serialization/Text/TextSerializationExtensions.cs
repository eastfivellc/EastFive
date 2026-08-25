using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Reflection;
using EastFive.Serialization.Parquet;

namespace EastFive.Serialization.Text
{
	public static class TextSerializationExtensions
	{
		public static IEnumerable<TResource> ParseCSV<TResource>(this Stream csvData,
            string scope = default, Stream[] extraStreams = default)
		{
            var filters = typeof(TResource)
                .GetAttributesInterface<IFilterText>()
                .If(scope.HasBlackSpace(),
                    items => items.Where(attrInter => attrInter.DoesFilter(scope)))
                .ToArray();

            return typeof(TResource)
                .GetAttributesInterface<IMapText>()
                .If(scope.HasBlackSpace(),
                    items => items.Where(attrInter => attrInter.DoesParse(scope)))
                .First<IMapText, IEnumerable<TResource>>(
                    (attrInter, next) =>
                    {
                        var resources = attrInter.Parse<TResource>(csvData, filters, extraStreams);
                        return resources;
                    },
                    () => throw new Exception($"{typeof(TResource).FullName} does not have any attributes implementing {typeof(IMapText).FullName}."));
        }

        public static Func<TResource, TResource> ParseTextAsAssignment<TResource>(this MemberInfo member,
            Type type, string rowValue, StringComparison comparisonType)
        {
            if (typeof(string).IsAssignableTo(type))
            {
                Func<TResource, TResource> assign = (res) =>
                    (TResource)member.SetPropertyOrFieldValue(res, rowValue);
                return assign;
            }
            if (typeof(Guid).IsAssignableTo(type))
            {
                Guid.TryParse(rowValue, out Guid guidValue);
                Func<TResource, TResource> assign = (res) =>
                    (TResource)member.SetPropertyOrFieldValue(res, guidValue);
                return assign;
            }
            if (typeof(DateTime).IsAssignableTo(type))
            {
                var didParseDt = DateTime.TryParse(rowValue, out DateTime dtValue);
                if ((!didParseDt) && type.IsNullable())
                {
                    Func<TResource, TResource> assign = (res) =>
                        (TResource)member.SetPropertyOrFieldValue(res, default(DateTime?));
                    return assign;
                }
                else
                {
                    Func<TResource, TResource> assign = (res) =>
                        (TResource)member.SetPropertyOrFieldValue(res, dtValue);
                    return assign;
                }
            }
            if (typeof(bool).IsAssignableTo(type))
            {
                bool.TryParse(rowValue, out bool boolValue);
                Func<TResource, TResource> assign = (res) =>
                    (TResource)member.SetPropertyOrFieldValue(res, boolValue);
                return assign;
            }
            if (typeof(int).IsAssignableTo(type))
            {
                int.TryParse(rowValue, out var intValue);
                Func<TResource, TResource> assign = (res) =>
                    (TResource)member.SetPropertyOrFieldValue(res, intValue);
                return assign;
            }
            if (type.IsEnum)
            {

                var rowValueMapped = Enum.GetNames(type)
                    .Where(
                        enumVal =>
                        {
                            var memInfo = type.GetMember(enumVal).First();
                            if (!memInfo.TryGetAttributeInterface(out IMapEnumValues mapper))
                                return false;
                            return mapper.DoesMatch(rowValue);
                        })
                    .First(
                        (x, next) => x,
                        () => rowValue);

                var ignoreCase = comparisonType == StringComparison.OrdinalIgnoreCase
                    || comparisonType == StringComparison.InvariantCultureIgnoreCase
                    || comparisonType == StringComparison.CurrentCultureIgnoreCase;

                if (Enum.TryParse(type, rowValueMapped, ignoreCase, out object newValue))
                {
                    Func<TResource, TResource> assign = (res) =>
                        (TResource)member.SetPropertyOrFieldValue(res, newValue);
                    return assign;
                }
                Func<TResource, TResource> noAssign = (res) => res;
                return noAssign;
            }
            if (rowValue.TryParseRef(type, out object refObj, out var didMatch))
            {
                Func<TResource, TResource> assign = (res) =>
                    (TResource)member.SetPropertyOrFieldValue(res, refObj);
                return assign;
            }
            if (didMatch)
            {
                Func<TResource, TResource> assign = (res) => res;
                return assign;
            }
            return type.IsNullable(
                (baseType) =>
                {
                    if (rowValue.IsNullOrWhiteSpace())
                    {
                        Func<TResource, TResource> noop = (res) => res;
                        return noop;
                    }
                    return ParseTextAsAssignment<TResource>(member, baseType, rowValue, comparisonType);
                },
                () =>
                {
                    throw new Exception($"{nameof(TextPropertyAttribute)} cannot parse {type.FullName} on {member.DeclaringType.FullName}..{member.Name}");
                });
            
        }

        public static Func<TResource, TResource> ParseObjectAsAssignment<TResource>(this MemberInfo member,
            Type type, object rowValue, StringComparison comparisonType)
        {
            try
            {
                if (type.TryGetNullableUnderlyingType(out Type baseType))
                {
                    if(rowValue.IsDefaultOrNull())
                        return (r) => r;
                    return ParseObjectAsAssignment<TResource>(member, baseType, rowValue, comparisonType);
                }

                if (!rowValue.TryGetType(out Type rowValueType))
                {
                    Func<TResource, TResource> noAssign = (res) => res;
                    return noAssign;
                }

                if (type.IsAssignableFrom(rowValueType))
                {
                    Func<TResource, TResource> assign = (res) =>
                        (TResource)member.SetPropertyOrFieldValue(res, rowValue);
                    return assign;
                }

                if (typeof(string).IsAssignableFrom(rowValueType))
                {
                    var strValue = (string)rowValue;
                    return member.ParseTextAsAssignment<TResource>(type, strValue, comparisonType);
                }

                if (typeof(int).IsAssignableFrom(rowValueType))
                {
                    var intValue = (int)rowValue;
                    var memberType = member.GetPropertyOrFieldType();
                    if (memberType.IsAssignableFrom(typeof(int)))
                    {
                        Func<TResource, TResource> assign = (res) =>
                            (TResource)member.SetPropertyOrFieldValue(res, intValue);
                        return assign;
                    }

                    if (memberType.IsAssignableFrom(typeof(string)))
                    {
                        var strValue = intValue.ToString();
                        Func<TResource, TResource> assign = (res) =>
                            (TResource)member.SetPropertyOrFieldValue(res, strValue);
                        return assign;
                    }
                }

                if (typeof(long).IsAssignableFrom(rowValueType))
                {
                    var longValue = (long)rowValue;
                    var memberType = member.GetPropertyOrFieldType();
                    if (memberType.IsAssignableFrom(typeof(long)))
                    {
                        Func<TResource, TResource> assign = (res) =>
                            (TResource)member.SetPropertyOrFieldValue(res, longValue);
                        return assign;
                    }

                    if (memberType.IsAssignableFrom(typeof(string)))
                    {
                        var strValue = longValue.ToString();
                        Func<TResource, TResource> assign = (res) =>
                            (TResource)member.SetPropertyOrFieldValue(res, strValue);
                        return assign;
                    }
                }
                
                if (rowValueType.IsNumeric())
                {
                    var memberType = member.GetPropertyOrFieldType();

                    if (memberType.IsAssignableFrom(typeof(string)))
                    {
                        var strValue = rowValue.ToString();
                        Func<TResource, TResource> assign = (res) =>
                            (TResource)member.SetPropertyOrFieldValue(res, strValue);
                        return assign;
                    }
                }

                if (typeof(DateTime).IsAssignableFrom(rowValueType))
                {
                    var memberType = member.GetPropertyOrFieldType();
                    var dtValue = memberType.IsNullable() ?
                        (rowValue.IsDefaultOrNull() ?
                            default(DateTime?)
                            :
                            (DateTime?)rowValue)
                        :
                        (DateTime)rowValue;
                    if (memberType.IsAssignableFrom(typeof(DateTime)))
                    {
                        Func<TResource, TResource> assign = (res) =>
                            (TResource)member.SetPropertyOrFieldValue(res, dtValue);
                        return assign;
                    }
                }

                if (typeof(DateTimeOffset).IsAssignableFrom(rowValueType))
                {
                    var dtoValue = (DateTimeOffset)rowValue;
                    var dtValue = dtoValue.DateTime;
                    var memberType = member.GetPropertyOrFieldType();
                    if (memberType.IsAssignableFrom(typeof(DateTime)))
                    {
                        Func<TResource, TResource> assign = (res) =>
                            (TResource)member.SetPropertyOrFieldValue(res, dtValue);
                        return assign;
                    }
                }

                if (rowValue.TryCastObjRef(type, out object parsedRefObj))
                {
                    Func<TResource, TResource> assign = (res) =>
                        (TResource)member.SetPropertyOrFieldValue(res, parsedRefObj);
                    return assign;
                }
            }
            catch (Exception)
            {
                return (r) => r;
            }

            throw new Exception($"{nameof(ParquetPropertyAttribute)} cannot parse {type.FullName} on {member.DeclaringType.FullName}..{member.Name}");

        }
    }
}

