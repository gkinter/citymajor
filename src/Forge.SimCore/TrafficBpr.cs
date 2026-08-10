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
}
