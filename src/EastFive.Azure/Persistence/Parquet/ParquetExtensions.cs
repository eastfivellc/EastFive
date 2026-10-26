using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Azure.Storage.Blobs.Models;

using EastFive;
using EastFive.Extensions;
using EastFive.Azure.Persistence.AzureStorageTables;
using EastFive.Azure.Persistence.StorageTables;
using EastFive.Linq;
using EastFive.Linq.Async;
using EastFive.Serialization;
using EastFive.Serialization.Parquet;

namespace EastFive.Azure.Persistence
{
	public static class ParquetExtensions
	{
        /// <summary>
        /// Replaces <paramref name="exportLocation"/> with the resources written as parquet through
        /// their <see cref="IMapParquet"/> mapping, <paramref name="rowsPerfile"/> rows per file.
        /// </summary>
        public static IEnumerableAsync<BlobContentInfo> WriteToBlobAsParquet<TEntity>(this IEnumerable<TEntity> data,
                AzureBlobFileSystemUri exportLocation, int rowsPerfile = 100000)
            => WriteToBlobAsParquetInternal(data, exportLocation, rowsPerfile: rowsPerfile).FoldTask();

        private static async Task<IEnumerableAsync<BlobContentInfo>> WriteToBlobAsParquetInternal<TEntity>(
            this IEnumerable<TEntity> data, AzureBlobFileSystemUri exportLocation, int rowsPerfile)
        {
            bool deleted = await exportLocation.BlobDeleteIfExistsAsync(
                () => true);

            return data
                .Segment(rowsPerfile)
                .Select(
                    (segment, index) =>
                    {
                        var rows = segment.ToArray();
                        var fileName = $"{index:00000}.parquet";
                        return exportLocation
                            .AppendToPath(fileName)
                            .BlobCreateOrUpdateAsync(
                                writeStreamAsync: async (stream) =>
                                {
                                    await rows.WriteToParquetStreamAsync(stream);
                                    await stream.FlushAsync();
                                },
                                (blobContentInfo) => blobContentInfo,
                                    contentTypeString: "application/vnd.apache.parquet",
                                    fileName: fileName);
                    })
                .AsyncEnumerable(readAhead:3);
        }

        public static global::Parquet.Schema.ParquetSchema GetParquetSchema<TEntity>() =>
            ParquetSerializationExtensions.GetParquetSchema<TEntity>();
    }
}
