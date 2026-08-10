using System.Runtime.CompilerServices;
using Forge.Engine.Data;
using Forge.Engine.Simulation;
using Forge.SimCore;

namespace Forge.Game.Simulation;

/// <summary>
/// Statistical BPR traffic model with multinomial logit mode choice.
///
/// Traffic tick (called 2x/sec):
/// 1. Build O-D matrix from household home-to-work pairs (clustered ~500 zones)
/// 2. Apply MNL mode choice (car, transit, walk, cycle)
/// 3. Distribute car trips on road network
/// 4. BPR congestion formula with Frank-Wolfe iterative assignment (3-5 iterations)
/// 5. Store congestion per road edge for overlay rendering
/// 6. Generate vehicle spawn data for TrafficRenderer
///
/// Zone clustering: 1024x1024 tiles -> ~500 zones for O-D matrix tractability.
/// Each zone is a square region of tiles. Zone size auto-computed from world size.
/// </summary>
public sealed class TrafficSystem
{
    // =========================================================================
    // Constants
    // =========================================================================

    private const int TargetZoneCount = 500;
    /// <summary>P4.1 static assignment; P4.2 raises for Frank-Wolfe convergence.</summary>
    private const int MaxFrankWolfeIterations = 3;
    private const float FrankWolfeConvergenceThreshold = 0.01f;

    // BPR formula delegated to <see cref="TrafficBpr"/>

    // MNL mode choice coefficients (calibrated from transport research)
    // V_car = beta_time * time + beta_cost * cost + ASC_car
    private const float BetaTime = -0.025f;          // Disutility per minute
    private const float BetaCost = -0.10f;            // Disutility per currency unit
    private const float BetaOvtt = -0.060f;           // Out-of-vehicle travel time (walking to stop)
    private const float BetaTransfers = -0.30f;       // Penalty per transit transfer

    // Alternative-Specific Constants (cultural baseline)
    private const float AscCarBase = 0.5f;
    private const float AscTransitBase = 0.0f;
    private const float AscWalkBase = -0.2f;
    private const float AscCycleBase = -0.1f;

    // Speed assumptions (tiles per minute)
    private const float CarFreeFlowSpeed = 2.0f;      // ~60 km/h if 1 tile = ~500m
    private const float TransitSpeed = 1.2f;
    private const float WalkSpeed = 0.15f;
    private const float CycleSpeed = 0.5f;

    // Transit assumptions
    private const float TransitFarePerTrip = 2.5f;
    private const float TransitAvgOvtt = 8f;           // minutes walking to stop
    private const float TransitAvgTransfers = 0.5f;

    // Car cost per tile
    private const float CarCostPerTile = 0.15f;

    // Rush hour multipliers
    private const float MorningPeakMultiplier = 1.8f;
    private const float EveningPeakMultiplier = 1.6f;
    private const float OffPeakMultiplier = 0.4f;

    // =========================================================================
    // State
    // =========================================================================

    private int _zoneSize;           // Tiles per zone side
    private int _zonesPerAxis;       // Zones per world axis
    private int _totalZones;         // Total zone count
    private int _worldSize;          // World size cached
    private bool _initialized;

    // O-D matrix (zone-to-zone trip counts)
    private float[]? _odMatrix;       // [origin * totalZones + dest]

    // Mode split results per zone
    private float[]? _carShare;
    private float[]? _transitShare;
    private float[]? _walkShare;
    private float[]? _cycleShare;

    // Road network assignment
    private float[]? _edgeVolume;      // Assigned volume per edge
    private float[]? _edgeCapacity;    // Capacity per edge
    private float[]? _edgeFreeFlow;    // Free flow time per edge
    private int _edgeCount;

    // Per-zone centroid node in road graph (closest road node to zone center)
    private int[]? _zoneCentroidNode;

    // Zone-to-zone shortest path cost cache
    private float[]? _zoneDistanceCache;

    // =========================================================================
    // Public properties
    // =========================================================================

    /// <summary>Congestion per road edge (0.0 = free flow, values > 1.0 = congested).</summary>
    public float[] EdgeCongestion { get; private set; } = Array.Empty<float>();

    /// <summary>Assigned volume per road edge (parallel to graph edge index).</summary>
    public float[] EdgeVolumes => _edgeVolume ?? Array.Empty<float>();

    /// <summary>BPR travel time per road edge after assignment (parallel to graph edge index).</summary>
    public float[] EdgeTravelTimes { get; private set; } = Array.Empty<float>();

    /// <summary>Average commute time in minutes across all commuting households.</summary>
    public float AverageCommuteMinutes { get; private set; }

    /// <summary>City-wide car mode share (0.0-1.0).</summary>
    public float CarModeShare { get; private set; }

    /// <summary>City-wide transit mode share (0.0-1.0).</summary>
    public float TransitModeShare { get; private set; }

    /// <summary>City-wide walk mode share (0.0-1.0).</summary>
    public float WalkModeShare { get; private set; }

    /// <summary>City-wide cycle mode share (0.0-1.0).</summary>
    public float CycleModeShare { get; private set; }

    /// <summary>Number of traffic zones.</summary>
    public int ZoneCount => _totalZones;

    // =========================================================================
    // Tick
    // =========================================================================

    /// <summary>
    /// Traffic tick called 2x per second.
    /// Runs the full traffic assignment pipeline: O-D matrix -> mode choice ->
    /// road assignment -> BPR congestion -> tile traffic overlay update.
    /// </summary>
    public void Tick(WorldState state, double dt)
    {
        if (!_initialized || state.Tiles.Size != _worldSize)
        {
            Initialize(state);
        }

        if (_totalZones == 0 || state.Households.Count == 0 || state.Roads.NodeCount == 0)
        {
            AverageCommuteMinutes = 0;
            CarModeShare = 0;
            TransitModeShare = 0;
            WalkModeShare = 0;
            CycleModeShare = 0;
            return;
        }

        // 1. Build O-D matrix from household home->work pairs
        BuildOdMatrix(state);

        // 2. Apply time-of-day multiplier
        float todMultiplier = GetTimeOfDayMultiplier(state);
        ApplyTimeOfDayFactor(todMultiplier);

        // 3. Mode choice (MNL logit)
        ApplyModeChoice(state);

        // 4. Frank-Wolfe iterative traffic assignment
        RunFrankWolfeAssignment(state);

        // 5. Update tile-level traffic overlay
        UpdateTileTraffic(state);
        UpdateMeanTrafficDensity(state);

        // 6. Calculate summary statistics
        CalculateStatistics(state);
    }

    // =========================================================================
    // Initialization
    // =========================================================================

    private void Initialize(WorldState state)
    {
        _worldSize = state.Tiles.Size;

        // Compute zone size to get ~500 zones
        // zones = (worldSize / zoneSize)^2 ≈ 500 -> zoneSize ≈ worldSize / sqrt(500) ≈ worldSize / 22
        _zoneSize = Math.Max(1, _worldSize / (int)MathF.Ceiling(MathF.Sqrt(TargetZoneCount)));
        _zonesPerAxis = (_worldSize + _zoneSize - 1) / _zoneSize;
        _totalZones = _zonesPerAxis * _zonesPerAxis;

        _odMatrix = new float[_totalZones * _totalZones];
        _carShare = new float[_totalZones];
        _transitShare = new float[_totalZones];
        _walkShare = new float[_totalZones];
        _cycleShare = new float[_totalZones];
        _zoneCentroidNode = new int[_totalZones];
        _zoneDistanceCache = new float[_totalZones * _totalZones];

        // Initialize zone centroids
        UpdateZoneCentroids(state);

        // Initialize edge data from road graph
        UpdateEdgeData(state);

        // Compute zone-to-zone distances
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

                // Find closest road node to zone center
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
        if (_edgeCount == 0)
        {
            _edgeVolume = Array.Empty<float>();
            _edgeCapacity = Array.Empty<float>();
            _edgeFreeFlow = Array.Empty<float>();
            EdgeCongestion = Array.Empty<float>();
            EdgeTravelTimes = Array.Empty<float>();
            return;
        }

        _edgeVolume = new float[_edgeCount];
        _edgeCapacity = new float[_edgeCount];
        _edgeFreeFlow = new float[_edgeCount];
        EdgeCongestion = new float[_edgeCount];
        EdgeTravelTimes = new float[_edgeCount];

        // Set capacity and free flow times based on road level
        // We derive edge data from the road graph structure
        int edgeIdx = 0;
        for (int n = 0; n < state.Roads.NodeCount; n++)
        {
            foreach (var (target, cost, level) in state.Roads.GetNeighbors(n))
            {
                if (edgeIdx < _edgeCount)
                {
                    float lanes = level switch
                    {
                        0 => 1f,   // Dirt road
                        1 => 2f,   // Paved
                        2 => 4f,   // Highway
                        _ => 2f,
                    };
                    _edgeCapacity[edgeIdx] = TrafficBpr.EdgeCapacity(level, lanes);
                    _edgeFreeFlow[edgeIdx] = cost;
                    edgeIdx++;
                }
            }
        }
    }

    // =========================================================================
    // Zone utilities
    // =========================================================================

    /// <summary>Get the zone index for a tile position.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetZoneForTile(int tileX, int tileY)
    {
        if (_zoneSize <= 0) return 0;
        int zx = Math.Clamp(tileX / _zoneSize, 0, _zonesPerAxis - 1);
        int zy = Math.Clamp(tileY / _zoneSize, 0, _zonesPerAxis - 1);
        return zy * _zonesPerAxis + zx;
    }

    // =========================================================================
    // O-D Matrix
    // =========================================================================

    private void BuildOdMatrix(WorldState state)
    {
        if (_odMatrix == null) return;
        Array.Clear(_odMatrix, 0, _odMatrix.Length);

        var hh = state.Households;
        var buildings = state.Buildings;

        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;
            if (hh.WorkBuildingId[i] == 0) continue; // No workplace
            if (hh.HomeBuildingId[i] == 0) continue;  // No home

            int homeId = hh.HomeBuildingId[i];
            int workId = hh.WorkBuildingId[i];

            if (homeId >= buildings.Capacity || workId >= buildings.Capacity) continue;
            if (!buildings.IsActive(homeId) || !buildings.IsActive(workId)) continue;

            int homeZone = GetZoneForTile(buildings.GridX[homeId], buildings.GridY[homeId]);
            int workZone = GetZoneForTile(buildings.GridX[workId], buildings.GridY[workId]);

            // Each household = 1 trip (simplified from member count for tractability)
            _odMatrix[homeZone * _totalZones + workZone] += 1f;
            // Return trip
            _odMatrix[workZone * _totalZones + homeZone] += 1f;
        }
    }

    // =========================================================================
    // Time of Day
    // =========================================================================

    private float GetTimeOfDayMultiplier(WorldState state)
    {
        // Derive hour from tick count (each tick = 1 game-minute, 1440 ticks/day)
        float hour = (state.TickCount % 1440) / 60f;

        // Morning peak: 7-9 AM
        if (hour >= 7f && hour < 9f)
            return MorningPeakMultiplier;
        // Evening peak: 17-19
        if (hour >= 17f && hour < 19f)
            return EveningPeakMultiplier;
        // Off-peak
        if (hour < 6f || hour >= 22f)
            return OffPeakMultiplier;

        return 1.0f;
    }

    private void ApplyTimeOfDayFactor(float multiplier)
    {
        if (_odMatrix == null) return;
        for (int i = 0; i < _odMatrix.Length; i++)
        {
            _odMatrix[i] *= multiplier;
        }
    }

    // =========================================================================
    // Mode Choice — Multinomial Logit (MNL)
    // =========================================================================

    private void ApplyModeChoice(WorldState state)
    {
        if (_carShare == null || _transitShare == null ||
            _walkShare == null || _cycleShare == null ||
            _zoneDistanceCache == null)
            return;

        float totalCarTrips = 0;
        float totalTransitTrips = 0;
        float totalWalkTrips = 0;
        float totalCycleTrips = 0;
        float totalTrips = 0;

        // Cultural ASC modifiers
        float carCulture = GetCarCultureModifier(state);
        float ascCar = AscCarBase + carCulture;
        float ascTransit = AscTransitBase;
        float ascWalk = AscWalkBase;
        float ascCycle = AscCycleBase;

        // Per-zone average income for cost scaling
        float cityAvgIncome = CalculateCityAverageIncome(state);

        for (int origin = 0; origin < _totalZones; origin++)
        {
            float zoneTrips = 0;
            float zoneCarTrips = 0;
            float zoneTransitTrips = 0;
            float zoneWalkTrips = 0;
            float zoneCycleTrips = 0;

            for (int dest = 0; dest < _totalZones; dest++)
            {
                float trips = _odMatrix[origin * _totalZones + dest];
                if (trips <= 0) continue;

                float distance = _zoneDistanceCache![origin * _totalZones + dest];
                if (distance <= 0 && origin != dest) distance = 1f;

                // Calculate travel times for each mode
                float carTime = distance / CarFreeFlowSpeed;
                float transitIvtt = distance / TransitSpeed;
                float walkTime = distance / WalkSpeed;
                float cycleTime = distance / CycleSpeed;

                // Calculate costs
                float carCost = distance * CarCostPerTile;
                float transitFare = TransitFarePerTrip;

                // Income-scaled cost sensitivity
                float costScale = cityAvgIncome > 0
                    ? 1f / MathF.Sqrt(Math.Max(1f, cityAvgIncome / 1000f))
                    : 1f;

                float scaledCarCost = carCost * costScale;
                float scaledTransitFare = transitFare * costScale;

                // Utility functions
                float vCar = BetaTime * carTime + BetaCost * scaledCarCost + ascCar;
                float vTransit = BetaTime * transitIvtt + BetaOvtt * TransitAvgOvtt +
                                 BetaCost * scaledTransitFare +
                                 BetaTransfers * TransitAvgTransfers + ascTransit;
                float vWalk = BetaTime * walkTime + ascWalk;
                float vCycle = BetaTime * cycleTime + ascCycle;

                // Cap long walks/cycles: if walk > 60 min, set utility very low
                if (walkTime > 60f) vWalk = -100f;
                if (cycleTime > 30f) vCycle = -50f;

                // Logit probabilities
                ComputeLogitProbabilities(vCar, vTransit, vWalk, vCycle,
                    out float pCar, out float pTransit, out float pWalk, out float pCycle);

                zoneCarTrips += trips * pCar;
                zoneTransitTrips += trips * pTransit;
                zoneWalkTrips += trips * pWalk;
                zoneCycleTrips += trips * pCycle;
                zoneTrips += trips;
            }

            if (zoneTrips > 0)
            {
                _carShare[origin] = zoneCarTrips / zoneTrips;
                _transitShare[origin] = zoneTransitTrips / zoneTrips;
                _walkShare[origin] = zoneWalkTrips / zoneTrips;
                _cycleShare[origin] = zoneCycleTrips / zoneTrips;
            }
            else
            {
                _carShare[origin] = 0;
                _transitShare[origin] = 0;
                _walkShare[origin] = 0;
                _cycleShare[origin] = 0;
            }

            totalCarTrips += zoneCarTrips;
            totalTransitTrips += zoneTransitTrips;
            totalWalkTrips += zoneWalkTrips;
            totalCycleTrips += zoneCycleTrips;
            totalTrips += zoneTrips;
        }

        // Update city-wide mode shares
        if (totalTrips > 0)
        {
            CarModeShare = totalCarTrips / totalTrips;
            TransitModeShare = totalTransitTrips / totalTrips;
            WalkModeShare = totalWalkTrips / totalTrips;
            CycleModeShare = totalCycleTrips / totalTrips;
        }
    }

    /// <summary>
    /// Compute MNL logit probabilities: P(j) = exp(V_j) / sum(exp(V_k)).
    /// Numerically stable via max subtraction.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputeLogitProbabilities(
        float vCar, float vTransit, float vWalk, float vCycle,
        out float pCar, out float pTransit, out float pWalk, out float pCycle)
    {
        float maxV = Math.Max(Math.Max(vCar, vTransit), Math.Max(vWalk, vCycle));

        float expCar = MathF.Exp(vCar - maxV);
        float expTransit = MathF.Exp(vTransit - maxV);
        float expWalk = MathF.Exp(vWalk - maxV);
        float expCycle = MathF.Exp(vCycle - maxV);

        float sumExp = expCar + expTransit + expWalk + expCycle;

        if (sumExp <= 0f)
        {
            pCar = 0.25f;
            pTransit = 0.25f;
            pWalk = 0.25f;
            pCycle = 0.25f;
            return;
        }

        float invSum = 1f / sumExp;
        pCar = expCar * invSum;
        pTransit = expTransit * invSum;
        pWalk = expWalk * invSum;
        pCycle = expCycle * invSum;
    }

    // =========================================================================
    // Frank-Wolfe Traffic Assignment
    // =========================================================================

    private void RunFrankWolfeAssignment(WorldState state)
    {
        if (_edgeCount == 0 || _edgeVolume == null || _edgeCapacity == null ||
            _edgeFreeFlow == null || _odMatrix == null || _zoneCentroidNode == null)
            return;

        // Initialize volumes to zero
        Array.Clear(_edgeVolume, 0, _edgeVolume.Length);

        // Auxiliary volume array for Frank-Wolfe direction
        var auxVolume = new float[_edgeCount];

        for (int iteration = 0; iteration < MaxFrankWolfeIterations; iteration++)
        {
            // Calculate current travel times using BPR
            var currentTimes = new float[_edgeCount];
            for (int e = 0; e < _edgeCount; e++)
            {
                currentTimes[e] = CalculateBprTravelTime(_edgeFreeFlow[e], _edgeVolume[e], _edgeCapacity[e]);
            }

            // All-or-nothing assignment based on current times
            Array.Clear(auxVolume, 0, auxVolume.Length);
            AssignAllOrNothing(state, auxVolume, currentTimes);

            float gap = TrafficBpr.FrankWolfeRelativeGap(currentTimes, _edgeVolume, auxVolume);
            if (gap < FrankWolfeConvergenceThreshold)
                break;

            // Frank-Wolfe step size: lambda = 2 / (iteration + 2)
            float lambda = 2f / (iteration + 2);

            // Update volumes: volume = (1 - lambda) * volume + lambda * auxVolume
            for (int e = 0; e < _edgeCount; e++)
                _edgeVolume[e] = (1f - lambda) * _edgeVolume[e] + lambda * auxVolume[e];
        }

        // Compute final congestion ratios and BPR travel times
        for (int e = 0; e < _edgeCount; e++)
        {
            float cap = _edgeCapacity[e];
            EdgeCongestion[e] = cap > 0 ? _edgeVolume[e] / cap : 0f;
            EdgeTravelTimes[e] = CalculateBprTravelTime(
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
            times[e] = CalculateBprTravelTime(
                _edgeFreeFlow[e], _edgeVolume[e], _edgeCapacity[e]);
        }
    }

    /// <summary>
    /// BPR congestion formula:
    /// travel_time = free_flow * (1 + 0.15 * (volume/capacity)^4)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CalculateBprTravelTime(float freeFlowTime, float volume, float capacity) =>
        TrafficBpr.CalculateTravelTime(freeFlowTime, volume, capacity);

    private void AssignAllOrNothing(WorldState state, float[] targetVolume, float[] edgeTimes)
    {
        if (_odMatrix == null || _zoneCentroidNode == null) return;

        // For each O-D pair with car trips, find shortest path and assign volume
        for (int origin = 0; origin < _totalZones; origin++)
        {
            int originNode = _zoneCentroidNode[origin];
            if (originNode < 0) continue;

            float carPortion = _carShare![origin];
            if (carPortion <= 0) continue;

            for (int dest = 0; dest < _totalZones; dest++)
            {
                if (origin == dest) continue;

                float trips = _odMatrix[origin * _totalZones + dest];
                if (trips <= 0) continue;

                float carTrips = trips * carPortion;
                if (carTrips < 0.01f) continue;

                int destNode = _zoneCentroidNode[dest];
                if (destNode < 0) continue;

                // Find shortest path on current edge times
                // Use simple Dijkstra from origin to dest with edge times
                var path = FindShortestPathEdges(state, originNode, destNode, edgeTimes);

                // Assign volume to each edge on the path
                foreach (int edgeIdx in path)
                {
                    if (edgeIdx >= 0 && edgeIdx < targetVolume.Length)
                    {
                        targetVolume[edgeIdx] += carTrips;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Dijkstra shortest path that returns edge indices along the path.
    /// Uses provided edge times instead of graph costs.
    /// </summary>
    private List<int> FindShortestPathEdges(WorldState state, int startNode, int endNode, float[] edgeTimes)
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

        // Precompute edge start indices per node
        var nodeEdgeStart = new int[nodeCount];
        int runningEdge = 0;
        for (int n = 0; n < nodeCount; n++)
        {
            nodeEdgeStart[n] = runningEdge;
            foreach (var _ in state.Roads.GetNeighbors(n))
                runningEdge++;
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

        // Reconstruct edge path
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

    // =========================================================================
    // Tile traffic overlay
    // =========================================================================

    private void UpdateTileTraffic(WorldState state)
    {
        // Clear existing traffic data
        Array.Clear(state.Tiles.Traffic, 0, state.Tiles.Traffic.Length);

        if (_edgeCount == 0) return;

        // Map edge congestion onto tiles along roads
        int edgeIdx = 0;
        for (int n = 0; n < state.Roads.NodeCount; n++)
        {
            var (nx, ny) = state.Roads.GetNodePosition(n);

            foreach (var (target, _, _) in state.Roads.GetNeighbors(n))
            {
                if (edgeIdx >= _edgeCount) break;

                float congestion = EdgeCongestion[edgeIdx];
                float trafficDensity = Math.Clamp(congestion, 0f, 1f);

                // Set traffic on the source tile
                if (state.Tiles.InBounds(nx, ny))
                {
                    int tileIdx = state.Tiles.Index(nx, ny);
                    state.Tiles.Traffic[tileIdx] = Math.Max(state.Tiles.Traffic[tileIdx], trafficDensity);
                }

                // Also set on target tile
                var (tx, ty) = state.Roads.GetNodePosition(target);
                if (state.Tiles.InBounds(tx, ty))
                {
                    int tileIdx = state.Tiles.Index(tx, ty);
                    state.Tiles.Traffic[tileIdx] = Math.Max(state.Tiles.Traffic[tileIdx], trafficDensity);
                }

                edgeIdx++;
            }
        }
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

    // =========================================================================
    // Statistics
    // =========================================================================

    private void CalculateStatistics(WorldState state)
    {
        float totalCommuteTime = 0;
        int commuterCount = 0;

        var hh = state.Households;
        var buildings = state.Buildings;

        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;
            if (hh.WorkBuildingId[i] == 0 || hh.HomeBuildingId[i] == 0) continue;

            int homeId = hh.HomeBuildingId[i];
            int workId = hh.WorkBuildingId[i];
            if (homeId >= buildings.Capacity || workId >= buildings.Capacity) continue;

            int homeZone = GetZoneForTile(buildings.GridX[homeId], buildings.GridY[homeId]);
            int workZone = GetZoneForTile(buildings.GridX[workId], buildings.GridY[workId]);

            float distance = GetZoneDistance(homeZone, workZone);

            // Commute time depends on mode share of origin zone
            float carTime = distance / CarFreeFlowSpeed;
            float transitTime = distance / TransitSpeed + TransitAvgOvtt;
            float walkTime = distance / WalkSpeed;
            float cycleTime = distance / CycleSpeed;

            float carShare = _carShare![homeZone];
            float transitShareLocal = _transitShare![homeZone];
            float walkShareLocal = _walkShare![homeZone];
            float cycleShareLocal = _cycleShare![homeZone];

            // Apply congestion to car time
            float avgCongestion = GetAverageCongestion();
            float congestedCarTime = carTime * (1f + TrafficBpr.Alpha * MathF.Pow(avgCongestion, TrafficBpr.Beta));

            float avgCommuteTime = congestedCarTime * carShare +
                                   transitTime * transitShareLocal +
                                   walkTime * walkShareLocal +
                                   cycleTime * cycleShareLocal;

            totalCommuteTime += avgCommuteTime;
            commuterCount++;

            // Update per-household transport satisfaction
            float satisfaction = Math.Clamp(1f - avgCommuteTime / 60f, 0f, 1f);
            hh.TransportSatisfaction[i] = (byte)(satisfaction * 255f);
        }

        AverageCommuteMinutes = commuterCount > 0 ? totalCommuteTime / commuterCount : 0f;
    }

    // =========================================================================
    // Helper methods
    // =========================================================================

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

                // Euclidean distance between zone centers as fallback
                int oZx = o % _zonesPerAxis;
                int oZy = o / _zonesPerAxis;
                int dZx = d % _zonesPerAxis;
                int dZy = d / _zonesPerAxis;

                float dx = (oZx - dZx) * _zoneSize;
                float dy = (oZy - dZy) * _zoneSize;
                float dist = MathF.Sqrt(dx * dx + dy * dy);

                // Use road graph distance if both centroids have road nodes
                int oNode = _zoneCentroidNode[o];
                int dNode = _zoneCentroidNode[d];

                if (oNode >= 0 && dNode >= 0)
                {
                    // Use graph-based distance (edge cost from CH or Dijkstra)
                    float roadDist = Engine.Math.Pathfinding.FindPathAStar(state.Roads, oNode, dNode).Count;
                    if (roadDist > 0)
                        dist = roadDist; // Road distance in edge count (approximation)
                }

                _zoneDistanceCache[o * _totalZones + d] = Math.Max(1f, dist);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float GetZoneDistance(int zoneA, int zoneB)
    {
        if (_zoneDistanceCache == null || zoneA < 0 || zoneB < 0 ||
            zoneA >= _totalZones || zoneB >= _totalZones)
            return 1f;
        return _zoneDistanceCache[zoneA * _totalZones + zoneB];
    }

    private float GetCarCultureModifier(WorldState state)
    {
        // CulturalDna[1]: Collectivism vs Individualism
        // More individualistic = more car-dependent
        float individualism = state.CulturalDna[1];
        // CulturalDna[0]: Tradition vs Innovation
        // More traditional = more car-dependent in modern era
        float tradition = -state.CulturalDna[0];
        return (individualism + tradition) * 0.3f;
    }

    private float CalculateCityAverageIncome(WorldState state)
    {
        long total = 0;
        int count = 0;
        var hh = state.Households;
        for (int i = 0; i < hh.Capacity; i++)
        {
            if (!hh.IsActive(i)) continue;
            total += hh.Income[i];
            count++;
        }
        return count > 0 ? (float)total / count : 1000f;
    }

    /// <summary>
    /// Income-scaled cost sensitivity for mode choice.
    /// cost_perceived = actual_cost / sqrt(income)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ScaleCostByIncome(float cost, float income)
    {
        if (income <= 0) return cost;
        return cost / MathF.Sqrt(income);
    }

    private float GetAverageCongestion()
    {
        if (_edgeCount == 0 || EdgeCongestion.Length == 0) return 0f;
        float total = 0;
        for (int e = 0; e < _edgeCount && e < EdgeCongestion.Length; e++)
        {
            total += EdgeCongestion[e];
        }
        return total / _edgeCount;
    }

    // =========================================================================
    // Public API for testing/debugging
    // =========================================================================

    /// <summary>Get the O-D matrix value between two zones.</summary>
    public float GetOdValue(int originZone, int destZone)
    {
        if (_odMatrix == null || originZone < 0 || destZone < 0 ||
            originZone >= _totalZones || destZone >= _totalZones)
            return 0f;
        return _odMatrix[originZone * _totalZones + destZone];
    }

    /// <summary>Get the car share for a zone.</summary>
    public float GetZoneCarShare(int zone)
    {
        if (_carShare == null || zone < 0 || zone >= _totalZones) return 0f;
        return _carShare[zone];
    }

    /// <summary>Force re-initialization on next tick.</summary>
    public void InvalidateCache()
    {
        _initialized = false;
    }
}
