using EastFive.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EastFive.Persistence
{
    public static class AttributeExtensions
    {
        /// <summary>
        /// A type's storage members, scanned once per type. The attribute instances are shared by
        /// every row and thread that reads or writes the type, so a storage attribute must not
        /// write its own state while handling a row.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, KeyValuePair<MemberInfo, IPersistInAzureStorageTables[]>[]> persistenceAttributes =
            new ConcurrentDictionary<Type, KeyValuePair<MemberInfo, IPersistInAzureStorageTables[]>[]>();

        public static IEnumerable<KeyValuePair<MemberInfo, IPersistInAzureStorageTables[]>> GetPersistenceAttributes(this Type type)
        {
            return persistenceAttributes.GetOrAdd(type,
                scanned =>
                {
                    var memberQuery = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
                    return scanned
                        .GetMembers(memberQuery)
                        .Where(field => field.ContainsAttributeInterface<IPersistInAzureStorageTables>())
                        .Select(field => field.PairWithValue(field.GetAttributesInterface<IPersistInAzureStorageTables>()))
                        .ToArray();
                });
        }

        public static IEnumerable<KeyValuePair<MemberInfo, IPersistInAzureStorageTables>> StorageProperties(this Type entityType)
        {
            return entityType.GetPersistenceAttributes()
                .Select(
                    propInfoAttrsKvp =>
                    {
                        var propInfo = propInfoAttrsKvp.Key;
                        var attrs = propInfoAttrsKvp.Value;
                        if (attrs.Length > 1)
                        {
                            var propIdentifier = $"{propInfo.DeclaringType.FullName}__{propInfo.Name}";
                            var attributesInConflict = attrs.Select(a => a.GetType().FullName).Join(",");
                            throw new Exception($"{propIdentifier} has multiple IPersistInAzureStorageTables attributes:{attributesInConflict}.");
                        }
                        var attr = attrs.First() as IPersistInAzureStorageTables;
                        return attr.PairWithKey(propInfo);
                    });
        }

        public static readonly Regex DisallowedCharsInTableKeys = new Regex(@"[\\\\#%+/?\u0000-\u001F\u007F-\u009F]");

        public static string AsAzureStorageTablesSafeKey(this string keyValueRow)
        {
            if(keyValueRow.IsNullOrEmpty())
                return keyValueRow;
            string sanitizedKey = DisallowedCharsInTableKeys.Replace(keyValueRow, "_");
            return sanitizedKey;
        }
    }
}
