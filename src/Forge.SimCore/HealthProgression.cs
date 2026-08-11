using Forge.Engine.Data;
using Forge.Engine.Simulation;

namespace Forge.SimCore;

/// <summary>
/// Tier-2 hospital → household health progression (Cathedral P5 / education analogue).
/// Households under hospital coverage raise <see cref="HouseholdData.HealthSatisfaction"/>
/// over time; uncovered households slowly decay toward a baseline. Park exercise access
/// still lifts the target via <see cref="ParkAmenity.ExerciseContribution"/>. Aggregates
/// feed ResourcesHud Hosp coverage + mean health (P4 sat / migration).
/// </summary>
public static class HealthProgression
{
    /// <summary>ServiceFlags bit — clinic / hospital.</summary>
    public const uint ServiceHealth = 1u << 4;

    /// <summary>Minimum influence coverage to recover toward the hospital target.</summary>
    public const float MinCoverageForRecovery = 0.2f;

    /// <summary>Coverage at or above this suppresses decay.</summary>
    public const float MaxCoverageForDecay = 0.1f;

    /// <summary>Per-day blend rate toward the covered hospital target.</summary>
    public const float RecoveryBlendPerDay = 0.18f;

    /// <summary>Per-day blend rate toward the uncovered baseline.</summary>
    public const float DecayBlendPerDay = 0.06f;

    /// <summary>Mild drift rate when coverage is between decay and recovery thresholds.</summary>
    public const float NeutralBlendPerDay = 0.04f;

    /// <summary>0–1 health floor when uncovered (before pollution / exercise).</summary>
    public const float UncoveredBaseline = 0.35f;

    /// <summary>0–1 health floor when covered before quality × coverage.</summary>
    public const float CoveredBaseTarget = 0.55f;

    /// <summary>Extra 0–1 span at coverage=1, quality=1.</summary>
    public const float CoveredQualitySpan = 0.40f;

    /// <summary>
    /// Target HealthSatisfaction (0–1) under hospital coverage.
    /// </summary>
    public static float RecoveryTarget(
        float coverage,
        float hospitalQuality,
        float pollution,
        float exerciseContribution)
    {
        float cov = Math.Clamp(coverage, 0f, 1f);
        float quality = Math.Clamp(hospitalQuality, 0.3f, 1f);
        float score = CoveredBaseTarget
                      + quality * CoveredQualitySpan * cov
                      - Math.Clamp(pollution, 0f, 1f) * 0.25f
                      + Math.Clamp(exerciseContribution, 0f, ParkAmenity.HealthExerciseWeight);
        return Math.Clamp(score, 0f, 1f);
    }

    /// <summary>
    /// Target HealthSatisfaction (0–1) when hospital coverage is thin / absent.
    /// </summary>
    public static float UncoveredTarget(float pollution, float exerciseContribution)
    {
        float score = UncoveredBaseline
                      - Math.Clamp(pollution, 0f, 1f) * 0.25f
                      + Math.Clamp(exerciseContribution, 0f, ParkAmenity.HealthExerciseWeight);
        return Math.Clamp(score, 0f, 1f);
    }

    /// <summary>
    /// Mean hospital quality (0–1) across active health buildings.
    /// Falls back to 0.6 when none exist.
    /// </summary>
    public static float MeanHospitalQuality(
        WorldState state,
        Func<WorldState, int, float>? qualityAtBuilding = null)
    {
        var buildings = state.Buildings;
        double sum = 0;
        int count = 0;
        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if ((buildings.ServiceFlags[i] & ServiceHealth) == 0) continue;

            float q = qualityAtBuilding?.Invoke(state, i)
                      ?? DefaultHospitalQuality(buildings.Level[i], buildings.Condition[i]);
            sum += Math.Clamp(q, 0f, 1f);
            count++;
        }

        return count == 0 ? 0.6f : (float)(sum / count);
    }

    /// <summary>
    /// Fraction of active households whose home tile has health coverage
    /// ≥ <see cref="MinCoverageForRecovery"/>.
    /// </summary>
    public static float CoverageFraction(WorldState state, InfluenceMap healthCoverage)
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
            if (healthCoverage.GetValue(hx, hy) >= MinCoverageForRecovery)
                covered++;
        }

        return count == 0 ? 0f : covered / (float)count;
    }

    /// <summary>
    /// Mean household HealthSatisfaction mapped to 0–1.
    /// </summary>
    public static float MeanHealthSatisfaction(WorldState state)
        => ParkAmenity.MeanHealthSatisfaction(state);

    /// <summary>
    /// Advance / decay household HealthSatisfaction for <paramref name="days"/>, then write
    /// <see cref="WorldState.HealthCoverageFraction"/> and
    /// <see cref="WorldState.MeanHealthSatisfaction"/>.
    /// Returns net households that gained HealthSatisfaction this pass.
    /// </summary>
    public static int Tick(
        WorldState state,
        InfluenceMap healthCoverage,
        float hospitalQuality,
        float days = 1f)
    {
        float daysClamped = Math.Max(0f, days);
        if (daysClamped <= 0f)
        {
            RefreshAggregates(state, healthCoverage);
            return 0;
        }

        var hh = state.Households;
        float quality = Math.Clamp(hospitalQuality, 0.3f, 1f);
        int improved = 0;

        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;
            if (!TryHomeTile(state, hh.HomeBuildingId[i], out int hx, out int hy))
                continue;

            float coverage = Math.Clamp(healthCoverage.GetValue(hx, hy), 0f, 1f);
            float access = ParkAmenity.LocalParkAccess(state, hx, hy);
            float exercise = ParkAmenity.ExerciseContribution(access);
            int idx = state.Tiles.Index(hx, hy);
            float pollution = Math.Clamp(state.Tiles.Pollution[idx], 0f, 1f);

            float target01;
            float blendPerDay;
            if (coverage >= MinCoverageForRecovery)
            {
                target01 = RecoveryTarget(coverage, quality, pollution, exercise);
                blendPerDay = RecoveryBlendPerDay;
            }
            else if (coverage < MaxCoverageForDecay)
            {
                target01 = UncoveredTarget(pollution, exercise);
                blendPerDay = DecayBlendPerDay;
            }
            else
            {
                // Thin coverage — drift gently toward a soft blend of both regimes.
                float covered = RecoveryTarget(coverage, quality, pollution, exercise);
                float uncovered = UncoveredTarget(pollution, exercise);
                float t = (coverage - MaxCoverageForDecay)
                          / Math.Max(1e-4f, MinCoverageForRecovery - MaxCoverageForDecay);
                target01 = uncovered + (covered - uncovered) * Math.Clamp(t, 0f, 1f);
                blendPerDay = NeutralBlendPerDay;
            }

            float blend = Math.Clamp(blendPerDay * daysClamped, 0f, 1f);
            byte target = (byte)Math.Clamp((int)MathF.Round(target01 * 255f), 0, 255);
            byte before = hh.HealthSatisfaction[i];
            hh.HealthSatisfaction[i] = BlendByte(before, target, blend);
            if (hh.HealthSatisfaction[i] > before)
                improved++;
        }

        RefreshAggregates(state, healthCoverage);
        return improved;
    }

    public static void RefreshAggregates(WorldState state, InfluenceMap healthCoverage)
    {
        state.HealthCoverageFraction = CoverageFraction(state, healthCoverage);
        state.MeanHealthSatisfaction = MeanHealthSatisfaction(state);
    }

    private static float DefaultHospitalQuality(byte level, byte condition)
    {
        float baseQ = 0.3f + level * 0.14f;
        float conditionMod = 0.3f + (condition / 255f) * 0.7f;
        return Math.Clamp(baseQ * conditionMod, 0f, 1f);
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
