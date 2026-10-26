using System;
using System.Linq;
using System.Reflection;

using EastFive.Reflection;

namespace EastFive.Serialization.Parquet
{
    /// <summary>
    /// The default member ⇄ column conversions behind <see cref="ParquetPropertyAttribute"/>. Each is a
    /// pair of typed delegates chosen once per member type, then bound through
    /// <see cref="ParquetColumnBinding.Create"/>.
    /// </summary>
    internal static class ParquetMemberMapping
    {
        public static IBindParquetColumn<TResource> Bind<TResource>(MemberInfo member, string columnName,
            StringComparison nameComparison)
        {
            var memberType = member.GetPropertyOrFieldType();
            var (columnType, toColumn, fromColumn) = Conversions(memberType)
                ?? throw new NotSupportedException(
                    $"{member.DeclaringType?.FullName}.{member.Name} is {memberType.FullName}, which has no parquet mapping.");
            try
            {
                return (IBindParquetColumn<TResource>)typeof(ParquetColumnBinding)
                    .GetMethod(nameof(ParquetColumnBinding.Create))
                    .MakeGenericMethod(typeof(TResource), memberType, columnType)
                    .Invoke(null, new object[] { member, columnName, toColumn, fromColumn, nameComparison });
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }

        private static (Type columnType, Delegate toColumn, Delegate fromColumn)? Conversions(Type memberType)
        {
            if (memberType == typeof(string) || memberType == typeof(byte[]))
                return Pair(memberType, nameof(Identity), memberType);
            if (memberType == typeof(DateTime))
                return Pair(typeof(DateTime), nameof(DateTimeToColumn), nameof(DateTimeFromColumn));
            if (memberType == typeof(DateTime?))
                return Pair(typeof(DateTime?), nameof(NullableDateTimeToColumn), nameof(NullableDateTimeFromColumn));
            if (memberType == typeof(DateTimeOffset))
                return Pair(typeof(DateTime), nameof(OffsetToColumn), nameof(OffsetFromColumn));
            if (memberType == typeof(DateTimeOffset?))
                return Pair(typeof(DateTime?), nameof(NullableOffsetToColumn), nameof(NullableOffsetFromColumn));
            if (memberType == typeof(Guid))
                return Pair(typeof(string), nameof(GuidToColumn), nameof(GuidFromColumn));
            if (memberType == typeof(Guid?))
                return Pair(typeof(string), nameof(NullableGuidToColumn), nameof(NullableGuidFromColumn));
            if (memberType.IsEnum)
                return Pair(typeof(string), nameof(EnumToColumn), nameof(EnumFromColumn), memberType);
            if (Nullable.GetUnderlyingType(memberType) is { IsEnum: true } enumType)
                return Pair(typeof(string), nameof(NullableEnumToColumn), nameof(NullableEnumFromColumn), enumType);
            if (memberType.IsGenericType && memberType.GetGenericTypeDefinition() == typeof(IRef<>))
                return Pair(typeof(string), nameof(RefToColumn), nameof(RefFromColumn), memberType.GenericTypeArguments);
            if (memberType.IsGenericType && memberType.GetGenericTypeDefinition() == typeof(IRefOptional<>))
                return Pair(typeof(string), nameof(RefOptionalToColumn), nameof(RefOptionalFromColumn), memberType.GenericTypeArguments);
            // Any other struct is stored as itself; Parquet.Net decides whether it can be.
            if (memberType.IsValueType)
                return Pair(memberType, nameof(Identity), memberType);
            return null;
        }

        private static (Type, Delegate, Delegate) Pair(Type columnType, string identity, Type valueType)
        {
            var method = Method(identity).MakeGenericMethod(valueType);
            var delegateType = typeof(Func<,>).MakeGenericType(valueType, valueType);
            var converter = method.CreateDelegate(delegateType);
            return (columnType, converter, converter);
        }

        private static (Type, Delegate, Delegate) Pair(Type columnType, string toColumn, string fromColumn,
            params Type[] typeArguments)
        {
            var to = Close(Method(toColumn), typeArguments);
            var from = Close(Method(fromColumn), typeArguments);
            return (columnType, CreateFunc(to), CreateFunc(from));

            static MethodInfo Close(MethodInfo method, Type[] arguments) =>
                method.IsGenericMethodDefinition ? method.MakeGenericMethod(arguments) : method;

            static Delegate CreateFunc(MethodInfo method) => method.CreateDelegate(
                typeof(Func<,>).MakeGenericType(method.GetParameters().Single().ParameterType, method.ReturnType));
        }

        private static MethodInfo Method(string name) =>
            typeof(ParquetMemberMapping).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);

        private static T Identity<T>(T value) => value;

        private static DateTime DateTimeToColumn(DateTime value) => ParquetValueConversion.AsUtc(value);
        private static DateTime DateTimeFromColumn(DateTime value) => ParquetValueConversion.AsUtc(value);
        private static DateTime? NullableDateTimeToColumn(DateTime? value) => value.HasValue ? ParquetValueConversion.AsUtc(value.Value) : null;
        private static DateTime? NullableDateTimeFromColumn(DateTime? value) => value.HasValue ? ParquetValueConversion.AsUtc(value.Value) : null;

        private static DateTime OffsetToColumn(DateTimeOffset value) => value.UtcDateTime;
        private static DateTimeOffset OffsetFromColumn(DateTime value) => ParquetValueConversion.ToUtcOffset(value);
        private static DateTime? NullableOffsetToColumn(DateTimeOffset? value) => value?.UtcDateTime;
        private static DateTimeOffset? NullableOffsetFromColumn(DateTime? value) =>
            value.HasValue ? ParquetValueConversion.ToUtcOffset(value.Value) : null;

        private static string GuidToColumn(Guid value) => value.ToString("N");
        private static Guid GuidFromColumn(string value) => value.HasBlackSpace() ? Guid.Parse(value) : default;
        private static string NullableGuidToColumn(Guid? value) => value?.ToString("N");
        private static Guid? NullableGuidFromColumn(string value) => value.HasBlackSpace() ? Guid.Parse(value) : null;

        private static string EnumToColumn<TEnum>(TEnum value) where TEnum : struct, Enum => value.ToString();
        private static TEnum EnumFromColumn<TEnum>(string value) where TEnum : struct, Enum =>
            value.HasBlackSpace() ? Enum.Parse<TEnum>(value.Trim(), ignoreCase: true) : default;
        private static string NullableEnumToColumn<TEnum>(TEnum? value) where TEnum : struct, Enum => value?.ToString();
        private static TEnum? NullableEnumFromColumn<TEnum>(string value) where TEnum : struct, Enum =>
            value.HasBlackSpace() ? Enum.Parse<TEnum>(value.Trim(), ignoreCase: true) : null;

        private static string RefToColumn<T>(IRef<T> value) where T : IReferenceable => value?.id.ToString("N");
        private static IRef<T> RefFromColumn<T>(string value) where T : IReferenceable =>
            value.HasBlackSpace() ? Guid.Parse(value).AsRef<T>() : null;

        // The 3.x mapping wrote an empty reference as "", not null.
        private static string RefOptionalToColumn<T>(IRefOptional<T> value) where T : IReferenceable =>
            value is { HasValue: true } ? value.id.Value.ToString("N") : string.Empty;
        private static IRefOptional<T> RefOptionalFromColumn<T>(string value) where T : IReferenceable =>
            (value.HasBlackSpace() ? Guid.Parse(value) : default(Guid?)).AsRefOptional<T>();
    }
}
