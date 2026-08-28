using System.Text;

using Xunit;

using Microsoft.Azure.Cosmos.Table;

using EastFive.Persistence;

namespace EastFive.Azure.Tests;

/// <summary>
/// [StorageOverflow] splits oversized string/binary values into `{prop}_overflow_{index}`
/// sibling properties and reassembles them on read. Reassembly must order chunks
/// NUMERICALLY: with ten or more chunks, lexicographic ordering interleaves them
/// (_0, _1, _10, _11, …, _2), silently corrupting the round-tripped value.
/// </summary>
public class StorageOverflowChunkOrderTests
{
    private const int ChunkChars = 0x8000;

    private static string RoundTripString(string value)
    {
        var written = StorageOverflowAttribute
            .ComputeOverflowValues("payload", new EntityProperty(value))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var parsed = StorageOverflowAttribute.ParseOverflowValues(
            "payload", written["payload"], written);
        return parsed.Value.StringValue;
    }

    private static byte[] RoundTripBinary(byte[] value)
    {
        var written = StorageOverflowAttribute
            .ComputeOverflowValues("payload", new EntityProperty(value))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var parsed = StorageOverflowAttribute.ParseOverflowValues(
            "payload", written["payload"], written);
        return parsed.Value.BinaryValue;
    }

    /// <summary>Position-stamped so any reordering changes the content, not just the length.</summary>
    private static string BuildMarkedString(int chunkCount)
    {
        var sb = new StringBuilder(chunkCount * ChunkChars);
        for (var chunk = 0; chunk < chunkCount; chunk++)
        {
            var marker = $"[chunk:{chunk:D3}]";
            sb.Append(marker);
            sb.Append('x', ChunkChars - marker.Length);
        }
        return sb.ToString();
    }

    [Fact]
    public void StringOverflow_TwoChunks_RoundTrips()
    {
        var value = BuildMarkedString(2);
        Assert.Equal(value, RoundTripString(value));
    }

    [Fact]
    public void StringOverflow_ElevenChunks_RoundTrips()
    {
        // 11 chunks: lexicographic ordering yields _0,_1,_10,_2,… — content scrambles.
        var value = BuildMarkedString(11);
        Assert.Equal(value, RoundTripString(value));
    }

    [Fact]
    public void BinaryOverflow_ElevenChunks_RoundTrips()
    {
        var value = new byte[11 * 0x10000];
        for (var i = 0; i < value.Length; i++)
            value[i] = (byte)(i / 0x10000);
        Assert.Equal(value, RoundTripBinary(value));
    }
}
