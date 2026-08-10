namespace Forge.SimCore;

/// <summary>
/// Road tier helpers — tile RoadFlags bits 4–5 and graph edge level/capacity (SIMULATION_ARCHITECTURE §4).
/// </summary>
public static class RoadTier
{
    /// <summary>Extract road level from tile RoadFlags bits 4–5 (0=dirt, 1=paved, 2=highway).</summary>
    public static byte ExtractLevel(byte roadFlags) => (byte)((roadFlags >> 4) & 0x03);

    /// <summary>Capacity per lane in vehicles/hour (dirt 200, paved 800, highway 2200).</summary>
    public static float RoadCapacityForLevel(byte level) => level switch
    {
        0 => 200f,
        1 => 800f,
        2 => 2200f,
        _ => 800f,
    };

    /// <summary>Free-flow travel cost multiplier for pathfinding / BPR free-flow time.</summary>
    public static float RoadTravelCostForLevel(byte level) => level switch
    {
        0 => 1.5f,
        1 => 1.0f,
        2 => 0.7f,
        _ => 1.0f,
    };

    /// <summary>Edge level from two adjacent road tiles — bottleneck (min tier).</summary>
    public static byte EdgeLevelFromTiles(byte flagsA, byte flagsB) =>
        Math.Min(ExtractLevel(flagsA), ExtractLevel(flagsB));
}
