using Forge.Engine.Data;
using Forge.Engine.Simulation;

namespace Forge.SimCore;

/// <summary>
/// Tier-2 police stations → crime → city outcomes (Cathedral P5 / health analogue).
/// Station quality deepens the police term in the AGENT_06 crime formula; households
/// blend <see cref="HouseholdData.SafetySatisfaction"/> toward local (1 − crime).
/// Aggregates feed ResourcesHud Police + P4 immigration attractiveness. Arson /
/// decline already read tile crime — better stations lower those risk paths too.
/// </summary>
public static class PoliceCrime
{
    /// <summary>ServiceFlags bit — police station.</summary>
    public const uint ServicePolice = 1u << 2;

    /// <summary>Minimum influence coverage counted as "covered" for HUD fraction.</summary>
    public const float MinCoverageForSafety = 0.2f;

    /// <summary>Per-day blend rate of SafetySatisfaction toward local safety target.</summary>
    public const float SafetyBlendPerDay = 0.22f;

    /// <summary>AGENT_06 police weight in crime formula.</summary>
    public const float CrimePoliceWeight = 0.7f;

    public const float CrimeUnemploymentWeight = 0.5f;
    public const float CrimePovertyWeight = 0.3f;
    public const float CrimeEducationWeight = 0.2f;
    public const float CrimeLightingWeight = 0.1f;
    public const float CrimeAbandonedWeight = 0.4f;
    public const float BaseCrime = 0.3f;

    /// <summary>
    /// Effective police factor (0–1) for the crime formula.
    /// Quality scales coverage: poor stations (~0.3) ≈ 65% effect; excellent (1.0) = full coverage.
    /// </summary>
    public static float EffectivePoliceFactor(float coverage, float policeQuality)
    {
        float cov = Math.Clamp(coverage, 0f, 1f);
        float q = Math.Clamp(policeQuality, 0.3f, 1f);
        float qualityScale = 0.5f + 0.5f * q;
        return Math.Clamp(cov * qualityScale, 0f, 1f);
    }

    /// <summary>
    /// Crime rate 0–1 from AGENT_06 factors with quality-weighted police coverage.
    /// </summary>
    public static float CalculateCrimeRate(
        float policeCoverage,
        float policeQuality,
        float unemployment,
        float poverty,
        float education,
        float lighting,
        float abandoned)
    {
        float police = EffectivePoliceFactor(policeCoverage, policeQuality);
        float crime = BaseCrime
                      * (1f - police * CrimePoliceWeight)
                      * (1f + Math.Clamp(unemployment, 0f, 1f) * CrimeUnemploymentWeight)
                      * (1f + Math.Clamp(poverty, 0f, 1f) * CrimePovertyWeight)
                      * (1f - Math.Clamp(education, 0f, 1f) * CrimeEducationWeight)
                      * (1f - Math.Clamp(lighting, 0f, 1f) * CrimeLightingWeight)
                      * (1f + Math.Clamp(abandoned, 0f, 1f) * CrimeAbandonedWeight);
        return Math.Clamp(crime, 0f, 1f);
    }

    /// <summary>
    /// Mean police-station quality (0–1) across active police buildings.
    /// Falls back to 0.6 when none exist.
    /// </summary>
    public static float MeanPoliceQuality(
        WorldState state,
        Func<WorldState, int, float>? qualityAtBuilding = null)
    {
        var buildings = state.Buildings;
        double sum = 0;
        int count = 0;
        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if ((buildings.ServiceFlags[i] & ServicePolice) == 0) continue;

            float q = qualityAtBuilding?.Invoke(state, i)
                      ?? DefaultPoliceQuality(buildings.Level[i], buildings.Condition[i]);
            sum += Math.Clamp(q, 0f, 1f);
            count++;
        }

        return count == 0 ? 0.6f : (float)(sum / count);
    }

    /// <summary>
    /// Fraction of active households whose home tile has police coverage
    /// ≥ <see cref="MinCoverageForSafety"/>.
    /// </summary>
    public static float CoverageFraction(WorldState state, InfluenceMap policeCoverage)
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
            if (policeCoverage.GetValue(hx, hy) >= MinCoverageForSafety)
                covered++;
        }

        return count == 0 ? 0f : covered / (float)count;
    }

    /// <summary>
    /// Mean tile crime (0–1) at active household home tiles.
    /// </summary>
    public static float MeanCrimeRate(WorldState state)
    {
        var hh = state.Households;
        double sum = 0;
        int count = 0;

        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;
            if (!TryHomeTile(state, hh.HomeBuildingId[i], out int hx, out int hy))
                continue;
            sum += Math.Clamp(state.Tiles.Crime[state.Tiles.Index(hx, hy)], 0f, 1f);
            count++;
        }

        return count == 0 ? 0f : (float)(sum / count);
    }

    /// <summary>
    /// Mean household SafetySatisfaction mapped to 0–1.
    /// </summary>
    public static float MeanSafetySatisfaction(WorldState state)
    {
        var hh = state.Households;
        double sum = 0;
        int count = 0;
        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;
            sum += hh.SafetySatisfaction[i] / 255.0;
            count++;
        }

        return count == 0 ? 0f : (float)(sum / count);
    }

    /// <summary>
    /// Blend household SafetySatisfaction toward local (1 − crime) for <paramref name="days"/>,
    /// then write <see cref="WorldState.PoliceCoverageFraction"/>,
    /// <see cref="WorldState.MeanCrimeRate"/>, and
    /// <see cref="WorldState.MeanSafetySatisfaction"/>.
    /// Returns net households that gained SafetySatisfaction this pass.
    /// </summary>
    public static int Tick(
        WorldState state,
        InfluenceMap policeCoverage,
        float days = 1f)
    {
        float daysClamped = Math.Max(0f, days);
        if (daysClamped <= 0f)
        {
            RefreshAggregates(state, policeCoverage);
            return 0;
        }

        var hh = state.Households;
        float blend = Math.Clamp(SafetyBlendPerDay * daysClamped, 0f, 1f);
        int improved = 0;

        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;
            if (!TryHomeTile(state, hh.HomeBuildingId[i], out int hx, out int hy))
                continue;

            float crime = Math.Clamp(state.Tiles.Crime[state.Tiles.Index(hx, hy)], 0f, 1f);
            float target01 = Math.Clamp(1f - crime, 0f, 1f);
            byte target = (byte)Math.Clamp((int)MathF.Round(target01 * 255f), 0, 255);
            byte before = hh.SafetySatisfaction[i];
            hh.SafetySatisfaction[i] = BlendByte(before, target, blend);
            if (hh.SafetySatisfaction[i] > before)
                improved++;
        }

        RefreshAggregates(state, policeCoverage);
        return improved;
    }

    public static void RefreshAggregates(WorldState state, InfluenceMap policeCoverage)
    {
        state.PoliceCoverageFraction = CoverageFraction(state, policeCoverage);
        state.MeanCrimeRate = MeanCrimeRate(state);
        state.MeanSafetySatisfaction = MeanSafetySatisfaction(state);
    }

    /// <summary>
    /// Immigration attractiveness from mean HH safety (police → crime → SafetySatisfaction).
    /// 0.55 at safety=0 → 1.45 at safety=1 (neutral ~1.0 at 128/255).
    /// </summary>
    public static float SafetyAttractivenessModifier(float meanSafety01)
    {
        float safety = Math.Clamp(meanSafety01, 0f, 1f);
        return Math.Clamp(0.55f + safety * 0.9f, 0.55f, 1.45f);
    }

    private static float DefaultPoliceQuality(byte level, byte condition)
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
