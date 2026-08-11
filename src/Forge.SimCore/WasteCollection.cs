using Forge.Engine.Data;
using Forge.Engine.Simulation;

namespace Forge.SimCore;

/// <summary>
/// Tier-2 garbage / waste collection → pollution → city outcomes (Cathedral P5).
/// Depots (<see cref="ServiceGarbage"/>) publish coverage; quality scales abatement.
/// Uncovered residential / commercial tiles accumulate waste pollution; coverage
/// clears it. <b>Landfill capacity</b> (MaxOccupants fill) softens abatement when
/// full; <b>recycling diversion</b> (quality × level) shrinks intake so better
/// depots prolong landfill life. Overflow dumps illegally → pollution spikes.
/// Pollution already feeds environment satisfaction, health progression, and
/// death modifiers. Aggregates feed ResourcesHud Waste + immigration attractiveness.
/// </summary>
public static class WasteCollection
{
    /// <summary>ServiceFlags bit — garbage depot / landfill / recycling plant.</summary>
    public const uint ServiceGarbage = 1u << 13;

    /// <summary>Minimum influence coverage counted as "covered" for HUD fraction.</summary>
    public const float MinCoverageForCollection = 0.2f;

    /// <summary>Per-day pollution added on uncovered R/C/O zones (no depot reach).</summary>
    public const float UncoveredWastePerDay = 0.035f;

    /// <summary>Per-day pollution cleared at coverage=1, quality=1, capacity scale=1.</summary>
    public const float AbatementPerDay = 0.09f;

    /// <summary>Commercial / office waste rate relative to residential.</summary>
    public const float CommercialWasteScale = 0.7f;

    /// <summary>
    /// Landfill capacity when <see cref="BuildingData.MaxOccupants"/> is 0
    /// (units ≈ ton-days). Multiplied by building level.
    /// </summary>
    public const ushort DefaultLandfillCapacityPerLevel = 200;

    /// <summary>
    /// Normalized waste units collected per day at coverage=1 on a residential tile.
    /// Kept modest so mid-capacity depots last many sim days before filling.
    /// </summary>
    public const float WasteUnitsPerDayAtFullCoverage = 0.35f;

    /// <summary>Per-day pollution from illegal dumping when landfill overflows.</summary>
    public const float IllegalDumpPollutionPerDay = 0.1f;

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
    /// Recycling diversion 0–1 from depot quality + mean level.
    /// Poor L1 ≈ 15%; excellent L5 ≈ 75% (materials recovery / curbside).
    /// </summary>
    public static float CalculateRecyclingDiversion(float depotQuality, float meanDepotLevel)
    {
        float q = Math.Clamp(depotQuality, 0.3f, 1f);
        float level = Math.Clamp(meanDepotLevel, 1f, 5f);
        float rate = 0.05f + 0.42f * q + 0.07f * (level - 1f);
        return Math.Clamp(rate, 0.05f, 0.85f);
    }

    /// <summary>
    /// Collection scale from landfill utilization. Comfortable (&lt;70%) = full;
    /// approaching capacity softens cleanup; full/over ≈ 15–20% effect (streets fill).
    /// </summary>
    public static float CapacityAbatementScale(float landfillUtilization)
    {
        float u = Math.Max(0f, landfillUtilization);
        if (u <= 0.7f) return 1f;
        if (u >= 1.15f) return 0.12f;
        if (u >= 1f) return 0.15f;
        // 0.7 → 1.0: 1.0 → 0.35
        return Math.Clamp(1f - (u - 0.7f) / 0.3f * 0.65f, 0.35f, 1f);
    }

    /// <summary>
    /// Net pollution delta for one day at a tile (positive = dirtier).
    /// <paramref name="capacityScale"/> multiplies effective collection — full landfills
    /// behave like weak coverage (waste stays on streets).
    /// </summary>
    public static float PollutionDelta(
        float coverage,
        float depotQuality,
        byte zoneType,
        float days = 1f,
        float capacityScale = 1f)
    {
        float daysClamped = Math.Max(0f, days);
        if (daysClamped <= 0f) return 0f;

        float wasteGen = ZoneWasteGeneration(zoneType);
        if (wasteGen <= 0f) return 0f;

        float scale = Math.Clamp(capacityScale, 0.1f, 1f);
        float factor = EffectiveCollectionFactor(coverage, depotQuality) * scale;
        float uncovered = UncoveredWastePerDay * wasteGen * (1f - factor);
        float abatement = AbatementPerDay * factor;
        return (uncovered - abatement) * daysClamped;
    }

    /// <summary>Landfill capacity for a garbage building (MaxOccupants, else default × level).</summary>
    public static int GetLandfillCapacity(BuildingData buildings, int buildingId)
    {
        if (buildingId < 0 || buildingId >= buildings.Capacity) return 0;
        if (!buildings.IsActive(buildingId)) return 0;
        if ((buildings.ServiceFlags[buildingId] & ServiceGarbage) == 0) return 0;

        ushort max = buildings.MaxOccupants[buildingId];
        if (max > 0) return max;

        int level = Math.Max(1, (int)buildings.Level[buildingId]);
        return DefaultLandfillCapacityPerLevel * level;
    }

    /// <summary>Current landfill fill (Occupants) clamped to capacity.</summary>
    public static int GetLandfillFill(BuildingData buildings, int buildingId)
    {
        int capacity = GetLandfillCapacity(buildings, buildingId);
        if (capacity <= 0) return 0;
        return Math.Min(capacity, buildings.Occupants[buildingId]);
    }

    /// <summary>City-wide landfill capacity across active garbage buildings.</summary>
    public static int CountTotalCapacity(WorldState state)
    {
        var buildings = state.Buildings;
        int sum = 0;
        for (int i = 0; i < buildings.Capacity; i++)
            sum += GetLandfillCapacity(buildings, i);
        return sum;
    }

    /// <summary>City-wide landfill fill across active garbage buildings.</summary>
    public static int CountTotalFill(WorldState state)
    {
        var buildings = state.Buildings;
        int sum = 0;
        for (int i = 0; i < buildings.Capacity; i++)
            sum += GetLandfillFill(buildings, i);
        return sum;
    }

    /// <summary>
    /// Fill / capacity (0–1+). Empty city (no depots) reports 0 — no unmet demand yet.
    /// </summary>
    public static float CalculateLandfillUtilization(WorldState state)
    {
        int total = CountTotalCapacity(state);
        if (total <= 0) return 0f;
        return Math.Clamp(CountTotalFill(state) / (float)total, 0f, 2f);
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

    /// <summary>Mean depot level (1–5). Falls back to 1 when none exist.</summary>
    public static float MeanDepotLevel(WorldState state)
    {
        var buildings = state.Buildings;
        double sum = 0;
        int count = 0;
        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if ((buildings.ServiceFlags[i] & ServiceGarbage) == 0) continue;
            sum += Math.Max(1, (int)buildings.Level[i]);
            count++;
        }

        return count == 0 ? 1f : (float)(sum / count);
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
    /// <paramref name="days"/>, fill landfills (net of recycling diversion),
    /// dump illegally on overflow, then write aggregates including
    /// <see cref="WorldState.LandfillUtilizationFraction"/>,
    /// <see cref="WorldState.RecyclingDiversionRate"/>, and
    /// <see cref="WorldState.LandfillOverflowRate"/>.
    /// Returns net tiles whose pollution decreased this pass.
    /// </summary>
    public static int Tick(
        WorldState state,
        InfluenceMap wasteCoverage,
        float days = 1f)
    {
        float daysClamped = Math.Max(0f, days);
        float quality = MeanDepotQuality(state);
        float meanLevel = MeanDepotLevel(state);
        float diversion = CalculateRecyclingDiversion(quality, meanLevel);
        float utilBefore = CalculateLandfillUtilization(state);
        float capacityScale = CapacityAbatementScale(utilBefore);
        int cleaned = 0;

        double intakeSum = 0;

        if (daysClamped > 0f)
        {
            var tiles = state.Tiles;
            for (int y = 0; y < tiles.Size; y++)
            for (int x = 0; x < tiles.Size; x++)
            {
                int idx = tiles.Index(x, y);
                byte zone = tiles.ZoneType[idx];
                float wasteGen = ZoneWasteGeneration(zone);
                if (wasteGen <= 0f) continue;

                float coverage = Math.Clamp(wasteCoverage.GetValue(x, y), 0f, 1f);
                float factor = EffectiveCollectionFactor(coverage, quality);
                intakeSum += WasteUnitsPerDayAtFullCoverage * wasteGen * factor * daysClamped;

                float delta = PollutionDelta(coverage, quality, zone, daysClamped, capacityScale);
                if (delta == 0f) continue;

                float before = tiles.Pollution[idx];
                float after = Math.Clamp(before + delta, 0f, 1f);
                tiles.Pollution[idx] = after;
                if (after < before)
                    cleaned++;
            }
        }

        float collected = (float)intakeSum;
        float diverted = collected * diversion;
        float toLandfill = Math.Max(0f, collected - diverted);

        float overflow;
        if (utilBefore >= 1f && toLandfill > 0f)
        {
            // No room — entire undiverted stream dumps illegally (don't bank pending).
            state.LandfillPendingFill = 0f;
            overflow = toLandfill;
        }
        else
        {
            state.LandfillPendingFill += toLandfill;
            float wholeUnits = MathF.Floor(state.LandfillPendingFill);
            state.LandfillPendingFill -= wholeUnits;
            overflow = DepositInLandfills(state, wholeUnits);
        }

        if (overflow > 0f && daysClamped > 0f)
            ApplyIllegalDumpPollution(state, wasteCoverage, overflow, daysClamped);

        float overflowRate = 0f;
        if (collected > 0.001f)
            overflowRate = Math.Clamp(overflow / collected, 0f, 1f);
        else if (overflow > 0f)
            overflowRate = 1f;

        state.LandfillUtilizationFraction = CalculateLandfillUtilization(state);
        state.RecyclingDiversionRate = diversion;
        state.LandfillOverflowRate = overflowRate;

        RefreshAggregates(state, wasteCoverage);
        return cleaned;
    }

    public static void RefreshAggregates(WorldState state, InfluenceMap wasteCoverage)
    {
        state.WasteCoverageFraction = CoverageFraction(state, wasteCoverage);
        state.MeanPollution = MeanPollution(state);
        state.MeanEnvironmentScore = MeanEnvironmentScore(state);
        // Landfill aggregates are owned by Tick (need intake/overflow context);
        // keep utilization fresh when only refreshing coverage/pollution.
        if (CountTotalCapacity(state) > 0)
            state.LandfillUtilizationFraction = CalculateLandfillUtilization(state);
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

    /// <summary>
    /// Distribute whole <paramref name="wasteUnits"/> into active landfill Occupants.
    /// Returns unplaced overflow (illegal dump tonnage). Fractional leftovers should
    /// be accumulated by the caller via <see cref="WorldState.LandfillPendingFill"/>.
    /// </summary>
    public static float DepositInLandfills(WorldState state, float wasteUnits)
    {
        if (wasteUnits < 1f) return 0f;

        var buildings = state.Buildings;
        float remaining = wasteUnits;

        for (int pass = 0; pass < buildings.Capacity && remaining >= 1f; pass++)
        {
            int best = -1;
            float bestFillFrac = float.MaxValue;
            for (int i = 0; i < buildings.Capacity; i++)
            {
                int cap = GetLandfillCapacity(buildings, i);
                if (cap <= 0) continue;
                int fill = GetLandfillFill(buildings, i);
                if (fill >= cap) continue;
                float frac = fill / (float)cap;
                if (frac < bestFillFrac)
                {
                    bestFillFrac = frac;
                    best = i;
                }
            }

            if (best < 0) break;

            int capacity = GetLandfillCapacity(buildings, best);
            int fillNow = GetLandfillFill(buildings, best);
            int room = capacity - fillNow;
            int add = Math.Min((int)MathF.Floor(remaining), room);
            if (add < 1) break;
            buildings.Occupants[best] = (ushort)(fillNow + add);
            remaining -= add;
        }

        return Math.Max(0f, remaining);
    }

    private static void ApplyIllegalDumpPollution(
        WorldState state,
        InfluenceMap wasteCoverage,
        float overflowUnits,
        float days)
    {
        // Overflow that couldn't fit anywhere dumps city-wide on waste generators.
        // Floor intensity so even a small daily overflow is visible vs residual abatement.
        float intensity = Math.Clamp(0.35f + overflowUnits * 0.85f, 0.35f, 1.5f);
        var tiles = state.Tiles;
        for (int y = 0; y < tiles.Size; y++)
        for (int x = 0; x < tiles.Size; x++)
        {
            int idx = tiles.Index(x, y);
            float wasteGen = ZoneWasteGeneration(tiles.ZoneType[idx]);
            if (wasteGen <= 0f) continue;
            float coverage = Math.Clamp(wasteCoverage.GetValue(x, y), 0f, 1f);
            // Prefer dumping where collection is weakest; keep a floor so covered tiles still dirty.
            float dumpWeight = Math.Max(0.35f, 1f - coverage * 0.5f);
            float delta = IllegalDumpPollutionPerDay * intensity * dumpWeight * wasteGen * days;
            tiles.Pollution[idx] = Math.Clamp(tiles.Pollution[idx] + delta, 0f, 1f);
        }
    }

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
