using Forge.Engine.Data;

namespace Forge.SimCore;

/// <summary>
/// Builds a segment-based road graph from tile data (SIMULATION_ARCHITECTURE §4, Cathedral P1.2).
/// Intersection nodes sit at dead-ends, T/4-way junctions, and 90° bends; degree-2 straights collapse into edges.
/// </summary>
public static class RoadGraphBuilder
{
    private static readonly (int dx, int dy)[] Dirs = [(0, -1), (1, 0), (0, 1), (-1, 0)];

    /// <summary>Rebuild <paramref name="graph"/> from <paramref name="tiles"/> road layout.</summary>
    public static void Build(TileData tiles, RoadGraph graph)
    {
        graph.Clear();
        int size = tiles.Size;
        var isNode = new bool[tiles.Count];

        // Phase 1: classify intersection / endpoint tiles.
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            if (!IsRoad(tiles, x, y)) continue;
            if (IsNodeTile(tiles, x, y))
                isNode[tiles.Index(x, y)] = true;
        }

        // Phase 2: register nodes.
        var nodeAt = new int[tiles.Count];
        Array.Fill(nodeAt, -1);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int idx = tiles.Index(x, y);
            if (!isNode[idx]) continue;
            nodeAt[idx] = graph.AddNode(x, y);
        }

        // Phase 3: trace segments between nodes and emit CSR edges.
        var edges = new List<(int from, int to, float cost, byte level, int length)>();

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int idx = tiles.Index(x, y);
            if (!isNode[idx]) continue;

            int fromNode = nodeAt[idx];
            foreach (var (dx, dy) in Dirs)
            {
                if (!IsRoad(tiles, x + dx, y + dy)) continue;

                var (toX, toY, length, level) = TraceSegment(tiles, isNode, x, y, dx, dy);
                int toIdx = tiles.Index(toX, toY);
                if (!isNode[toIdx]) continue;

                int toNode = nodeAt[toIdx];
                if (toNode < 0) continue;

                float cost = length * RoadTier.RoadTravelCostForLevel(level);
                edges.Add((fromNode, toNode, cost, level, length));
            }
        }

        graph.BuildFromEdgeList(edges);
    }

    /// <summary>
    /// Node if degree ≥ 3, dead-end (degree ≤ 1), or degree-2 corner (neighbors not opposite).
    /// </summary>
    internal static bool IsNodeTile(TileData tiles, int x, int y)
    {
        int degree = CountRoadNeighbors(tiles, x, y);
        if (degree <= 1) return true;
        if (degree >= 3) return true;

        // Degree 2: straight-through tiles are collapsed; corners remain nodes.
        Span<(int nx, int ny)> neighbors = stackalloc (int, int)[4];
        int n = 0;
        foreach (var (dx, dy) in Dirs)
        {
            int nx = x + dx;
            int ny = y + dy;
            if (!IsRoad(tiles, nx, ny)) continue;
            neighbors[n++] = (nx, ny);
        }

        int dx1 = neighbors[0].nx - x;
        int dy1 = neighbors[0].ny - y;
        int dx2 = neighbors[1].nx - x;
        int dy2 = neighbors[1].ny - y;
        bool opposite = dx1 == -dx2 && dy1 == -dy2;
        return !opposite;
    }

    private static (int toX, int toY, int length, byte minLevel) TraceSegment(
        TileData tiles,
        bool[] isNode,
        int startX,
        int startY,
        int dirX,
        int dirY)
    {
        byte minLevel = RoadTier.ExtractLevel(tiles.RoadFlags[tiles.Index(startX, startY)]);
        int prevX = startX;
        int prevY = startY;
        int x = startX + dirX;
        int y = startY + dirY;
        int length = 0;

        while (IsRoad(tiles, x, y))
        {
            length++;
            int idx = tiles.Index(x, y);
            byte tier = RoadTier.ExtractLevel(tiles.RoadFlags[idx]);
            if (tier < minLevel) minLevel = tier;

            if (isNode[idx])
                return (x, y, length, minLevel);

            int nextX = -1;
            int nextY = -1;
            foreach (var (dx, dy) in Dirs)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (nx == prevX && ny == prevY) continue;
                if (!IsRoad(tiles, nx, ny)) continue;
                nextX = nx;
                nextY = ny;
                break;
            }

            if (nextX < 0)
                return (x, y, length, minLevel);

            prevX = x;
            prevY = y;
            x = nextX;
            y = nextY;
        }

        return (prevX, prevY, length, minLevel);
    }

    private static int CountRoadNeighbors(TileData tiles, int x, int y)
    {
        int count = 0;
        foreach (var (dx, dy) in Dirs)
        {
            if (IsRoad(tiles, x + dx, y + dy))
                count++;
        }

        return count;
    }

    private static bool IsRoad(TileData tiles, int x, int y)
    {
        if (!tiles.InBounds(x, y)) return false;
        return tiles.RoadFlags[tiles.Index(x, y)] != 0;
    }
}
