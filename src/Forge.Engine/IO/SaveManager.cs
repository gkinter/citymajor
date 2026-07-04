using System.Text.Json;
using K4os.Compression.LZ4;
using Forge.Engine.Simulation;

namespace Forge.Engine.IO;

/// <summary>
/// Async save/load with LZ4 compression. Saves are versioned binary blobs
/// with a JSON metadata header for compatibility checking.
/// </summary>
public sealed class SaveManager
{
    private const uint MagicNumber = 0x464F5247; // "FORG"
    private const ushort CurrentVersion = 2;

    public record SaveMetadata(
        string CityName,
        int Population,
        long Funds,
        string Date,
        DateTime SavedAt,
        ushort Version
    );

    /// <summary>
    /// Save the world state to a file with LZ4 compression.
    /// Runs asynchronously to avoid blocking the game loop.
    /// </summary>
    public async Task SaveAsync(string filePath, WorldState state, string cityName)
    {
        var metadata = new SaveMetadata(
            CityName: cityName,
            Population: state.Population,
            Funds: state.CityFunds,
            Date: state.DateString,
            SavedAt: DateTime.UtcNow,
            Version: CurrentVersion
        );

        await Task.Run(() =>
        {
            using var stream = File.Create(filePath);
            using var writer = new BinaryWriter(stream);

            // Header
            writer.Write(MagicNumber);
            writer.Write(CurrentVersion);

            // Metadata (JSON, length-prefixed)
            string metaJson = JsonSerializer.Serialize(metadata);
            byte[] metaBytes = System.Text.Encoding.UTF8.GetBytes(metaJson);
            writer.Write(metaBytes.Length);
            writer.Write(metaBytes);

            // World data (compressed)
            byte[] rawData = SerializeWorldState(state);
            byte[] compressed = new byte[LZ4Codec.MaximumOutputSize(rawData.Length)];
            int compressedSize = LZ4Codec.Encode(rawData, compressed, LZ4Level.L03_HC);

            writer.Write(rawData.Length);      // Uncompressed size
            writer.Write(compressedSize);       // Compressed size
            writer.Write(compressed, 0, compressedSize);
        });

        Console.WriteLine($"[SaveManager] Saved to {filePath}");
    }

    /// <summary>
    /// Load a world state from a save file.
    /// </summary>
    public async Task<(WorldState state, SaveMetadata metadata)?> LoadAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"[SaveManager] File not found: {filePath}");
            return null;
        }

        return await Task.Run(() =>
        {
            using var stream = File.OpenRead(filePath);
            using var reader = new BinaryReader(stream);

            // Verify magic number
            uint magic = reader.ReadUInt32();
            if (magic != MagicNumber)
                throw new InvalidDataException("Not a valid Forge save file.");

            ushort version = reader.ReadUInt16();
            if (version > CurrentVersion)
                throw new InvalidDataException($"Save version {version} is newer than supported {CurrentVersion}.");

            // Read metadata
            int metaLen = reader.ReadInt32();
            byte[] metaBytes = reader.ReadBytes(metaLen);
            string metaJson = System.Text.Encoding.UTF8.GetString(metaBytes);
            var metadata = JsonSerializer.Deserialize<SaveMetadata>(metaJson)
                ?? throw new InvalidDataException("Failed to parse save metadata.");

            // Read compressed world data
            int uncompressedSize = reader.ReadInt32();
            int compressedSize = reader.ReadInt32();
            byte[] compressed = reader.ReadBytes(compressedSize);
            byte[] rawData = new byte[uncompressedSize];
            LZ4Codec.Decode(compressed, 0, compressedSize, rawData, 0, uncompressedSize);

            var state = DeserializeWorldState(rawData);
            Console.WriteLine($"[SaveManager] Loaded from {filePath}: {metadata.CityName}");
            return (state, metadata);
        });
    }

    /// <summary>Read save metadata without loading the full state.</summary>
    public SaveMetadata? ReadMetadata(string filePath)
    {
        if (!File.Exists(filePath)) return null;

        using var stream = File.OpenRead(filePath);
        using var reader = new BinaryReader(stream);

        uint magic = reader.ReadUInt32();
        if (magic != MagicNumber) return null;

        reader.ReadUInt16(); // version

        int metaLen = reader.ReadInt32();
        byte[] metaBytes = reader.ReadBytes(metaLen);
        string metaJson = System.Text.Encoding.UTF8.GetString(metaBytes);
        return JsonSerializer.Deserialize<SaveMetadata>(metaJson);
    }

    private static byte[] SerializeWorldState(WorldState state)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);

        // World size
        w.Write(state.Tiles.Size);

        // Calendar
        w.Write(state.TickCount); // long (v2)
        w.Write(state.Day);
        w.Write(state.Month);
        w.Write(state.Year);
        w.Write(state.CityFunds);
        w.Write(state.Population);
        w.Write(state.Happiness);

        // Tile data (write each SoA array)
        int count = state.Tiles.Count;
        w.Write(state.Tiles.TerrainType, 0, count);
        WriteUShortArray(w, state.Tiles.Elevation, count);  // ushort[] (v2)
        w.Write(state.Tiles.ZoneType, 0, count);
        w.Write(state.Tiles.ZoneDensity, 0, count);
        w.Write(state.Tiles.RoadFlags, 0, count);
        w.Write(state.Tiles.PowerGrid, 0, count);
        w.Write(state.Tiles.WaterGrid, 0, count);
        w.Write(state.Tiles.SewageConnection, 0, count);
        w.Write(state.Tiles.InternetConnection, 0, count);
        w.Write(state.Tiles.OwnerPlayerId, 0, count);
        w.Write(state.Tiles.ServiceCoverage, 0, count);
        WriteUShortArray(w, state.Tiles.BuildingId, count);
        WriteUShortArray(w, state.Tiles.ResourceDeposit, count);
        WriteFloatArray(w, state.Tiles.LandValue, count);
        WriteFloatArray(w, state.Tiles.Pollution, count);
        WriteFloatArray(w, state.Tiles.Crime, count);
        WriteFloatArray(w, state.Tiles.FireRisk, count);
        WriteFloatArray(w, state.Tiles.Traffic, count);
        WriteFloatArray(w, state.Tiles.Desirability, count);
        WriteFloatArray(w, state.Tiles.Noise, count);
        WriteFloatArray(w, state.Tiles.WaterPressure, count);
        WriteFloatArray(w, state.Tiles.Temperature, count);

        return ms.ToArray();
    }

    private static WorldState DeserializeWorldState(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var r = new BinaryReader(ms);

        int worldSize = r.ReadInt32();
        var state = new WorldState(worldSize);

        state.TickCount = r.ReadInt64(); // long (v2)
        state.Day = r.ReadInt32();
        state.Month = r.ReadInt32();
        state.Year = r.ReadInt32();
        state.CityFunds = r.ReadInt64();
        state.Population = r.ReadInt32();
        state.Happiness = r.ReadSingle();

        int count = worldSize * worldSize;
        r.Read(state.Tiles.TerrainType, 0, count);
        ReadUShortArray(r, state.Tiles.Elevation, count);   // ushort[] (v2)
        r.Read(state.Tiles.ZoneType, 0, count);
        r.Read(state.Tiles.ZoneDensity, 0, count);
        r.Read(state.Tiles.RoadFlags, 0, count);
        r.Read(state.Tiles.PowerGrid, 0, count);
        r.Read(state.Tiles.WaterGrid, 0, count);
        r.Read(state.Tiles.SewageConnection, 0, count);
        r.Read(state.Tiles.InternetConnection, 0, count);
        r.Read(state.Tiles.OwnerPlayerId, 0, count);
        r.Read(state.Tiles.ServiceCoverage, 0, count);
        ReadUShortArray(r, state.Tiles.BuildingId, count);
        ReadUShortArray(r, state.Tiles.ResourceDeposit, count);
        ReadFloatArray(r, state.Tiles.LandValue, count);
        ReadFloatArray(r, state.Tiles.Pollution, count);
        ReadFloatArray(r, state.Tiles.Crime, count);
        ReadFloatArray(r, state.Tiles.FireRisk, count);
        ReadFloatArray(r, state.Tiles.Traffic, count);
        ReadFloatArray(r, state.Tiles.Desirability, count);
        ReadFloatArray(r, state.Tiles.Noise, count);
        ReadFloatArray(r, state.Tiles.WaterPressure, count);
        ReadFloatArray(r, state.Tiles.Temperature, count);

        return state;
    }

    private static void WriteUShortArray(BinaryWriter w, ushort[] array, int count)
    {
        var bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(array.AsSpan(0, count));
        w.Write(bytes);
    }

    private static void ReadUShortArray(BinaryReader r, ushort[] array, int count)
    {
        var bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(array.AsSpan(0, count));
        r.Read(bytes);
    }

    private static void WriteFloatArray(BinaryWriter w, float[] array, int count)
    {
        var bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(array.AsSpan(0, count));
        w.Write(bytes);
    }

    private static void ReadFloatArray(BinaryReader r, float[] array, int count)
    {
        var bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(array.AsSpan(0, count));
        r.Read(bytes);
    }
}
