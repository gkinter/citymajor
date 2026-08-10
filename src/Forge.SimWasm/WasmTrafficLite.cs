using System.Runtime.CompilerServices;
using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Forge.SimCore;

namespace Forge.SimWasm;

/// <summary>
/// Lightweight BPR traffic for browser WASM (SB-3685 partial).
/// P4.1: household home/work O-D (gravity fallback) + BPR edge times.
/// P4.2: multi-iteration Frank-Wolfe at
/// <see cref="WasmConfig.TrafficLiteInterval"/> — never every 8 Hz sim tick.
/// P4 mode choice: simple 3-mode MNL (car/transit/walk) replaces flat 65% car.
/// P4.3: transit ASC sensitive to <see cref="WorldState.BusCoverage"/> /
/// <see cref="WorldState.TransitLineCount"/> (0 stub until a transit graph exists).
/// </summary>
public sealed class WasmTrafficLite
{
    // -------------------------------------------------------------------------
    // Simple MNL stub (car / transit / walk) — ASC calibrated so mid-range
    // distances (~10–20 tiles) yield ~65% car, matching the former flat share.
    // -------------------------------------------------------------------------
    private const float BetaTime = -0.025f;
    private const float AscCar = 0.5f;
    private const float AscTransit = 0.0f;
    private const float AscWalk = -0.2f;
    /// <summary>Positive ASC boost per unit bus coverage (0–1).</summary>
    private const float GammaCoverage = 1.5f;
    private const float CarSpeedTilesPerMin = 2.0f;
    private const float TransitSpeedTilesPerMin = 1.2f;
    private const float WalkSpeedTilesPerMin = 0.15f;
    private const float MaxWalkMinutes = 60f;

    /// <summary>Line count that maps to full <see cref="WorldState.BusCoverage"/> (1.0).</summary>
    public const int BusCoverageRefLines = 8;

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
    private int _cachedNodeCount;
    private int _edgeBatchCursor;
    private int _targetZoneCount = WasmConfig.TrafficLiteZoneCount;

    /// <summary>
    /// Cached node → first-outgoing-edge offset. Invalidated only when
    /// <see cref="UpdateEdgeData"/> rebuilds the edge table (roads changed);
    /// avoids rebuilding this on every Dijkstra invocation during
    /// AssignAllOrNothing, which is O(V + E) per O-D pair.
    /// </summary>
    private int[]? _nodeEdgeStart;

    public float[] EdgeCongestion { get; private set; } = Array.Empty<float>();

    /// <summary>Assigned volume per road edge (parallel to graph edge index).</summary>
    public float[] EdgeVolumes => _edgeVolume ?? Array.Empty<float>();

    /// <summary>BPR travel time per road edge after assignment (parallel to graph edge index).</summary>
    public float[] EdgeTravelTimes { get; private set; } = Array.Empty<float>();

    /// <summary>City-wide car mode share after last assignment (0–1).</summary>
    public float CarModeShare { get; private set; } = 0.65f;

    /// <summary>City-wide transit mode share after last assignment (0–1).</summary>
    public float TransitModeShare { get; private set; }

    /// <summary>City-wide walk mode share after last assignment (0–1).</summary>
    public float WalkModeShare { get; private set; }

    /// <summary>Set O-D zone grid resolution; invalidates cached zone layout when changed.</summary>
    public void Configure(int zoneCount)
    {
        zoneCount = Math.Max(1, zoneCount);
        if (zoneCount == _targetZoneCount) return;
        _targetZoneCount = zoneCount;
        _initialized = false;
    }

    public void Tick(WorldState state, double dt)
    {
        _ = dt;

        if (!_initialized || state.Tiles.Size != _worldSize)
            Initialize(state);
        else
            EnsureRoadGraphCurrent(state);

        if (_totalZones == 0 || state.Roads.NodeCount == 0 || _edgeCount == 0)
            return;

        RefreshBusCoverage(state);
        BuildLiteOdMatrix(state);
        ApplyRushHourToOd(state);
        RunFrankWolfeAssignment(state);
        UpdateTileTraffic(state);
        UpdateMeanTrafficDensity(state);
    }

    /// <summary>
    /// Derive <see cref="WorldState.BusCoverage"/> from transit line count.
    /// No transit graph yet → count stays 0 → coverage 0 (explicit stub).
    /// </summary>
    public static void RefreshBusCoverage(WorldState state)
    {
        int lines = Math.Max(0, state.TransitLineCount);
        state.TransitLineCount = lines;
        state.BusCoverage = DeriveBusCoverage(lines);
    }

    /// <summary>Map transit line count → 0–1 coverage (0 lines → 0).</summary>
    public static float DeriveBusCoverage(int transitLineCount) =>
        transitLineCount <= 0
            ? 0f
            : Math.Clamp(transitLineCount / (float)BusCoverageRefLines, 0f, 1f);

    /// <summary>
    /// Cheap BPR refresh on a rotating subset of edges — called between full lite ticks
    /// to keep congestion overlay responsive without re-running O-D / Frank-Wolfe.
    /// </summary>
    public void TickEdgeBatch(WorldState state, double dt)
    {
        _ = dt;

        if (!_initialized || state.Tiles.Size != _worldSize)
            Initialize(state);
        else
            EnsureRoadGraphCurrent(state);

        if (_edgeCount == 0 || _edgeVolume == null ||
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
        _zoneSize = Math.Max(1, _worldSize / (int)MathF.Ceiling(MathF.Sqrt(_targetZoneCount)));
        _zonesPerAxis = (_worldSize + _zoneSize - 1) / _zoneSize;
        _totalZones = _zonesPerAxis * _zonesPerAxis;

        _odMatrix = new float[_totalZones * _totalZones];
        _zoneCentroidNode = new int[_totalZones];
        _zoneDistanceCache = new float[_totalZones * _totalZones];
        _edgeBatchCursor = 0;

        UpdateZoneCentroids(state);
        UpdateEdgeData(state);
        _cachedNodeCount = state.Roads.NodeCount;
        ComputeZoneDistances(state);

        _initialized = true;
    }

    /// <summary>
    /// Refresh edge table and zone centroids when the road graph changes without a
    /// world resize (e.g. PlaceRoad during play or snapshot load).
    /// </summary>
    private void EnsureRoadGraphCurrent(WorldState state)
    {
        int nodeCount = state.Roads.NodeCount;
        if (state.Roads.EdgeCount == _edgeCount && nodeCount == _cachedNodeCount)
            return;

        UpdateZoneCentroids(state);
        UpdateEdgeData(state);
        _cachedNodeCount = nodeCount;
        ComputeZoneDistances(state);
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
            EdgeTravelTimes = Array.Empty<float>();
            _nodeEdgeStart = nodeCount > 0 ? new int[nodeCount] : Array.Empty<int>();
            return;
        }

        _edgeVolume = new float[_edgeCount];
        _edgeCapacity = new float[_edgeCount];
        _edgeFreeFlow = new float[_edgeCount];
        EdgeCongestion = new float[_edgeCount];
        EdgeTravelTimes = new float[_edgeCount];
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
                float lawCapacityMult = state.LawTrafficCapacityMult > 0f
                    ? state.LawTrafficCapacityMult
                    : 1f;
                _edgeCapacity[edgeIdx] = TrafficBpr.EdgeCapacity(level, lanes, lawCapacityMult);
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
    /// O-D from household home/work building pairs (Cathedral P4.1).
    /// Falls back to gravity only when no commuters are assigned yet.
    /// </summary>
    private void BuildLiteOdMatrix(WorldState state)
    {
        if (_odMatrix == null) return;
        Array.Clear(_odMatrix, 0, _odMatrix.Length);

        var hh = state.Households;
        var buildings = state.Buildings;
        bool hasCommuters = false;

        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;
            if (hh.AgeGroup[i] != 1) continue;
            if (hh.WorkBuildingId[i] == 0 || hh.HomeBuildingId[i] == 0) continue;

            int homeId = hh.HomeBuildingId[i];
            int workId = hh.WorkBuildingId[i];
            if (homeId >= buildings.Capacity || workId >= buildings.Capacity) continue;
            if (!buildings.IsActive(homeId) || !buildings.IsActive(workId)) continue;
            if (buildings.State[homeId] != 1 || buildings.State[workId] != 1) continue;

            int homeZone = GetZoneForTile(buildings.GridX[homeId], buildings.GridY[homeId]);
            int workZone = GetZoneForTile(buildings.GridX[workId], buildings.GridY[workId]);

            _odMatrix[homeZone * _totalZones + workZone] += 1f;
            _odMatrix[workZone * _totalZones + homeZone] += 1f;
            hasCommuters = true;
        }

        if (!hasCommuters)
            BuildGravityOdMatrix(state);
    }

    /// <summary>Gravity-style O-D fallback before households receive home/work links.</summary>
    private void BuildGravityOdMatrix(WorldState state)
    {
        if (_odMatrix == null) return;

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

    /// <summary>
    /// Scale gravity O-D by time-of-day rush curve so arterials peak morning/evening.
    /// </summary>
    private void ApplyRushHourToOd(WorldState state)
    {
        if (_odMatrix == null) return;

        float timeOfDay = (state.TickCount % 1440) / 60f;
        float rush = LifeSimMath.RushHourMultiplier(timeOfDay);
        if (MathF.Abs(rush - 1f) < 0.01f) return;

        for (int i = 0; i < _odMatrix.Length; i++)
            _odMatrix[i] *= rush;
    }

    private static void UpdateMeanTrafficDensity(WorldState state)
    {
        var traffic = state.Tiles.Traffic;
        var roads = state.Tiles.RoadFlags;
        double sum = 0;
        int count = 0;
        for (int i = 0; i < traffic.Length; i++)
        {
            if (roads[i] == 0) continue;
            sum += traffic[i];
            count++;
        }

        state.MeanTrafficDensity = count == 0 ? 0f : (float)(sum / count);
    }

    private void RunFrankWolfeAssignment(WorldState state)
    {
        if (_edgeVolume == null || _edgeCapacity == null || _edgeFreeFlow == null ||
            _odMatrix == null || _zoneCentroidNode == null)
            return;

        Array.Clear(_edgeVolume, 0, _edgeVolume.Length);
        var auxVolume = new float[_edgeCount];
        int maxIter = WasmConfig.TrafficLiteFrankWolfeIterations;

        for (int iteration = 0; iteration < maxIter; iteration++)
        {
            var currentTimes = new float[_edgeCount];
            for (int e = 0; e < _edgeCount; e++)
            {
                currentTimes[e] = TrafficBpr.CalculateTravelTime(
                    _edgeFreeFlow[e], _edgeVolume[e], _edgeCapacity[e]);
            }

            Array.Clear(auxVolume, 0, auxVolume.Length);
            AssignAllOrNothing(state, auxVolume, currentTimes);

            float gap = TrafficBpr.FrankWolfeRelativeGap(currentTimes, _edgeVolume, auxVolume);
            if (gap < WasmConfig.TrafficLiteFrankWolfeConvergenceThreshold)
                break;

            float lambda = 2f / (iteration + 2);
            for (int e = 0; e < _edgeCount; e++)
                _edgeVolume[e] = (1f - lambda) * _edgeVolume[e] + lambda * auxVolume[e];
        }

        for (int e = 0; e < _edgeCount; e++)
        {
            float cap = _edgeCapacity[e];
            EdgeCongestion[e] = cap > 0 ? _edgeVolume[e] / cap : 0f;
            EdgeTravelTimes[e] = TrafficBpr.CalculateTravelTime(
                _edgeFreeFlow[e], _edgeVolume[e], _edgeCapacity[e]);
        }

        PublishEdgeTravelTimes(state);
    }

    private void PublishEdgeTravelTimes(WorldState state)
    {
        if (_edgeCount == 0 || _edgeFreeFlow == null || _edgeVolume == null || _edgeCapacity == null)
        {
            state.RoadEdgeTravelTimes = null;
            return;
        }

        var times = state.RoadEdgeTravelTimes;
        if (times == null || times.Length < _edgeCount)
            times = state.RoadEdgeTravelTimes = new float[_edgeCount];

        for (int e = 0; e < _edgeCount; e++)
        {
            times[e] = TrafficBpr.CalculateTravelTime(
                _edgeFreeFlow[e], _edgeVolume[e], _edgeCapacity[e]);
        }
    }

    /// <summary>
    /// Simple 3-mode multinomial logit on zone distance (tiles).
    /// P(j) = exp(V_j) / Σ exp(V_k); V = ASC + β_time · travel_minutes
    /// (+ γ · busCoverage on transit).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputeLiteModeShares(
        float distanceTiles,
        out float pCar,
        out float pTransit,
        out float pWalk) =>
        ComputeLiteModeShares(distanceTiles, busCoverage: 0f, out pCar, out pTransit, out pWalk);

    /// <summary>
    /// MNL with bus coverage sensitivity — higher coverage raises transit ASC.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputeLiteModeShares(
        float distanceTiles,
        float busCoverage,
        out float pCar,
        out float pTransit,
        out float pWalk)
    {
        float dist = Math.Max(1f, distanceTiles);
        float carTime = dist / CarSpeedTilesPerMin;
        float transitTime = dist / TransitSpeedTilesPerMin;
        float walkTime = dist / WalkSpeedTilesPerMin;
        float coverage = Math.Clamp(busCoverage, 0f, 1f);

        float vCar = BetaTime * carTime + AscCar;
        float vTransit = BetaTime * transitTime + AscTransit + GammaCoverage * coverage;
        float vWalk = walkTime > MaxWalkMinutes
            ? -100f
            : BetaTime * walkTime + AscWalk;

        float maxV = Math.Max(vCar, Math.Max(vTransit, vWalk));
        float expCar = MathF.Exp(vCar - maxV);
        float expTransit = MathF.Exp(vTransit - maxV);
        float expWalk = MathF.Exp(vWalk - maxV);
        float sum = expCar + expTransit + expWalk;

        if (sum <= 0f)
        {
            pCar = 0.65f;
            pTransit = 0.25f;
            pWalk = 0.10f;
            return;
        }

        float inv = 1f / sum;
        pCar = expCar * inv;
        pTransit = expTransit * inv;
        pWalk = expWalk * inv;
    }

    private void AssignAllOrNothing(WorldState state, float[] targetVolume, float[] edgeTimes)
    {
        if (_odMatrix == null || _zoneCentroidNode == null || _zoneDistanceCache == null)
            return;

        float totalCar = 0f;
        float totalTransit = 0f;
        float totalWalk = 0f;
        float totalTrips = 0f;
        float busCoverage = Math.Clamp(state.BusCoverage, 0f, 1f);

        for (int origin = 0; origin < _totalZones; origin++)
        {
            int originNode = _zoneCentroidNode[origin];
            if (originNode < 0) continue;

            for (int dest = 0; dest < _totalZones; dest++)
            {
                if (origin == dest) continue;

                float trips = _odMatrix[origin * _totalZones + dest];
                if (trips <= 0) continue;

                float distance = _zoneDistanceCache[origin * _totalZones + dest];
                ComputeLiteModeShares(
                    distance, busCoverage, out float pCar, out float pTransit, out float pWalk);

                totalCar += trips * pCar;
                totalTransit += trips * pTransit;
                totalWalk += trips * pWalk;
                totalTrips += trips;

                float carTrips = trips * pCar;
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

        if (totalTrips > 0f)
        {
            float inv = 1f / totalTrips;
            CarModeShare = totalCar * inv;
            TransitModeShare = totalTransit * inv;
            WalkModeShare = totalWalk * inv;
        }
        else
        {
            CarModeShare = 0f;
            TransitModeShare = 0f;
            WalkModeShare = 0f;
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
        Array.Clear(state.Tiles.Traffic, 0, state.Tiles.Traffic.Length);
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
        Array.Clear(_zoneDistanceCache, 0, _zoneDistanceCache.Length);

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
