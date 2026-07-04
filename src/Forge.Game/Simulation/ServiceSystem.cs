using Forge.Engine.Core;
using Forge.Engine.Data;
using Forge.Engine.Simulation;

namespace Forge.Game.Simulation;

/// <summary>
/// City services simulation: fire, police, health, education coverage and response.
/// Manages influence maps per service type, calculates coverage falloff, and handles
/// cascading effects between services (e.g., crime rises with low education).
///
/// Reuses the engine's <see cref="InfluenceMap"/> for spatial coverage calculations.
/// Service buildings act as influence sources; coverage decays with distance.
/// </summary>
public sealed class ServiceSystem
{
    // =========================================================================
    // Service coverage influence maps
    // =========================================================================

    private readonly InfluenceMap _fireCoverage;
    private readonly InfluenceMap _policeCoverage;
    private readonly InfluenceMap _healthCoverage;
    private readonly InfluenceMap _educationCoverage;
    private readonly InfluenceMap _pollutionMap;
    private readonly InfluenceMap _noiseMap;
    private readonly InfluenceMap _crimeMap;

    private readonly int _worldSize;

    // =========================================================================
    // Building type classification constants
    // =========================================================================

    // ServiceFlags bitmask (from BuildingData.ServiceFlags)
    private const uint ServicePower = 1 << 0;
    private const uint ServiceWater = 1 << 1;
    private const uint ServicePolice = 1 << 2;
    private const uint ServiceFire = 1 << 3;
    private const uint ServiceHealth = 1 << 4;
    private const uint ServiceEducation = 1 << 5;
    private const uint ServicePark = 1 << 6;
    private const uint ServicePowerPlant = 1 << 7;
    private const uint ServiceWaterPump = 1 << 8;

    // Zone types (from TileData.ZoneType)
    private const byte ZoneNone = 0;
    private const byte ZoneResidentialLow = 1;
    private const byte ZoneResidentialHigh = 2;
    private const byte ZoneCommercial = 3;
    private const byte ZoneIndustrial = 4;
    private const byte ZoneOffice = 5;
    private const byte ZoneMixedUse = 6;
    private const byte ZoneAgricultural = 7;

    // =========================================================================
    // Tuning constants
    // =========================================================================

    private const float DefaultServiceRadius = 15f;
    private const float DefaultServiceStrength = 1.0f;
    private const float FireStationRadius = 20f;
    private const float PoliceStationRadius = 18f;
    private const float HospitalRadius = 25f;
    private const float SchoolRadius = 15f;

    // Fire risk material multipliers
    internal const float MaterialWood = 1.5f;
    internal const float MaterialBrick = 1.0f;
    internal const float MaterialConcrete = 0.5f;

    // Crime formula weights
    internal const float CrimePoliceWeight = 0.7f;
    internal const float CrimeUnemploymentWeight = 0.5f;
    internal const float CrimePovertyWeight = 0.3f;
    internal const float CrimeEducationWeight = 0.2f;
    internal const float CrimeLightingWeight = 0.1f;
    internal const float CrimeAbandonedWeight = 0.4f;

    // Health formula weights
    internal const float HealthPollutionWeight = 0.3f;
    internal const float HealthCommuteWeight = 0.1f;
    internal const float HealthCareWeight = 0.4f;
    internal const float HealthFoodWeight = 0.1f;
    internal const float HealthExerciseWeight = 0.1f;

    // Pollution constants
    private const float IndustrialPollutionStrength = 0.8f;
    private const float IndustrialPollutionRadius = 12f;
    private const float PollutionDiffusionRate = 0.15f;
    private const float PollutionDecayRate = 0.98f;

    // Noise constants
    private const float RoadNoiseStrength = 0.3f;
    private const float IndustrialNoiseStrength = 0.6f;
    private const float IndustrialNoiseRadius = 10f;

    // BFS grid states for power/water
    private const byte GridNone = 0;
    private const byte GridConnected = 1;

    public ServiceSystem(int worldSize)
    {
        _worldSize = worldSize;
        _fireCoverage = new InfluenceMap(worldSize, worldSize);
        _policeCoverage = new InfluenceMap(worldSize, worldSize);
        _healthCoverage = new InfluenceMap(worldSize, worldSize);
        _educationCoverage = new InfluenceMap(worldSize, worldSize);
        _pollutionMap = new InfluenceMap(worldSize, worldSize);
        _noiseMap = new InfluenceMap(worldSize, worldSize);
        _crimeMap = new InfluenceMap(worldSize, worldSize);
    }

    // =========================================================================
    // Read-only access to influence maps (for overlays, tests, etc.)
    // =========================================================================

    public InfluenceMap FireCoverage => _fireCoverage;
    public InfluenceMap PoliceCoverage => _policeCoverage;
    public InfluenceMap HealthCoverage => _healthCoverage;
    public InfluenceMap EducationCoverage => _educationCoverage;
    public InfluenceMap PollutionMap => _pollutionMap;
    public InfluenceMap NoiseMap => _noiseMap;
    public InfluenceMap CrimeMap => _crimeMap;

    // =========================================================================
    // Event handlers: recalculate when buildings change
    // =========================================================================

    /// <summary>
    /// Called when a building is placed. Adds influence sources for any services
    /// the building provides and pollution/noise sources for industrial buildings.
    /// </summary>
    public void OnBuildingPlaced(BuildingPlacedEvent evt, WorldState state)
    {
        int buildingId = evt.BuildingId;
        if (buildingId < 0 || buildingId >= state.Buildings.Capacity) return;
        if (!state.Buildings.IsActive(buildingId)) return;

        uint flags = state.Buildings.ServiceFlags[buildingId];
        int cx = evt.TileX;
        int cy = evt.TileY;

        if ((flags & ServiceFire) != 0)
            _fireCoverage.AddSource(cx, cy, DefaultServiceStrength, FireStationRadius, FalloffType.Linear);

        if ((flags & ServicePolice) != 0)
            _policeCoverage.AddSource(cx, cy, DefaultServiceStrength, PoliceStationRadius, FalloffType.Linear);

        if ((flags & ServiceHealth) != 0)
            _healthCoverage.AddSource(cx, cy, DefaultServiceStrength, HospitalRadius, FalloffType.Linear);

        if ((flags & ServiceEducation) != 0)
            _educationCoverage.AddSource(cx, cy, DefaultServiceStrength, SchoolRadius, FalloffType.Linear);

        // Industrial buildings produce pollution and noise
        byte zone = state.Tiles.ZoneType[state.Tiles.Index(cx, cy)];
        if (zone == ZoneIndustrial)
        {
            _pollutionMap.AddSource(cx, cy, IndustrialPollutionStrength, IndustrialPollutionRadius, FalloffType.Exponential);
            _noiseMap.AddSource(cx, cy, IndustrialNoiseStrength, IndustrialNoiseRadius, FalloffType.Linear);
        }

        RecalculateAllMaps();
    }

    /// <summary>
    /// Called when a building is demolished. Removes matching influence sources.
    /// </summary>
    public void OnBuildingDemolished(BuildingDemolishedEvent evt, WorldState state)
    {
        // We remove sources at the demolished position for all service types.
        // RemoveSource is a no-op if no matching source exists.
        int cx = evt.TileX;
        int cy = evt.TileY;

        _fireCoverage.RemoveSource(cx, cy, DefaultServiceStrength, FireStationRadius, FalloffType.Linear);
        _policeCoverage.RemoveSource(cx, cy, DefaultServiceStrength, PoliceStationRadius, FalloffType.Linear);
        _healthCoverage.RemoveSource(cx, cy, DefaultServiceStrength, HospitalRadius, FalloffType.Linear);
        _educationCoverage.RemoveSource(cx, cy, DefaultServiceStrength, SchoolRadius, FalloffType.Linear);
        _pollutionMap.RemoveSource(cx, cy, IndustrialPollutionStrength, IndustrialPollutionRadius, FalloffType.Exponential);
        _noiseMap.RemoveSource(cx, cy, IndustrialNoiseStrength, IndustrialNoiseRadius, FalloffType.Linear);

        RecalculateAllMaps();
    }

    /// <summary>
    /// Recalculate all dirty influence map regions.
    /// </summary>
    private void RecalculateAllMaps()
    {
        _fireCoverage.Recalculate();
        _policeCoverage.Recalculate();
        _healthCoverage.Recalculate();
        _educationCoverage.Recalculate();
        _pollutionMap.Recalculate();
        _noiseMap.Recalculate();
        _crimeMap.Recalculate();
    }

    // =========================================================================
    // Daily tick (called from IronAndOakGame via SimulationLoop.OnDayTick)
    // =========================================================================

    /// <summary>
    /// Daily simulation tick. Updates per-tile service coverage values,
    /// writes fire risk and crime into TileData for other systems to read.
    /// </summary>
    public void DailyTick(WorldState state, double dt)
    {
        UpdateServiceCoverageTiles(state);
        UpdatePerTileFireRisk(state);
        UpdatePerTileCrime(state);
    }

    // =========================================================================
    // Monthly tick: heavier recalculations
    // =========================================================================

    /// <summary>
    /// Monthly tick. Recalculates pollution diffusion, noise, crime map, and
    /// utility grids (power, water).
    /// </summary>
    public void MonthlyTick(WorldState state)
    {
        RecalculatePowerGrid(state);
        RecalculateWaterGrid(state);
        UpdatePollution(state, 30f); // approximate 30 days
        UpdateNoise(state);

        // Rebuild crime map from current conditions
        RebuildCrimeMap(state);
    }

    // =========================================================================
    // Fire Department
    // =========================================================================

    /// <summary>
    /// Calculate fire risk at a tile.
    /// Formula: material_modifier * (1 - fire_coverage) * drought_modifier
    /// material: wood=1.5, brick=1.0, concrete=0.5 (derived from building level)
    /// drought: 1.0 normally, 1.3 in summer/heatwave
    /// </summary>
    public float CalculateFireRisk(WorldState state, int tileX, int tileY)
    {
        if (!state.Tiles.InBounds(tileX, tileY)) return 0f;
        int idx = state.Tiles.Index(tileX, tileY);

        // No building = minimal fire risk
        ushort buildingId = state.Tiles.BuildingId[idx];
        if (buildingId == 0) return 0.01f;

        // Base risk before material modifier
        const float baseRisk = 0.5f;

        // Material modifier based on building level:
        // Level 1 = wood (1.5), Level 2 = brick (1.0), Level 3+ = concrete (0.5)
        float materialMod = GetMaterialModifier(state, buildingId);

        // Fire coverage from influence map (0 = no coverage, 1 = full)
        float coverage = Math.Clamp(_fireCoverage.GetValue(tileX, tileY), 0f, 1f);

        // Drought modifier: heatwave (weather=6) or summer with low precipitation
        float droughtMod = 1.0f;
        if (state.WeatherCondition == 6) // heatwave
            droughtMod = 1.3f;
        else if (state.Season == 1 && state.Precipitation < 0.1f) // dry summer
            droughtMod = 1.15f;

        float risk = baseRisk * materialMod * (1f - coverage) * droughtMod;
        return Math.Clamp(risk, 0f, 1f);
    }

    /// <summary>
    /// Calculate fire response time in minutes for a tile.
    /// Formula: distance_to_nearest_station / speed + crew_readiness + traffic_delay
    /// </summary>
    public float CalculateFireResponseTime(WorldState state, int tileX, int tileY)
    {
        if (!state.Tiles.InBounds(tileX, tileY)) return float.MaxValue;

        // Find the nearest fire station by scanning buildings
        float minDist = float.MaxValue;
        var buildings = state.Buildings;

        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if ((buildings.ServiceFlags[i] & ServiceFire) == 0) continue;

            float dx = buildings.GridX[i] - tileX;
            float dy = buildings.GridY[i] - tileY;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist < minDist) minDist = dist;
        }

        if (minDist >= float.MaxValue) return 30f; // No fire station: 30 min response

        // Base speed: 2 tiles/minute for fire trucks
        const float firetruckSpeed = 2f;
        float travelTime = minDist / firetruckSpeed;

        // Crew readiness: 1 minute base dispatch time
        const float crewReadiness = 1f;

        // Traffic delay: based on local traffic density
        int idx = state.Tiles.Index(tileX, tileY);
        float traffic = state.Tiles.Traffic[idx];
        float trafficDelay = traffic * 5f; // up to 5 min delay in gridlock

        return travelTime + crewReadiness + trafficDelay;
    }

    /// <summary>
    /// Calculate fire damage multiplier based on response time.
    /// Formula: base * (1 + 0.15 * response_minutes)
    /// </summary>
    public static float CalculateFireDamage(float baseDamage, float responseMinutes)
    {
        return baseDamage * (1f + 0.15f * responseMinutes);
    }

    // =========================================================================
    // Police
    // =========================================================================

    /// <summary>
    /// Calculate crime rate at a tile.
    /// Formula: base * (1 - police*0.7) * (1 + unemployment*0.5) * (1 + poverty*0.3)
    ///        * (1 - education*0.2) * (1 - lighting*0.1) * (1 + abandoned*0.4)
    /// Returns 0.0 (no crime) to 1.0 (maximum crime).
    /// </summary>
    public float CalculateCrimeRate(WorldState state, int tileX, int tileY)
    {
        if (!state.Tiles.InBounds(tileX, tileY)) return 0f;

        const float baseCrime = 0.3f;

        // Police coverage (0-1)
        float police = Math.Clamp(_policeCoverage.GetValue(tileX, tileY), 0f, 1f);

        // Unemployment rate (approximated from household data)
        float unemployment = CalculateLocalUnemployment(state, tileX, tileY);

        // Poverty rate (approximated from household wealth levels)
        float poverty = CalculateLocalPoverty(state, tileX, tileY);

        // Education level (from education coverage)
        float education = Math.Clamp(_educationCoverage.GetValue(tileX, tileY), 0f, 1f);

        // Lighting: powered areas have lighting (simple proxy)
        int idx = state.Tiles.Index(tileX, tileY);
        float lighting = state.Tiles.PowerGrid[idx] != 0 ? 1f : 0f;

        // Abandoned buildings nearby
        float abandoned = CalculateLocalAbandonment(state, tileX, tileY);

        float crime = baseCrime
            * (1f - police * CrimePoliceWeight)
            * (1f + unemployment * CrimeUnemploymentWeight)
            * (1f + poverty * CrimePovertyWeight)
            * (1f - education * CrimeEducationWeight)
            * (1f - lighting * CrimeLightingWeight)
            * (1f + abandoned * CrimeAbandonedWeight);

        return Math.Clamp(crime, 0f, 1f);
    }

    // =========================================================================
    // Healthcare
    // =========================================================================

    /// <summary>
    /// Calculate health score for a tile.
    /// Formula: base - pollution*0.3 - commute_stress*0.1 + healthcare*0.4 + food*0.1 + exercise*0.1
    /// Returns 0.0 (terrible) to 1.0 (excellent).
    /// </summary>
    public float CalculateHealthScore(WorldState state, int tileX, int tileY)
    {
        if (!state.Tiles.InBounds(tileX, tileY)) return 0.5f;

        const float baseHealth = 0.6f;
        int idx = state.Tiles.Index(tileX, tileY);

        float pollution = Math.Clamp(state.Tiles.Pollution[idx], 0f, 1f);
        float commuteStress = Math.Clamp(state.Tiles.Traffic[idx], 0f, 1f);
        float healthcare = Math.Clamp(_healthCoverage.GetValue(tileX, tileY), 0f, 1f);

        // Food access: commercial zones nearby provide food (simple proxy)
        float food = CalculateLocalFoodAccess(state, tileX, tileY);

        // Exercise: parks nearby
        float exercise = CalculateLocalParkAccess(state, tileX, tileY);

        float health = baseHealth
            - pollution * HealthPollutionWeight
            - commuteStress * HealthCommuteWeight
            + healthcare * HealthCareWeight
            + food * HealthFoodWeight
            + exercise * HealthExerciseWeight;

        return Math.Clamp(health, 0f, 1f);
    }

    /// <summary>
    /// Calculate EMS survival rate based on response time.
    /// Under 5 min: 90%, 5-10 min: 70%, 10-15 min: 55%, over 15 min: 40%.
    /// </summary>
    public static float CalculateEmsSurvivalRate(float responseMinutes)
    {
        if (responseMinutes < 5f) return 0.90f;
        if (responseMinutes < 10f) return 0.70f;
        if (responseMinutes < 15f) return 0.55f;
        return 0.40f;
    }

    // =========================================================================
    // Education
    // =========================================================================

    /// <summary>
    /// Calculate education quality for a specific school building.
    /// quality = base * student_teacher_ratio_mod * funding_mod * condition_mod
    ///         * teacher_quality_mod * facilities_mod * class_size_mod
    /// Returns 0.0 (terrible) to 1.0 (excellent).
    /// </summary>
    public float CalculateEducationQuality(WorldState state, int buildingId)
    {
        if (buildingId < 0 || buildingId >= state.Buildings.Capacity) return 0f;
        if (!state.Buildings.IsActive(buildingId)) return 0f;
        if ((state.Buildings.ServiceFlags[buildingId] & ServiceEducation) == 0) return 0f;

        var buildings = state.Buildings;

        // Base quality from building level (1-5 mapped to 0.4-1.0)
        float baseQuality = 0.3f + buildings.Level[buildingId] * 0.14f;

        // Student-teacher ratio modifier: fewer students per teacher = better
        // Occupants represent students, MaxOccupants is capacity
        float occupancy = buildings.MaxOccupants[buildingId] > 0
            ? (float)buildings.Occupants[buildingId] / buildings.MaxOccupants[buildingId]
            : 0f;
        // Overcrowded schools (>80% capacity) lose quality
        float ratioMod = occupancy <= 0.8f ? 1.0f : 1.0f - (occupancy - 0.8f) * 2.5f;
        ratioMod = Math.Clamp(ratioMod, 0.5f, 1.0f);

        // Funding modifier: derived from city education expense allocation
        // Higher education budget = better funding (simplified: use city funds as proxy)
        float fundingMod = state.CityFunds > 20000 ? 1.0f :
                           state.CityFunds > 5000 ? 0.8f : 0.6f;

        // Condition modifier: building condition 0-255 mapped to 0.3-1.0
        float conditionMod = 0.3f + (buildings.Condition[buildingId] / 255f) * 0.7f;

        // Teacher quality: correlates with education level in area
        float educationCoverage = _educationCoverage.GetValue(
            buildings.GridX[buildingId], buildings.GridY[buildingId]);
        float teacherQuality = 0.5f + Math.Clamp(educationCoverage, 0f, 1f) * 0.5f;

        // Facilities: higher level buildings have better facilities
        float facilitiesMod = 0.5f + buildings.Level[buildingId] * 0.1f;
        facilitiesMod = Math.Clamp(facilitiesMod, 0.5f, 1.0f);

        // Class size modifier: inverse of occupancy rate
        float classSizeMod = 1.0f - occupancy * 0.3f;
        classSizeMod = Math.Clamp(classSizeMod, 0.5f, 1.0f);

        float quality = baseQuality * ratioMod * fundingMod * conditionMod
                        * teacherQuality * facilitiesMod * classSizeMod;
        return Math.Clamp(quality, 0f, 1f);
    }

    // =========================================================================
    // Utilities: Power Grid (BFS flood-fill)
    // =========================================================================

    /// <summary>
    /// Recalculate the power grid using BFS flood-fill from power plant buildings.
    /// Power spreads along roads and to adjacent buildings within range.
    /// </summary>
    public void RecalculatePowerGrid(WorldState state)
    {
        var tiles = state.Tiles;
        int size = tiles.Size;

        // Clear existing power grid
        Array.Clear(tiles.PowerGrid, 0, tiles.Count);

        // Find all power plant positions
        var queue = new Queue<(int x, int y)>();
        var visited = new bool[tiles.Count];

        for (int i = 0; i < state.Buildings.Capacity; i++)
        {
            if (!state.Buildings.IsActive(i)) continue;
            if ((state.Buildings.ServiceFlags[i] & ServicePowerPlant) == 0) continue;

            int bx = state.Buildings.GridX[i];
            int by = state.Buildings.GridY[i];
            int bw = state.Buildings.Width[i];
            int bh = state.Buildings.Height[i];

            // Mark all tiles of the power plant as powered and enqueue them
            for (int dy = 0; dy < bh; dy++)
            {
                for (int dx = 0; dx < bw; dx++)
                {
                    int px = bx + dx;
                    int py = by + dy;
                    if (!tiles.InBounds(px, py)) continue;
                    int pidx = tiles.Index(px, py);
                    if (!visited[pidx])
                    {
                        visited[pidx] = true;
                        tiles.PowerGrid[pidx] = GridConnected;
                        queue.Enqueue((px, py));
                    }
                }
            }
        }

        // BFS: spread power along roads and to adjacent tiles with buildings/roads
        ReadOnlySpan<(int dx, int dy)> powerDirs = stackalloc (int, int)[]
        {
            (0, -1), (1, 0), (0, 1), (-1, 0)
        };

        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();

            foreach (var (dx, dy) in powerDirs)
            {
                int nx = cx + dx;
                int ny = cy + dy;
                if (!tiles.InBounds(nx, ny)) continue;

                int nidx = tiles.Index(nx, ny);
                if (visited[nidx]) continue;

                // Power spreads through roads or to tiles with buildings
                bool hasRoad = tiles.RoadFlags[nidx] != 0;
                bool hasBuilding = tiles.BuildingId[nidx] != 0;

                if (hasRoad || hasBuilding)
                {
                    visited[nidx] = true;
                    tiles.PowerGrid[nidx] = GridConnected;
                    queue.Enqueue((nx, ny));
                }
            }
        }
    }

    // =========================================================================
    // Utilities: Water Grid (BFS with pressure falloff)
    // =========================================================================

    /// <summary>
    /// Recalculate the water grid using BFS from water pump buildings.
    /// Pressure drops with distance and elevation change.
    /// </summary>
    public void RecalculateWaterGrid(WorldState state)
    {
        var tiles = state.Tiles;
        int size = tiles.Size;

        // Clear existing water grid and pressure
        Array.Clear(tiles.WaterGrid, 0, tiles.Count);
        Array.Clear(tiles.WaterPressure, 0, tiles.Count);

        // BFS with pressure tracking
        var queue = new Queue<(int x, int y, float pressure)>();
        var visited = new bool[tiles.Count];

        // Find all water pump positions
        for (int i = 0; i < state.Buildings.Capacity; i++)
        {
            if (!state.Buildings.IsActive(i)) continue;
            if ((state.Buildings.ServiceFlags[i] & ServiceWaterPump) == 0) continue;

            int bx = state.Buildings.GridX[i];
            int by = state.Buildings.GridY[i];
            if (!tiles.InBounds(bx, by)) continue;

            int bidx = tiles.Index(bx, by);
            visited[bidx] = true;
            tiles.WaterGrid[bidx] = GridConnected;
            tiles.WaterPressure[bidx] = 1.0f;
            queue.Enqueue((bx, by, 1.0f));
        }

        const float pressureDropPerTile = 0.02f;
        const float pressureDropPerElevation = 0.0005f;
        const float minPressure = 0.05f;

        ReadOnlySpan<(int dx, int dy)> waterDirs = stackalloc (int, int)[]
        {
            (0, -1), (1, 0), (0, 1), (-1, 0)
        };

        while (queue.Count > 0)
        {
            var (cx, cy, pressure) = queue.Dequeue();

            int cIdx = tiles.Index(cx, cy);
            ushort cElev = tiles.Elevation[cIdx];

            foreach (var (dx, dy) in waterDirs)
            {
                int nx = cx + dx;
                int ny = cy + dy;
                if (!tiles.InBounds(nx, ny)) continue;

                int nidx = tiles.Index(nx, ny);
                if (visited[nidx]) continue;

                // Water only flows through roads or to buildings
                bool hasRoad = tiles.RoadFlags[nidx] != 0;
                bool hasBuilding = tiles.BuildingId[nidx] != 0;
                if (!hasRoad && !hasBuilding) continue;

                // Pressure drops with distance and elevation gain
                ushort nElev = tiles.Elevation[nidx];
                float elevDiff = Math.Max(0, (nElev - cElev));
                float newPressure = pressure - pressureDropPerTile - elevDiff * pressureDropPerElevation;

                if (newPressure < minPressure) continue;

                visited[nidx] = true;
                tiles.WaterGrid[nidx] = GridConnected;
                tiles.WaterPressure[nidx] = newPressure;
                queue.Enqueue((nx, ny, newPressure));
            }
        }
    }

    // =========================================================================
    // Pollution diffusion (wind-driven advection)
    // =========================================================================

    /// <summary>
    /// Update pollution map with diffusion and wind-driven advection.
    /// </summary>
    /// <param name="state">World state containing wind data.</param>
    /// <param name="dt">Time step in days.</param>
    public void UpdatePollution(WorldState state, float dt)
    {
        // Recalculate pollution sources from influence map
        _pollutionMap.Recalculate();

        // Diffusion: isotropic spreading with decay
        _pollutionMap.Diffuse(PollutionDiffusionRate, PollutionDecayRate);

        // Wind-driven advection
        float windX = MathF.Sin(state.WindDirection) * state.WindSpeed * 0.1f;
        float windY = -MathF.Cos(state.WindDirection) * state.WindSpeed * 0.1f;
        _pollutionMap.Advect(windX, windY, dt * 0.01f);

        // Write pollution values back to tile data
        var tiles = state.Tiles;
        for (int y = 0; y < tiles.Size; y++)
        {
            for (int x = 0; x < tiles.Size; x++)
            {
                int idx = tiles.Index(x, y);
                tiles.Pollution[idx] = Math.Clamp(_pollutionMap.GetValue(x, y), 0f, 1f);
            }
        }
    }

    // =========================================================================
    // Noise calculation
    // =========================================================================

    /// <summary>
    /// Update noise levels from roads and industrial buildings.
    /// Noise is recalculated from scratch (stateless per tick).
    /// </summary>
    public void UpdateNoise(WorldState state)
    {
        _noiseMap.RecalculateAll();

        var tiles = state.Tiles;
        for (int y = 0; y < tiles.Size; y++)
        {
            for (int x = 0; x < tiles.Size; x++)
            {
                int idx = tiles.Index(x, y);
                float noise = _noiseMap.GetValue(x, y);

                // Roads add noise based on traffic density
                if (tiles.RoadFlags[idx] != 0)
                {
                    noise += tiles.Traffic[idx] * RoadNoiseStrength;
                }

                tiles.Noise[idx] = Math.Clamp(noise, 0f, 1f);
            }
        }
    }

    // =========================================================================
    // Aggregate service score
    // =========================================================================

    /// <summary>
    /// Get an aggregate service score for a tile (used for satisfaction calculations).
    /// Combines fire, police, health, education, power, and water into a single 0-1 score.
    /// </summary>
    public float GetServiceScore(WorldState state, int tileX, int tileY)
    {
        if (!state.Tiles.InBounds(tileX, tileY)) return 0f;

        int idx = state.Tiles.Index(tileX, tileY);

        float fire = Math.Clamp(_fireCoverage.GetValue(tileX, tileY), 0f, 1f);
        float police = Math.Clamp(_policeCoverage.GetValue(tileX, tileY), 0f, 1f);
        float health = Math.Clamp(_healthCoverage.GetValue(tileX, tileY), 0f, 1f);
        float education = Math.Clamp(_educationCoverage.GetValue(tileX, tileY), 0f, 1f);
        float power = state.Tiles.PowerGrid[idx] != 0 ? 1f : 0f;
        float water = state.Tiles.WaterGrid[idx] != 0 ? 1f : 0f;

        // Weighted average: utilities are critical, services are important
        return (fire * 0.10f + police * 0.15f + health * 0.20f + education * 0.15f
              + power * 0.25f + water * 0.15f);
    }

    // =========================================================================
    // Internal helpers
    // =========================================================================

    /// <summary>
    /// Get material fire modifier from building level.
    /// Level 1 = wood (1.5), Level 2 = brick (1.0), Level 3+ = concrete (0.5).
    /// </summary>
    internal static float GetMaterialModifier(WorldState state, int buildingId)
    {
        if (buildingId < 0 || buildingId >= state.Buildings.Capacity) return MaterialBrick;
        if (!state.Buildings.IsActive(buildingId)) return MaterialBrick;

        byte level = state.Buildings.Level[buildingId];
        return level switch
        {
            1 => MaterialWood,
            2 => MaterialBrick,
            _ => MaterialConcrete
        };
    }

    /// <summary>
    /// Calculate local unemployment rate in a 5-tile radius.
    /// Returns 0.0 (full employment) to 1.0 (all unemployed).
    /// </summary>
    internal static float CalculateLocalUnemployment(WorldState state, int tileX, int tileY)
    {
        int totalHouseholds = 0;
        int unemployedHouseholds = 0;
        const int radius = 5;

        // Scan households that live in nearby buildings
        for (int i = 0; i < state.Households.Capacity; i++)
        {
            if (!state.Households.IsActive(i)) continue;

            ushort homeId = state.Households.HomeBuildingId[i];
            if (homeId == 0 || homeId >= state.Buildings.Capacity) continue;
            if (!state.Buildings.IsActive(homeId)) continue;

            int bx = state.Buildings.GridX[homeId];
            int by = state.Buildings.GridY[homeId];
            float dist = MathF.Sqrt((bx - tileX) * (bx - tileX) + (by - tileY) * (by - tileY));
            if (dist > radius) continue;

            totalHouseholds++;
            // Unemployed flag: bit 2 of household flags
            if ((state.Households.Flags[i] & 0x04) != 0)
                unemployedHouseholds++;
        }

        if (totalHouseholds == 0) return 0f;
        return (float)unemployedHouseholds / totalHouseholds;
    }

    /// <summary>
    /// Calculate local poverty rate in a 5-tile radius.
    /// Returns 0.0 (wealthy) to 1.0 (all poor).
    /// </summary>
    internal static float CalculateLocalPoverty(WorldState state, int tileX, int tileY)
    {
        int totalHouseholds = 0;
        int poorHouseholds = 0;
        const int radius = 5;

        for (int i = 0; i < state.Households.Capacity; i++)
        {
            if (!state.Households.IsActive(i)) continue;

            ushort homeId = state.Households.HomeBuildingId[i];
            if (homeId == 0 || homeId >= state.Buildings.Capacity) continue;
            if (!state.Buildings.IsActive(homeId)) continue;

            int bx = state.Buildings.GridX[homeId];
            int by = state.Buildings.GridY[homeId];
            float dist = MathF.Sqrt((bx - tileX) * (bx - tileX) + (by - tileY) * (by - tileY));
            if (dist > radius) continue;

            totalHouseholds++;
            if (state.Households.WealthLevel[i] == 0) // WealthLevel 0 = poor
                poorHouseholds++;
        }

        if (totalHouseholds == 0) return 0f;
        return (float)poorHouseholds / totalHouseholds;
    }

    /// <summary>
    /// Calculate fraction of abandoned buildings in a 5-tile radius.
    /// </summary>
    internal static float CalculateLocalAbandonment(WorldState state, int tileX, int tileY)
    {
        int totalBuildings = 0;
        int abandonedBuildings = 0;
        const int radius = 5;

        for (int i = 0; i < state.Buildings.Capacity; i++)
        {
            if (!state.Buildings.IsActive(i)) continue;

            int bx = state.Buildings.GridX[i];
            int by = state.Buildings.GridY[i];
            float dist = MathF.Sqrt((bx - tileX) * (bx - tileX) + (by - tileY) * (by - tileY));
            if (dist > radius) continue;

            totalBuildings++;
            if (state.Buildings.State[i] == 2) // State 2 = abandoned
                abandonedBuildings++;
        }

        if (totalBuildings == 0) return 0f;
        return (float)abandonedBuildings / totalBuildings;
    }

    /// <summary>
    /// Calculate local food access: fraction of nearby tiles with commercial zones.
    /// Returns 0-1.
    /// </summary>
    private static float CalculateLocalFoodAccess(WorldState state, int tileX, int tileY)
    {
        int count = 0;
        int commercial = 0;
        const int radius = 8;

        var tiles = state.Tiles;
        int minX = Math.Max(0, tileX - radius);
        int maxX = Math.Min(tiles.Size - 1, tileX + radius);
        int minY = Math.Max(0, tileY - radius);
        int maxY = Math.Min(tiles.Size - 1, tileY + radius);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int idx = tiles.Index(x, y);
                count++;
                if (tiles.ZoneType[idx] == ZoneCommercial && tiles.BuildingId[idx] != 0)
                    commercial++;
            }
        }

        if (count == 0) return 0f;
        // Normalize: if 5% of nearby tiles are active commercial, that's good food access
        return Math.Clamp(commercial / (count * 0.05f), 0f, 1f);
    }

    /// <summary>
    /// Calculate local park access: any park service building nearby.
    /// Returns 0-1.
    /// </summary>
    private static float CalculateLocalParkAccess(WorldState state, int tileX, int tileY)
    {
        const int radius = 10;

        for (int i = 0; i < state.Buildings.Capacity; i++)
        {
            if (!state.Buildings.IsActive(i)) continue;
            if ((state.Buildings.ServiceFlags[i] & ServicePark) == 0) continue;

            int bx = state.Buildings.GridX[i];
            int by = state.Buildings.GridY[i];
            float dist = MathF.Sqrt((bx - tileX) * (bx - tileX) + (by - tileY) * (by - tileY));
            if (dist <= radius) return 1f;
        }
        return 0f;
    }

    /// <summary>
    /// Update the packed ServiceCoverage byte in TileData from influence maps.
    /// Maps continuous coverage to 4 discrete levels (0-3).
    /// </summary>
    private void UpdateServiceCoverageTiles(WorldState state)
    {
        var tiles = state.Tiles;
        for (int y = 0; y < tiles.Size; y++)
        {
            for (int x = 0; x < tiles.Size; x++)
            {
                int idx = tiles.Index(x, y);

                int fireLevel = CoverageToLevel(_fireCoverage.GetValue(x, y));
                int policeLevel = CoverageToLevel(_policeCoverage.GetValue(x, y));
                int healthLevel = CoverageToLevel(_healthCoverage.GetValue(x, y));
                int educationLevel = CoverageToLevel(_educationCoverage.GetValue(x, y));

                tiles.SetFireCoverage(idx, fireLevel);
                tiles.SetPoliceCoverage(idx, policeLevel);
                tiles.SetHealthCoverage(idx, healthLevel);
                tiles.SetEducationCoverage(idx, educationLevel);
            }
        }
    }

    /// <summary>Map continuous coverage (0-1) to discrete level (0-3).</summary>
    private static int CoverageToLevel(float coverage)
    {
        if (coverage >= 0.75f) return 3;
        if (coverage >= 0.5f) return 2;
        if (coverage >= 0.25f) return 1;
        return 0;
    }

    /// <summary>
    /// Write per-tile fire risk from calculations into TileData.
    /// </summary>
    private void UpdatePerTileFireRisk(WorldState state)
    {
        var tiles = state.Tiles;
        for (int y = 0; y < tiles.Size; y++)
        {
            for (int x = 0; x < tiles.Size; x++)
            {
                tiles.FireRisk[tiles.Index(x, y)] = CalculateFireRisk(state, x, y);
            }
        }
    }

    /// <summary>
    /// Write per-tile crime from calculations into TileData.
    /// </summary>
    private void UpdatePerTileCrime(WorldState state)
    {
        var tiles = state.Tiles;
        for (int y = 0; y < tiles.Size; y++)
        {
            for (int x = 0; x < tiles.Size; x++)
            {
                tiles.Crime[tiles.Index(x, y)] = CalculateCrimeRate(state, x, y);
            }
        }
    }

    /// <summary>
    /// Rebuild the crime influence map from current per-tile crime values.
    /// This is used for overlay rendering and spatial queries.
    /// </summary>
    private void RebuildCrimeMap(WorldState state)
    {
        _crimeMap.ClearAll();
        var tiles = state.Tiles;
        for (int y = 0; y < tiles.Size; y++)
        {
            for (int x = 0; x < tiles.Size; x++)
            {
                float crime = tiles.Crime[tiles.Index(x, y)];
                if (crime > 0.01f)
                {
                    _crimeMap.SetValue(x, y, crime);
                }
            }
        }
    }
}
