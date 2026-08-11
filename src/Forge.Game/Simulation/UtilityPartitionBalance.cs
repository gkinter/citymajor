using Forge.Engine.Simulation;

namespace Forge.Game.Simulation;

/// <summary>
/// L0 utility balance: per-partition power/water supply vs demand (O(partitions), not O(tiles)).
/// Full tile BFS grids remain on the daily/monthly tick in <see cref="ServiceSystem"/>.
/// </summary>
public sealed class UtilityPartitionBalance
{
    private const float PowerPlantMwPerLevel = 100f;
    private const float WaterPumpMlPerLevel = 80f;
    private const float RollingAlpha = 0.12f;

    private const uint ServicePowerPlant = 1u << 7;
    private const uint ServiceWaterPump = 1u << 8;

    private readonly int _worldSize;
    private readonly int _partitionSize;
    private readonly int _partitionsPerAxis;
    private readonly int _partitionCount;

    private readonly float[] _powerSupply;
    private readonly float[] _powerDemand;
    private readonly float[] _waterSupply;
    private readonly float[] _waterDemand;

    public int PartitionSize => _partitionSize;
    public int PartitionCount => _partitionCount;

    public float BlackoutFraction { get; private set; }
    public float WaterShortageFraction { get; private set; }

    /// <summary>
    /// Seed rolling L0 fractions after save/load so the next <see cref="Tick"/>
    /// does not lerp from zero and overwrite restored WorldState values.
    /// </summary>
    public void RestoreRollingFractions(float blackoutFraction, float waterShortageFraction)
    {
        BlackoutFraction = Math.Clamp(blackoutFraction, 0f, 1f);
        WaterShortageFraction = Math.Clamp(waterShortageFraction, 0f, 1f);
    }

    public UtilityPartitionBalance(int worldSize, int partitionSize = 32)
    {
        if (worldSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(worldSize));
        if (partitionSize <= 0 || worldSize % partitionSize != 0)
            throw new ArgumentException(
                $"partitionSize ({partitionSize}) must divide worldSize ({worldSize}) evenly.");

        _worldSize = worldSize;
        _partitionSize = partitionSize;
        _partitionsPerAxis = worldSize / partitionSize;
        _partitionCount = _partitionsPerAxis * _partitionsPerAxis;

        _powerSupply = new float[_partitionCount];
        _powerDemand = new float[_partitionCount];
        _waterSupply = new float[_partitionCount];
        _waterDemand = new float[_partitionCount];
    }

    public static int ResolvePartitionSize(int worldSize, int? overrideSize = null)
    {
        if (overrideSize is int size && size > 0 && worldSize % size == 0)
            return size;

        // Prefer 32×32 partitions; fall back to Wasm chunk size or whole map.
        if (worldSize % 32 == 0) return 32;
        if (worldSize % 64 == 0) return 64;
        return worldSize;
    }

    public void Tick(WorldState state, float dt)
    {
        _ = dt;
        ClearAccumulators();
        AccumulateFromWorld(state);

        int poweredPartitions = 0;
        int wateredPartitions = 0;
        int blackoutPartitions = 0;
        int shortagePartitions = 0;

        for (int i = 0; i < _partitionCount; i++)
        {
            float powerBalance = _powerSupply[i] - _powerDemand[i];
            float waterBalance = _waterSupply[i] - _waterDemand[i];

            if (powerBalance >= 0f) poweredPartitions++;
            else blackoutPartitions++;

            if (waterBalance >= 0f) wateredPartitions++;
            else shortagePartitions++;
        }

        float powerCoverage = _partitionCount > 0 ? poweredPartitions / (float)_partitionCount : 1f;
        float waterCoverage = _partitionCount > 0 ? wateredPartitions / (float)_partitionCount : 1f;
        float instantBlackout = _partitionCount > 0 ? blackoutPartitions / (float)_partitionCount : 0f;
        float instantShortage = _partitionCount > 0 ? shortagePartitions / (float)_partitionCount : 0f;

        BlackoutFraction = Lerp(BlackoutFraction, instantBlackout, RollingAlpha);
        WaterShortageFraction = Lerp(WaterShortageFraction, instantShortage, RollingAlpha);

        state.PowerCoverageFraction = powerCoverage;
        state.WaterCoverageFraction = waterCoverage;
        state.BlackoutFraction = BlackoutFraction;
        state.WaterShortageFraction = WaterShortageFraction;
        state.UtilityStressIndex = Math.Clamp(
            (1f - powerCoverage) * 0.5f + (1f - waterCoverage) * 0.5f,
            0f,
            1f);
    }

    public float GetPowerBalance(int partitionX, int partitionY)
    {
        int idx = PartitionIndex(partitionX, partitionY);
        return _powerSupply[idx] - _powerDemand[idx];
    }

    public float GetWaterBalance(int partitionX, int partitionY)
    {
        int idx = PartitionIndex(partitionX, partitionY);
        return _waterSupply[idx] - _waterDemand[idx];
    }

    private void ClearAccumulators()
    {
        Array.Clear(_powerSupply, 0, _powerSupply.Length);
        Array.Clear(_powerDemand, 0, _powerDemand.Length);
        Array.Clear(_waterSupply, 0, _waterSupply.Length);
        Array.Clear(_waterDemand, 0, _waterDemand.Length);
    }

    private void AccumulateFromWorld(WorldState state)
    {
        AccumulateZonedTileDemand(state);
        AccumulateBuildingSupplyAndDemand(state);
    }

    private void AccumulateZonedTileDemand(WorldState state)
    {
        var tiles = state.Tiles;
        for (int y = 0; y < _worldSize; y++)
        {
            for (int x = 0; x < _worldSize; x++)
            {
                int idx = tiles.Index(x, y);
                byte zone = tiles.ZoneType[idx];
                if (zone == 0) continue;

                int pIdx = PartitionIndexFromTile(x, y);
                GetZoneDemand(zone, out float power, out float water);
                _powerDemand[pIdx] += power;
                _waterDemand[pIdx] += water;
            }
        }
    }

    private void AccumulateBuildingSupplyAndDemand(WorldState state)
    {
        var buildings = state.Buildings;
        var tiles = state.Tiles;

        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if (buildings.State[i] != 1) continue;

            int bx = buildings.GridX[i];
            int by = buildings.GridY[i];
            if (bx < 0 || bx >= _worldSize || by < 0 || by >= _worldSize) continue;

            int pIdx = PartitionIndexFromTile(bx, by);
            uint flags = buildings.ServiceFlags[i];
            byte level = Math.Max((byte)1, buildings.Level[i]);

            if ((flags & ServicePowerPlant) != 0)
                _powerSupply[pIdx] += PowerPlantMwPerLevel * level;

            if ((flags & ServiceWaterPump) != 0)
                _waterSupply[pIdx] += WaterPumpMlPerLevel * level;

            float occupantLoad = buildings.Occupants[i] * 0.15f;
            if (occupantLoad <= 0f) continue;

            byte zone = tiles.ZoneType[tiles.Index(bx, by)];
            GetOccupantDemand(zone, out float power, out float water);
            _powerDemand[pIdx] += occupantLoad * power;
            _waterDemand[pIdx] += occupantLoad * water;
        }
    }

    private static void GetZoneDemand(byte zoneType, out float power, out float water)
    {
        switch (zoneType)
        {
            case 1: // residential low
            case 2: // residential high
                power = 0.6f;
                water = 0.8f;
                break;
            case 3: // commercial
            case 5: // office
                power = 1.2f;
                water = 0.9f;
                break;
            case 4: // industrial
                power = 2.0f;
                water = 1.4f;
                break;
            case 6: // mixed use
                power = 1.0f;
                water = 0.9f;
                break;
            case 7: // agricultural
                power = 0.4f;
                water = 1.2f;
                break;
            default:
                power = 0.5f;
                water = 0.5f;
                break;
        }
    }

    private static void GetOccupantDemand(byte zoneType, out float power, out float water)
    {
        switch (zoneType)
        {
            case 4:
                power = 0.25f;
                water = 0.15f;
                break;
            case 3:
            case 5:
            case 6:
                power = 0.18f;
                water = 0.12f;
                break;
            default:
                power = 0.12f;
                water = 0.10f;
                break;
        }
    }

    private int PartitionIndexFromTile(int tileX, int tileY)
    {
        int px = tileX / _partitionSize;
        int py = tileY / _partitionSize;
        return PartitionIndex(px, py);
    }

    private int PartitionIndex(int partitionX, int partitionY)
    {
        return partitionY * _partitionsPerAxis + partitionX;
    }

    private static float Lerp(float from, float to, float alpha) =>
        from + (to - from) * alpha;
}
