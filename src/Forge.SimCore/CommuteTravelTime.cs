using Forge.Engine.Data;
using Forge.Engine.Simulation;

namespace Forge.SimCore;

/// <summary>
/// Road-graph travel time between building tiles (Cathedral P4.2 / SB-4232).
/// Uses BPR edge times from the latest traffic assignment when available;
/// otherwise shortest-path on free-flow graph edge costs; Euclidean tile distance as last resort.
/// </summary>
public static class CommuteTravelTime
{
    private const int DefaultMaxNodeSearchTiles = 12;

    /// <summary>
    /// Travel cost in road-graph units (same scale as BPR edge times).
    /// </summary>
    public static float CalculateTravelCost(
        WorldState state,
        int homeBuildingId,
        int workBuildingId,
        float[]? edgeTravelTimes = null)
    {
        if (!TryGetBuildingTile(state, homeBuildingId, out int homeX, out int homeY) ||
            !TryGetBuildingTile(state, workBuildingId, out int workX, out int workY))
            return float.MaxValue;

        if (homeBuildingId == workBuildingId)
            return 0f;

        int homeNode = ResolveNearestRoadNode(state, homeX, homeY);
        int workNode = ResolveNearestRoadNode(state, workX, workY);

        if (homeNode >= 0 && workNode >= 0 && state.Roads.NodeCount > 0)
        {
            float pathCost = ShortestPathCost(state.Roads, homeNode, workNode, edgeTravelTimes);
            if (pathCost < float.MaxValue)
                return pathCost;
        }

        return EuclideanTileDistance(homeX, homeY, workX, workY);
    }

    /// <summary>Nearest graph node to a tile, searching outward up to <paramref name="maxRadius"/> tiles.</summary>
    public static int ResolveNearestRoadNode(WorldState state, int gridX, int gridY, int maxRadius = DefaultMaxNodeSearchTiles)
    {
        var roads = state.Roads;
        if (roads.NodeCount == 0) return -1;

        int direct = roads.GetNodeAt(gridX, gridY);
        if (direct >= 0) return direct;

        int bestNode = -1;
        float bestDistSq = float.MaxValue;
        float maxDistSq = (maxRadius + 0.5f) * (maxRadius + 0.5f);

        for (int n = 0; n < roads.NodeCount; n++)
        {
            var (nx, ny) = roads.GetNodePosition(n);
            float dx = nx - gridX;
            float dy = ny - gridY;
            float distSq = dx * dx + dy * dy;
            if (distSq > maxDistSq) continue;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestNode = n;
            }
        }

        return bestNode;
    }

    private static float ShortestPathCost(
        RoadGraph graph,
        int startNode,
        int endNode,
        float[]? edgeTravelTimes)
    {
        if (startNode == endNode) return 0f;

        int nodeCount = graph.NodeCount;
        if (nodeCount == 0) return float.MaxValue;

        var dist = new float[nodeCount];
        var visited = new bool[nodeCount];
        Array.Fill(dist, float.MaxValue);
        dist[startNode] = 0f;

        var nodeEdgeStart = BuildNodeEdgeStart(graph);
        bool useEdgeTimes = edgeTravelTimes is { Length: > 0 };

        var pq = new PriorityQueue<int, float>();
        pq.Enqueue(startNode, 0f);

        while (pq.Count > 0)
        {
            int u = pq.Dequeue();
            if (visited[u]) continue;
            visited[u] = true;
            if (u == endNode) break;

            int ei = nodeEdgeStart[u];
            foreach (var (v, freeFlowCost, _) in graph.GetNeighbors(u))
            {
                if (visited[v])
                {
                    ei++;
                    continue;
                }

                float edgeCost = useEdgeTimes && ei < edgeTravelTimes!.Length
                    ? edgeTravelTimes[ei]
                    : freeFlowCost;
                float newDist = dist[u] + edgeCost;
                if (newDist < dist[v])
                {
                    dist[v] = newDist;
                    pq.Enqueue(v, newDist);
                }

                ei++;
            }
        }

        return dist[endNode];
    }

    private static int[] BuildNodeEdgeStart(RoadGraph graph)
    {
        int nodeCount = graph.NodeCount;
        var nodeEdgeStart = new int[nodeCount];
        int runningEdge = 0;
        for (int n = 0; n < nodeCount; n++)
        {
            nodeEdgeStart[n] = runningEdge;
            foreach (var _ in graph.GetNeighbors(n))
                runningEdge++;
        }

        return nodeEdgeStart;
    }

    private static bool TryGetBuildingTile(WorldState state, int buildingId, out int gridX, out int gridY)
    {
        gridX = 0;
        gridY = 0;
        if (buildingId <= 0 || buildingId >= state.Buildings.Capacity) return false;
        if (!state.Buildings.IsActive(buildingId)) return false;

        gridX = state.Buildings.GridX[buildingId];
        gridY = state.Buildings.GridY[buildingId];
        return true;
    }

    private static float EuclideanTileDistance(int ax, int ay, int bx, int by)
    {
        float dx = ax - bx;
        float dy = ay - by;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
