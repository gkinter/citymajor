namespace Forge.SimCore;

/// <summary>
/// Bureau of Public Roads (BPR) edge travel time — Cathedral P4.1 foundation.
/// t = t0 * (1 + α * (flow/capacity)^β) with α=0.15, β=4 (SIMULATION_ARCHITECTURE §4).
/// </summary>
public static class TrafficBpr
{
    public const float Alpha = 0.15f;
    public const float Beta = 4f;

    /// <summary>Travel time from free-flow time, assigned volume, and edge capacity.</summary>
    public static float CalculateTravelTime(float freeFlowTime, float volume, float capacity)
    {
        if (capacity <= 0f) return freeFlowTime * 10f;
        float vc = volume / capacity;
        float vc2 = vc * vc;
        float vc4 = vc2 * vc2;
        return freeFlowTime * (1f + Alpha * vc4);
    }

    /// <summary>Edge capacity from P1.2 tier table × lane count × law multiplier.</summary>
    public static float EdgeCapacity(byte level, float lanes, float lawCapacityMult = 1f)
    {
        float lawMult = lawCapacityMult > 0f ? lawCapacityMult : 1f;
        return RoadTier.RoadCapacityForLevel(level) * lanes * lawMult;
    }

    /// <summary>
    /// Frank-Wolfe relative gap: (TSTT − SPTT) / TSTT using marginal edge times.
    /// TSTT = Σ t_e·x_e; SPTT = Σ t_e·y_e with auxiliary all-or-nothing flows y.
    /// </summary>
    public static float FrankWolfeRelativeGap(
        ReadOnlySpan<float> edgeTimes,
        ReadOnlySpan<float> currentVolumes,
        ReadOnlySpan<float> auxVolumes)
    {
        int n = Math.Min(edgeTimes.Length, Math.Min(currentVolumes.Length, auxVolumes.Length));
        double tstt = 0;
        double sptt = 0;
        for (int e = 0; e < n; e++)
        {
            float t = edgeTimes[e];
            tstt += t * currentVolumes[e];
            sptt += t * auxVolumes[e];
        }

        if (tstt <= 0d) return 0f;
        return (float)Math.Max(0d, (tstt - sptt) / tstt);
    }
}
