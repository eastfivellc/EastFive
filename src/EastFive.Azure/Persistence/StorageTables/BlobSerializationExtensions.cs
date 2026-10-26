using System;
using System.IO;
using System.Threading.Tasks;

using EastFive.Extensions;
using EastFive.Serialization.Parquet;

namespace EastFive.Azure.Persistence.AzureStorageTables
{
	public static class BlobSerializationExtensions
	{
        /// <summary>
        /// Loads a parquet file from the data lake and parses it through
        /// <see cref="ParquetSerializationExtensions.ParseParquetAsync"/>: rows that parsed, and the
        /// cells that did not, arrive together in <paramref name="onLoaded"/>.
        /// </summary>
        /// <param name="onUnreadable">The blob could not be loaded, or is not a parquet file this
        /// resource's mapping can read.</param>
        public static async Task<TResult> ReadParquetDataFromDataLakeAsync<TResource, TResult>(this string path, string containerName,
            Func<TResource[], UnparsedRow[], TResult> onLoaded,
            Func<TResult> onNotFound,
            Func<string, TResult> onUnreadable)
		{
            return await await path.BlobLoadBytesAsync(containerName,
                (parquetBytes, properties) => new MemoryStream(parquetBytes)
                    .ParseParquetAsync<TResource, TResult>(onLoaded, onUnreadable),
                onNotFound: () => onNotFound().AsTask(),
                onFailure: (code, why) => onUnreadable($"{code}: {why}").AsTask(),
                storageAccount: EastFive.Azure.Persistence.StorageAccount.DataLake);
        }
	}
}
