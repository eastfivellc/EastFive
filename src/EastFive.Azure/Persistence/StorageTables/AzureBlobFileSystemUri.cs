using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EastFive;
using EastFive.Api;
using EastFive.Configuration;
using EastFive.Extensions;
using EastFive.Linq;
using EastFive.Persistence;
using EastFive.Text;
using EastFive.Web.Configuration;

using Microsoft.Azure.Cosmos.Table;
using Newtonsoft.Json;

namespace EastFive.Azure.Persistence.StorageTables
{
    [AzureBlobFileSystemUriStorage]
    public class AzureBlobFileSystemUri
    {
        public string containerName;
        public string path;
        public string storageName;

        public AzureBlobFileSystemUri()
        {

        }

        public AzureBlobFileSystemUri(string containerName, string path, string storageName)
        {
            this.containerName = containerName;
            this.path = path;
            this.storageName = storageName;
        }

        /// <summary>A path in a store; the abfs account segment is the store's account name.</summary>
        public static AzureBlobFileSystemUri FromStorageAccount(EastFive.Azure.Persistence.StorageAccount storageAccount,
            string containerName, string path)
            => new AzureBlobFileSystemUri(containerName, path, storageAccount.AccountName);

        public AzureBlobFileSystemUri AppendToPath(string fileNameOrDirectory)
        {
            var newPath = this.path.EndsWith('/') ?
                $"{this.path}{fileNameOrDirectory}"
                :
                $"{this.path}/{fileNameOrDirectory}";
            return new AzureBlobFileSystemUri(this.containerName, newPath, this.storageName);
        }

        /// <summary>The store this uri's account belongs to: the data lake when the account names
        /// match, otherwise the default table store.</summary>
        public EastFive.Azure.Persistence.StorageAccount StorageAccount
            => EastFive.Azure.Persistence.StorageAccount.DataLake.Configuration(
                dataLake => String.Equals(dataLake.Account.Trim(), this.storageName, StringComparison.OrdinalIgnoreCase)
                    ? EastFive.Azure.Persistence.StorageAccount.DataLake
                    : EastFive.Azure.Persistence.StorageAccount.Tables,
                _ => EastFive.Azure.Persistence.StorageAccount.Tables);

        public string AbfssUri
        {
            get
            {
                return $"abfss://{containerName}@{storageName}.dfs.core.windows.net/{path}";
            }
            set
            {
                var containerNameRegexVariable = "containerName";
                var storageNameRegexVariable = "storageName";
                var pathRegexVariable = "path";
                var fileNameRegexVariable = "filename";
                if (value.TryMatchRegex(
                        $"abfss://(?<{containerNameRegexVariable}>[^@]+)@(?<{storageNameRegexVariable}>[^\\.]+).dfs.core.windows.net/(?<{pathRegexVariable}>[^@]+/)(?<{fileNameRegexVariable}>[^/]+)",
                        out (string, string)[] matches))
                {
                    this.storageName = matches
                        .Where(match => storageNameRegexVariable.Equals(match.Item1, StringComparison.OrdinalIgnoreCase))
                        .First(
                            (match, next) => match.Item2,
                            () => string.Empty);
                    this.containerName = matches
                        .Where(match => containerNameRegexVariable.Equals(match.Item1, StringComparison.OrdinalIgnoreCase))
                        .First(
                            (match, next) => match.Item2,
                            () => string.Empty);
                    var path = matches
                        .Where(match => pathRegexVariable.Equals(match.Item1, StringComparison.OrdinalIgnoreCase))
                        .First(
                            (match, next) => match.Item2,
                            () => string.Empty);
                    var fileName = matches
                        .Where(match => fileNameRegexVariable.Equals(match.Item1, StringComparison.OrdinalIgnoreCase))
                        .First(
                            (match, next) => match.Item2,
                            () => string.Empty);

                    this.path = fileName.HasBlackSpace() ?
                        $"{path}{fileName}"
                        :
                        path;
                    return;
                }


                if (value.TryMatchRegex(
                    $"abfss://(?<{containerNameRegexVariable}>[^@]+)@(?<{storageNameRegexVariable}>[^\\.]+).dfs.core.windows.net/(?<{pathRegexVariable}>.+)",
                    out (string, string)[] matchesNoFilename))
                {
                    this.containerName = matchesNoFilename
                        .Where(match => containerNameRegexVariable.Equals(match.Item1, StringComparison.OrdinalIgnoreCase))
                        .First(
                            (match, next) => match.Item2,
                            () => string.Empty);
                    this.path = matchesNoFilename
                        .Where(match => pathRegexVariable.Equals(match.Item1, StringComparison.OrdinalIgnoreCase))
                        .First(
                            (match, next) => match.Item2,
                            () => string.Empty);
                    this.storageName = matchesNoFilename
                        .Where(match => storageNameRegexVariable.Equals(match.Item1, StringComparison.OrdinalIgnoreCase))
                        .First(
                            (match, next) => match.Item2,
                            () => string.Empty);
                    return;
                }

                this.path = value;
            }
        }

        public bool IsValid()
        {
            return this.containerName.HasBlackSpace() && this.storageName.HasBlackSpace();
        }
    }

    public class AzureBlobFileSystemUriStorageAttribute : System.Attribute, ICastEntityProperty, IBindEntityProperty, ICastJson
    {

        public TResult BindEntityProperty<TResult>(EntityProperty value, Type type,
            Func<object, TResult> onBound,
            Func<TResult> onFailedToBind)
        {
            var abfsUriValue = new AzureBlobFileSystemUri();
            if (value.PropertyType != EdmType.String)
                return onBound(abfsUriValue);

            if (value.StringValue.IsNullOrWhiteSpace())
                return onBound(abfsUriValue);

            abfsUriValue.AbfssUri = value.StringValue;
            return onBound(abfsUriValue);
        }

        public TResult CastEntityProperty<TResult>(object value, Type valueType,
            Func<EntityProperty, TResult> onValue,
            Func<TResult> onNoCast)
        {
            if (value.IsDefaultOrNull())
                return onValue(new EntityProperty(default(string)));

            var abfsUriValue = (AzureBlobFileSystemUri)value;
            return onValue(new EntityProperty(abfsUriValue.AbfssUri));
        }

        public bool CanConvert(Type type, object value, IHttpRequest httpRequest, IApplication application)
        {
            return type.IsAssignableTo(typeof(AzureBlobFileSystemUri));
        }

        public async Task WriteAsync(JsonWriter writer, JsonSerializer serializer, Type type, object value, IHttpRequest httpRequest, IApplication application)
        {
            var abfsUriValue = (AzureBlobFileSystemUri)value;

            await writer.WriteValueAsync(abfsUriValue.AbfssUri);
        }
    }
}

