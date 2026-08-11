using Forge.Engine.Data;
using Forge.Engine.Simulation;

namespace Forge.SimCore;

/// <summary>
/// Fire response v1 — hydrant coverage + spread chance (Cathedral P5.3).
/// Hydrants are buildings with <see cref="ServiceHydrant"/>; without one nearby,
/// trucks shuttle water (2× response minutes) and spread chance is higher.
/// Burning intensity lives in <see cref="BuildingData.FireRisk"/> (0 = not on fire).
/// </summary>
public static class FireResponse
{
    /// <summary>ServiceFlags bit for a placeable fire hydrant.</summary>
    public const uint ServiceHydrant = 1u << 9;

    /// <summary>Hydrant covers incidents within this Euclidean tile radius.</summary>
    public const float HydrantRadiusTiles = 3f;

    /// <summary>No hydrant → trucks shuttle water (MISSING_SYSTEMS / AGENT_06).</summary>
    public const float NoHydrantResponseMultiplier = 2f;

    /// <summary>Base per-hour chance a burning building ignites one adjacent neighbor.</summary>
    public const float BaseSpreadChancePerHour = 0.3f;

    public const float MaterialWood = 1.5f;
    public const float MaterialBrick = 1.0f;
    public const float MaterialConcrete = 0.5f;
    public const float MaterialSteel = 0.3f;

    /// <summary>Spread multiplier when a hydrant covers the target tile.</summary>
    public const float HydrantSpreadInverse = 0.5f;

    /// <summary>Spread multiplier when no hydrant covers the target tile.</summary>
    public const float NoHydrantSpreadInverse = 1f;

    /// <summary>Initial intensity written to <c>BuildingData.FireRisk</c> on ignite.</summary>
    public const byte IgniteIntensity = 64;

    /// <summary>Intensity at or above this counts as an active fire.</summary>
    public const byte BurningThreshold = 1;

    private static readonly (int Dx, int Dy)[] Cardinal =
    {
        (1, 0), (-1, 0), (0, 1), (0, -1),
    };

    /// <summary>
    /// True when an active hydrant building lies within <paramref name="radiusTiles"/>
    /// of the incident tile.
    /// </summary>
    public static bool HasHydrantCoverage(
        WorldState state,
        int tileX,
        int tileY,
        float radiusTiles = HydrantRadiusTiles)
    {
        if (!state.Tiles.InBounds(tileX, tileY))
            return false;

        float radiusSq = radiusTiles * radiusTiles;
        var buildings = state.Buildings;

        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if ((buildings.ServiceFlags[i] & ServiceHydrant) == 0) continue;

            float dx = buildings.GridX[i] - tileX;
            float dy = buildings.GridY[i] - tileY;
            if (dx * dx + dy * dy <= radiusSq)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Fraction of sampled zoned tiles (with buildings) that have hydrant coverage.
    /// Empty / unzoned cities report 1 (no unmet hydrant demand).
    /// </summary>
    public static float CalculateHydrantCoverageFraction(
        WorldState state,
        int sampleStride = 8)
    {
        int stride = Math.Max(1, sampleStride);
        var tiles = state.Tiles;
        int covered = 0;
        int count = 0;
        int seen = 0;

        for (int y = 0; y < tiles.Size; y++)
        for (int x = 0; x < tiles.Size; x++)
        {
            int idx = tiles.Index(x, y);
            if (tiles.ZoneType[idx] == 0) continue;
            if (tiles.BuildingId[idx] == 0) continue;
            if ((seen++ % stride) != 0) continue;

            count++;
            if (HasHydrantCoverage(state, x, y))
                covered++;
        }

        return count == 0 ? 1f : covered / (float)count;
    }

    /// <summary>
    /// Apply the shuttle-water penalty when the incident tile has no hydrant.
    /// </summary>
    public static float ApplyHydrantResponseMultiplier(
        float responseMinutes,
        bool hasHydrant)
    {
        if (responseMinutes <= 0f || float.IsInfinity(responseMinutes) || float.IsNaN(responseMinutes))
            return responseMinutes;

        return hasHydrant
            ? responseMinutes
            : responseMinutes * NoHydrantResponseMultiplier;
    }

    /// <summary>
    /// Material factor from building upgrade level (wood → brick → concrete → steel).
    /// </summary>
    public static float MaterialFactorFromLevel(byte level) => level switch
    {
        0 or 1 => MaterialWood,
        2 => MaterialBrick,
        3 => MaterialConcrete,
        _ => MaterialSteel,
    };

    /// <summary>
    /// Map <see cref="WorldState.WindSpeed"/> (m/s) to AGENT_06 wind_factor [0.5, 2.0].
    /// Calm (~0) → 0.5; typical breeze (~5) → ~1.0; gale (15+) → 2.0.
    /// </summary>
    public static float WindFactor(float windSpeedMs)
    {
        float t = Math.Clamp(windSpeedMs / 15f, 0f, 1f);
        return 0.5f + 1.5f * t;
    }

    /// <summary>
    /// Per-hour chance a burning source ignites a specific adjacent target.
    /// <c>spread = base * material * wind * adjacency * hydrant_inverse</c>, clamped to [0, 1].
    /// </summary>
    public static float CalculateSpreadChance(
        float materialFactor,
        float windFactor,
        int adjacentBurningCount,
        bool targetHasHydrant,
        float hours = 1f)
    {
        float adjacency = Math.Max(1, adjacentBurningCount);
        float hydrantInverse = targetHasHydrant ? HydrantSpreadInverse : NoHydrantSpreadInverse;
        float chance = BaseSpreadChancePerHour
            * Math.Max(0f, materialFactor)
            * Math.Max(0f, windFactor)
            * adjacency
            * hydrantInverse
            * Math.Max(0f, hours);
        return Math.Clamp(chance, 0f, 1f);
    }

    /// <summary>True when the building slot is active and currently burning.</summary>
    public static bool IsBurning(BuildingData buildings, int buildingId)
    {
        if (buildingId < 0 || buildingId >= buildings.Capacity) return false;
        if (!buildings.IsActive(buildingId)) return false;
        return buildings.FireRisk[buildingId] >= BurningThreshold;
    }

    /// <summary>Count active buildings with fire intensity ≥ <see cref="BurningThreshold"/>.</summary>
    public static int CountActiveFires(WorldState state)
    {
        var buildings = state.Buildings;
        int count = 0;
        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (IsBurning(buildings, i))
                count++;
        }

        return count;
    }

    /// <summary>
    /// Mark a building as burning. Returns false if the slot is inactive / OOB.
    /// </summary>
    public static bool TryIgnite(WorldState state, int buildingId, byte intensity = IgniteIntensity)
    {
        var buildings = state.Buildings;
        if (buildingId < 0 || buildingId >= buildings.Capacity) return false;
        if (!buildings.IsActive(buildingId)) return false;

        byte next = Math.Max(buildings.FireRisk[buildingId], intensity);
        if (next < BurningThreshold)
            next = BurningThreshold;
        buildings.FireRisk[buildingId] = next;
        return true;
    }

    /// <summary>
    /// Extinguish a burning building (intensity → 0). Returns false if inactive / OOB.
    /// </summary>
    public static bool TryExtinguish(WorldState state, int buildingId)
    {
        var buildings = state.Buildings;
        if (buildingId < 0 || buildingId >= buildings.Capacity) return false;
        if (!buildings.IsActive(buildingId)) return false;
        buildings.FireRisk[buildingId] = 0;
        return true;
    }

    /// <summary>
    /// Daily/hourly fire-spread tick: each burning building may ignite cardinal
    /// neighbors. Returns the number of newly ignited buildings.
    /// </summary>
    public static int TickSpread(WorldState state, float hours = 1f, Random? rng = null)
    {
        rng ??= new Random();
        float wind = WindFactor(state.WindSpeed);
        var buildings = state.Buildings;
        var tiles = state.Tiles;

        // Snapshot burners first so same-tick chain reactions stay deterministic.
        var burners = new List<(int Id, int X, int Y)>();
        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!IsBurning(buildings, i)) continue;
            burners.Add((i, buildings.GridX[i], buildings.GridY[i]));
        }

        int ignited = 0;
        foreach (var (sourceId, sx, sy) in burners)
        {
            int adjacentBurning = CountAdjacentBurning(state, sx, sy);

            foreach (var (dx, dy) in Cardinal)
            {
                int nx = sx + dx;
                int ny = sy + dy;
                if (!tiles.InBounds(nx, ny)) continue;

                ushort neighborTileBuilding = tiles.BuildingId[tiles.Index(nx, ny)];
                if (neighborTileBuilding == 0) continue;

                // BuildingId on tiles is 1-based in some paths; prefer coordinate match.
                int neighborId = FindBuildingAt(buildings, nx, ny);
                if (neighborId < 0 || neighborId == sourceId) continue;
                if (IsBurning(buildings, neighborId)) continue;

                float material = MaterialFactorFromLevel(buildings.Level[neighborId]);
                bool hydrant = HasHydrantCoverage(state, nx, ny);
                float chance = CalculateSpreadChance(
                    material, wind, adjacentBurning, hydrant, hours);

                if (rng.NextDouble() < chance && TryIgnite(state, neighborId))
                    ignited++;
            }
        }

        return ignited;
    }

    private static int CountAdjacentBurning(WorldState state, int tileX, int tileY)
    {
        int count = 0;
        var buildings = state.Buildings;
        var tiles = state.Tiles;

        foreach (var (dx, dy) in Cardinal)
        {
            int nx = tileX + dx;
            int ny = tileY + dy;
            if (!tiles.InBounds(nx, ny)) continue;
            int id = FindBuildingAt(buildings, nx, ny);
            if (id >= 0 && IsBurning(buildings, id))
                count++;
        }

        return Math.Max(1, count);
    }

    /// <summary>Find active building whose grid origin matches the tile.</summary>
    public static int FindBuildingAt(BuildingData buildings, int tileX, int tileY)
    {
        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if (buildings.GridX[i] == tileX && buildings.GridY[i] == tileY)
                return i;
        }

        return -1;
    }
}
