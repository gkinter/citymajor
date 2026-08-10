using Forge.Engine.Data;
using Forge.Engine.Simulation;

namespace Forge.SimCore;

/// <summary>
/// Emergency vehicle response time in minutes (Cathedral P5.2 / SB-4242).
/// Travel = road-graph shortest path using BPR edge times when available;
/// Euclidean tile distance / vehicle speed as fallback. Plus crew readiness.
/// </summary>
public static class EmergencyResponseTime
{
    public const uint ServiceFire = 1u << 3;
    public const uint ServiceHealth = 1u << 4;

    /// <summary>Fire-truck / ambulance free-flow speed in tiles per minute.</summary>
    public const float DefaultVehicleSpeedTilesPerMinute = 2f;

    /// <summary>Base dispatch delay before the vehicle leaves the station.</summary>
    public const float DefaultCrewReadinessMinutes = 1f;

    /// <summary>Response when no matching station exists in the city.</summary>
    public const float NoStationResponseMinutes = 30f;

    /// <summary>
    /// Minutes from the nearest station matching <paramref name="stationServiceFlag"/>
    /// to the incident tile. Congested BPR edge times increase travel minutes.
    /// </summary>
    public static float CalculateMinutes(
        WorldState state,
        int incidentX,
        int incidentY,
        uint stationServiceFlag = ServiceFire,
        float[]? edgeTravelTimes = null,
        float vehicleSpeedTilesPerMinute = DefaultVehicleSpeedTilesPerMinute,
        float crewReadinessMinutes = DefaultCrewReadinessMinutes)
    {
        if (!state.Tiles.InBounds(incidentX, incidentY))
            return float.MaxValue;

        if (!TryFindNearestStation(
                state, incidentX, incidentY, stationServiceFlag,
                out int stationX, out int stationY, out float euclideanDist))
            return NoStationResponseMinutes;

        float pathCost = CalculatePathCost(
            state, stationX, stationY, incidentX, incidentY, edgeTravelTimes, euclideanDist);

        float speed = Math.Max(0.1f, vehicleSpeedTilesPerMinute);
        return pathCost / speed + Math.Max(0f, crewReadinessMinutes);
    }

    /// <summary>
    /// Road-graph travel cost (free-flow or BPR units ≈ tile-hops under free flow)
    /// from station tile to incident tile.
    /// </summary>
    public static float CalculatePathCost(
        WorldState state,
        int fromX,
        int fromY,
        int toX,
        int toY,
        float[]? edgeTravelTimes = null,
        float? euclideanFallback = null)
    {
        if (fromX == toX && fromY == toY)
            return 0f;

        int fromNode = CommuteTravelTime.ResolveNearestRoadNode(state, fromX, fromY);
        int toNode = CommuteTravelTime.ResolveNearestRoadNode(state, toX, toY);

        if (fromNode >= 0 && toNode >= 0 && state.Roads.NodeCount > 0)
        {
            float pathCost = ShortestPathCost(state.Roads, fromNode, toNode, edgeTravelTimes);
            if (pathCost < float.MaxValue)
                return pathCost;
        }

        return euclideanFallback ?? EuclideanTileDistance(fromX, fromY, toX, toY);
    }

    /// <summary>Nearest active building whose ServiceFlags include <paramref name="stationServiceFlag"/>.</summary>
    public static bool TryFindNearestStation(
        WorldState state,
        int tileX,
        int tileY,
        uint stationServiceFlag,
        out int stationX,
        out int stationY,
        out float euclideanDistance)
    {
        stationX = 0;
        stationY = 0;
        euclideanDistance = float.MaxValue;

        var buildings = state.Buildings;
        float bestDistSq = float.MaxValue;
        int bestX = 0;
        int bestY = 0;
        bool found = false;

        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if ((buildings.ServiceFlags[i] & stationServiceFlag) == 0) continue;

            float dx = buildings.GridX[i] - tileX;
            float dy = buildings.GridY[i] - tileY;
            float distSq = dx * dx + dy * dy;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestX = buildings.GridX[i];
                bestY = buildings.GridY[i];
                found = true;
            }
        }

        if (!found) return false;

        stationX = bestX;
        stationY = bestY;
        euclideanDistance = MathF.Sqrt(bestDistSq);
        return true;
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

    private static float EuclideanTileDistance(int ax, int ay, int bx, int by)
    {
        float dx = ax - bx;
        float dy = ay - by;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
