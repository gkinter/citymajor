using Forge.Engine.Data;
using Forge.Engine.Simulation;

namespace Forge.SimCore;

/// <summary>
/// Tier-2 garbage / waste collection → pollution → city outcomes (Cathedral P5).
/// Depots (<see cref="ServiceGarbage"/>) publish coverage; quality scales abatement.
/// Uncovered residential / commercial tiles accumulate waste pollution; coverage
/// clears it. Pollution already feeds environment satisfaction, health progression,
/// and death modifiers — cleaner streets raise those outcomes. Aggregates feed
/// ResourcesHud Waste + optional immigration attractiveness.
/// </summary>
public static class WasteCollection
{
    /// <summary>ServiceFlags bit — garbage depot / landfill / recycling plant.</summary>
    public const uint ServiceGarbage = 1u << 13;

    /// <summary>Minimum influence coverage counted as "covered" for HUD fraction.</summary>
    public const float MinCoverageForCollection = 0.2f;

    /// <summary>Per-day pollution added on uncovered R/C/O zones (no depot reach).</summary>
    public const float UncoveredWastePerDay = 0.035f;

    /// <summary>Per-day pollution cleared at coverage=1, quality=1.</summary>
    public const float AbatementPerDay = 0.09f;

    /// <summary>Commercial / office waste rate relative to residential.</summary>
    public const float CommercialWasteScale = 0.7f;

    /// <summary>
    /// Effective collection factor (0–1). Quality scales coverage:
    /// poor depots (~0.3) ≈ 65% effect; excellent (1.0) = full coverage.
    /// </summary>
    public static float EffectiveCollectionFactor(float coverage, float depotQuality)
    {
        float cov = Math.Clamp(coverage, 0f, 1f);
        float q = Math.Clamp(depotQuality, 0.3f, 1f);
        float qualityScale = 0.5f + 0.5f * q;
        return Math.Clamp(cov * qualityScale, 0f, 1f);
    }

    /// <summary>
    /// Net pollution delta for one day at a tile (positive = dirtier).
    /// </summary>
    public static float PollutionDelta(
        float coverage,
        float depotQuality,
        byte zoneType,
        float days = 1f)
    {
        float daysClamped = Math.Max(0f, days);
        if (daysClamped <= 0f) return 0f;

        float wasteGen = ZoneWasteGeneration(zoneType);
        if (wasteGen <= 0f) return 0f;

        float factor = EffectiveCollectionFactor(coverage, depotQuality);
        float uncovered = UncoveredWastePerDay * wasteGen * (1f - factor);
        float abatement = AbatementPerDay * factor;
        return (uncovered - abatement) * daysClamped;
    }

    /// <summary>
    /// Mean garbage-depot quality (0–1) across active waste buildings.
    /// Falls back to 0.6 when none exist.
    /// </summary>
    public static float MeanDepotQuality(
        WorldState state,
        Func<WorldState, int, float>? qualityAtBuilding = null)
    {
        var buildings = state.Buildings;
        double sum = 0;
        int count = 0;
        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if ((buildings.ServiceFlags[i] & ServiceGarbage) == 0) continue;

            float q = qualityAtBuilding?.Invoke(state, i)
                      ?? DefaultDepotQuality(buildings.Level[i], buildings.Condition[i]);
            sum += Math.Clamp(q, 0f, 1f);
            count++;
        }

        return count == 0 ? 0.6f : (float)(sum / count);
    }

    /// <summary>
    /// Fraction of active households whose home tile has waste coverage
    /// ≥ <see cref="MinCoverageForCollection"/>.
    /// </summary>
    public static float CoverageFraction(WorldState state, InfluenceMap wasteCoverage)
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
            if (wasteCoverage.GetValue(hx, hy) >= MinCoverageForCollection)
                covered++;
        }

        return count == 0 ? 0f : covered / (float)count;
    }

    /// <summary>
    /// Mean tile pollution (0–1) at active household home tiles.
    /// </summary>
    public static float MeanPollution(WorldState state)
    {
        var hh = state.Households;
        double sum = 0;
        int count = 0;

        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;
            if (!TryHomeTile(state, hh.HomeBuildingId[i], out int hx, out int hy))
                continue;
            sum += Math.Clamp(state.Tiles.Pollution[state.Tiles.Index(hx, hy)], 0f, 1f);
            count++;
        }

        return count == 0 ? 0f : (float)(sum / count);
    }

    /// <summary>
    /// Mean environment score 0–1 ≈ (1 − pollution) at household homes.
    /// </summary>
    public static float MeanEnvironmentScore(WorldState state)
        => Math.Clamp(1f - MeanPollution(state), 0f, 1f);

    /// <summary>
    /// Apply waste accumulation / abatement to zoned R/C/O tiles for
    /// <paramref name="days"/>, then write
    /// <see cref="WorldState.WasteCoverageFraction"/>,
    /// <see cref="WorldState.MeanPollution"/>, and
    /// <see cref="WorldState.MeanEnvironmentScore"/>.
    /// Returns net tiles whose pollution decreased this pass.
    /// </summary>
    public static int Tick(
        WorldState state,
        InfluenceMap wasteCoverage,
        float days = 1f)
    {
        float daysClamped = Math.Max(0f, days);
        float quality = MeanDepotQuality(state);
        int cleaned = 0;

        if (daysClamped > 0f)
        {
            var tiles = state.Tiles;
            for (int y = 0; y < tiles.Size; y++)
            for (int x = 0; x < tiles.Size; x++)
            {
                int idx = tiles.Index(x, y);
                byte zone = tiles.ZoneType[idx];
                if (ZoneWasteGeneration(zone) <= 0f) continue;

                float coverage = Math.Clamp(wasteCoverage.GetValue(x, y), 0f, 1f);
                float delta = PollutionDelta(coverage, quality, zone, daysClamped);
                if (delta == 0f) continue;

                float before = tiles.Pollution[idx];
                float after = Math.Clamp(before + delta, 0f, 1f);
                tiles.Pollution[idx] = after;
                if (after < before)
                    cleaned++;
            }
        }

        RefreshAggregates(state, wasteCoverage);
        return cleaned;
    }

    public static void RefreshAggregates(WorldState state, InfluenceMap wasteCoverage)
    {
        state.WasteCoverageFraction = CoverageFraction(state, wasteCoverage);
        state.MeanPollution = MeanPollution(state);
        state.MeanEnvironmentScore = MeanEnvironmentScore(state);
    }

    /// <summary>
    /// Immigration attractiveness from mean cleanliness (1 − pollution).
    /// 0.55 at pollution=1 → 1.45 at pollution=0 (neutral ~1.0 at 0.5).
    /// </summary>
    public static float EnvironmentAttractivenessModifier(float meanEnvironment01)
    {
        float env = Math.Clamp(meanEnvironment01, 0f, 1f);
        return Math.Clamp(0.55f + env * 0.9f, 0.55f, 1.45f);
    }

    /// <summary>
    /// Residential / commercial / office / mixed-use generate household waste.
    /// Industrial already has industrial pollution sources.
    /// </summary>
    public static float ZoneWasteGeneration(byte zoneType) => zoneType switch
    {
        1 or 2 => 1f,           // Residential low / high
        3 or 5 => CommercialWasteScale, // Commercial / office
        6 => 0.85f,             // Mixed-use
        _ => 0f,
    };

    private static float DefaultDepotQuality(byte level, byte condition)
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
