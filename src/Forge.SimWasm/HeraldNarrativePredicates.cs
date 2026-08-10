namespace Forge.SimWasm;

/// <summary>
/// Herald template-bucket predicates shared by WASM/Unity clients.
/// Mirrors <c>web/lib/sim-metrics.ts</c> <c>deriveNarrativeBucket</c> (Cathedral P5.5).
/// </summary>
public enum HeraldNarrativeBucket
{
    BudgetCrisis,
    HousingShortage,
    EconomyShortage,
    HappinessLow,
    ProsperityHigh,
    HealthcareLow,
    Default,
}

/// <summary>
/// Hard gates for Herald unrest / prosperity buckets from live snapshot metrics.
/// </summary>
public static class HeraldNarrativePredicates
{
    /// <summary>Mayor approval below this percent maps to the happiness_low (unrest) Herald bucket.</summary>
    public const float LowHappinessApproval = 45f;

    public const float RciExtremeDemand = 0.65f;
    public const float GoodsShortageThreshold = 0.35f;
    public const float ProsperityApproval = 70f;
    public const long ProsperityFunds = 500_000;

    /// <summary>
    /// Map live sim metrics to a Herald template bucket.
    /// Crisis signals (treasury, goods, approval) take priority over RCI extremes.
    /// </summary>
    public static HeraldNarrativeBucket DeriveBucket(
        float healthcareCoverage,
        float approvalPercent,
        long cityFunds,
        float residentialDemand,
        float commercialDemand,
        float industrialDemand,
        float goodsShortageIndex)
    {
        if (cityFunds < 0)
            return HeraldNarrativeBucket.BudgetCrisis;

        if (goodsShortageIndex >= GoodsShortageThreshold)
        {
            if (residentialDemand >= RciExtremeDemand)
                return HeraldNarrativeBucket.HousingShortage;

            return HeraldNarrativeBucket.EconomyShortage;
        }

        if (approvalPercent < LowHappinessApproval)
            return HeraldNarrativeBucket.HappinessLow;

        if (residentialDemand >= RciExtremeDemand)
            return HeraldNarrativeBucket.HousingShortage;

        if (commercialDemand >= RciExtremeDemand
            && industrialDemand >= RciExtremeDemand
            && cityFunds >= 0)
            return HeraldNarrativeBucket.ProsperityHigh;

        if (healthcareCoverage < 0.3f)
            return HeraldNarrativeBucket.HealthcareLow;

        if (approvalPercent >= ProsperityApproval && cityFunds >= ProsperityFunds)
            return HeraldNarrativeBucket.ProsperityHigh;

        return HeraldNarrativeBucket.Default;
    }

    /// <summary>True when approval alone would select the unrest (happiness_low) bucket.</summary>
    public static bool IsUnrestApproval(float approvalPercent) =>
        approvalPercent < LowHappinessApproval;
}
