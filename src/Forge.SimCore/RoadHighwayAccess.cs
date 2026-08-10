using Forge.Engine.Data;

namespace Forge.SimCore;

/// <summary>
/// Highway ramp access rules — local roads may only touch highways via ramp connectors (P1.4).
/// </summary>
public static class RoadHighwayAccess
{
    /// <summary>True when placing would give a non-highway tile two or more highway neighbors.</summary>
    public static bool WouldCreateIllegalMerge(TileData tiles, int x, int y, byte newTier)
    {
        if (!tiles.InBounds(x, y)) return false;

        newTier = (byte)(newTier & 0x03);
        bool placingHighway = RoadTier.IsHighwayTier(newTier);

        foreach (var (dx, dy) in NeighborDirs)
        {
            int nx = x + dx;
            int ny = y + dy;
            if (!IsRoad(tiles, nx, ny)) continue;

            byte neighborTier = RoadTier.ExtractLevel(tiles.RoadFlags[tiles.Index(nx, ny)]);
            bool neighborHighway = RoadTier.IsHighwayTier(neighborTier);
            if (placingHighway == neighborHighway) continue;

            if (!placingHighway)
            {
                if (CountHighwayRoadNeighbors(tiles, x, y, placingAt: (x, y, newTier)) > 1)
                    return true;
            }
            else if (CountHighwayRoadNeighbors(tiles, nx, ny, placingAt: (x, y, newTier)) > 1)
            {
                return true;
            }
        }

        return false;
    }

    internal static int CountHighwayRoadNeighbors(
        TileData tiles,
        int x,
        int y,
        (int x, int y, byte tier)? placingAt = null)
    {
        int count = 0;
        foreach (var (dx, dy) in NeighborDirs)
        {
            int nx = x + dx;
            int ny = y + dy;
            if (placingAt is { } place && nx == place.x && ny == place.y)
            {
                if (RoadTier.IsHighwayTier(place.tier))
                    count++;
                continue;
            }

            if (!IsRoad(tiles, nx, ny)) continue;
            if (RoadTier.IsHighwayTier(RoadTier.ExtractLevel(tiles.RoadFlags[tiles.Index(nx, ny)])))
                count++;
        }

        return count;
    }

    private static readonly (int dx, int dy)[] NeighborDirs = [(0, -1), (1, 0), (0, 1), (-1, 0)];

    private static bool IsRoad(TileData tiles, int x, int y)
    {
        if (!tiles.InBounds(x, y)) return false;
        return tiles.RoadFlags[tiles.Index(x, y)] != 0;
    }
}
