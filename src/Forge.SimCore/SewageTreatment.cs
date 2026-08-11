using Forge.Engine.Data;
using Forge.Engine.Simulation;

namespace Forge.SimCore;

/// <summary>
/// Tier-2 sewage / water contamination → health &amp; environment outcomes (Cathedral P5).
/// Treatment plants (<see cref="ServiceSewage"/>) publish coverage; quality scales
/// removal. Uncovered residential / commercial tiles accumulate waterborne
/// pollution and clear <see cref="TileData.SewageConnection"/>; coverage abates
/// contamination and marks tiles connected. Pollution already feeds environment
/// satisfaction and health progression — cleaner water raises those outcomes.
/// Aggregates feed ResourcesHud Sewage + immigration water-quality attractiveness.
/// </summary>
public static class SewageTreatment
{
    /// <summary>ServiceFlags bit — sewage treatment plant / pumping station.</summary>
    public const uint ServiceSewage = 1u << 14;

    /// <summary>Minimum influence coverage counted as "covered" for HUD fraction.</summary>
    public const float MinCoverageForTreatment = 0.2f;

    /// <summary>Per-day pollution added on uncovered R/C/O zones (raw sewage).</summary>
    public const float UncoveredContaminationPerDay = 0.04f;

    /// <summary>Per-day pollution cleared at coverage=1, quality=1.</summary>
    public const float TreatmentPerDay = 0.1f;

    /// <summary>Commercial / office sewage load relative to residential.</summary>
    public const float CommercialSewageScale = 0.75f;

    /// <summary>Industrial sewage / process-water load relative to residential.</summary>
    public const float IndustrialSewageScale = 1.15f;

    /// <summary>
    /// Effective treatment factor (0–1). Quality scales coverage:
    /// poor plants (~0.3) ≈ 65% effect; excellent (1.0) = full coverage.
    /// </summary>
    public static float EffectiveTreatmentFactor(float coverage, float plantQuality)
    {
        float cov = Math.Clamp(coverage, 0f, 1f);
        float q = Math.Clamp(plantQuality, 0.3f, 1f);
        float qualityScale = 0.5f + 0.5f * q;
        return Math.Clamp(cov * qualityScale, 0f, 1f);
    }

    /// <summary>
    /// Net pollution delta for one day at a tile (positive = dirtier water).
    /// </summary>
    public static float ContaminationDelta(
        float coverage,
        float plantQuality,
        byte zoneType,
        float days = 1f)
    {
        float daysClamped = Math.Max(0f, days);
        if (daysClamped <= 0f) return 0f;

        float load = ZoneSewageLoad(zoneType);
        if (load <= 0f) return 0f;

        float factor = EffectiveTreatmentFactor(coverage, plantQuality);
        float uncovered = UncoveredContaminationPerDay * load * (1f - factor);
        float treatment = TreatmentPerDay * factor;
        return (uncovered - treatment) * daysClamped;
    }

    /// <summary>
    /// Water contamination 0–1 at a tile from sewage coverage + residual pollution.
    /// Untreated sewage dominates; residual pollution still dirties drinking water.
    /// </summary>
    public static float ContaminationAtTile(float coverage, float plantQuality, float pollution)
    {
        float factor = EffectiveTreatmentFactor(coverage, plantQuality);
        float sewageLoad = 1f - factor;
        return Math.Clamp(
            sewageLoad * 0.75f + Math.Clamp(pollution, 0f, 1f) * 0.25f,
            0f,
            1f);
    }

    /// <summary>
    /// Mean treatment-plant quality (0–1) across active sewage buildings.
    /// Falls back to 0.6 when none exist.
    /// </summary>
    public static float MeanPlantQuality(
        WorldState state,
        Func<WorldState, int, float>? qualityAtBuilding = null)
    {
        var buildings = state.Buildings;
        double sum = 0;
        int count = 0;
        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if ((buildings.ServiceFlags[i] & ServiceSewage) == 0) continue;

            float q = qualityAtBuilding?.Invoke(state, i)
                      ?? DefaultPlantQuality(buildings.Level[i], buildings.Condition[i]);
            sum += Math.Clamp(q, 0f, 1f);
            count++;
        }

        return count == 0 ? 0.6f : (float)(sum / count);
    }

    /// <summary>
    /// Fraction of active households whose home tile has sewage coverage
    /// ≥ <see cref="MinCoverageForTreatment"/>.
    /// </summary>
    public static float CoverageFraction(WorldState state, InfluenceMap sewageCoverage)
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
            if (sewageCoverage.GetValue(hx, hy) >= MinCoverageForTreatment)
                covered++;
        }

        return count == 0 ? 0f : covered / (float)count;
    }

    /// <summary>
    /// Mean water contamination (0–1) at active household home tiles.
    /// </summary>
    public static float MeanWaterContamination(WorldState state, InfluenceMap sewageCoverage)
    {
        var hh = state.Households;
        float quality = MeanPlantQuality(state);
        double sum = 0;
        int count = 0;

        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;
            if (!TryHomeTile(state, hh.HomeBuildingId[i], out int hx, out int hy))
                continue;
            int idx = state.Tiles.Index(hx, hy);
            float coverage = Math.Clamp(sewageCoverage.GetValue(hx, hy), 0f, 1f);
            float pollution = Math.Clamp(state.Tiles.Pollution[idx], 0f, 1f);
            sum += ContaminationAtTile(coverage, quality, pollution);
            count++;
        }

        return count == 0 ? 0f : (float)(sum / count);
    }

    /// <summary>
    /// Mean water quality 0–1 ≈ (1 − contamination) at household homes.
    /// </summary>
    public static float MeanWaterQuality(WorldState state, InfluenceMap sewageCoverage)
        => Math.Clamp(1f - MeanWaterContamination(state, sewageCoverage), 0f, 1f);

    /// <summary>
    /// Apply sewage accumulation / treatment to zoned tiles for
    /// <paramref name="days"/>, update <see cref="TileData.SewageConnection"/>,
    /// then write
    /// <see cref="WorldState.SewageCoverageFraction"/>,
    /// <see cref="WorldState.MeanWaterContamination"/>, and
    /// <see cref="WorldState.MeanWaterQuality"/>.
    /// Returns net tiles whose pollution decreased this pass.
    /// </summary>
    public static int Tick(
        WorldState state,
        InfluenceMap sewageCoverage,
        float days = 1f)
    {
        float daysClamped = Math.Max(0f, days);
        float quality = MeanPlantQuality(state);
        int cleaned = 0;

        var tiles = state.Tiles;
        for (int y = 0; y < tiles.Size; y++)
        for (int x = 0; x < tiles.Size; x++)
        {
            int idx = tiles.Index(x, y);
            float coverage = Math.Clamp(sewageCoverage.GetValue(x, y), 0f, 1f);
            bool connected = coverage >= MinCoverageForTreatment;
            tiles.SewageConnection[idx] = connected ? (byte)1 : (byte)0;

            byte zone = tiles.ZoneType[idx];
            if (ZoneSewageLoad(zone) <= 0f) continue;
            if (daysClamped <= 0f) continue;

            float delta = ContaminationDelta(coverage, quality, zone, daysClamped);
            if (delta == 0f) continue;

            float before = tiles.Pollution[idx];
            float after = Math.Clamp(before + delta, 0f, 1f);
            tiles.Pollution[idx] = after;
            if (after < before)
                cleaned++;
        }

        RefreshAggregates(state, sewageCoverage);
        return cleaned;
    }

    public static void RefreshAggregates(WorldState state, InfluenceMap sewageCoverage)
    {
        state.SewageCoverageFraction = CoverageFraction(state, sewageCoverage);
        state.MeanWaterContamination = MeanWaterContamination(state, sewageCoverage);
        state.MeanWaterQuality = MeanWaterQuality(state, sewageCoverage);
    }

    /// <summary>
    /// Immigration attractiveness from mean water quality.
    /// 0.55 at contamination=1 → 1.45 at contamination=0 (neutral ~1.0 at 0.5).
    /// </summary>
    public static float WaterQualityAttractivenessModifier(float meanWaterQuality01)
    {
        float q = Math.Clamp(meanWaterQuality01, 0f, 1f);
        return Math.Clamp(0.55f + q * 0.9f, 0.55f, 1.45f);
    }

    /// <summary>
    /// Residential / commercial / office / mixed-use / industrial generate sewage.
    /// </summary>
    public static float ZoneSewageLoad(byte zoneType) => zoneType switch
    {
        1 or 2 => 1f,                    // Residential low / high
        3 or 5 => CommercialSewageScale, // Commercial / office
        4 => IndustrialSewageScale,      // Industrial process water
        6 => 0.9f,                       // Mixed-use
        _ => 0f,
    };

    private static float DefaultPlantQuality(byte level, byte condition)
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
