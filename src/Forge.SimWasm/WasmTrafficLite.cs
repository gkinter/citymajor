using Forge.Engine.Simulation;
using Forge.Game.Simulation;

namespace Forge.SimWasm;

/// <summary>
/// Lightweight BPR traffic for browser WASM (SB-3685 partial).
/// Runs Frank-Wolfe on a 64-zone grid with at most 4 iterations, scheduled at
/// <see cref="WasmConfig.TrafficLiteInterval"/> — never every 8 Hz sim tick.
/// </summary>
public sealed class WasmTrafficLite
{
    private const float BaseLaneCapacity = 50f;
    private const float DefaultCarShare = 0.65f;

    private int _zoneSize;
    private int _zonesPerAxis;
    private int _totalZones;
    private int _worldSize;
    private bool _initialized;

    private float[]? _odMatrix;
    private float[]? _edgeVolume;
    private float[]? _edgeCapacity;
    private float[]? _edgeFreeFlow;
    private int[]? _zoneCentroidNode;
    private float[]? _zoneDistanceCache;
    private int _edgeCount;
    private int _edgeBatchCursor;

    /// <summary>
    /// Cached node → first-outgoing-edge offset. Invalidated only when
    /// <see cref="UpdateEdgeData"/> rebuilds the edge table (roads changed);
    /// avoids rebuilding this on every Dijkstra invocation during
    /// AssignAllOrNothing, which is O(V + E) per O-D pair.
    /// </summary>
    private int[]? _nodeEdgeStart;

    public float[] EdgeCongestion { get; private set; } = Array.Empty<float>();

    public void Tick(WorldState state, double dt)
    {
        _ = dt;

        if (!_initialized || state.Tiles.Size != _worldSize)
            Initialize(state);

        if (_totalZones == 0 || state.Roads.NodeCount == 0 || _edgeCount == 0)
            return;

        BuildLiteOdMatrix(state);
        RunFrankWolfeAssignment(state);
        UpdateTileTraffic(state);
    }

    /// <summary>
    /// Cheap BPR refresh on a rotating subset of edges — called between full lite ticks
    /// to keep congestion overlay responsive without re-running O-D / Frank-Wolfe.
    /// </summary>
    public void TickEdgeBatch(WorldState state, double dt)
    {
        _ = dt;

        if (!_initialized || _edgeCount == 0 || _edgeVolume == null ||
            _edgeCapacity == null || _edgeFreeFlow == null)
            return;

        int batchSize = Math.Max(1, _edgeCount / WasmConfig.TrafficLiteEdgeBatchCount);
        int start = _edgeBatchCursor * batchSize;
        int end = Math.Min(_edgeCount, start + batchSize);
        _edgeBatchCursor = (_edgeBatchCursor + 1) % WasmConfig.TrafficLiteEdgeBatchCount;

        for (int e = start; e < end; e++)
        {
            float cap = _edgeCapacity[e];
            EdgeCongestion[e] = cap > 0 ? _edgeVolume[e] / cap : 0f;
        }

        UpdateTileTrafficSubset(state, start, end);
    }

    private void Initialize(WorldState state)
    {
        _worldSize = state.Tiles.Size;
        int targetZones = WasmConfig.TrafficLiteZoneCount;
        _zoneSize = Math.Max(1, _worldSize / (int)MathF.Ceiling(MathF.Sqrt(targetZones)));
        _zonesPerAxis = (_worldSize + _zoneSize - 1) / _zoneSize;
        _totalZones = _zonesPerAxis * _zonesPerAxis;

        _odMatrix = new float[_totalZones * _totalZones];
        _zoneCentroidNode = new int[_totalZones];
        _zoneDistanceCache = new float[_totalZones * _totalZones];
        _edgeBatchCursor = 0;

        UpdateZoneCentroids(state);
        UpdateEdgeData(state);
        ComputeZoneDistances(state);

        _initialized = true;
    }

    private void UpdateZoneCentroids(WorldState state)
    {
        if (_zoneCentroidNode == null) return;

        for (int zy = 0; zy < _zonesPerAxis; zy++)
        {
            for (int zx = 0; zx < _zonesPerAxis; zx++)
            {
                int zoneIdx = zy * _zonesPerAxis + zx;
                int centerX = zx * _zoneSize + _zoneSize / 2;
                int centerY = zy * _zoneSize + _zoneSize / 2;

                int bestNode = -1;
                float bestDist = float.MaxValue;

                for (int n = 0; n < state.Roads.NodeCount; n++)
                {
                    var (nx, ny) = state.Roads.GetNodePosition(n);
                    float dx = nx - centerX;
                    float dy = ny - centerY;
                    float d = dx * dx + dy * dy;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestNode = n;
                    }
                }

                _zoneCentroidNode[zoneIdx] = bestNode;
            }
        }
    }

    private void UpdateEdgeData(WorldState state)
    {
        _edgeCount = state.Roads.EdgeCount;
        int nodeCount = state.Roads.NodeCount;

        if (_edgeCount == 0)
        {
            _edgeVolume = Array.Empty<float>();
            _edgeCapacity = Array.Empty<float>();
            _edgeFreeFlow = Array.Empty<float>();
            EdgeCongestion = Array.Empty<float>();
            _nodeEdgeStart = nodeCount > 0 ? new int[nodeCount] : Array.Empty<int>();
            return;
        }

        _edgeVolume = new float[_edgeCount];
        _edgeCapacity = new float[_edgeCount];
        _edgeFreeFlow = new float[_edgeCount];
        EdgeCongestion = new float[_edgeCount];
        _nodeEdgeStart = new int[nodeCount];

        int edgeIdx = 0;
        for (int n = 0; n < nodeCount; n++)
        {
            _nodeEdgeStart[n] = edgeIdx;
            foreach (var (_, cost, level) in state.Roads.GetNeighbors(n))
            {
                if (edgeIdx >= _edgeCount) break;

                float lanes = level switch
                {
                    0 => 1f,
                    1 => 2f,
                    2 => 4f,
                    _ => 2f,
                };
                _edgeCapacity[edgeIdx] = BaseLaneCapacity * lanes;
                _edgeFreeFlow[edgeIdx] = cost;
                edgeIdx++;
            }
        }
    }

    private int GetZoneForTile(int tileX, int tileY)
    {
        int zx = Math.Clamp(tileX / _zoneSize, 0, _zonesPerAxis - 1);
        int zy = Math.Clamp(tileY / _zoneSize, 0, _zonesPerAxis - 1);
        return zy * _zonesPerAxis + zx;
    }

    /// <summary>
    /// Gravity-style O-D from zone building counts — WASM households lack home/work links.
    /// </summary>
    private void BuildLiteOdMatrix(WorldState state)
    {
        if (_odMatrix == null) return;
        Array.Clear(_odMatrix);

        var zonePop = new float[_totalZones];
        var zoneJobs = new float[_totalZones];
        var buildings = state.Buildings;
        var tiles = state.Tiles;

        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i) || buildings.State[i] != 1) continue;

            int bx = buildings.GridX[i];
            int by = buildings.GridY[i];
            if (!tiles.InBounds(bx, by)) continue;

            int zone = GetZoneForTile(bx, by);
            byte tileZone = tiles.ZoneType[tiles.Index(bx, by)];
            int occ = buildings.Occupants[i];

            if (tileZone == 1)
                zonePop[zone] += Math.Max(1, occ);
            else if (tileZone is 2 or 3 or 4)
                zoneJobs[zone] += Math.Max(1, occ);
        }

        for (int origin = 0; origin < _totalZones; origin++)
        {
            if (zonePop[origin] <= 0) continue;

            for (int dest = 0; dest < _totalZones; dest++)
            {
                if (origin == dest || zoneJobs[dest] <= 0) continue;

                float dist = _zoneDistanceCache![origin * _totalZones + dest];
                if (dist <= 0) dist = 1f;

                float trips = zonePop[origin] * zoneJobs[dest] / (dist * dist);
                if (trips < 0.01f) continue;

                _odMatrix[origin * _totalZones + dest] += trips;
                _odMatrix[dest * _totalZones + origin] += trips * 0.5f;
            }
        }
    }

    private void RunFrankWolfeAssignment(WorldState state)
    {
        if (_edgeVolume == null || _edgeCapacity == null || _edgeFreeFlow == null ||
            _odMatrix == null || _zoneCentroidNode == null)
            return;

        Array.Clear(_edgeVolume);
        var auxVolume = new float[_edgeCount];
        int maxIter = WasmConfig.TrafficLiteFrankWolfeIterations;

        for (int iteration = 0; iteration < maxIter; iteration++)
        {
            var currentTimes = new float[_edgeCount];
            for (int e = 0; e < _edgeCount; e++)
            {
                currentTimes[e] = TrafficSystem.CalculateBprTravelTime(
                    _edgeFreeFlow[e], _edgeVolume[e], _edgeCapacity[e]);
            }

            Array.Clear(auxVolume);
            AssignAllOrNothing(state, auxVolume, currentTimes);

            float lambda = 2f / (iteration + 2);
            for (int e = 0; e < _edgeCount; e++)
                _edgeVolume[e] = (1f - lambda) * _edgeVolume[e] + lambda * auxVolume[e];
        }

        for (int e = 0; e < _edgeCount; e++)
        {
            float cap = _edgeCapacity[e];
            EdgeCongestion[e] = cap > 0 ? _edgeVolume[e] / cap : 0f;
        }
    }

    private void AssignAllOrNothing(WorldState state, float[] targetVolume, float[] edgeTimes)
    {
        if (_odMatrix == null || _zoneCentroidNode == null) return;

        for (int origin = 0; origin < _totalZones; origin++)
        {
            int originNode = _zoneCentroidNode[origin];
            if (originNode < 0) continue;

            for (int dest = 0; dest < _totalZones; dest++)
            {
                if (origin == dest) continue;

                float trips = _odMatrix[origin * _totalZones + dest];
                if (trips <= 0) continue;

                float carTrips = trips * DefaultCarShare;
                if (carTrips < 0.01f) continue;

                int destNode = _zoneCentroidNode[dest];
                if (destNode < 0) continue;

                var path = FindShortestPathEdges(state, originNode, destNode, edgeTimes, _nodeEdgeStart);
                foreach (int edgeIdx in path)
                {
                    if (edgeIdx >= 0 && edgeIdx < targetVolume.Length)
                        targetVolume[edgeIdx] += carTrips;
                }
            }
        }
    }

    private static List<int> FindShortestPathEdges(
        WorldState state, int startNode, int endNode, float[] edgeTimes, int[]? nodeEdgeStart)
    {
        var result = new List<int>();
        if (startNode == endNode) return result;

        int nodeCount = state.Roads.NodeCount;
        if (nodeCount == 0) return result;

        var dist = new float[nodeCount];
        var prev = new int[nodeCount];
        var prevEdge = new int[nodeCount];
        var visited = new bool[nodeCount];

        Array.Fill(dist, float.MaxValue);
        Array.Fill(prev, -1);
        Array.Fill(prevEdge, -1);
        dist[startNode] = 0;

        var pq = new PriorityQueue<int, float>();
        pq.Enqueue(startNode, 0);

        // Reuse the cached node → first-outgoing-edge map when available; only
        // rebuild locally as a safety fallback (should not happen once
        // UpdateEdgeData has run).
        if (nodeEdgeStart == null || nodeEdgeStart.Length < nodeCount)
        {
            nodeEdgeStart = new int[nodeCount];
            int runningEdge = 0;
            for (int n = 0; n < nodeCount; n++)
            {
                nodeEdgeStart[n] = runningEdge;
                foreach (var _ in state.Roads.GetNeighbors(n))
                    runningEdge++;
            }
        }

        while (pq.Count > 0)
        {
            int u = pq.Dequeue();
            if (visited[u]) continue;
            visited[u] = true;
            if (u == endNode) break;

            int ei = nodeEdgeStart[u];
            foreach (var (v, _, _) in state.Roads.GetNeighbors(u))
            {
                if (visited[v])
                {
                    ei++;
                    continue;
                }

                float time = ei < edgeTimes.Length ? edgeTimes[ei] : 1f;
                float newDist = dist[u] + time;

                if (newDist < dist[v])
                {
                    dist[v] = newDist;
                    prev[v] = u;
                    prevEdge[v] = ei;
                    pq.Enqueue(v, newDist);
                }
                ei++;
            }
        }

        if (prev[endNode] == -1 && startNode != endNode) return result;

        var edgePath = new Stack<int>();
        int cur = endNode;
        while (cur != startNode && prev[cur] != -1)
        {
            edgePath.Push(prevEdge[cur]);
            cur = prev[cur];
        }

        while (edgePath.Count > 0)
            result.Add(edgePath.Pop());

        return result;
    }

    private void UpdateTileTraffic(WorldState state)
    {
        Array.Clear(state.Tiles.Traffic);
        if (_edgeCount == 0) return;

        int edgeIdx = 0;
        for (int n = 0; n < state.Roads.NodeCount; n++)
        {
            var (nx, ny) = state.Roads.GetNodePosition(n);

            foreach (var (target, _, _) in state.Roads.GetNeighbors(n))
            {
                if (edgeIdx >= _edgeCount) break;

                ApplyEdgeCongestionToTiles(state, edgeIdx, nx, ny, target);
                edgeIdx++;
            }
        }
    }

    private void UpdateTileTrafficSubset(WorldState state, int startEdge, int endEdge)
    {
        if (_edgeCount == 0) return;

        int edgeIdx = 0;
        for (int n = 0; n < state.Roads.NodeCount; n++)
        {
            var (nx, ny) = state.Roads.GetNodePosition(n);

            foreach (var (target, _, _) in state.Roads.GetNeighbors(n))
            {
                if (edgeIdx >= _edgeCount) break;

                if (edgeIdx >= startEdge && edgeIdx < endEdge)
                    ApplyEdgeCongestionToTiles(state, edgeIdx, nx, ny, target);

                edgeIdx++;
            }
        }
    }

    private void ApplyEdgeCongestionToTiles(
        WorldState state, int edgeIdx, int nx, int ny, int targetNode)
    {
        float trafficDensity = Math.Clamp(EdgeCongestion[edgeIdx], 0f, 1f);

        if (state.Tiles.InBounds(nx, ny))
        {
            int tileIdx = state.Tiles.Index(nx, ny);
            state.Tiles.Traffic[tileIdx] = Math.Max(state.Tiles.Traffic[tileIdx], trafficDensity);
        }

        var (tx, ty) = state.Roads.GetNodePosition(targetNode);
        if (state.Tiles.InBounds(tx, ty))
        {
            int tileIdx = state.Tiles.Index(tx, ty);
            state.Tiles.Traffic[tileIdx] = Math.Max(state.Tiles.Traffic[tileIdx], trafficDensity);
        }
    }

    private void ComputeZoneDistances(WorldState state)
    {
        if (_zoneDistanceCache == null || _zoneCentroidNode == null) return;
        Array.Clear(_zoneDistanceCache);

        for (int o = 0; o < _totalZones; o++)
        {
            for (int d = 0; d < _totalZones; d++)
            {
                if (o == d)
                {
                    _zoneDistanceCache[o * _totalZones + d] = _zoneSize * 0.5f;
                    continue;
                }

                int oZx = o % _zonesPerAxis;
                int oZy = o / _zonesPerAxis;
                int dZx = d % _zonesPerAxis;
                int dZy = d / _zonesPerAxis;

                float dx = (oZx - dZx) * _zoneSize;
                float dy = (oZy - dZy) * _zoneSize;
                float dist = MathF.Sqrt(dx * dx + dy * dy);

                int oNode = _zoneCentroidNode[o];
                int dNode = _zoneCentroidNode[d];

                if (oNode >= 0 && dNode >= 0)
                {
                    float roadDist = Engine.Math.Pathfinding.FindPathAStar(state.Roads, oNode, dNode).Count;
                    if (roadDist > 0)
                        dist = roadDist;
                }

                _zoneDistanceCache[o * _totalZones + d] = Math.Max(1f, dist);
            }
        }
    }
}
