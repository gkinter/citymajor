using Forge.Engine.Data;
using Forge.Engine.Simulation;

namespace Forge.SimCore;

/// <summary>
/// Park amenity parity (Cathedral P5 diagram S3 health depth / U3.4 park zones).
/// Painted <see cref="ZonePark"/> tiles and ploppable <see cref="ServicePark"/>
/// buildings raise exercise / health — not only land value via
/// <c>ZoneGrowthSystem.HasNearbyPark</c>.
/// </summary>
public static class ParkAmenity
{
    /// <summary>ServiceFlags bit — park / recreation ploppable.</summary>
    public const uint ServicePark = 1u << 6;

    /// <summary>Painted park / recreation zone (TileData.ZoneType byte 8).</summary>
    public const byte ZonePark = 8;

    /// <summary>Radius used by land-value / HasNearbyPark checks.</summary>
    public const int LandValueRadius = 8;

    /// <summary>Radius used by health exercise / LocalParkAccess.</summary>
    public const int AccessRadius = 10;

    /// <summary>Matches <c>ServiceSystem.HealthExerciseWeight</c>.</summary>
    public const float HealthExerciseWeight = 0.1f;

    /// <summary>Minimum access to count a household as park-covered.</summary>
    public const float MinAccessForCoverage = 0.5f;

    /// <summary>Per-day blend rate of HealthSatisfaction toward the park-aware target.</summary>
    public const float HealthBlendPerDay = 0.20f;

    /// <summary>LeisureSatisfaction boost at access=1 (on top of neutral 128).</summary>
    public const float LeisureBoostAtFullAccess = 50f;

    /// <summary>
    /// True when a park building or painted park zone exists within
    /// <paramref name="radius"/> (squared Euclidean).
    /// </summary>
    public static bool HasNearbyPark(
        WorldState state,
        int tileX,
        int tileY,
        int radius = LandValueRadius)
    {
        if (!state.Tiles.InBounds(tileX, tileY) || radius < 0)
            return false;

        int r2 = radius * radius;
        var buildings = state.Buildings;
        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if ((buildings.ServiceFlags[i] & ServicePark) == 0) continue;

            int dx = buildings.GridX[i] - tileX;
            int dy = buildings.GridY[i] - tileY;
            if (dx * dx + dy * dy <= r2) return true;
        }

        var tiles = state.Tiles;
        int minX = Math.Max(0, tileX - radius);
        int maxX = Math.Min(tiles.Size - 1, tileX + radius);
        int minY = Math.Max(0, tileY - radius);
        int maxY = Math.Min(tiles.Size - 1, tileY + radius);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int dx = x - tileX;
                int dy = y - tileY;
                if (dx * dx + dy * dy > r2) continue;
                if (tiles.ZoneType[tiles.Index(x, y)] == ZonePark)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Local park / exercise access 0–1. Nearest park building or painted park
    /// zone within <paramref name="radius"/> with linear distance falloff
    /// (<c>1 - dist/radius</c>). Returns 0 when none nearby.
    /// </summary>
    public static float LocalParkAccess(
        WorldState state,
        int tileX,
        int tileY,
        int radius = AccessRadius)
    {
        if (!state.Tiles.InBounds(tileX, tileY) || radius <= 0)
            return 0f;

        float best = 0f;
        float invR = 1f / radius;

        var buildings = state.Buildings;
        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if ((buildings.ServiceFlags[i] & ServicePark) == 0) continue;

            int dx = buildings.GridX[i] - tileX;
            int dy = buildings.GridY[i] - tileY;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist > radius) continue;
            float access = 1f - dist * invR;
            if (access > best) best = access;
        }

        var tiles = state.Tiles;
        int minX = Math.Max(0, tileX - radius);
        int maxX = Math.Min(tiles.Size - 1, tileX + radius);
        int minY = Math.Max(0, tileY - radius);
        int maxY = Math.Min(tiles.Size - 1, tileY + radius);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (tiles.ZoneType[tiles.Index(x, y)] != ZonePark) continue;
                int dx = x - tileX;
                int dy = y - tileY;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                if (dist > radius) continue;
                float access = 1f - dist * invR;
                if (access > best) best = access;
            }
        }

        return Math.Clamp(best, 0f, 1f);
    }

    /// <summary>
    /// Exercise term for the AGENT_06 health formula (<c>exercise * 0.1</c>).
    /// </summary>
    public static float ExerciseContribution(float parkAccess)
        => Math.Clamp(parkAccess, 0f, 1f) * HealthExerciseWeight;

    /// <summary>
    /// Mean LocalParkAccess across active households with a resolvable home tile.
    /// </summary>
    public static float MeanParkAccess(WorldState state)
    {
        var hh = state.Households;
        double sum = 0;
        int count = 0;
        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;
            if (!TryHomeTile(state, hh.HomeBuildingId[i], out int hx, out int hy))
                continue;
            sum += LocalParkAccess(state, hx, hy);
            count++;
        }

        return count == 0 ? 0f : (float)(sum / count);
    }

    /// <summary>
    /// Fraction of active households with <see cref="LocalParkAccess"/> ≥
    /// <see cref="MinAccessForCoverage"/>.
    /// </summary>
    public static float ParkAccessFraction(WorldState state)
    {
        var hh = state.Households;
        int covered = 0;
        int count = 0;
        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;
            count++;
            if (!TryHomeTile(state, hh.HomeBuildingId[i], out int hx, out int hy))
                continue;
            if (LocalParkAccess(state, hx, hy) >= MinAccessForCoverage)
                covered++;
        }

        return count == 0 ? 0f : covered / (float)count;
    }

    /// <summary>
    /// Mean household HealthSatisfaction mapped to 0–1.
    /// </summary>
    public static float MeanHealthSatisfaction(WorldState state)
    {
        var hh = state.Households;
        double sum = 0;
        int count = 0;
        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;
            sum += hh.HealthSatisfaction[i] / 255f;
            count++;
        }

        return count == 0 ? 0f : (float)(sum / count);
    }

    /// <summary>
    /// Blend household LeisureSatisfaction toward park-aware targets, then write
    /// <see cref="WorldState.MeanParkAccess"/> and
    /// <see cref="WorldState.ParkAccessFraction"/>.
    /// HealthSatisfaction is owned by <see cref="HealthProgression"/> (hospitals +
    /// exercise contribution); this tick only refreshes mean health for HUD convenience.
    /// Returns net households that gained LeisureSatisfaction this pass.
    /// </summary>
    public static int Tick(WorldState state, float days = 1f)
    {
        float daysClamped = Math.Max(0f, days);
        if (daysClamped <= 0f)
        {
            RefreshAggregates(state);
            return 0;
        }

        float blend = Math.Clamp(HealthBlendPerDay * daysClamped, 0f, 1f);
        var hh = state.Households;
        int improved = 0;

        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;
            if (!TryHomeTile(state, hh.HomeBuildingId[i], out int hx, out int hy))
                continue;

            float access = LocalParkAccess(state, hx, hy);
            byte leisureTarget = (byte)Math.Clamp(
                128f + access * LeisureBoostAtFullAccess, 0f, 255f);
            byte before = hh.LeisureSatisfaction[i];
            hh.LeisureSatisfaction[i] = BlendByte(before, leisureTarget, blend);
            if (hh.LeisureSatisfaction[i] > before)
                improved++;
        }

        RefreshAggregates(state);
        return improved;
    }

    public static void RefreshAggregates(WorldState state)
    {
        state.MeanParkAccess = MeanParkAccess(state);
        state.ParkAccessFraction = ParkAccessFraction(state);
        // Mean health is primarily written by HealthProgression; keep in sync when
        // only park amenity ran (e.g. characterization tests that skip hospitals).
        state.MeanHealthSatisfaction = MeanHealthSatisfaction(state);
    }

    private static byte BlendByte(byte current, byte target, float t)
    {
        float blended = current + (target - current) * t;
        return (byte)Math.Clamp((int)MathF.Round(blended), 0, 255);
    }

    private static bool TryHomeTile(WorldState state, ushort homeBuildingId, out int x, out int y)
    {
        x = 0;
        y = 0;
        if (homeBuildingId == 0) return false;
        int bid = homeBuildingId;
        if (bid < 0 || bid >= state.Buildings.Capacity) return false;
        if (!state.Buildings.IsActive(bid)) return false;
        x = state.Buildings.GridX[bid];
        y = state.Buildings.GridY[bid];
        return state.Tiles.InBounds(x, y);
    }
}
