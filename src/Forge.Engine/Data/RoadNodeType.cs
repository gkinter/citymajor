namespace Forge.Engine.Data;

/// <summary>
/// Intersection topology for segment-graph nodes (SIMULATION_ARCHITECTURE §4, Cathedral P1.4).
/// </summary>
public enum RoadNodeType : byte
{
    Intersection = 0,
    DeadEnd = 1,
    Corner = 2,
    Ramp = 3,
    HighwayOn = 4,
    HighwayOff = 5,
}
