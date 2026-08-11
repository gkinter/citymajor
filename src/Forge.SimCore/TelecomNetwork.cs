using Forge.Engine.Data;
using Forge.Engine.Simulation;

namespace Forge.SimCore;

/// <summary>
/// Tier-2 internet / telecom coverage → city outcomes (Cathedral P5).
/// Telecom hubs (<see cref="ServiceTelecom"/>) publish coverage; hub quality
/// deepens the tier written to <see cref="TileData.InternetConnection"/>
/// (0=none, 1=copper, 2=fiber, 3=5G). Coverage feeds services satisfaction and
/// immigration attractiveness. Aggregates feed ResourcesHud Net.
/// </summary>
public static class TelecomNetwork
{
    /// <summary>ServiceFlags bit — telephone exchange / cell tower / fiber hub / 5G.</summary>
    public const uint ServiceTelecom = 1u << 15;

    /// <summary>TileData.InternetConnection — no service.</summary>
    public const byte TierNone = 0;

    /// <summary>TileData.InternetConnection — copper / DSL.</summary>
    public const byte TierCopper = 1;

    /// <summary>TileData.InternetConnection — fiber.</summary>
    public const byte TierFiber = 2;

    /// <summary>TileData.InternetConnection — 5G / dense wireless.</summary>
    public const byte Tier5G = 3;

    /// <summary>Minimum effective factor counted as "covered" for HUD fraction.</summary>
    public const float MinCoverageForService = 0.2f;

    /// <summary>
    /// Effective telecom factor (0–1). Quality scales coverage:
    /// poor hubs (~0.3) ≈ 65% effect; excellent (1.0) = full coverage.
    /// </summary>
    public static float EffectiveTelecomFactor(float coverage, float hubQuality)
    {
        float cov = Math.Clamp(coverage, 0f, 1f);
        float q = Math.Clamp(hubQuality, 0.3f, 1f);
        float qualityScale = 0.5f + 0.5f * q;
        return Math.Clamp(cov * qualityScale, 0f, 1f);
    }

    /// <summary>
    /// Map effective factor to <see cref="TileData.InternetConnection"/> tier.
    /// </summary>
    public static byte TierFromFactor(float effectiveFactor)
    {
        float f = Math.Clamp(effectiveFactor, 0f, 1f);
        if (f < MinCoverageForService) return TierNone;
        if (f < 0.45f) return TierCopper;
        if (f < 0.75f) return TierFiber;
        return Tier5G;
    }

    /// <summary>
    /// Connection tier from coverage + hub quality (test / HUD helper).
    /// </summary>
    public static byte ConnectionTier(float coverage, float hubQuality)
        => TierFromFactor(EffectiveTelecomFactor(coverage, hubQuality));

    /// <summary>
    /// Telecom access 0–1 from a connection tier (tier / 3).
    /// </summary>
    public static float AccessFromTier(byte tier)
        => Math.Clamp(tier / 3f, 0f, 1f);

    /// <summary>
    /// Mean telecom-hub quality (0–1) across active telecom buildings.
    /// Falls back to 0.6 when none exist.
    /// </summary>
    public static float MeanHubQuality(
        WorldState state,
        Func<WorldState, int, float>? qualityAtBuilding = null)
    {
        var buildings = state.Buildings;
        double sum = 0;
        int count = 0;
        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if ((buildings.ServiceFlags[i] & ServiceTelecom) == 0) continue;

            float q = qualityAtBuilding?.Invoke(state, i)
                      ?? DefaultHubQuality(buildings.Level[i], buildings.Condition[i]);
            sum += Math.Clamp(q, 0f, 1f);
            count++;
        }

        return count == 0 ? 0.6f : (float)(sum / count);
    }

    /// <summary>
    /// Fraction of active households whose home tile has internet tier ≥ copper.
    /// </summary>
    public static float CoverageFraction(WorldState state)
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
            if (state.Tiles.InternetConnection[state.Tiles.Index(hx, hy)] >= TierCopper)
                covered++;
        }

        return count == 0 ? 0f : covered / (float)count;
    }

    /// <summary>
    /// Mean <see cref="TileData.InternetConnection"/> tier (0–3) at household homes.
    /// </summary>
    public static float MeanInternetTier(WorldState state)
    {
        var hh = state.Households;
        double sum = 0;
        int count = 0;

        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;
            if (!TryHomeTile(state, hh.HomeBuildingId[i], out int hx, out int hy))
                continue;
            sum += state.Tiles.InternetConnection[state.Tiles.Index(hx, hy)];
            count++;
        }

        return count == 0 ? 0f : (float)(sum / count);
    }

    /// <summary>
    /// Mean telecom access 0–1 ≈ mean tier / 3 at household homes.
    /// </summary>
    public static float MeanTelecomAccess(WorldState state)
        => Math.Clamp(MeanInternetTier(state) / 3f, 0f, 1f);

    /// <summary>
    /// Write <see cref="TileData.InternetConnection"/> from coverage × hub quality
    /// on zoned tiles, then export
    /// <see cref="WorldState.InternetCoverageFraction"/>,
    /// <see cref="WorldState.MeanInternetTier"/>, and
    /// <see cref="WorldState.MeanTelecomAccess"/>.
    /// Returns tiles whose connection tier increased this pass.
    /// </summary>
    public static int Tick(
        WorldState state,
        InfluenceMap telecomCoverage,
        float days = 1f)
    {
        _ = days; // connection is stateful coverage rewrite (like sewage connection bit)
        float quality = MeanHubQuality(state);
        int upgraded = 0;

        var tiles = state.Tiles;
        for (int y = 0; y < tiles.Size; y++)
        for (int x = 0; x < tiles.Size; x++)
        {
            int idx = tiles.Index(x, y);
            byte zone = tiles.ZoneType[idx];
            if (ZoneNeedsInternet(zone) <= 0f)
            {
                // Clear stale connection outside demand zones.
                if (tiles.InternetConnection[idx] != TierNone)
                    tiles.InternetConnection[idx] = TierNone;
                continue;
            }

            float coverage = Math.Clamp(telecomCoverage.GetValue(x, y), 0f, 1f);
            byte before = tiles.InternetConnection[idx];
            byte after = ConnectionTier(coverage, quality);
            tiles.InternetConnection[idx] = after;
            if (after > before)
                upgraded++;
        }

        RefreshAggregates(state);
        return upgraded;
    }

    public static void RefreshAggregates(WorldState state)
    {
        state.InternetCoverageFraction = CoverageFraction(state);
        state.MeanInternetTier = MeanInternetTier(state);
        state.MeanTelecomAccess = MeanTelecomAccess(state);
    }

    /// <summary>
    /// Immigration attractiveness from mean telecom access.
    /// 0.55 at dead-zone → 1.45 at full 5G (neutral ~1.0 at mid tier).
    /// </summary>
    public static float TelecomAttractivenessModifier(float meanTelecomAccess01)
    {
        float a = Math.Clamp(meanTelecomAccess01, 0f, 1f);
        return Math.Clamp(0.55f + a * 0.9f, 0.55f, 1.45f);
    }

    /// <summary>
    /// Residential / commercial / office / mixed-use / industrial demand internet.
    /// Industrial uses telemetry; offices / tech need fiber (MISSING_SYSTEMS §11).
    /// </summary>
    public static float ZoneNeedsInternet(byte zoneType) => zoneType switch
    {
        1 or 2 => 1f,      // Residential
        3 or 5 => 1.2f,    // Commercial / office (higher demand)
        4 => 0.85f,        // Industrial telemetry
        6 => 1.1f,         // Mixed-use
        _ => 0f,
    };

    private static float DefaultHubQuality(byte level, byte condition)
    {
        float baseQ = 0.3f + level * 0.14f;
        float conditionMod = 0.3f + (condition / 255f) * 0.7f;
        return Math.Clamp(baseQ * conditionMod, 0f, 1f);
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
