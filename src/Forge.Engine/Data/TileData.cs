using System.Runtime.CompilerServices;

namespace Forge.Engine.Data;

/// <summary>
/// Structure-of-Arrays (SoA) tile storage for up to 4096x4096 tiles.
/// Each property is a flat array indexed by (y * Size + x).
/// SoA layout enables SIMD-friendly iteration and cache-efficient access
/// when processing a single property across many tiles.
///
/// Design choices:
/// - byte for enums/flags (terrain type, zone type, connection booleans)
/// - float for continuous simulation values (land value, pollution, etc.)
///   because simulation math (decay, diffusion, weighted averages) needs
///   sub-integer precision and avoids constant byte-float round-tripping.
/// - ushort for IDs (building, resource) -- 65535 max per map.
/// - Span accessors for SIMD-friendly bulk processing.
/// </summary>
public sealed class TileData
{
    /// <summary>World size in tiles (one axis of the square grid).</summary>
    public int Size { get; }

    /// <summary>Total number of tiles (Size * Size).</summary>
    public int Count { get; }

    // =========================================================================
    // Terrain layer (immutable after world generation, except terraforming)
    // =========================================================================

    /// <summary>Terrain type: 0=grass, 1=dirt, 2=sand, 3=water, 4=rock, 5=forest, 6=marsh, 7=snow.</summary>
    public byte[] TerrainType;

    /// <summary>Terrain elevation in half-meters (0-65535 = 0m to 32767.5m).</summary>
    public ushort[] Elevation;

    // =========================================================================
    // Zone layer (set by player, consumed by growth simulation)
    // =========================================================================

    /// <summary>Zone type: 0=none, 1=residential_low, 2=residential_high, 3=commercial, 4=industrial, 5=office, 6=mixed_use, 7=agricultural, 8=park.</summary>
    public byte[] ZoneType;

    /// <summary>Zone density (0-3): empty, low, medium, high.</summary>
    public byte[] ZoneDensity;

    // =========================================================================
    // Infrastructure layer (binary connectivity + road details)
    // =========================================================================

    /// <summary>Road bitfield: bits 0-3 = N/E/S/W connections, bits 4-5 = road level (dirt/paved/highway), bits 6-7 = structure (none/bridge/tunnel/ramp).</summary>
    public byte[] RoadFlags;

    /// <summary>Power connection: 0=none, 1=has_power.</summary>
    public byte[] PowerGrid;

    /// <summary>Water connection: 0=none, 1=has_water.</summary>
    public byte[] WaterGrid;

    /// <summary>Sewage connection: 0=none, 1=connected.</summary>
    public byte[] SewageConnection;

    /// <summary>Internet/telecom connection: 0=none, 1=copper, 2=fiber, 3=5G.</summary>
    public byte[] InternetConnection;

    // =========================================================================
    // Building layer (references into BuildingData pool)
    // =========================================================================

    /// <summary>Building ID occupying this tile (0 = empty). References BuildingData pool.</summary>
    public ushort[] BuildingId;

    // =========================================================================
    // Ownership layer (multiplayer)
    // =========================================================================

    /// <summary>Player ID that owns this tile. 0 = unowned/public.</summary>
    public byte[] OwnerPlayerId;

    // =========================================================================
    // Resource layer (underground deposits)
    // =========================================================================

    /// <summary>Resource deposit: high byte = type (0=none, 1=oil, 2=ore, 3=coal, 4=gas, 5=fertile_soil, 6=groundwater), low byte = remaining quantity (0-255).</summary>
    public ushort[] ResourceDeposit;

    // =========================================================================
    // Simulation values -- continuous floats for precision
    // All values are normalized 0.0-1.0 unless otherwise noted.
    // Updated by simulation systems each tick.
    // =========================================================================

    /// <summary>Land value (0.0 = worthless, 1.0 = maximum).</summary>
    public float[] LandValue;

    /// <summary>Air pollution level (0.0 = pristine, 1.0 = lethal).</summary>
    public float[] Pollution;

    /// <summary>Crime level (0.0 = safe, 1.0 = extreme danger).</summary>
    public float[] Crime;

    /// <summary>Fire risk (0.0 = fireproof, 1.0 = imminent).</summary>
    public float[] FireRisk;

    /// <summary>Traffic density (0.0 = empty road, 1.0 = total gridlock).</summary>
    public float[] Traffic;

    /// <summary>Desirability score (-1.0 = repulsive, 0.0 = neutral, +1.0 = highly desirable).</summary>
    public float[] Desirability;

    /// <summary>Noise level from roads, industry, airports (0.0 = silent, 1.0 = unbearable).</summary>
    public float[] Noise;

    /// <summary>Water system pressure at this tile (0.0 = no pressure, 1.0 = full). Used by water simulation for pipe networks.</summary>
    public float[] WaterPressure;

    /// <summary>Local temperature offset from city baseline in Celsius (-20.0 to +20.0). Heat island effect, industrial waste heat, park cooling.</summary>
    public float[] Temperature;

    // =========================================================================
    // Service coverage -- packed bitfield per tile
    // Layout: fire(2bit) + police(2bit) + health(2bit) + education(2bit)
    // Each 2-bit value: 0=none, 1=low, 2=medium, 3=full coverage
    // =========================================================================

    /// <summary>
    /// Packed service coverage bitfield.
    /// Bits 0-1: fire, 2-3: police, 4-5: health, 6-7: education.
    /// Each pair: 0=none, 1=low, 2=medium, 3=full.
    /// </summary>
    public byte[] ServiceCoverage;

    public TileData(int size)
    {
        if (size < 1 || size > 4096)
            throw new ArgumentOutOfRangeException(nameof(size), "World size must be 1-4096.");

        Size = size;
        Count = size * size;

        // Terrain
        TerrainType = new byte[Count];
        Elevation = new ushort[Count];

        // Zones
        ZoneType = new byte[Count];
        ZoneDensity = new byte[Count];

        // Infrastructure
        RoadFlags = new byte[Count];
        PowerGrid = new byte[Count];
        WaterGrid = new byte[Count];
        SewageConnection = new byte[Count];
        InternetConnection = new byte[Count];

        // Buildings
        BuildingId = new ushort[Count];

        // Ownership
        OwnerPlayerId = new byte[Count];

        // Resources
        ResourceDeposit = new ushort[Count];

        // Simulation (float)
        LandValue = new float[Count];
        Pollution = new float[Count];
        Crime = new float[Count];
        FireRisk = new float[Count];
        Traffic = new float[Count];
        Desirability = new float[Count];
        Noise = new float[Count];
        WaterPressure = new float[Count];
        Temperature = new float[Count];

        // Service coverage
        ServiceCoverage = new byte[Count];

        // Defaults
        Array.Fill(TerrainType, (byte)0); // grass
        Array.Fill(Elevation, (ushort)20); // ~10m above sea level
    }

    // =========================================================================
    // Coordinate helpers
    // =========================================================================

    /// <summary>Convert (x, y) to flat index. No bounds checking for performance.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Index(int x, int y) => y * Size + x;

    /// <summary>Convert flat index to (x, y).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (int x, int y) FromIndex(int index) => (index % Size, index / Size);

    /// <summary>Check if coordinates are within bounds.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool InBounds(int x, int y) => (uint)x < (uint)Size && (uint)y < (uint)Size;

    // =========================================================================
    // Safe accessors
    // =========================================================================

    /// <summary>Get terrain type with bounds checking.</summary>
    public byte GetTerrainSafe(int x, int y) =>
        InBounds(x, y) ? TerrainType[Index(x, y)] : (byte)0;

    /// <summary>Check if a tile is buildable (no water, no existing building, not rock).</summary>
    public bool IsBuildable(int x, int y)
    {
        if (!InBounds(x, y)) return false;
        int idx = Index(x, y);
        return TerrainType[idx] != 3  // Not water
            && TerrainType[idx] != 4  // Not rock
            && BuildingId[idx] == 0;  // No existing building
    }

    /// <summary>Check if a rectangular area is fully buildable.</summary>
    public bool IsAreaBuildable(int x, int y, int width, int height)
    {
        // Early bounds check on the entire rectangle
        if (x < 0 || y < 0 || x + width > Size || y + height > Size)
            return false;

        for (int dy = 0; dy < height; dy++)
        {
            int rowStart = (y + dy) * Size + x;
            for (int dx = 0; dx < width; dx++)
            {
                int idx = rowStart + dx;
                if (TerrainType[idx] == 3 || TerrainType[idx] == 4 || BuildingId[idx] != 0)
                    return false;
            }
        }
        return true;
    }

    // =========================================================================
    // Service coverage helpers
    // =========================================================================

    /// <summary>Get fire coverage level (0-3) for a tile.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetFireCoverage(int index) => ServiceCoverage[index] & 0x03;

    /// <summary>Get police coverage level (0-3) for a tile.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetPoliceCoverage(int index) => (ServiceCoverage[index] >> 2) & 0x03;

    /// <summary>Get health coverage level (0-3) for a tile.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetHealthCoverage(int index) => (ServiceCoverage[index] >> 4) & 0x03;

    /// <summary>Get education coverage level (0-3) for a tile.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetEducationCoverage(int index) => (ServiceCoverage[index] >> 6) & 0x03;

    /// <summary>Set fire coverage level (0-3) for a tile.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetFireCoverage(int index, int level)
    {
        ServiceCoverage[index] = (byte)((ServiceCoverage[index] & ~0x03) | (level & 0x03));
    }

    /// <summary>Set police coverage level (0-3) for a tile.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetPoliceCoverage(int index, int level)
    {
        ServiceCoverage[index] = (byte)((ServiceCoverage[index] & ~0x0C) | ((level & 0x03) << 2));
    }

    /// <summary>Set health coverage level (0-3) for a tile.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetHealthCoverage(int index, int level)
    {
        ServiceCoverage[index] = (byte)((ServiceCoverage[index] & ~0x30) | ((level & 0x03) << 4));
    }

    /// <summary>Set education coverage level (0-3) for a tile.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetEducationCoverage(int index, int level)
    {
        ServiceCoverage[index] = (byte)((ServiceCoverage[index] & ~0xC0) | ((level & 0x03) << 6));
    }

    // =========================================================================
    // Resource deposit helpers
    // =========================================================================

    /// <summary>Get the resource type at a tile (high byte of ResourceDeposit). 0=none.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetResourceType(int index) => ResourceDeposit[index] >> 8;

    /// <summary>Get the remaining resource quantity at a tile (low byte). 0-255.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetResourceQuantity(int index) => ResourceDeposit[index] & 0xFF;

    /// <summary>Set a resource deposit at a tile.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetResourceDeposit(int index, int type, int quantity)
    {
        ResourceDeposit[index] = (ushort)(((type & 0xFF) << 8) | (quantity & 0xFF));
    }

    // =========================================================================
    // Bulk operations (zone painting, terraforming)
    // =========================================================================

    /// <summary>
    /// Set zone type for a rectangular area. Skips tiles that are unbuildable.
    /// Returns the number of tiles actually zoned.
    /// </summary>
    public int SetZoneRect(int x, int y, int width, int height, byte zoneType, byte density)
    {
        int x1 = System.Math.Max(0, x);
        int y1 = System.Math.Max(0, y);
        int x2 = System.Math.Min(Size, x + width);
        int y2 = System.Math.Min(Size, y + height);

        int count = 0;
        for (int row = y1; row < y2; row++)
        {
            int rowStart = row * Size;
            for (int col = x1; col < x2; col++)
            {
                int idx = rowStart + col;
                // Only zone buildable tiles (not water/rock, no existing building)
                if (TerrainType[idx] != 3 && TerrainType[idx] != 4 && BuildingId[idx] == 0)
                {
                    ZoneType[idx] = zoneType;
                    ZoneDensity[idx] = density;
                    count++;
                }
            }
        }
        return count;
    }

    /// <summary>
    /// Clear zones in a rectangular area.
    /// Returns the number of tiles cleared.
    /// </summary>
    public int ClearZoneRect(int x, int y, int width, int height)
    {
        return SetZoneRect(x, y, width, height, 0, 0);
    }

    /// <summary>
    /// Fill a rectangular area with a terrain type (for terraforming tools).
    /// </summary>
    public void SetTerrainRect(int x, int y, int width, int height, byte terrainType)
    {
        int x1 = System.Math.Max(0, x);
        int y1 = System.Math.Max(0, y);
        int x2 = System.Math.Min(Size, x + width);
        int y2 = System.Math.Min(Size, y + height);

        for (int row = y1; row < y2; row++)
        {
            int rowStart = row * Size;
            for (int col = x1; col < x2; col++)
            {
                TerrainType[rowStart + col] = terrainType;
            }
        }
    }

    /// <summary>
    /// Set owner for a rectangular area (multiplayer land claiming).
    /// </summary>
    public void SetOwnerRect(int x, int y, int width, int height, byte playerId)
    {
        int x1 = System.Math.Max(0, x);
        int y1 = System.Math.Max(0, y);
        int x2 = System.Math.Min(Size, x + width);
        int y2 = System.Math.Min(Size, y + height);

        for (int row = y1; row < y2; row++)
        {
            int rowStart = row * Size;
            for (int col = x1; col < x2; col++)
            {
                OwnerPlayerId[rowStart + col] = playerId;
            }
        }
    }

    // =========================================================================
    // Span-based access for SIMD-friendly iteration
    // =========================================================================

    /// <summary>Get a Span over the entire LandValue array for vectorized processing.</summary>
    public Span<float> LandValueSpan => LandValue.AsSpan();

    /// <summary>Get a Span over the entire Pollution array.</summary>
    public Span<float> PollutionSpan => Pollution.AsSpan();

    /// <summary>Get a Span over the entire Crime array.</summary>
    public Span<float> CrimeSpan => Crime.AsSpan();

    /// <summary>Get a Span over the entire Traffic array.</summary>
    public Span<float> TrafficSpan => Traffic.AsSpan();

    /// <summary>Get a Span over the entire Desirability array.</summary>
    public Span<float> DesirabilitySpan => Desirability.AsSpan();

    /// <summary>Get a Span over the entire Noise array.</summary>
    public Span<float> NoiseSpan => Noise.AsSpan();

    /// <summary>Get a Span over the entire WaterPressure array.</summary>
    public Span<float> WaterPressureSpan => WaterPressure.AsSpan();

    /// <summary>Get a Span over the entire Temperature array.</summary>
    public Span<float> TemperatureSpan => Temperature.AsSpan();

    /// <summary>Get a Span over the entire FireRisk array.</summary>
    public Span<float> FireRiskSpan => FireRisk.AsSpan();

    /// <summary>Get a Span for a single row of a float layer. Useful for row-by-row SIMD processing.</summary>
    public Span<float> GetRowSpan(float[] layer, int row)
    {
        return layer.AsSpan(row * Size, Size);
    }

    /// <summary>Get a Span for a single row of a byte layer.</summary>
    public Span<byte> GetRowSpan(byte[] layer, int row)
    {
        return layer.AsSpan(row * Size, Size);
    }

    /// <summary>Get a Span for a single row of a ushort layer.</summary>
    public Span<ushort> GetRowSpan(ushort[] layer, int row)
    {
        return layer.AsSpan(row * Size, Size);
    }

    // =========================================================================
    // Memory diagnostics
    // =========================================================================

    /// <summary>
    /// Compute total memory consumption of all tile arrays in bytes.
    /// Useful for tracking memory budget against target (e.g. 256 MB for tile data).
    /// </summary>
    public long ComputeMemoryBytes()
    {
        long bytes = 0;

        // byte arrays: 1 byte each
        bytes += (long)Count * 1 * 8; // TerrainType, ZoneType, ZoneDensity, RoadFlags, PowerGrid, WaterGrid, SewageConnection, InternetConnection
        bytes += (long)Count * 1 * 2; // OwnerPlayerId, ServiceCoverage

        // ushort arrays: 2 bytes each
        bytes += (long)Count * 2 * 3; // Elevation, BuildingId, ResourceDeposit

        // float arrays: 4 bytes each
        bytes += (long)Count * 4 * 9; // LandValue, Pollution, Crime, FireRisk, Traffic, Desirability, Noise, WaterPressure, Temperature

        return bytes;
    }

    /// <summary>
    /// Human-readable memory usage string. Example: "TileData: 512x512 = 262144 tiles, 12.50 MB"
    /// </summary>
    public string MemoryReport()
    {
        long bytes = ComputeMemoryBytes();
        double mb = bytes / (1024.0 * 1024.0);
        return $"TileData: {Size}x{Size} = {Count:N0} tiles, {mb:F2} MB";
    }
}
