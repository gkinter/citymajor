using Forge.Engine.Math;

namespace Forge.Engine.Data;

/// <summary>
/// Terrain type identifiers matching TileData.TerrainType encoding.
/// </summary>
public enum TerrainId : byte
{
    Grass = 0,
    Dirt = 1,
    Sand = 2,
    Water = 3,
    Rock = 4,
    Forest = 5,
}

/// <summary>
/// Resource type identifiers stored in the resource overlay.
/// </summary>
public enum ResourceId : byte
{
    None = 0,
    Iron = 1,
    Coal = 2,
    Stone = 3,
    FertileSoil = 4,
    Gold = 5,
    Oil = 6,
}

/// <summary>
/// Predefined map archetypes with tuned noise parameters.
/// </summary>
public enum MapType
{
    Valley,
    Coastal,
    Mountain,
    Plains,
    RiverDelta,
}

/// <summary>
/// Per-map-type noise parameter overrides layered on top of the base generation.
/// </summary>
public readonly record struct MapTypeParams(
    float ContinentalFreq,
    float ContinentalAmplitude,
    float RegionalFreq,
    float RegionalAmplitude,
    float WaterThreshold,
    float BeachUpper,
    float FlatUpper,
    float HillUpper,
    float MountainUpper,
    int MinRivers,
    int MaxRivers,
    float ForestDensityMultiplier);

/// <summary>
/// Result of the playability validation pass. If <see cref="IsPlayable"/> is false
/// the map was regenerated with a different seed.
/// </summary>
public readonly record struct ValidationResult(
    bool IsPlayable,
    bool HasWaterAccess,
    bool HasEnoughFlatLand,
    bool IsConnected,
    bool HasRequiredResources,
    bool HasStartingZone,
    bool HasEdgeAccess,
    int FlatTileCount,
    int WaterTileCount,
    int StartX,
    int StartY);

/// <summary>
/// Procedural map generator for the Forge Engine city builder.
/// Produces fully-populated <see cref="TileData"/> instances with terrain, rivers,
/// forests, and resource deposits. Supports maps up to 1024x1024 tiles.
///
/// Generation pipeline:
///   1. Layered simplex noise → elevation heightmap
///   2. Terrain classification from elevation thresholds
///   3. River simulation (gradient descent from mountain sources)
///   4. Forest placement (Poisson-disc clustered, riparian buffers)
///   5. Resource deposit placement (clustered, guaranteed minimums)
///   6. Playability validation (with automatic re-seed on failure)
/// </summary>
public static class MapGenerator
{
    // ------------------------------------------------------------------
    //  Elevation thresholds (base; overridden per MapType)
    // ------------------------------------------------------------------
    private const float BaseWaterThreshold = 0.35f;
    private const float BaseBeachUpper = 0.40f;
    private const float BaseFlatUpper = 0.60f;
    private const float BaseHillUpper = 0.75f;
    private const float BaseMountainUpper = 0.90f;

    // ------------------------------------------------------------------
    //  Noise layer weights (spec)
    // ------------------------------------------------------------------
    private const float WeightContinental = 0.50f;
    private const float WeightRegional = 0.30f;
    private const float WeightLocal = 0.15f;
    private const float WeightMicro = 0.05f;

    // ------------------------------------------------------------------
    //  River constants
    // ------------------------------------------------------------------
    private const float RiverSourceMinElevation = 0.75f;
    private const float RiverMeanderChance = 0.10f;
    private const int RiverWidthIncreaseInterval = 40;
    private const int MaxRiverSteps = 4096;
    private const int LakeMinSize = 4;
    private const int LakeMaxSize = 12;

    // ------------------------------------------------------------------
    //  Forest constants
    // ------------------------------------------------------------------
    private const float ForestMinElevation = 0.40f;
    private const float ForestMaxElevation = 0.75f;
    private const int ForestClusterMinDistance = 12;
    private const int ForestClusterRadiusMin = 5;
    private const int ForestClusterRadiusMax = 20;
    private const float ForestDensityMin = 0.6f;
    private const float ForestDensityMax = 0.9f;
    private const int RiparianBuffer = 3;

    // ------------------------------------------------------------------
    //  Resource placement constants
    // ------------------------------------------------------------------
    private const int ResourceClusterMinSpacing = 20;

    // ------------------------------------------------------------------
    //  Playability thresholds
    // ------------------------------------------------------------------
    private const float MinFlatLandRatio = 0.15f;
    private const float MinWaterRatio = 0.05f;
    private const int StartingZoneSize = 20;
    private const int MaxReseeds = 10;

    /// <summary>
    /// Resource overlay for the generated map. Indexed identically to TileData arrays.
    /// Stored separately because TileData has no resource field.
    /// </summary>
    public static ResourceId[]? LastResourceOverlay { get; private set; }

    // ------------------------------------------------------------------
    //  Map type parameter sets
    // ------------------------------------------------------------------
    private static MapTypeParams GetParams(MapType type) => type switch
    {
        MapType.Valley => new MapTypeParams(
            ContinentalFreq: 0.002f, ContinentalAmplitude: 1.2f,
            RegionalFreq: 0.008f, RegionalAmplitude: 0.7f,
            WaterThreshold: 0.33f, BeachUpper: 0.38f,
            FlatUpper: 0.55f, HillUpper: 0.72f, MountainUpper: 0.88f,
            MinRivers: 2, MaxRivers: 3,
            ForestDensityMultiplier: 1.2f),

        MapType.Coastal => new MapTypeParams(
            ContinentalFreq: 0.003f, ContinentalAmplitude: 0.8f,
            RegionalFreq: 0.008f, RegionalAmplitude: 0.5f,
            WaterThreshold: 0.42f, BeachUpper: 0.47f,
            FlatUpper: 0.62f, HillUpper: 0.76f, MountainUpper: 0.91f,
            MinRivers: 1, MaxRivers: 2,
            ForestDensityMultiplier: 0.8f),

        MapType.Mountain => new MapTypeParams(
            ContinentalFreq: 0.003f, ContinentalAmplitude: 1.4f,
            RegionalFreq: 0.010f, RegionalAmplitude: 0.8f,
            WaterThreshold: 0.30f, BeachUpper: 0.35f,
            FlatUpper: 0.50f, HillUpper: 0.68f, MountainUpper: 0.85f,
            MinRivers: 2, MaxRivers: 3,
            ForestDensityMultiplier: 0.9f),

        MapType.Plains => new MapTypeParams(
            ContinentalFreq: 0.002f, ContinentalAmplitude: 0.6f,
            RegionalFreq: 0.006f, RegionalAmplitude: 0.4f,
            WaterThreshold: 0.36f, BeachUpper: 0.41f,
            FlatUpper: 0.68f, HillUpper: 0.82f, MountainUpper: 0.94f,
            MinRivers: 1, MaxRivers: 2,
            ForestDensityMultiplier: 1.0f),

        MapType.RiverDelta => new MapTypeParams(
            ContinentalFreq: 0.003f, ContinentalAmplitude: 0.7f,
            RegionalFreq: 0.009f, RegionalAmplitude: 0.5f,
            WaterThreshold: 0.38f, BeachUpper: 0.43f,
            FlatUpper: 0.60f, HillUpper: 0.74f, MountainUpper: 0.90f,
            MinRivers: 3, MaxRivers: 3,
            ForestDensityMultiplier: 1.1f),

        _ => GetParams(MapType.Plains),
    };

    // ==================================================================
    //  PUBLIC API
    // ==================================================================

    /// <summary>
    /// Generate a fully populated map. Retries with incremented seeds
    /// until playability validation passes (up to <see cref="MaxReseeds"/> attempts).
    /// </summary>
    /// <param name="seed">Base RNG seed.</param>
    /// <param name="mapType">Map archetype.</param>
    /// <param name="size">Map width/height in tiles (1-1024, should be power of 2).</param>
    /// <returns>Populated TileData and the validation report.</returns>
    public static (TileData tiles, ValidationResult validation) Generate(
        int seed, MapType mapType, int size = 512)
    {
        if (size < 1 || size > 1024)
            throw new ArgumentOutOfRangeException(nameof(size), "Map size must be 1-1024.");

        var p = GetParams(mapType);

        for (int attempt = 0; attempt < MaxReseeds; attempt++)
        {
            int currentSeed = seed + attempt;
            var tiles = new TileData(size);
            var heightmap = GenerateHeightmap(size, currentSeed, p);
            var resources = new ResourceId[size * size];

            ClassifyTerrain(tiles, heightmap, p);
            SimulateRivers(tiles, heightmap, size, currentSeed, p);
            PlaceForests(tiles, heightmap, size, currentSeed, p);
            PlaceResources(tiles, heightmap, resources, size, currentSeed, p);
            SetElevationBytes(tiles, heightmap);

            var validation = Validate(tiles, heightmap, resources, size, p);
            if (validation.IsPlayable)
            {
                LastResourceOverlay = resources;
                return (tiles, validation);
            }
        }

        // Last-resort: generate with final seed and accept it
        int finalSeed = seed + MaxReseeds;
        var finalTiles = new TileData(size);
        var finalHeightmap = GenerateHeightmap(size, finalSeed, p);
        var finalResources = new ResourceId[size * size];

        ClassifyTerrain(finalTiles, finalHeightmap, p);
        SimulateRivers(finalTiles, finalHeightmap, size, finalSeed, p);
        PlaceForests(finalTiles, finalHeightmap, size, finalSeed, p);
        PlaceResources(finalTiles, finalHeightmap, finalResources, size, finalSeed, p);
        SetElevationBytes(finalTiles, finalHeightmap);
        ForcePlayability(finalTiles, finalHeightmap, finalResources, size, p);

        var finalValidation = Validate(finalTiles, finalHeightmap, finalResources, size, p);
        LastResourceOverlay = finalResources;
        return (finalTiles, finalValidation);
    }

    // ==================================================================
    //  HEIGHTMAP GENERATION — 4-layer simplex noise
    // ==================================================================

    private static float[] GenerateHeightmap(int size, int seed, MapTypeParams p)
    {
        SimplexNoise.Seed(seed);
        var map = new float[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Layer 1: Continental (1 octave)
                float continental = SimplexNoise.Noise2D(
                    x * p.ContinentalFreq,
                    y * p.ContinentalFreq) * p.ContinentalAmplitude;

                // Layer 2: Regional (4 octaves)
                float regional = SimplexNoise.Fbm(
                    x * p.RegionalFreq,
                    y * p.RegionalFreq,
                    octaves: 4,
                    lacunarity: 2.0f,
                    persistence: 0.5f) * p.RegionalAmplitude;

                // Layer 3: Local detail (6 octaves)
                float local = SimplexNoise.Fbm(
                    x * 0.025f,
                    y * 0.025f,
                    octaves: 6,
                    lacunarity: 2.2f,
                    persistence: 0.45f) * 0.15f;

                // Layer 4: Micro detail (3 octaves)
                float micro = SimplexNoise.Fbm(
                    x * 0.08f,
                    y * 0.08f,
                    octaves: 3,
                    lacunarity: 2.0f,
                    persistence: 0.5f) * 0.04f;

                float combined = continental * WeightContinental
                               + regional * WeightRegional
                               + local * WeightLocal
                               + micro * WeightMicro;

                // Normalize from [-1,1] ish range to [0,1]
                map[y * size + x] = (combined + 1.0f) * 0.5f;
            }
        }

        // Clamp to [0,1]
        for (int i = 0; i < map.Length; i++)
        {
            if (map[i] < 0f) map[i] = 0f;
            else if (map[i] > 1f) map[i] = 1f;
        }

        return map;
    }

    // ==================================================================
    //  TERRAIN CLASSIFICATION
    // ==================================================================

    private static void ClassifyTerrain(TileData tiles, float[] heightmap, MapTypeParams p)
    {
        int count = tiles.Count;
        for (int i = 0; i < count; i++)
        {
            float h = heightmap[i];
            if (h < p.WaterThreshold)
                tiles.TerrainType[i] = (byte)TerrainId.Water;
            else if (h < p.BeachUpper)
                tiles.TerrainType[i] = (byte)TerrainId.Sand; // Beach / wetland
            else if (h < p.FlatUpper)
                tiles.TerrainType[i] = (byte)TerrainId.Grass; // Flat buildable
            else if (h < p.HillUpper)
                tiles.TerrainType[i] = (byte)TerrainId.Dirt; // Hills
            else if (h < p.MountainUpper)
                tiles.TerrainType[i] = (byte)TerrainId.Rock; // Mountains
            else
                tiles.TerrainType[i] = (byte)TerrainId.Rock; // Peaks (unbuildable)
        }
    }

    // ==================================================================
    //  RIVER SIMULATION
    // ==================================================================

    private static void SimulateRivers(
        TileData tiles, float[] heightmap, int size, int seed, MapTypeParams p)
    {
        var rng = new Random(seed ^ 0x52495645); // "RIVE" hash

        int riverCount = rng.Next(p.MinRivers, p.MaxRivers + 1);
        var sources = FindRiverSources(heightmap, size, riverCount, rng);

        foreach (var (sx, sy) in sources)
        {
            SimulateSingleRiver(tiles, heightmap, size, sx, sy, rng, p);
        }
    }

    private static List<(int x, int y)> FindRiverSources(
        float[] heightmap, int size, int count, Random rng)
    {
        // Collect all tiles above the river source threshold
        var candidates = new List<(int x, int y)>();
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (heightmap[y * size + x] >= RiverSourceMinElevation)
                    candidates.Add((x, y));
            }
        }

        if (candidates.Count == 0)
        {
            // Fallback: use highest points
            float maxH = 0;
            for (int i = 0; i < heightmap.Length; i++)
            {
                if (heightmap[i] > maxH) maxH = heightmap[i];
            }

            float threshold = maxH - 0.05f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (heightmap[y * size + x] >= threshold)
                        candidates.Add((x, y));
                }
            }
        }

        // Pick spread-out sources using rejection sampling
        var sources = new List<(int x, int y)>();
        int minSpacing = size / 4;
        int attempts = 0;

        while (sources.Count < count && attempts < 1000)
        {
            var candidate = candidates[rng.Next(candidates.Count)];
            bool tooClose = false;
            foreach (var src in sources)
            {
                int dx = candidate.x - src.x;
                int dy = candidate.y - src.y;
                if (dx * dx + dy * dy < minSpacing * minSpacing)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
                sources.Add(candidate);

            attempts++;
        }

        // If rejection sampling failed, just take whatever we can
        while (sources.Count < count && candidates.Count > 0)
        {
            sources.Add(candidates[rng.Next(candidates.Count)]);
        }

        return sources;
    }

    private static void SimulateSingleRiver(
        TileData tiles, float[] heightmap, int size,
        int startX, int startY, Random rng, MapTypeParams p)
    {
        int cx = startX;
        int cy = startY;
        int width = 1;
        int stepsSinceWidthIncrease = 0;
        var visited = new HashSet<long>();

        for (int step = 0; step < MaxRiverSteps; step++)
        {
            // Carve the river at current position
            CarveRiverTile(tiles, heightmap, size, cx, cy, width, p);
            long key = ((long)cy << 16) | (uint)cx;
            if (!visited.Add(key))
                break; // Stuck in a loop

            // Check if we've reached water or the map edge
            if (heightmap[cy * size + cx] < p.WaterThreshold)
                break;
            if (cx <= 0 || cx >= size - 1 || cy <= 0 || cy >= size - 1)
                break;

            // Find the lowest neighbour (gradient descent)
            int bestX = cx, bestY = cy;
            float bestH = heightmap[cy * size + cx];
            bool foundLower = false;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = cx + dx;
                    int ny = cy + dy;
                    if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;

                    float nh = heightmap[ny * size + nx];
                    if (nh < bestH)
                    {
                        bestH = nh;
                        bestX = nx;
                        bestY = ny;
                        foundLower = true;
                    }
                }
            }

            // Local minimum → create a lake
            if (!foundLower)
            {
                CreateLake(tiles, heightmap, size, cx, cy, rng, p);
                // Lower the heightmap at the lake edge to allow overflow
                float overflow = bestH - 0.01f;
                if (overflow < 0) overflow = 0;
                heightmap[cy * size + cx] = overflow;
                break;
            }

            // 10% meander chance: pick a random adjacent tile that isn't uphill
            if (rng.NextDouble() < RiverMeanderChance)
            {
                var candidates = new List<(int x, int y)>();
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = cx + dx;
                        int ny = cy + dy;
                        if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
                        if (heightmap[ny * size + nx] <= heightmap[cy * size + cx] + 0.02f)
                            candidates.Add((nx, ny));
                    }
                }

                if (candidates.Count > 0)
                {
                    var pick = candidates[rng.Next(candidates.Count)];
                    bestX = pick.x;
                    bestY = pick.y;
                }
            }

            cx = bestX;
            cy = bestY;

            // Width increases every N tiles
            stepsSinceWidthIncrease++;
            if (stepsSinceWidthIncrease >= RiverWidthIncreaseInterval)
            {
                stepsSinceWidthIncrease = 0;
                width = System.Math.Min(width + 1, 5);
            }
        }
    }

    private static void CarveRiverTile(
        TileData tiles, float[] heightmap, int size,
        int cx, int cy, int width, MapTypeParams p)
    {
        int half = width / 2;
        for (int dy = -half; dy <= half; dy++)
        {
            for (int dx = -half; dx <= half; dx++)
            {
                int nx = cx + dx;
                int ny = cy + dy;
                if (nx >= 0 && nx < size && ny >= 0 && ny < size)
                {
                    int idx = ny * size + nx;
                    tiles.TerrainType[idx] = (byte)TerrainId.Water;
                    // Depress the heightmap so rivers are consistent
                    heightmap[idx] = System.Math.Min(heightmap[idx], p.WaterThreshold - 0.02f);
                }
            }
        }
    }

    private static void CreateLake(
        TileData tiles, float[] heightmap, int size,
        int cx, int cy, Random rng, MapTypeParams p)
    {
        int radius = rng.Next(LakeMinSize, LakeMaxSize + 1);
        int r2 = radius * radius;

        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx * dx + dy * dy > r2) continue;
                int nx = cx + dx;
                int ny = cy + dy;
                if (nx >= 0 && nx < size && ny >= 0 && ny < size)
                {
                    int idx = ny * size + nx;
                    tiles.TerrainType[idx] = (byte)TerrainId.Water;
                    heightmap[idx] = System.Math.Min(heightmap[idx], p.WaterThreshold - 0.03f);
                }
            }
        }
    }

    // ==================================================================
    //  FOREST PLACEMENT — Poisson disc cluster centres
    // ==================================================================

    private static void PlaceForests(
        TileData tiles, float[] heightmap, int size, int seed, MapTypeParams p)
    {
        var rng = new Random(seed ^ 0x464F5245); // "FORE" hash
        var clusterCentres = PoissonDiscSample(size, ForestClusterMinDistance, rng);

        // Pre-compute water adjacency for riparian buffer
        var nearWater = ComputeWaterProximity(tiles, size, RiparianBuffer);

        foreach (var (cx, cy) in clusterCentres)
        {
            float elev = heightmap[cy * size + cx];
            if (elev < ForestMinElevation || elev > ForestMaxElevation)
                continue;

            int radius = rng.Next(ForestClusterRadiusMin, ForestClusterRadiusMax + 1);
            float density = ForestDensityMin + (float)rng.NextDouble() * (ForestDensityMax - ForestDensityMin);
            density *= p.ForestDensityMultiplier;
            if (density > 1.0f) density = 1.0f;

            int r2 = radius * radius;
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy > r2) continue;
                    int nx = cx + dx;
                    int ny = cy + dy;
                    if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;

                    int idx = ny * size + nx;
                    float tileElev = heightmap[idx];

                    // Only place on valid elevation range
                    if (tileElev < ForestMinElevation || tileElev > ForestMaxElevation)
                        continue;

                    // Skip water tiles
                    if (tiles.TerrainType[idx] == (byte)TerrainId.Water)
                        continue;

                    // Riparian buffer: always place forest near water
                    bool riparian = nearWater[idx];

                    // Density roll
                    if (!riparian && rng.NextDouble() > density)
                        continue;

                    // Don't overwrite water or sand (beach)
                    byte current = tiles.TerrainType[idx];
                    if (current == (byte)TerrainId.Water || current == (byte)TerrainId.Sand)
                        continue;

                    tiles.TerrainType[idx] = (byte)TerrainId.Forest;
                }
            }
        }
    }

    /// <summary>
    /// Simple Poisson disc sampling using a grid accelerator.
    /// Returns well-distributed points with a guaranteed minimum distance.
    /// </summary>
    private static List<(int x, int y)> PoissonDiscSample(int size, int minDist, Random rng)
    {
        int cellSize = (int)(minDist / 1.414f); // sqrt(2)
        if (cellSize < 1) cellSize = 1;
        int gridW = (size + cellSize - 1) / cellSize;
        var grid = new int[gridW * gridW];
        Array.Fill(grid, -1);

        var points = new List<(int x, int y)>();
        var active = new List<int>();

        // Seed point
        int sx = rng.Next(size);
        int sy = rng.Next(size);
        points.Add((sx, sy));
        active.Add(0);
        grid[(sy / cellSize) * gridW + (sx / cellSize)] = 0;

        int k = 30; // rejection attempts per active point

        while (active.Count > 0)
        {
            int activeIdx = rng.Next(active.Count);
            int pointIdx = active[activeIdx];
            var (px, py) = points[pointIdx];
            bool found = false;

            for (int attempt = 0; attempt < k; attempt++)
            {
                double angle = rng.NextDouble() * System.Math.PI * 2;
                double dist = minDist + rng.NextDouble() * minDist;
                int nx = px + (int)(System.Math.Cos(angle) * dist);
                int ny = py + (int)(System.Math.Sin(angle) * dist);

                if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;

                int gx = nx / cellSize;
                int gy = ny / cellSize;
                bool valid = true;

                // Check neighbouring cells
                for (int dy = -2; dy <= 2 && valid; dy++)
                {
                    for (int dx = -2; dx <= 2 && valid; dx++)
                    {
                        int cgx = gx + dx;
                        int cgy = gy + dy;
                        if (cgx < 0 || cgx >= gridW || cgy < 0 || cgy >= gridW) continue;
                        int cell = grid[cgy * gridW + cgx];
                        if (cell < 0) continue;
                        var (ex, ey) = points[cell];
                        int ddx = nx - ex;
                        int ddy = ny - ey;
                        if (ddx * ddx + ddy * ddy < minDist * minDist)
                            valid = false;
                    }
                }

                if (valid)
                {
                    int newIdx = points.Count;
                    points.Add((nx, ny));
                    active.Add(newIdx);
                    grid[gy * gridW + gx] = newIdx;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                active.RemoveAt(activeIdx);
            }
        }

        return points;
    }

    /// <summary>
    /// Returns a boolean array where true means the tile is within
    /// <paramref name="distance"/> tiles of a water tile.
    /// </summary>
    private static bool[] ComputeWaterProximity(TileData tiles, int size, int distance)
    {
        var result = new bool[size * size];
        int d2 = distance * distance;

        // BFS from all water tiles (more efficient for large maps)
        var queue = new Queue<(int x, int y, int depth)>();
        var visited = new bool[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int idx = y * size + x;
                if (tiles.TerrainType[idx] == (byte)TerrainId.Water)
                {
                    queue.Enqueue((x, y, 0));
                    visited[idx] = true;
                    result[idx] = true;
                }
            }
        }

        int[] dxArr = { -1, 1, 0, 0 };
        int[] dyArr = { 0, 0, -1, 1 };

        while (queue.Count > 0)
        {
            var (cx, cy, depth) = queue.Dequeue();
            if (depth >= distance) continue;

            for (int d = 0; d < 4; d++)
            {
                int nx = cx + dxArr[d];
                int ny = cy + dyArr[d];
                if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
                int nidx = ny * size + nx;
                if (visited[nidx]) continue;
                visited[nidx] = true;
                result[nidx] = true;
                queue.Enqueue((nx, ny, depth + 1));
            }
        }

        return result;
    }

    // ==================================================================
    //  RESOURCE PLACEMENT
    // ==================================================================

    private readonly record struct ResourceSpec(
        ResourceId Id,
        float MinElevation,
        float MaxElevation,
        int ClusterSize,
        int MinCount,
        TerrainId[] ValidTerrain);

    private static readonly ResourceSpec[] ResourceSpecs =
    [
        new(ResourceId.Iron,        0.55f, 0.85f, 8,  1, [TerrainId.Dirt, TerrainId.Rock]),
        new(ResourceId.Coal,        0.45f, 0.75f, 6,  1, [TerrainId.Dirt, TerrainId.Rock]),
        new(ResourceId.Stone,       0.50f, 0.90f, 10, 1, [TerrainId.Dirt, TerrainId.Rock, TerrainId.Grass]),
        new(ResourceId.FertileSoil, 0.38f, 0.58f, 12, 2, [TerrainId.Grass, TerrainId.Dirt]),
        new(ResourceId.Gold,        0.65f, 0.90f, 4,  0, [TerrainId.Rock]),
        new(ResourceId.Oil,         0.35f, 0.50f, 6,  0, [TerrainId.Grass, TerrainId.Sand]),
    ];

    private static void PlaceResources(
        TileData tiles, float[] heightmap, ResourceId[] resources,
        int size, int seed, MapTypeParams p)
    {
        var rng = new Random(seed ^ 0x52455352); // "RESR" hash

        foreach (var spec in ResourceSpecs)
        {
            int placed = 0;
            int targetClusters = System.Math.Max(spec.MinCount, 1 + size / 128);

            for (int cluster = 0; cluster < targetClusters * 3 && placed < targetClusters; cluster++)
            {
                int cx = rng.Next(size);
                int cy = rng.Next(size);
                int idx = cy * size + cx;
                float elev = heightmap[idx];

                if (elev < spec.MinElevation || elev > spec.MaxElevation) continue;

                // Check terrain validity
                var terrain = (TerrainId)tiles.TerrainType[idx];
                bool validTerrain = false;
                foreach (var vt in spec.ValidTerrain)
                {
                    if (terrain == vt) { validTerrain = true; break; }
                }
                if (!validTerrain) continue;

                // Check minimum spacing from same resource type
                if (HasNearbyResource(resources, size, cx, cy, spec.Id, ResourceClusterMinSpacing))
                    continue;

                // Place the cluster
                PlaceResourceCluster(tiles, heightmap, resources, size, cx, cy, spec, rng);
                placed++;
            }

            // Guarantee minimum deposits
            int guaranteeAttempts = 0;
            while (placed < spec.MinCount && guaranteeAttempts < 5000)
            {
                guaranteeAttempts++;
                int cx = rng.Next(size);
                int cy = rng.Next(size);
                int idx = cy * size + cx;

                // Relax elevation constraint for guarantees
                if (tiles.TerrainType[idx] == (byte)TerrainId.Water) continue;
                if (tiles.TerrainType[idx] == (byte)TerrainId.Rock &&
                    heightmap[idx] > BaseMountainUpper) continue;

                PlaceResourceCluster(tiles, heightmap, resources, size, cx, cy, spec, rng);
                placed++;
            }
        }
    }

    private static bool HasNearbyResource(
        ResourceId[] resources, int size, int cx, int cy, ResourceId id, int minDist)
    {
        int d2 = minDist * minDist;
        int startX = System.Math.Max(0, cx - minDist);
        int endX = System.Math.Min(size - 1, cx + minDist);
        int startY = System.Math.Max(0, cy - minDist);
        int endY = System.Math.Min(size - 1, cy + minDist);

        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                if (resources[y * size + x] == id)
                {
                    int dx = x - cx;
                    int dy = y - cy;
                    if (dx * dx + dy * dy < d2) return true;
                }
            }
        }
        return false;
    }

    private static void PlaceResourceCluster(
        TileData tiles, float[] heightmap, ResourceId[] resources,
        int size, int cx, int cy, ResourceSpec spec, Random rng)
    {
        int half = spec.ClusterSize / 2;
        int r2 = half * half;

        for (int dy = -half; dy <= half; dy++)
        {
            for (int dx = -half; dx <= half; dx++)
            {
                if (dx * dx + dy * dy > r2) continue;
                int nx = cx + dx;
                int ny = cy + dy;
                if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;

                int idx = ny * size + nx;
                if (tiles.TerrainType[idx] == (byte)TerrainId.Water) continue;
                if (resources[idx] != ResourceId.None) continue;

                // 70% fill within cluster radius
                if (rng.NextDouble() < 0.70)
                    resources[idx] = spec.Id;
            }
        }
    }

    // ==================================================================
    //  ELEVATION BYTE ENCODING
    // ==================================================================

    /// <summary>
    /// Convert the float [0,1] heightmap to the TileData ushort elevation (0-65535).
    /// Each unit represents 0.5m per the TileData spec (range 0m to 32767.5m).
    /// </summary>
    private static void SetElevationBytes(TileData tiles, float[] heightmap)
    {
        for (int i = 0; i < tiles.Count; i++)
        {
            tiles.Elevation[i] = (ushort)(heightmap[i] * 65535f);
        }
    }

    // ==================================================================
    //  PLAYABILITY VALIDATION
    // ==================================================================

    private static ValidationResult Validate(
        TileData tiles, float[] heightmap, ResourceId[] resources,
        int size, MapTypeParams p)
    {
        int totalTiles = size * size;
        int waterCount = 0;
        int flatCount = 0;

        for (int i = 0; i < totalTiles; i++)
        {
            byte t = tiles.TerrainType[i];
            if (t == (byte)TerrainId.Water) waterCount++;
            if (t == (byte)TerrainId.Grass || t == (byte)TerrainId.Sand) flatCount++;
        }

        bool hasWaterAccess = waterCount >= (int)(totalTiles * MinWaterRatio);
        bool hasEnoughFlat = flatCount >= (int)(totalTiles * MinFlatLandRatio);

        // Resource guarantees
        bool hasIron = false, hasCoal = false, hasStone = false;
        int fertileSoilClusters = 0;
        var countedFertile = new HashSet<long>();

        for (int i = 0; i < totalTiles; i++)
        {
            switch (resources[i])
            {
                case ResourceId.Iron: hasIron = true; break;
                case ResourceId.Coal: hasCoal = true; break;
                case ResourceId.Stone: hasStone = true; break;
                case ResourceId.FertileSoil:
                    // Count distinct clusters by grid cell
                    var (fx, fy) = (i % size / 20, i / size / 20);
                    long fkey = ((long)fy << 16) | (uint)fx;
                    if (countedFertile.Add(fkey))
                        fertileSoilClusters++;
                    break;
            }
        }

        bool hasResources = hasIron && hasCoal && hasStone && fertileSoilClusters >= 2;

        // Starting zone: find a 20x20 flat area near water
        var (startX, startY, hasStart) = FindStartingZone(tiles, heightmap, size, p);

        // Edge access: at least one map edge has buildable land
        bool hasEdge = CheckEdgeAccess(tiles, size);

        // Connectivity: flood-fill from starting zone reaches >80% of flat land
        bool connected = hasStart && CheckConnectivity(tiles, size, startX, startY, flatCount);

        bool playable = hasWaterAccess && hasEnoughFlat && hasResources
                     && hasStart && hasEdge && connected;

        return new ValidationResult(
            IsPlayable: playable,
            HasWaterAccess: hasWaterAccess,
            HasEnoughFlatLand: hasEnoughFlat,
            IsConnected: connected,
            HasRequiredResources: hasResources,
            HasStartingZone: hasStart,
            HasEdgeAccess: hasEdge,
            FlatTileCount: flatCount,
            WaterTileCount: waterCount,
            StartX: startX,
            StartY: startY);
    }

    private static (int x, int y, bool found) FindStartingZone(
        TileData tiles, float[] heightmap, int size, MapTypeParams p)
    {
        // Scan for a 20x20 area that is mostly flat and adjacent to water
        int zoneSize = System.Math.Min(StartingZoneSize, size);
        int step = System.Math.Max(1, zoneSize / 4);

        int bestX = -1, bestY = -1;
        int bestScore = -1;

        for (int y = 0; y <= size - zoneSize; y += step)
        {
            for (int x = 0; x <= size - zoneSize; x += step)
            {
                int flatTiles = 0;
                bool waterAdjacent = false;

                for (int dy = 0; dy < zoneSize; dy++)
                {
                    for (int dx = 0; dx < zoneSize; dx++)
                    {
                        int idx = (y + dy) * size + (x + dx);
                        byte t = tiles.TerrainType[idx];
                        if (t == (byte)TerrainId.Grass || t == (byte)TerrainId.Sand)
                            flatTiles++;
                        if (t == (byte)TerrainId.Water)
                            waterAdjacent = true;
                    }
                }

                // Check adjacent tiles for water if not found inside
                if (!waterAdjacent)
                {
                    waterAdjacent = CheckWaterNearby(tiles, size, x, y, zoneSize, 5);
                }

                int totalZone = zoneSize * zoneSize;
                if (flatTiles >= (int)(totalZone * 0.7) && waterAdjacent)
                {
                    int score = flatTiles;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestX = x;
                        bestY = y;
                    }
                }
            }
        }

        return (bestX, bestY, bestX >= 0);
    }

    private static bool CheckWaterNearby(
        TileData tiles, int size, int zoneX, int zoneY, int zoneSize, int searchDist)
    {
        int startX = System.Math.Max(0, zoneX - searchDist);
        int endX = System.Math.Min(size - 1, zoneX + zoneSize + searchDist);
        int startY = System.Math.Max(0, zoneY - searchDist);
        int endY = System.Math.Min(size - 1, zoneY + zoneSize + searchDist);

        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                if (tiles.TerrainType[y * size + x] == (byte)TerrainId.Water)
                    return true;
            }
        }
        return false;
    }

    private static bool CheckEdgeAccess(TileData tiles, int size)
    {
        // Check all four edges for buildable land
        for (int i = 0; i < size; i++)
        {
            if (IsBuildableTerrain(tiles.TerrainType[i])) return true;                          // Top
            if (IsBuildableTerrain(tiles.TerrainType[(size - 1) * size + i])) return true;      // Bottom
            if (IsBuildableTerrain(tiles.TerrainType[i * size])) return true;                   // Left
            if (IsBuildableTerrain(tiles.TerrainType[i * size + size - 1])) return true;        // Right
        }
        return false;
    }

    private static bool IsBuildableTerrain(byte terrainType)
    {
        return terrainType == (byte)TerrainId.Grass
            || terrainType == (byte)TerrainId.Sand
            || terrainType == (byte)TerrainId.Dirt
            || terrainType == (byte)TerrainId.Forest;
    }

    private static bool CheckConnectivity(
        TileData tiles, int size, int startX, int startY, int totalFlat)
    {
        if (startX < 0 || startY < 0) return false;

        // BFS from starting zone centre
        int cx = startX + StartingZoneSize / 2;
        int cy = startY + StartingZoneSize / 2;
        if (cx >= size) cx = size - 1;
        if (cy >= size) cy = size - 1;

        var visited = new bool[size * size];
        var queue = new Queue<int>();
        int startIdx = cy * size + cx;

        if (tiles.TerrainType[startIdx] == (byte)TerrainId.Water)
        {
            // Find nearest non-water tile in starting zone
            for (int dy = 0; dy < StartingZoneSize && dy + startY < size; dy++)
            {
                for (int dx = 0; dx < StartingZoneSize && dx + startX < size; dx++)
                {
                    int idx = (startY + dy) * size + (startX + dx);
                    if (tiles.TerrainType[idx] != (byte)TerrainId.Water)
                    {
                        startIdx = idx;
                        goto foundStart;
                    }
                }
            }
            return false;
        }
        foundStart:

        queue.Enqueue(startIdx);
        visited[startIdx] = true;
        int reachable = 0;

        int[] dxArr = { -1, 1, 0, 0 };
        int[] dyArr = { 0, 0, -1, 1 };

        while (queue.Count > 0)
        {
            int idx = queue.Dequeue();
            byte t = tiles.TerrainType[idx];
            if (t == (byte)TerrainId.Grass || t == (byte)TerrainId.Sand)
                reachable++;

            int ix = idx % size;
            int iy = idx / size;

            for (int d = 0; d < 4; d++)
            {
                int nx = ix + dxArr[d];
                int ny = iy + dyArr[d];
                if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
                int nidx = ny * size + nx;
                if (visited[nidx]) continue;
                byte nt = tiles.TerrainType[nidx];
                // Can traverse anything that isn't water or peak rock
                if (nt == (byte)TerrainId.Water) continue;
                visited[nidx] = true;
                queue.Enqueue(nidx);
            }
        }

        // Require 80% of flat tiles to be reachable
        return totalFlat == 0 || reachable >= (int)(totalFlat * 0.80);
    }

    // ==================================================================
    //  FORCE PLAYABILITY (last resort)
    // ==================================================================

    /// <summary>
    /// Brute-force corrections to make an otherwise unplayable map minimally viable.
    /// Called only when all re-seed attempts have been exhausted.
    /// </summary>
    private static void ForcePlayability(
        TileData tiles, float[] heightmap, ResourceId[] resources,
        int size, MapTypeParams p)
    {
        // Ensure minimum water by carving a river through the centre
        int waterCount = 0;
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles.TerrainType[i] == (byte)TerrainId.Water) waterCount++;
        }

        if (waterCount < (int)(tiles.Count * MinWaterRatio))
        {
            int mid = size / 2;
            for (int y = 0; y < size; y++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = mid + dx;
                    if (x >= 0 && x < size)
                    {
                        int idx = y * size + x;
                        tiles.TerrainType[idx] = (byte)TerrainId.Water;
                        heightmap[idx] = p.WaterThreshold - 0.05f;
                    }
                }
            }
        }

        // Ensure minimum flat land by flattening centre area
        int flatCount = 0;
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles.TerrainType[i] == (byte)TerrainId.Grass ||
                tiles.TerrainType[i] == (byte)TerrainId.Sand)
                flatCount++;
        }

        if (flatCount < (int)(tiles.Count * MinFlatLandRatio))
        {
            int cx = size / 2;
            int cy = size / 2;
            int radius = (int)System.Math.Sqrt(tiles.Count * MinFlatLandRatio / System.Math.PI) + 1;

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy > radius * radius) continue;
                    int nx = cx + dx;
                    int ny = cy + dy;
                    if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
                    int idx = ny * size + nx;
                    if (tiles.TerrainType[idx] != (byte)TerrainId.Water)
                    {
                        tiles.TerrainType[idx] = (byte)TerrainId.Grass;
                        heightmap[idx] = (p.BeachUpper + p.FlatUpper) * 0.5f;
                    }
                }
            }
        }

        // Guarantee resources by placing them near map centre
        var rng = new Random(42);
        EnsureResource(tiles, heightmap, resources, size, ResourceId.Iron, rng);
        EnsureResource(tiles, heightmap, resources, size, ResourceId.Coal, rng);
        EnsureResource(tiles, heightmap, resources, size, ResourceId.Stone, rng);
        EnsureResource(tiles, heightmap, resources, size, ResourceId.FertileSoil, rng);
        EnsureResource(tiles, heightmap, resources, size, ResourceId.FertileSoil, rng);
    }

    private static void EnsureResource(
        TileData tiles, float[] heightmap, ResourceId[] resources,
        int size, ResourceId id, Random rng)
    {
        // Check if already present
        for (int i = 0; i < resources.Length; i++)
        {
            if (resources[i] == id) return;
        }

        // Place a small cluster near the centre
        int cx = size / 2 + rng.Next(-size / 8, size / 8);
        int cy = size / 2 + rng.Next(-size / 8, size / 8);
        cx = System.Math.Clamp(cx, 5, size - 5);
        cy = System.Math.Clamp(cy, 5, size - 5);

        for (int dy = -3; dy <= 3; dy++)
        {
            for (int dx = -3; dx <= 3; dx++)
            {
                int nx = cx + dx;
                int ny = cy + dy;
                if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
                int idx = ny * size + nx;
                if (tiles.TerrainType[idx] != (byte)TerrainId.Water)
                    resources[idx] = id;
            }
        }
    }
}
