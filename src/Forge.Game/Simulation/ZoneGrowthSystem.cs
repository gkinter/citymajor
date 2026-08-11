using Forge.Engine.Core;
using Forge.Engine.Data;
using Forge.Engine.Simulation;

namespace Forge.Game.Simulation;

/// <summary>
/// Zone-based building growth and decline system. Handles RCI (Residential, Commercial,
/// Industrial) demand calculation, building spawning/upgrading in zoned tiles, and
/// abandonment/demolition of buildings in declining zones.
///
/// Runs once per game day. Growth and decline are probabilistic -- each tick rolls dice
/// against computed probabilities to decide whether to grow, upgrade, or abandon.
/// </summary>
public sealed class ZoneGrowthSystem
{
    private readonly Random _rng;
    private readonly InfluenceMap _landValueMap;
    private readonly int _worldSize;

    // =========================================================================
    // Zone type constants (from TileData.ZoneType)
    // =========================================================================

    private const byte ZoneNone = 0;
    private const byte ZoneResidentialLow = 1;
    private const byte ZoneResidentialHigh = 2;
    private const byte ZoneCommercial = 3;
    private const byte ZoneIndustrial = 4;
    private const byte ZoneOffice = 5;
    private const byte ZoneMixedUse = 6;
    private const byte ZoneAgricultural = 7;
    /// <summary>Park / recreation — no organic growth; raises nearby land value.</summary>
    private const byte ZonePark = 8;

    // =========================================================================
    // Building state constants (from BuildingData.State)
    // =========================================================================

    private const byte StateConstructing = 0;
    private const byte StateOperational = 1;
    private const byte StateAbandoned = 2;
    private const byte StateDemolishing = 3;

    // =========================================================================
    // Service flags for classification
    // =========================================================================

    private const uint ServicePark = 1 << 6;

    // =========================================================================
    // Growth tuning constants
    // =========================================================================

    /// <summary>Base growth probability multiplier. Higher = faster city growth.</summary>
    internal const float GrowthBaseMultiplier = 0.15f;

    /// <summary>Minimum demand required for any growth to occur.</summary>
    internal const float MinDemandForGrowth = 0.05f;

    /// <summary>Probability modifier for road access (0 if no road, 1 if road adjacent).</summary>
    internal const float RoadAccessWeight = 0.8f;

    /// <summary>Service coverage weight in growth probability.</summary>
    internal const float ServiceWeight = 0.3f;

    /// <summary>Decline base probability when demand is negative.</summary>
    internal const float DeclineBaseProbability = 0.05f;

    /// <summary>Pollution contribution to decline probability.</summary>
    internal const float DeclinePollutionWeight = 0.3f;

    /// <summary>Crime contribution to decline probability.</summary>
    internal const float DeclineCrimeWeight = 0.2f;

    /// <summary>Months of abandonment before auto-demolition.</summary>
    internal const int AbandonmentMonthsBeforeDemolition = 24;

    /// <summary>Condition value (0–255) at which construction completes.</summary>
    internal const int ConstructionCompleteCondition = 255;

    // =========================================================================
    // Construction duration (game days, before law speed mult)
    // =========================================================================

    internal const int ConstructionDaysResidentialLow = 3;
    internal const int ConstructionDaysResidentialHigh = 5;
    internal const int ConstructionDaysCommercial = 4;
    internal const int ConstructionDaysIndustrial = 7;
    internal const int ConstructionDaysDefault = 4;

    // =========================================================================
    // Demand formula weights
    // =========================================================================

    internal const float ResidentialJobWeight = 0.6f;
    internal const float ResidentialImmigrationPressure = 0.1f;
    internal const float CommercialPopulationWeight = 0.3f;
    internal const float IndustrialCommercialWeight = 0.5f;
    internal const float IndustrialExportWeight = 0.2f;

    // =========================================================================
    // Land value weights
    // =========================================================================

    internal const float LandValuePollutionWeight = -0.3f;
    internal const float LandValueCrimeWeight = -0.2f;
    internal const float LandValueTransportWeight = 0.15f;
    internal const float LandValueParkWeight = 0.2f;
    internal const float LandValueIndustryWeight = -0.15f;
    internal const float LandValueNoiseWeight = -0.1f;
    internal const float LandValueServiceWeight = 0.2f;

    // =========================================================================
    // Building type catalog (TypeId ranges for zone growth)
    // =========================================================================

    // Residential low-density
    private const ushort ResLowSmallHouse = 100;
    private const ushort ResLowMediumHouse = 101;
    private const ushort ResLowLargeHouse = 102;

    // Residential high-density
    private const ushort ResHighApartmentSmall = 200;
    private const ushort ResHighApartmentMedium = 201;
    private const ushort ResHighApartmentLarge = 202;

    // Commercial
    private const ushort ComSmallShop = 300;
    private const ushort ComMediumStore = 301;
    private const ushort ComLargeOffice = 302;

    // Industrial
    private const ushort IndSmallWorkshop = 400;
    private const ushort IndMediumFactory = 401;
    private const ushort IndLargeFactory = 402;

    // Wealth-appropriate residential upgrades
    private const ushort ResLuxuryVilla = 110;
    private const ushort ResLuxuryTower = 210;
    private const ushort ComUpscaleBoutique = 310;

    public ZoneGrowthSystem(int worldSize, int? seed = null)
    {
        _worldSize = worldSize;
        _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        _landValueMap = new InfluenceMap(worldSize, worldSize);
    }

    /// <summary>Read-only access to the land value map.</summary>
    public InfluenceMap LandValueMap => _landValueMap;

    // =========================================================================
    // Daily tick: check for zone growth/decline
    // =========================================================================

    /// <summary>
    /// Daily tick: evaluate each zoned tile for growth/decline.
    /// For empty zoned tiles, attempt to spawn a building if demand supports it.
    /// For occupied buildings in declining zones, attempt abandonment.
    /// </summary>
    public void Tick(WorldState state, EconomySystem economy)
    {
        float resDemand = GetResidentialDemand(state);
        float comDemand = GetCommercialDemand(state);
        float indDemand = GetIndustrialDemand(state);

        var tiles = state.Tiles;
        var buildings = state.Buildings;

        for (int y = 0; y < tiles.Size; y++)
        {
            for (int x = 0; x < tiles.Size; x++)
            {
                int idx = tiles.Index(x, y);
                byte zone = tiles.ZoneType[idx];
                if (zone == ZoneNone) continue;

                ushort buildingId = tiles.BuildingId[idx];

                if (buildingId == 0)
                {
                    // Empty zoned tile: try to grow
                    float demand = GetDemandForZone(zone, resDemand, comDemand, indDemand);
                    if (demand < MinDemandForGrowth) continue;

                    float desirability = GetDesirability(state, x, y);
                    float services = GetServiceScoreForGrowth(state, x, y);
                    float roadAccess = HasRoadAccess(state, x, y) ? 1f : 0f;

                    float growthChance = demand * desirability * (1f + services * ServiceWeight)
                                        * (roadAccess * RoadAccessWeight + (1f - RoadAccessWeight))
                                        * GrowthBaseMultiplier
                                        * GetDensityGrowthMult(tiles.ZoneDensity[idx])
                                        * GetZoneLawSpawnMult(state, zone);

                    growthChance = Math.Clamp(growthChance, 0f, 1f);

                    if (_rng.NextDouble() < growthChance)
                    {
                        SpawnBuilding(state, x, y, zone, tiles.ZoneDensity[idx]);
                    }
                }
                else
                {
                    // Occupied tile: check for decline
                    if (buildingId >= buildings.Capacity) continue;
                    if (!buildings.IsActive(buildingId)) continue;
                    if (buildings.State[buildingId] == StateAbandoned) continue;
                    if (buildings.State[buildingId] == StateConstructing) continue;

                    float demand = GetDemandForZone(zone, resDemand, comDemand, indDemand);
                    float pollution = tiles.Pollution[idx];
                    float crime = tiles.Crime[idx];

                    float declineChance = ComputeDeclineChance(demand, pollution, crime);

                    if (_rng.NextDouble() < declineChance)
                    {
                        buildings.State[buildingId] = StateAbandoned;
                    }
                }
            }
        }

        // Process auto-demolition of long-abandoned buildings
        ProcessAbandonedBuildings(state);

        // Advance in-progress construction and publish HUD counts
        ProcessConstruction(state);
        state.ConstructingBuildingCount = CountConstructingBuildings(state);
        state.AbandonedBuildingCount = CountAbandonedBuildings(state);
    }

    // =========================================================================
    // RCI Demand calculation
    // =========================================================================

    /// <summary>
    /// Residential demand = job_availability * weight + immigration_pressure - housing_supply.
    /// Returns a value where positive means growth, negative means decline.
    /// Normalized roughly to -1..+1 range.
    /// </summary>
    public float GetResidentialDemand(WorldState state)
    {
        float jobAvailability = CalculateJobAvailability(state);
        float housingSupply = CalculateHousingSupply(state);
        float populationNormalized = state.Population > 0 ? 1f : 0f;

        float demand = jobAvailability * ResidentialJobWeight
                     + ResidentialImmigrationPressure * populationNormalized
                     - housingSupply;

        return Math.Clamp(demand, -1f, 1f);
    }

    /// <summary>
    /// Commercial demand = population * wealth_avg * 0.3 - commercial_supply.
    /// More people with more money = more demand for shops and services.
    /// </summary>
    public float GetCommercialDemand(WorldState state)
    {
        float populationFactor = state.Population / 1000f; // normalize per 1000 pop
        float wealthAvg = CalculateAverageWealth(state);
        float commercialSupply = CalculateCommercialSupply(state);

        float demand = populationFactor * wealthAvg * CommercialPopulationWeight - commercialSupply;
        return Math.Clamp(demand, -1f, 1f);
    }

    /// <summary>
    /// Industrial demand = commercial_demand * 0.5 + export_demand - industrial_supply.
    /// Industry feeds commerce and exports.
    /// </summary>
    public float GetIndustrialDemand(WorldState state)
    {
        float commercialDemand = GetCommercialDemand(state);
        float industrialSupply = CalculateIndustrialSupply(state);

        // Export demand: simple proxy based on city prosperity
        float exportDemand = state.CityFunds > 30000 ? 0.3f :
                            state.CityFunds > 10000 ? 0.2f : 0.1f;

        float demand = commercialDemand * IndustrialCommercialWeight
                     + exportDemand * IndustrialExportWeight
                     - industrialSupply;

        return Math.Clamp(demand, -1f, 1f);
    }

    // =========================================================================
    // Desirability (land value normalized 0-1)
    // =========================================================================

    /// <summary>
    /// Get the desirability of a tile for building growth.
    /// Based on the land value map, normalized 0-1.
    /// Tiles with no land value data return a neutral 0.5.
    /// </summary>
    public float GetDesirability(WorldState state, int tileX, int tileY)
    {
        if (!state.Tiles.InBounds(tileX, tileY)) return 0f;

        float landValue = state.Tiles.LandValue[state.Tiles.Index(tileX, tileY)];
        // LandValue is already 0-1 in TileData
        return Math.Clamp(landValue, 0f, 1f);
    }

    // =========================================================================
    // Building selection
    // =========================================================================

    /// <summary>
    /// Select the building type to spawn based on zone, density, local wealth, and era.
    /// Returns a TypeId for the BuildingData pool.
    /// </summary>
    public ushort SelectBuildingType(WorldState state, int tileX, int tileY, byte zoneType, byte density)
    {
        float wealth = CalculateLocalWealth(state, tileX, tileY);
        int era = state.Era;

        return zoneType switch
        {
            ZoneResidentialLow => SelectResidentialLow(wealth, density, era),
            ZoneResidentialHigh => SelectResidentialHigh(wealth, density, era),
            ZoneCommercial => SelectCommercial(wealth, density, era),
            ZoneIndustrial => SelectIndustrial(density, era),
            ZoneOffice => SelectCommercial(wealth, density, era), // offices use commercial types
            ZoneMixedUse => SelectMixedUse(wealth, density, era),
            ZoneAgricultural => IndSmallWorkshop, // farms use workshop type
            ZonePark => 0, // parks do not spawn buildings
            _ => ResLowSmallHouse
        };
    }

    // =========================================================================
    // Building upgrade check
    // =========================================================================

    /// <summary>
    /// Check all operational buildings for upgrade potential.
    /// A building upgrades when: demand is high, services are good, and land value is rising.
    /// Maximum level is 5.
    /// </summary>
    public void CheckUpgrades(WorldState state)
    {
        var buildings = state.Buildings;
        var tiles = state.Tiles;

        float resDemand = GetResidentialDemand(state);
        float comDemand = GetCommercialDemand(state);
        float indDemand = GetIndustrialDemand(state);

        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if (buildings.State[i] != StateOperational) continue;
            if (buildings.Level[i] >= 5) continue; // already max

            int bx = buildings.GridX[i];
            int by = buildings.GridY[i];
            if (!tiles.InBounds(bx, by)) continue;

            int idx = tiles.Index(bx, by);
            byte zone = tiles.ZoneType[idx];

            float demand = GetDemandForZone(zone, resDemand, comDemand, indDemand);
            if (demand < 0.3f) continue; // need strong demand to upgrade

            float services = GetServiceScoreForGrowth(state, bx, by);
            if (services < 0.4f) continue; // need decent services

            float landValue = tiles.LandValue[idx];
            if (landValue < 0.3f) continue; // need decent land value

            float upgradeChance = demand * services * landValue * 0.05f;
            if (_rng.NextDouble() < upgradeChance)
            {
                buildings.Level[i]++;
                // Increase max occupants with level
                buildings.MaxOccupants[i] = (ushort)(buildings.MaxOccupants[i] * 1.3f);
            }
        }
    }

    // =========================================================================
    // Land value calculation
    // =========================================================================

    /// <summary>
    /// Recalculate land value for all tiles. Land value is a composite of:
    /// base_terrain + service_bonuses - pollution - crime + transport + parks - industry - noise
    /// Results are written to TileData.LandValue (0-1).
    /// </summary>
    public void RecalculateLandValue(WorldState state)
    {
        var tiles = state.Tiles;
        _landValueMap.ClearAll();

        for (int y = 0; y < tiles.Size; y++)
        {
            for (int x = 0; x < tiles.Size; x++)
            {
                int idx = tiles.Index(x, y);

                // Base terrain value: grass=0.5, forest=0.4, sand=0.3, water/rock=0
                float baseValue = tiles.TerrainType[idx] switch
                {
                    0 => 0.5f,  // grass
                    1 => 0.3f,  // dirt
                    2 => 0.3f,  // sand
                    3 => 0f,    // water
                    4 => 0f,    // rock
                    5 => 0.4f,  // forest
                    6 => 0.2f,  // marsh
                    7 => 0.3f,  // snow
                    _ => 0.3f
                };

                // Skip unbuildable terrain
                if (baseValue == 0f)
                {
                    tiles.LandValue[idx] = 0f;
                    continue;
                }

                // Service bonus
                float serviceMod = GetServiceScoreForGrowth(state, x, y) * LandValueServiceWeight;

                // Pollution penalty
                float pollutionMod = tiles.Pollution[idx] * LandValuePollutionWeight;

                // Crime penalty
                float crimeMod = tiles.Crime[idx] * LandValueCrimeWeight;

                // Transport bonus: tiles near roads are more valuable
                float transportMod = HasRoadAccess(state, x, y)
                    ? LandValueTransportWeight
                    : 0f;

                // Park bonus: check for nearby parks
                float parkMod = HasNearbyPark(state, x, y) ? LandValueParkWeight : 0f;

                // Industry penalty: nearby industrial zones decrease value for residential
                float industryMod = HasNearbyIndustry(state, x, y) ? LandValueIndustryWeight : 0f;

                // Noise penalty
                float noiseMod = tiles.Noise[idx] * LandValueNoiseWeight;

                float landValue = baseValue + serviceMod + pollutionMod + crimeMod
                                + transportMod + parkMod + industryMod + noiseMod;

                tiles.LandValue[idx] = Math.Clamp(landValue, 0f, 1f);
                _landValueMap.SetValue(x, y, tiles.LandValue[idx]);
            }
        }
    }

    // =========================================================================
    // Internal helpers: demand components
    // =========================================================================

    /// <summary>
    /// Job availability: ratio of unfilled jobs to labor force.
    /// Returns 0 (no jobs) to 1 (many unfilled jobs).
    /// </summary>
    internal float CalculateJobAvailability(WorldState state)
    {
        int totalJobs = 0;
        int filledJobs = 0;

        // Count total job capacity from commercial, industrial, and office buildings
        var buildings = state.Buildings;
        var tiles = state.Tiles;

        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if (buildings.State[i] != StateOperational) continue;

            int bx = buildings.GridX[i];
            int by = buildings.GridY[i];
            if (!tiles.InBounds(bx, by)) continue;

            byte zone = tiles.ZoneType[tiles.Index(bx, by)];
            if (zone == ZoneCommercial || zone == ZoneIndustrial || zone == ZoneOffice)
            {
                totalJobs += buildings.MaxOccupants[i];
                filledJobs += buildings.Occupants[i];
            }
        }

        if (totalJobs == 0) return 0.5f; // No commercial/industrial yet, neutral
        float unfilled = (float)(totalJobs - filledJobs) / totalJobs;
        return Math.Clamp(unfilled, 0f, 1f);
    }

    /// <summary>
    /// Housing supply: ratio of total housing capacity to population.
    /// Returns 0 (no housing) to 1+ (excess housing).
    /// </summary>
    internal float CalculateHousingSupply(WorldState state)
    {
        int totalCapacity = 0;
        var buildings = state.Buildings;
        var tiles = state.Tiles;

        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if (buildings.State[i] != StateOperational) continue;

            int bx = buildings.GridX[i];
            int by = buildings.GridY[i];
            if (!tiles.InBounds(bx, by)) continue;

            byte zone = tiles.ZoneType[tiles.Index(bx, by)];
            if (zone == ZoneResidentialLow || zone == ZoneResidentialHigh || zone == ZoneMixedUse)
            {
                totalCapacity += buildings.MaxOccupants[i];
            }
        }

        if (state.Population == 0) return totalCapacity > 0 ? 1f : 0f;
        return Math.Clamp((float)totalCapacity / state.Population, 0f, 2f);
    }

    /// <summary>
    /// Average household wealth level (0-4 scale, normalized to 0-1).
    /// </summary>
    internal static float CalculateAverageWealth(WorldState state)
    {
        int total = 0;
        int wealthSum = 0;

        for (int i = 0; i < state.Households.Capacity; i++)
        {
            if (!state.Households.IsActive(i)) continue;
            total++;
            wealthSum += state.Households.WealthLevel[i];
        }

        if (total == 0) return 0.5f;
        return Math.Clamp(wealthSum / (total * 4f), 0f, 1f);
    }

    /// <summary>
    /// Commercial supply: ratio of commercial building capacity to population demand.
    /// </summary>
    internal float CalculateCommercialSupply(WorldState state)
    {
        int totalCapacity = 0;
        var buildings = state.Buildings;
        var tiles = state.Tiles;

        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if (buildings.State[i] != StateOperational) continue;

            int bx = buildings.GridX[i];
            int by = buildings.GridY[i];
            if (!tiles.InBounds(bx, by)) continue;

            byte zone = tiles.ZoneType[tiles.Index(bx, by)];
            if (zone == ZoneCommercial || zone == ZoneOffice)
            {
                totalCapacity += buildings.MaxOccupants[i];
            }
        }

        if (state.Population == 0) return totalCapacity > 0 ? 1f : 0f;
        // Normalize: 1 commercial job per 5 population is equilibrium
        return Math.Clamp(totalCapacity / (state.Population * 0.2f), 0f, 2f);
    }

    /// <summary>
    /// Industrial supply: ratio of industrial capacity to commercial demand.
    /// </summary>
    internal float CalculateIndustrialSupply(WorldState state)
    {
        int totalCapacity = 0;
        var buildings = state.Buildings;
        var tiles = state.Tiles;

        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if (buildings.State[i] != StateOperational) continue;

            int bx = buildings.GridX[i];
            int by = buildings.GridY[i];
            if (!tiles.InBounds(bx, by)) continue;

            byte zone = tiles.ZoneType[tiles.Index(bx, by)];
            if (zone == ZoneIndustrial)
            {
                totalCapacity += buildings.MaxOccupants[i];
            }
        }

        // Normalize: 1 industrial job per 10 population is equilibrium
        if (state.Population == 0) return totalCapacity > 0 ? 1f : 0f;
        return Math.Clamp(totalCapacity / (state.Population * 0.1f), 0f, 2f);
    }

    // =========================================================================
    // Internal helpers: growth mechanics
    // =========================================================================

    /// <summary>
    /// Map a zone type to its corresponding demand value.
    /// </summary>
    private static float GetDemandForZone(byte zone, float resDemand, float comDemand, float indDemand)
    {
        return zone switch
        {
            ZoneResidentialLow or ZoneResidentialHigh => resDemand,
            ZoneCommercial or ZoneOffice => comDemand,
            ZoneIndustrial => indDemand,
            ZoneMixedUse => Math.Max(resDemand, comDemand),
            ZoneAgricultural => indDemand * 0.5f,
            ZonePark => 0f,
            _ => 0f
        };
    }

    /// <summary>
    /// Check if a tile has road access (adjacent to a road tile).
    /// </summary>
    internal static bool HasRoadAccess(WorldState state, int tileX, int tileY)
    {
        var tiles = state.Tiles;
        ReadOnlySpan<(int dx, int dy)> dirs = stackalloc (int, int)[]
        {
            (0, -1), (1, 0), (0, 1), (-1, 0)
        };

        foreach (var (dx, dy) in dirs)
        {
            int nx = tileX + dx;
            int ny = tileY + dy;
            if (tiles.InBounds(nx, ny) && tiles.RoadFlags[tiles.Index(nx, ny)] != 0)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Get a simplified service score for growth calculations.
    /// Checks power, water, and general service coverage.
    /// Returns 0-1.
    /// </summary>
    private static float GetServiceScoreForGrowth(WorldState state, int tileX, int tileY)
    {
        if (!state.Tiles.InBounds(tileX, tileY)) return 0f;
        int idx = state.Tiles.Index(tileX, tileY);

        float power = state.Tiles.PowerGrid[idx] != 0 ? 0.4f : 0f;
        float water = state.Tiles.WaterGrid[idx] != 0 ? 0.3f : 0f;

        // Service coverage from packed byte (each is 0-3, max 3)
        float fire = state.Tiles.GetFireCoverage(idx) / 3f * 0.1f;
        float police = state.Tiles.GetPoliceCoverage(idx) / 3f * 0.1f;
        float health = state.Tiles.GetHealthCoverage(idx) / 3f * 0.05f;
        float education = state.Tiles.GetEducationCoverage(idx) / 3f * 0.05f;

        return power + water + fire + police + health + education;
    }

    /// <summary>
    /// Spawn a building on a zoned tile. Allocates from the building pool,
    /// selects an appropriate type, and marks the tile.
    /// </summary>
    private void SpawnBuilding(WorldState state, int tileX, int tileY, byte zoneType, byte density)
    {
        ushort typeId = SelectBuildingType(state, tileX, tileY, zoneType, density);
        if (typeId == 0) return; // Park / no-growth zones

        var buildings = state.Buildings;
        int slot = buildings.Allocate();
        if (slot < 0) return; // Pool full

        buildings.GridX[slot] = tileX;
        buildings.GridY[slot] = tileY;
        buildings.Width[slot] = 1;
        buildings.Height[slot] = 1;
        buildings.TypeId[slot] = typeId;
        buildings.Level[slot] = 1;
        buildings.State[slot] = StateConstructing;
        buildings.Condition[slot] = 0;

        // Set max occupants based on zone and density
        buildings.MaxOccupants[slot] = CalculateMaxOccupants(zoneType, density);

        // Mark tile as occupied
        state.Tiles.BuildingId[state.Tiles.Index(tileX, tileY)] = (ushort)slot;
    }

    /// <summary>
    /// Advance constructing buildings toward operational. Uses Condition as 0–255 progress
    /// (matches BuildingRenderer construction visuals).
    /// </summary>
    private static void ProcessConstruction(WorldState state)
    {
        var buildings = state.Buildings;
        var tiles = state.Tiles;
        float speedMult = Math.Max(0.25f, state.LawConstructionSpeedMult);

        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if (buildings.State[i] != StateConstructing) continue;

            int bx = buildings.GridX[i];
            int by = buildings.GridY[i];
            if (!tiles.InBounds(bx, by)) continue;

            byte zone = tiles.ZoneType[tiles.Index(bx, by)];
            int days = GetConstructionDays(zone, buildings.Level[i], speedMult);
            int increment = Math.Max(1, ConstructionCompleteCondition / days);
            int newCondition = Math.Min(ConstructionCompleteCondition, buildings.Condition[i] + increment);
            buildings.Condition[i] = (byte)newCondition;

            if (newCondition >= ConstructionCompleteCondition)
            {
                buildings.State[i] = StateOperational;
                buildings.Condition[i] = (byte)ConstructionCompleteCondition;
            }
        }
    }

    /// <summary>Base construction duration in game days for a zone/level (before law speed mult).</summary>
    internal static int GetBaseConstructionDays(byte zoneType, byte level)
    {
        int baseDays = zoneType switch
        {
            ZoneResidentialLow => ConstructionDaysResidentialLow,
            ZoneResidentialHigh => ConstructionDaysResidentialHigh,
            ZoneCommercial or ZoneOffice => ConstructionDaysCommercial,
            ZoneIndustrial => ConstructionDaysIndustrial,
            ZoneMixedUse => ConstructionDaysResidentialHigh,
            ZoneAgricultural => ConstructionDaysResidentialLow,
            _ => ConstructionDaysDefault,
        };

        return baseDays + Math.Max(0, level - 1);
    }

    /// <summary>Effective construction days after law speed multiplier.</summary>
    internal static int GetConstructionDays(byte zoneType, byte level, float lawSpeedMult)
    {
        float adjusted = GetBaseConstructionDays(zoneType, level) / Math.Max(0.25f, lawSpeedMult);
        return Math.Clamp((int)Math.Ceiling(adjusted), 1, 30);
    }

    /// <summary>Higher painted density slightly increases spawn probability.</summary>
    internal static float GetDensityGrowthMult(byte density) => density switch
    {
        2 => 1.25f,
        3 => 1.5f,
        _ => 1f,
    };

    /// <summary>
    /// Effective spawn chance multiplier for a zone type.
    /// Compounds law + event (P6.2) city-wide and zone-specific multipliers.
    /// </summary>
    internal static float GetZoneLawSpawnMult(WorldState state, byte zoneType)
    {
        float zoneMult = zoneType switch
        {
            ZoneResidentialLow or ZoneResidentialHigh or ZoneMixedUse or ZoneAgricultural
                => state.LawResidentialSpawnMult,
            ZoneIndustrial => state.LawIndustrialSpawnMult * state.EventProductivityMult,
            ZoneCommercial or ZoneOffice
                => state.LawCommercialSpawnMult * state.EventCommercialSpawnMult,
            _ => 1f,
        };

        return state.LawSpawnDemandMult * state.EventSpawnDemandMult * zoneMult;
    }

    private static int CountConstructingBuildings(WorldState state)
    {
        int count = 0;
        var buildings = state.Buildings;
        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if (buildings.State[i] == StateConstructing) count++;
        }

        return count;
    }

    /// <summary>City-wide abandoned building count for snapshot / Cathedral HUD (P2.6).</summary>
    public static int CountAbandonedBuildings(WorldState state)
    {
        int count = 0;
        var buildings = state.Buildings;
        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if (buildings.State[i] == StateAbandoned) count++;
        }

        return count;
    }

    /// <summary>
    /// Decline probability for an occupied tile (Cathedral P2.6 / AGENT_05).
    /// Negative demand raises chance; pollution and crime add pressure. Capped at 10%/day.
    /// </summary>
    internal static float ComputeDeclineChance(float demand, float pollution, float crime)
    {
        float declineChance = Math.Max(0f,
            -demand * DeclineBaseProbability
            + pollution * DeclinePollutionWeight
            + crime * DeclineCrimeWeight);
        return Math.Clamp(declineChance, 0f, 0.1f);
    }

    /// <summary>
    /// Calculate max occupants based on zone type and density level.
    /// CATHEDRAL_P2 §4: each density step doubles the occupant multiplier (×1 → ×2 → ×4),
    /// so density 3 is 4× (double×double) vs density 1.
    /// </summary>
    internal static ushort CalculateMaxOccupants(byte zoneType, byte density)
    {
        int baseOccupants = zoneType switch
        {
            ZoneResidentialLow => 4,
            ZoneResidentialHigh => 20,
            ZoneCommercial => 8,
            ZoneIndustrial => 10,
            ZoneOffice => 15,
            ZoneMixedUse => 12,
            ZoneAgricultural => 3,
            _ => 4
        };

        // Density multiplier: low=1, medium=2, high=4 (each step doubles)
        int densityMul = density switch
        {
            0 => 1,
            1 => 1,
            2 => 2,
            3 => 4,
            _ => 1
        };

        return (ushort)Math.Min(baseOccupants * densityMul, ushort.MaxValue);
    }

    /// <summary>
    /// Process abandoned buildings: auto-demolish after <see cref="AbandonmentMonthsBeforeDemolition"/> months.
    /// We approximate this by checking building condition -- abandoned buildings lose condition
    /// over time, and are demolished when condition reaches 0.
    /// </summary>
    private static void ProcessAbandonedBuildings(WorldState state)
    {
        var buildings = state.Buildings;
        var tiles = state.Tiles;

        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if (buildings.State[i] != StateAbandoned) continue;

            // Degrade condition by ~10 per day (reaches 0 in ~25 days ~= 1 month at daily tick)
            int newCondition = buildings.Condition[i] - 10;
            if (newCondition <= 0)
            {
                // Demolish: clear tile and free building slot
                int bx = buildings.GridX[i];
                int by = buildings.GridY[i];
                int bw = buildings.Width[i];
                int bh = buildings.Height[i];

                for (int dy = 0; dy < bh; dy++)
                {
                    for (int dx = 0; dx < bw; dx++)
                    {
                        int tx = bx + dx;
                        int ty = by + dy;
                        if (tiles.InBounds(tx, ty))
                        {
                            tiles.BuildingId[tiles.Index(tx, ty)] = 0;
                        }
                    }
                }

                buildings.Free(i);
            }
            else
            {
                buildings.Condition[i] = (byte)newCondition;
            }
        }
    }

    // =========================================================================
    // Building type selection by zone
    // =========================================================================

    private static ushort SelectResidentialLow(float wealth, byte density, int era)
    {
        // Wealthy areas get luxury villas in modern+ eras
        if (wealth > 0.7f && era >= 4)
            return ResLuxuryVilla;

        return density switch
        {
            0 or 1 => ResLowSmallHouse,
            2 => ResLowMediumHouse,
            _ => ResLowLargeHouse
        };
    }

    private static ushort SelectResidentialHigh(float wealth, byte density, int era)
    {
        if (wealth > 0.7f && era >= 4)
            return ResLuxuryTower;

        return density switch
        {
            0 or 1 => ResHighApartmentSmall,
            2 => ResHighApartmentMedium,
            _ => ResHighApartmentLarge
        };
    }

    private static ushort SelectCommercial(float wealth, byte density, int era)
    {
        if (wealth > 0.7f && era >= 4)
            return ComUpscaleBoutique;

        return density switch
        {
            0 or 1 => ComSmallShop,
            2 => ComMediumStore,
            _ => ComLargeOffice
        };
    }

    private static ushort SelectIndustrial(byte density, int era)
    {
        return density switch
        {
            0 or 1 => IndSmallWorkshop,
            2 => IndMediumFactory,
            _ => IndLargeFactory
        };
    }

    private ushort SelectMixedUse(float wealth, byte density, int era)
    {
        // Mixed use: alternate between residential and commercial types
        if (_rng.NextDouble() < 0.5)
            return SelectResidentialHigh(wealth, density, era);
        return SelectCommercial(wealth, density, era);
    }

    // =========================================================================
    // Local calculation helpers
    // =========================================================================

    /// <summary>
    /// Calculate local wealth level from nearby households (0-1).
    /// </summary>
    internal static float CalculateLocalWealth(WorldState state, int tileX, int tileY)
    {
        int total = 0;
        int wealthSum = 0;
        const int radius = 8;

        for (int i = 0; i < state.Households.Capacity; i++)
        {
            if (!state.Households.IsActive(i)) continue;

            ushort homeId = state.Households.HomeBuildingId[i];
            if (homeId == 0 || homeId >= state.Buildings.Capacity) continue;
            if (!state.Buildings.IsActive(homeId)) continue;

            int bx = state.Buildings.GridX[homeId];
            int by = state.Buildings.GridY[homeId];
            if (Math.Abs(bx - tileX) > radius || Math.Abs(by - tileY) > radius) continue;

            total++;
            wealthSum += state.Households.WealthLevel[i];
        }

        if (total == 0) return 0.5f; // neutral
        return Math.Clamp(wealthSum / (total * 4f), 0f, 1f);
    }

    /// <summary>
    /// Check if any park building or painted park zone exists within a radius.
    /// </summary>
    private static bool HasNearbyPark(WorldState state, int tileX, int tileY)
    {
        const int radius = 8;
        for (int i = 0; i < state.Buildings.Capacity; i++)
        {
            if (!state.Buildings.IsActive(i)) continue;
            if ((state.Buildings.ServiceFlags[i] & ServicePark) == 0) continue;

            int dx = state.Buildings.GridX[i] - tileX;
            int dy = state.Buildings.GridY[i] - tileY;
            if (dx * dx + dy * dy <= radius * radius) return true;
        }

        var tiles = state.Tiles;
        int minX = Math.Max(0, tileX - radius);
        int maxX = Math.Min(tiles.Size - 1, tileX + radius);
        int minY = Math.Max(0, tileY - radius);
        int maxY = Math.Min(tiles.Size - 1, tileY + radius);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int dx = x - tileX;
                int dy = y - tileY;
                if (dx * dx + dy * dy > radius * radius) continue;
                if (tiles.ZoneType[tiles.Index(x, y)] == ZonePark)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Check if any industrial zone exists within a radius.
    /// </summary>
    private static bool HasNearbyIndustry(WorldState state, int tileX, int tileY)
    {
        const int radius = 6;
        var tiles = state.Tiles;
        int minX = Math.Max(0, tileX - radius);
        int maxX = Math.Min(tiles.Size - 1, tileX + radius);
        int minY = Math.Max(0, tileY - radius);
        int maxY = Math.Min(tiles.Size - 1, tileY + radius);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (tiles.ZoneType[tiles.Index(x, y)] == ZoneIndustrial
                    && tiles.BuildingId[tiles.Index(x, y)] != 0)
                    return true;
            }
        }
        return false;
    }
}
