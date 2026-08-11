using Forge.Engine.Data;
using Forge.Engine.Simulation;

namespace Forge.SimCore;

/// <summary>
/// Tier-2 education depth (Cathedral P5 diagram S3 / <c>MISSING_SYSTEMS</c> cultural
/// services). Households under school coverage raise <see cref="HouseholdData.Education"/>
/// over time; uncovered households slowly decay. Aggregates feed research RP via
/// <see cref="ResearchEducationMultiplier"/> and ResourcesHud Edu line.
/// </summary>
public static class EducationProgression
{
    /// <summary>ServiceFlags bit — school / college / university.</summary>
    public const uint ServiceEducation = 1u << 5;

    /// <summary>Household education levels: 0 none … 3 university.</summary>
    public const byte MaxEducationLevel = 3;

    /// <summary>Minimum influence coverage to attempt an upgrade.</summary>
    public const float MinCoverageForUpgrade = 0.2f;

    /// <summary>Coverage at or above this suppresses decay.</summary>
    public const float MaxCoverageForDecay = 0.1f;

    /// <summary>
    /// Daily upgrade chance at coverage=1, quality=1, level=0.
    /// Higher levels are harder (× remaining room to max).
    /// </summary>
    public const float BaseUpgradeChancePerDay = 0.08f;

    /// <summary>Daily decay chance at coverage=0 for level &gt; 0.</summary>
    public const float BaseDecayChancePerDay = 0.01f;

    /// <summary>Research RP mult at mean education 0.</summary>
    public const float MinResearchEducationMult = 0.70f;

    /// <summary>Research RP mult at mean education 3.</summary>
    public const float MaxResearchEducationMult = 1.30f;

    /// <summary>
    /// Per-day upgrade probability for one household.
    /// </summary>
    public static float UpgradeChance(float coverage, float schoolQuality, byte level)
    {
        if (level >= MaxEducationLevel) return 0f;
        if (coverage < MinCoverageForUpgrade) return 0f;

        float cov = Math.Clamp(coverage, 0f, 1f);
        float quality = Math.Clamp(schoolQuality, 0.3f, 1f);
        float room = 1f - level / (float)MaxEducationLevel;
        return Math.Clamp(BaseUpgradeChancePerDay * cov * quality * room, 0f, 1f);
    }

    /// <summary>
    /// Per-day decay probability when coverage is thin.
    /// </summary>
    public static float DecayChance(float coverage, byte level)
    {
        if (level == 0) return 0f;
        if (coverage >= MaxCoverageForDecay) return 0f;

        float deficit = 1f - Math.Clamp(coverage, 0f, MaxCoverageForDecay) / MaxCoverageForDecay;
        return Math.Clamp(BaseDecayChancePerDay * deficit, 0f, 1f);
    }

    /// <summary>
    /// Research funding multiplier from city mean education (0–3 → 0.70–1.30).
    /// </summary>
    public static float ResearchEducationMultiplier(float meanEducationLevel)
    {
        float t = Math.Clamp(meanEducationLevel, 0f, MaxEducationLevel) / MaxEducationLevel;
        return MinResearchEducationMult
               + t * (MaxResearchEducationMult - MinResearchEducationMult);
    }

    /// <summary>
    /// Mean household education level over active slots (0–3). Returns 0 when empty.
    /// </summary>
    public static float MeanEducationLevel(WorldState state)
    {
        var hh = state.Households;
        double sum = 0;
        int count = 0;
        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;
            sum += hh.Education[i];
            count++;
        }

        return count == 0 ? 0f : (float)(sum / count);
    }

    /// <summary>
    /// Fraction of active households whose home tile has education coverage
    /// ≥ <see cref="MinCoverageForUpgrade"/>.
    /// </summary>
    public static float CoverageFraction(WorldState state, InfluenceMap educationCoverage)
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
            if (educationCoverage.GetValue(hx, hy) >= MinCoverageForUpgrade)
                covered++;
        }

        return count == 0 ? 0f : covered / (float)count;
    }

    /// <summary>
    /// Mean school quality (0–1) across active education buildings.
    /// Falls back to 0.6 when no schools exist (caller usually skips upgrades then).
    /// </summary>
    public static float MeanSchoolQuality(
        WorldState state,
        Func<WorldState, int, float>? qualityAtBuilding = null)
    {
        var buildings = state.Buildings;
        double sum = 0;
        int count = 0;
        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if ((buildings.ServiceFlags[i] & ServiceEducation) == 0) continue;

            float q = qualityAtBuilding?.Invoke(state, i)
                      ?? DefaultSchoolQuality(buildings.Level[i], buildings.Condition[i]);
            sum += Math.Clamp(q, 0f, 1f);
            count++;
        }

        return count == 0 ? 0.6f : (float)(sum / count);
    }

    /// <summary>
    /// Advance / decay household education for <paramref name="days"/>, then write
    /// <see cref="WorldState.MeanEducationLevel"/> and
    /// <see cref="WorldState.EducationCoverageFraction"/>.
    /// Returns net level-ups minus level-downs.
    /// </summary>
    public static int Tick(
        WorldState state,
        InfluenceMap educationCoverage,
        float schoolQuality,
        float days = 1f,
        Random? rng = null)
    {
        rng ??= new Random();
        float daysClamped = Math.Max(0f, days);
        if (daysClamped <= 0f)
        {
            RefreshAggregates(state, educationCoverage);
            return 0;
        }

        var hh = state.Households;
        float quality = Math.Clamp(schoolQuality, 0.3f, 1f);
        int delta = 0;

        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;

            float coverage = 0f;
            if (TryHomeTile(state, hh.HomeBuildingId[i], out int hx, out int hy))
                coverage = Math.Clamp(educationCoverage.GetValue(hx, hy), 0f, 1f);

            byte level = hh.Education[i];
            float upgrade = UpgradeChance(coverage, quality, level);
            float decay = DecayChance(coverage, level);

            // Scale one-shot daily chances across multi-day ticks without exploding.
            float upgradeP = 1f - MathF.Pow(1f - upgrade, daysClamped);
            float decayP = 1f - MathF.Pow(1f - decay, daysClamped);

            if (upgradeP > 0f && rng.NextDouble() < upgradeP && level < MaxEducationLevel)
            {
                hh.Education[i] = (byte)(level + 1);
                delta++;
                continue; // one step per tick pass
            }

            if (decayP > 0f && rng.NextDouble() < decayP && level > 0)
            {
                hh.Education[i] = (byte)(level - 1);
                delta--;
            }
        }

        RefreshAggregates(state, educationCoverage);
        return delta;
    }

    public static void RefreshAggregates(WorldState state, InfluenceMap educationCoverage)
    {
        state.MeanEducationLevel = MeanEducationLevel(state);
        state.EducationCoverageFraction = CoverageFraction(state, educationCoverage);
    }

    private static float DefaultSchoolQuality(byte level, byte condition)
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
