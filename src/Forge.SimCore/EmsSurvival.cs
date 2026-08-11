namespace Forge.SimCore;

/// <summary>
/// EMS survival curve from response minutes (Cathedral Phase 5b / P5.4).
/// Buckets from <c>MISSING_SYSTEMS.md</c> §1.2:
/// &lt;5 min → 90%, 5–10 → 70%, 10–15 → 55%, ≥15 → 40%.
/// </summary>
public static class EmsSurvival
{
    public const float SurvivalUnder5Min = 0.90f;
    public const float Survival5To10Min = 0.70f;
    public const float Survival10To15Min = 0.55f;
    public const float SurvivalOver15Min = 0.40f;

    /// <summary>
    /// Survival probability (0–1) given emergency response minutes.
    /// </summary>
    public static float CalculateRate(float responseMinutes)
    {
        if (float.IsNaN(responseMinutes) || float.IsInfinity(responseMinutes) || responseMinutes < 0f)
            return SurvivalOver15Min;

        if (responseMinutes < 5f) return SurvivalUnder5Min;
        if (responseMinutes < 10f) return Survival5To10Min;
        if (responseMinutes < 15f) return Survival10To15Min;
        return SurvivalOver15Min;
    }

    /// <summary>
    /// Default city-wide rate when no zoned samples exist (no-station response minutes).
    /// </summary>
    public static float DefaultMeanRate =>
        CalculateRate(EmergencyResponseTime.NoStationResponseMinutes);
}
