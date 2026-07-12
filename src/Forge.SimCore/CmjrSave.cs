using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Forge.SimWasm;

namespace Forge.SimCore;

/// <summary>
/// CMJR save envelope per SAVE_FORMAT_WEB.md §3.
/// v1 interim: chunk 0x01 carries UTF-8 SimSnapshotDto JSON until full SoA chunks land.
/// </summary>
public static class CmjrSave
{
    public const uint Magic = 0x434D4A52; // "CMJR"
    public const uint FormatVersion = 1;
    public const ushort SnapshotSchemaVersion = 1;
    public const byte CompressionNone = 0;

    /// <summary>v1 interim JSON snapshot payload (replaces tile SoA in a follow-up).</summary>
    private const byte ChunkSnapshotJson = 0x01;
    private const byte ChunkEnd = 0xFF;

    private const int HeaderBytes = 256;

    public static byte[] Export(SimHost host, string cityName = "City")
    {
        if (!host.IsInitialized) return [];

        var jsonBytes = Encoding.UTF8.GetBytes(host.GetSnapshotJson());

        using var chunkStream = new MemoryStream();
        WriteChunk(chunkStream, ChunkSnapshotJson, jsonBytes);
        WriteChunk(chunkStream, ChunkEnd, ReadOnlySpan<byte>.Empty);

        var payload = chunkStream.ToArray();
        var file = new byte[HeaderBytes + payload.Length];
        WriteHeader(file, host, cityName, payload);
        payload.CopyTo(file, HeaderBytes);
        return file;
    }

    public static bool Load(SimHost host, ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderBytes) return false;

        var magic = BinaryPrimitives.ReadUInt32LittleEndian(data[..4]);
        if (magic != Magic) return false;

        var formatVersion = BinaryPrimitives.ReadUInt32LittleEndian(data[4..8]);
        if (formatVersion > FormatVersion) return false;

        var compression = data[20];
        if (compression != CompressionNone) return false;

        var compressedSize = BinaryPrimitives.ReadUInt32LittleEndian(data[16..20]);
        if (data.Length < HeaderBytes + compressedSize) return false;

        var payload = data.Slice(HeaderBytes, (int)compressedSize);
        var expectedChecksum = BinaryPrimitives.ReadUInt32LittleEndian(data[8..12]);
        if (Crc32(payload) != expectedChecksum) return false;

        var offset = 0;
        byte[]? jsonBytes = null;
        while (offset < payload.Length)
        {
            if (offset + 5 > payload.Length) return false;
            var chunkId = payload[offset];
            var chunkLen = BinaryPrimitives.ReadUInt32LittleEndian(payload[(offset + 1)..]);
            offset += 5;
            if (offset + chunkLen > payload.Length) return false;

            if (chunkId == ChunkSnapshotJson && chunkLen > 0)
                jsonBytes = payload.Slice(offset, (int)chunkLen).ToArray();
            else if (chunkId == ChunkEnd)
                break;

            offset += (int)chunkLen;
        }

        if (jsonBytes is null || jsonBytes.Length == 0) return false;

        var json = Encoding.UTF8.GetString(jsonBytes);
        return host.LoadSnapshotFromJson(json);
    }

    private static void WriteChunk(Stream stream, byte id, ReadOnlySpan<byte> body)
    {
        stream.WriteByte(id);
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(len, (uint)body.Length);
        stream.Write(len);
        if (!body.IsEmpty) stream.Write(body);
    }

    private static void WriteHeader(byte[] dest, SimHost host, string cityName, byte[] payload)
    {
        var checksum = Crc32(payload);
        var contentHash = SHA256.Create().ComputeHash(payload);
        var now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var worldSize = (ushort)(host.WorldSize > 0 ? host.WorldSize : WasmConfig.DefaultWorldSize);
        var state = host.State;

        BinaryPrimitives.WriteUInt32LittleEndian(dest.AsSpan(0, 4), Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(dest.AsSpan(4, 4), FormatVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(dest.AsSpan(8, 4), checksum);
        BinaryPrimitives.WriteUInt32LittleEndian(dest.AsSpan(12, 4), (uint)payload.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(dest.AsSpan(16, 4), (uint)payload.Length);
        dest[20] = CompressionNone;
        BinaryPrimitives.WriteUInt16LittleEndian(dest.AsSpan(21, 2), SnapshotSchemaVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(dest.AsSpan(23, 2), worldSize);
        BinaryPrimitives.WriteUInt16LittleEndian(dest.AsSpan(25, 2), 0);
        BinaryPrimitives.WriteUInt64LittleEndian(dest.AsSpan(27, 8), now);
        BinaryPrimitives.WriteUInt32LittleEndian(dest.AsSpan(35, 4), (uint)Math.Min(host.TickCount, uint.MaxValue));
        BinaryPrimitives.WriteUInt32LittleEndian(dest.AsSpan(39, 4), (uint)Math.Max(state?.Population ?? 0, 0));
        BinaryPrimitives.WriteInt32LittleEndian(dest.AsSpan(43, 4), (int)Math.Clamp(state?.CityFunds ?? 0, int.MinValue, int.MaxValue));
        dest[47] = (byte)Math.Clamp(state?.Era ?? 0, 0, 255);

        var nameBytes = Encoding.UTF8.GetBytes(cityName);
        var nameLen = Math.Min(nameBytes.Length, 63);
        nameBytes.AsSpan(0, nameLen).CopyTo(dest.AsSpan(48, nameLen));

        contentHash.CopyTo(dest.AsSpan(112, 32));
    }

    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (crc >> 1) ^ (0xEDB88320u & (uint)-(int)(crc & 1));
        }

        return ~crc;
    }
}
