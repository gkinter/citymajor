using Forge.Engine.Core;
using Forge.Engine.Data;
using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Xunit;

namespace Forge.Engine.Tests;

public class ServiceSystemTests
{
    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>Create a small world state with a service system for testing.</summary>
    private static (WorldState state, ServiceSystem services) CreateTestWorld(int size = 32)
    {
        var state = new WorldState(size);
        BurnSlotZero(state);
        var services = new ServiceSystem(size);
        return (state, services);
    }

    /// <summary>
    /// Burn slot 0 so that BuildingId=0 in TileData unambiguously means "no building".
    /// </summary>
    private static void BurnSlotZero(WorldState state)
    {
        if (state.Buildings.Count == 0)
            state.Buildings.Allocate(); // slot 0 is now reserved
    }

    /// <summary>
    /// Place a building with specific service flags and return its building ID.
    /// </summary>
    private static int PlaceServiceBuilding(WorldState state, int x, int y, uint serviceFlags,
        byte level = 1, byte buildingState = 1)
    {
        BurnSlotZero(state);
        int slot = state.Buildings.Allocate();
        if (slot < 0) return -1;

        state.Buildings.GridX[slot] = x;
        state.Buildings.GridY[slot] = y;
        state.Buildings.Width[slot] = 1;
        state.Buildings.Height[slot] = 1;
        state.Buildings.ServiceFlags[slot] = serviceFlags;
        state.Buildings.Level[slot] = level;
        state.Buildings.State[slot] = buildingState;
        state.Buildings.MaxOccupants[slot] = 20;

        if (state.Tiles.InBounds(x, y))
            state.Tiles.BuildingId[state.Tiles.Index(x, y)] = (ushort)slot;

        return slot;
    }

    // =========================================================================
    // Fire Risk Tests
    // =========================================================================

    [Fact]
    public void CalculateFireRisk_WoodBuilding_HigherThanBrick()
    {
        var (state, services) = CreateTestWorld();

        // Place a wood building (level 1)
        int woodId = PlaceServiceBuilding(state, 10, 10, 0, level: 1);

        // Place a brick building (level 2)
        int brickId = PlaceServiceBuilding(state, 15, 15, 0, level: 2);

        float woodRisk = services.CalculateFireRisk(state, 10, 10);
        float brickRisk = services.CalculateFireRisk(state, 15, 15);

        Assert.True(woodRisk > brickRisk,
            $"Wood fire risk ({woodRisk}) should exceed brick ({brickRisk})");
    }

    [Fact]
    public void CalculateFireRisk_ConcreteBuilding_LowestRisk()
    {
        var (state, services) = CreateTestWorld();

        // Level 3+ = concrete
        int concreteId = PlaceServiceBuilding(state, 10, 10, 0, level: 3);

        float concreteRisk = services.CalculateFireRisk(state, 10, 10);

        // Concrete modifier is 0.5, with no coverage = 0.5 * 1.0 * 1.0 = 0.5
        Assert.True(concreteRisk < 0.6f, $"Concrete risk should be low, got {concreteRisk}");
        Assert.True(concreteRisk > 0f, "Concrete risk should be nonzero without fire coverage");
    }

    [Fact]
    public void CalculateFireRisk_WithFireStation_ReducesRisk()
    {
        var (state, services) = CreateTestWorld();

        // Place a building
        PlaceServiceBuilding(state, 16, 16, 0, level: 1);

        float riskBefore = services.CalculateFireRisk(state, 16, 16);

        // Place a fire station nearby (ServiceFire = 1 << 3 = 8)
        int stationId = PlaceServiceBuilding(state, 15, 16, 1u << 3);
        services.OnBuildingPlaced(new BuildingPlacedEvent
        {
            BuildingId = stationId,
            TileX = 15,
            TileY = 16,
            TypeId = 0
        }, state);

        float riskAfter = services.CalculateFireRisk(state, 16, 16);

        Assert.True(riskAfter < riskBefore,
            $"Fire risk should decrease with fire station. Before: {riskBefore}, After: {riskAfter}");
    }

    [Fact]
    public void CalculateFireRisk_EmptyTile_MinimalRisk()
    {
        var (state, services) = CreateTestWorld();
        float risk = services.CalculateFireRisk(state, 5, 5);
        Assert.True(risk <= 0.02f, $"Empty tile should have minimal fire risk, got {risk}");
    }

    [Fact]
    public void CalculateFireRisk_DifferentMaterials_CorrectOrdering()
    {
        var (state, services) = CreateTestWorld();

        PlaceServiceBuilding(state, 5, 5, 0, level: 1);   // wood
        PlaceServiceBuilding(state, 10, 10, 0, level: 2);  // brick
        PlaceServiceBuilding(state, 20, 20, 0, level: 3);  // concrete

        float wood = services.CalculateFireRisk(state, 5, 5);
        float brick = services.CalculateFireRisk(state, 10, 10);
        float concrete = services.CalculateFireRisk(state, 20, 20);

        Assert.True(wood > brick, $"Wood ({wood}) > Brick ({brick})");
        Assert.True(brick > concrete, $"Brick ({brick}) > Concrete ({concrete})");
    }

    [Fact]
    public void GetMaterialModifier_ReturnsCorrectValues()
    {
        var state = new WorldState(16);
        int id = PlaceServiceBuilding(state, 5, 5, 0, level: 1);
        Assert.Equal(ServiceSystem.MaterialWood, ServiceSystem.GetMaterialModifier(state, id));

        int id2 = PlaceServiceBuilding(state, 6, 6, 0, level: 2);
        Assert.Equal(ServiceSystem.MaterialBrick, ServiceSystem.GetMaterialModifier(state, id2));

        int id3 = PlaceServiceBuilding(state, 7, 7, 0, level: 4);
        Assert.Equal(ServiceSystem.MaterialConcrete, ServiceSystem.GetMaterialModifier(state, id3));
    }

    // =========================================================================
    // Crime Tests
    // =========================================================================

    [Fact]
    public void CalculateCrimeRate_HighUnemployment_HigherCrime()
    {
        var (state, services) = CreateTestWorld();

        // Place a residential building and some households
        int buildingId = PlaceServiceBuilding(state, 16, 16, 0, level: 1);

        // Create employed households nearby
        for (int i = 0; i < 5; i++)
        {
            int hh = state.Households.Allocate();
            state.Households.HomeBuildingId[hh] = (ushort)buildingId;
            state.Households.Flags[hh] = 1; // active, employed
        }

        float crimeEmployed = services.CalculateCrimeRate(state, 16, 16);

        // Now create a new world with unemployed households
        var (state2, services2) = CreateTestWorld();
        int buildingId2 = PlaceServiceBuilding(state2, 16, 16, 0, level: 1);

        for (int i = 0; i < 5; i++)
        {
            int hh = state2.Households.Allocate();
            state2.Households.HomeBuildingId[hh] = (ushort)buildingId2;
            state2.Households.Flags[hh] = 1 | 4; // active + unemployed (bit 2)
        }

        float crimeUnemployed = services2.CalculateCrimeRate(state2, 16, 16);

        Assert.True(crimeUnemployed > crimeEmployed,
            $"Crime with unemployment ({crimeUnemployed}) should exceed without ({crimeEmployed})");
    }

    [Fact]
    public void CalculateCrimeRate_WithPolice_LowerCrime()
    {
        var (state, services) = CreateTestWorld();

        float crimeNoPolice = services.CalculateCrimeRate(state, 16, 16);

        // Place a police station (ServicePolice = 1 << 2 = 4)
        int stationId = PlaceServiceBuilding(state, 16, 16, 1u << 2);
        services.OnBuildingPlaced(new BuildingPlacedEvent
        {
            BuildingId = stationId,
            TileX = 16,
            TileY = 16,
            TypeId = 0
        }, state);

        float crimeWithPolice = services.CalculateCrimeRate(state, 16, 16);

        Assert.True(crimeWithPolice < crimeNoPolice,
            $"Crime with police ({crimeWithPolice}) should be less than without ({crimeNoPolice})");
    }

    [Fact]
    public void CalculateCrimeRate_WithAbandonedBuildings_HigherCrime()
    {
        var (state, services) = CreateTestWorld();

        float crimeNormal = services.CalculateCrimeRate(state, 16, 16);

        // Place abandoned buildings nearby
        for (int i = 0; i < 3; i++)
        {
            PlaceServiceBuilding(state, 15 + i, 16, 0, level: 1, buildingState: 2); // abandoned
        }

        float crimeAbandoned = services.CalculateCrimeRate(state, 16, 16);

        Assert.True(crimeAbandoned > crimeNormal,
            $"Crime with abandoned buildings ({crimeAbandoned}) should exceed normal ({crimeNormal})");
    }

    // =========================================================================
    // Power Grid BFS Tests
    // =========================================================================

    [Fact]
    public void RecalculatePowerGrid_ConnectsNearbyBuildings()
    {
        var state = new WorldState(32);
        var services = new ServiceSystem(32);

        // Place a power plant at (10, 10) with ServicePowerPlant = 1 << 7 = 128
        int powerPlant = PlaceServiceBuilding(state, 10, 10, 1u << 7);

        // Place roads connecting to a building at (15, 10)
        for (int x = 10; x <= 15; x++)
        {
            state.Tiles.RoadFlags[state.Tiles.Index(x, 10)] = 1; // has road
        }

        // Place a building at end of road
        PlaceServiceBuilding(state, 15, 10, 0);

        services.RecalculatePowerGrid(state);

        // Power plant tile should be powered
        Assert.Equal(1, state.Tiles.PowerGrid[state.Tiles.Index(10, 10)]);

        // Building at end of road should be powered
        Assert.Equal(1, state.Tiles.PowerGrid[state.Tiles.Index(15, 10)]);
    }

    [Fact]
    public void RecalculatePowerGrid_DisconnectedBuildings_NoPower()
    {
        var state = new WorldState(32);
        var services = new ServiceSystem(32);

        // Place a power plant at (5, 5)
        PlaceServiceBuilding(state, 5, 5, 1u << 7);

        // Place a building far away with no connecting roads at (25, 25)
        PlaceServiceBuilding(state, 25, 25, 0);

        services.RecalculatePowerGrid(state);

        // Power plant should be powered
        Assert.Equal(1, state.Tiles.PowerGrid[state.Tiles.Index(5, 5)]);

        // Disconnected building should NOT be powered
        Assert.Equal(0, state.Tiles.PowerGrid[state.Tiles.Index(25, 25)]);
    }

    [Fact]
    public void RecalculatePowerGrid_NoPowerPlant_NothingPowered()
    {
        var state = new WorldState(16);
        var services = new ServiceSystem(16);

        // Place some buildings and roads but no power plant
        PlaceServiceBuilding(state, 5, 5, 0);
        state.Tiles.RoadFlags[state.Tiles.Index(5, 5)] = 1;

        services.RecalculatePowerGrid(state);

        for (int i = 0; i < state.Tiles.Count; i++)
        {
            Assert.Equal(0, state.Tiles.PowerGrid[i]);
        }
    }

    [Fact]
    public void RecalculatePowerGrid_BFS_SpreadsAlongRoads()
    {
        var state = new WorldState(32);
        var services = new ServiceSystem(32);

        // Power plant at (5, 5)
        PlaceServiceBuilding(state, 5, 5, 1u << 7);

        // L-shaped road: east then south
        for (int x = 5; x <= 10; x++)
            state.Tiles.RoadFlags[state.Tiles.Index(x, 5)] = 1;
        for (int y = 5; y <= 10; y++)
            state.Tiles.RoadFlags[state.Tiles.Index(10, y)] = 1;

        // Building at end of L
        PlaceServiceBuilding(state, 10, 10, 0);

        services.RecalculatePowerGrid(state);

        // All road tiles and the building should be powered
        Assert.Equal(1, state.Tiles.PowerGrid[state.Tiles.Index(8, 5)]);
        Assert.Equal(1, state.Tiles.PowerGrid[state.Tiles.Index(10, 8)]);
        Assert.Equal(1, state.Tiles.PowerGrid[state.Tiles.Index(10, 10)]);
    }

    // =========================================================================
    // Water Grid Tests
    // =========================================================================

    [Fact]
    public void RecalculateWaterGrid_ConnectsNearbyBuildings()
    {
        var state = new WorldState(32);
        var services = new ServiceSystem(32);

        // Water pump at (10, 10): ServiceWaterPump = 1 << 8 = 256
        PlaceServiceBuilding(state, 10, 10, 1u << 8);

        // Road connection
        for (int x = 10; x <= 15; x++)
            state.Tiles.RoadFlags[state.Tiles.Index(x, 10)] = 1;

        PlaceServiceBuilding(state, 15, 10, 0);

        services.RecalculateWaterGrid(state);

        Assert.Equal(1, state.Tiles.WaterGrid[state.Tiles.Index(10, 10)]);
        Assert.Equal(1, state.Tiles.WaterGrid[state.Tiles.Index(15, 10)]);
        Assert.True(state.Tiles.WaterPressure[state.Tiles.Index(10, 10)] > 0.9f);
        Assert.True(state.Tiles.WaterPressure[state.Tiles.Index(15, 10)] < 1.0f,
            "Pressure should drop with distance");
    }

    // =========================================================================
    // Health Score Tests
    // =========================================================================

    [Fact]
    public void CalculateHealthScore_WithHospital_Higher()
    {
        var (state, services) = CreateTestWorld();

        float healthNoHospital = services.CalculateHealthScore(state, 16, 16);

        // Place a hospital (ServiceHealth = 1 << 4 = 16)
        int hospitalId = PlaceServiceBuilding(state, 16, 16, 1u << 4);
        services.OnBuildingPlaced(new BuildingPlacedEvent
        {
            BuildingId = hospitalId,
            TileX = 16,
            TileY = 16,
            TypeId = 0
        }, state);

        float healthWithHospital = services.CalculateHealthScore(state, 16, 16);

        Assert.True(healthWithHospital > healthNoHospital,
            $"Health with hospital ({healthWithHospital}) > without ({healthNoHospital})");
    }

    [Fact]
    public void CalculateHealthScore_WithPollution_Lower()
    {
        var (state, services) = CreateTestWorld();

        float healthClean = services.CalculateHealthScore(state, 16, 16);

        // Add pollution
        state.Tiles.Pollution[state.Tiles.Index(16, 16)] = 0.8f;

        float healthPolluted = services.CalculateHealthScore(state, 16, 16);

        Assert.True(healthPolluted < healthClean,
            $"Health with pollution ({healthPolluted}) < without ({healthClean})");
    }

    // =========================================================================
    // EMS Survival Rate Tests
    // =========================================================================

    [Fact]
    public void CalculateEmsSurvivalRate_FastResponse_HighSurvival()
    {
        Assert.Equal(0.90f, ServiceSystem.CalculateEmsSurvivalRate(3f));
        Assert.Equal(0.70f, ServiceSystem.CalculateEmsSurvivalRate(7f));
        Assert.Equal(0.55f, ServiceSystem.CalculateEmsSurvivalRate(12f));
        Assert.Equal(0.40f, ServiceSystem.CalculateEmsSurvivalRate(20f));
    }

    // =========================================================================
    // Education Quality Tests
    // =========================================================================

    [Fact]
    public void CalculateEducationQuality_HighLevelSchool_BetterQuality()
    {
        var (state, services) = CreateTestWorld();

        // Level 1 school
        int school1 = PlaceServiceBuilding(state, 10, 10, 1u << 5, level: 1);
        state.Buildings.MaxOccupants[school1] = 100;
        state.Buildings.Occupants[school1] = 50;

        // Level 3 school
        int school3 = PlaceServiceBuilding(state, 20, 20, 1u << 5, level: 3);
        state.Buildings.MaxOccupants[school3] = 100;
        state.Buildings.Occupants[school3] = 50;

        float quality1 = services.CalculateEducationQuality(state, school1);
        float quality3 = services.CalculateEducationQuality(state, school3);

        Assert.True(quality3 > quality1,
            $"Level 3 school quality ({quality3}) should exceed level 1 ({quality1})");
    }

    // =========================================================================
    // Service Score Tests
    // =========================================================================

    [Fact]
    public void GetServiceScore_PowerAndWater_SignificantContribution()
    {
        var (state, services) = CreateTestWorld();
        int idx = state.Tiles.Index(16, 16);

        float scoreWithout = services.GetServiceScore(state, 16, 16);

        state.Tiles.PowerGrid[idx] = 1;
        state.Tiles.WaterGrid[idx] = 1;

        float scoreWith = services.GetServiceScore(state, 16, 16);

        Assert.True(scoreWith > scoreWithout,
            $"Score with power/water ({scoreWith}) > without ({scoreWithout})");
    }

    // =========================================================================
    // Fire Damage Tests
    // =========================================================================

    [Fact]
    public void CalculateFireDamage_IncreasesWithResponseTime()
    {
        float fastDamage = ServiceSystem.CalculateFireDamage(100f, 2f);
        float slowDamage = ServiceSystem.CalculateFireDamage(100f, 10f);

        Assert.True(slowDamage > fastDamage,
            $"Slow response damage ({slowDamage}) > fast ({fastDamage})");
        Assert.Equal(130f, fastDamage, precision: 1);   // 100 * (1 + 0.15 * 2)
        Assert.Equal(250f, slowDamage, precision: 1);    // 100 * (1 + 0.15 * 10)
    }
}
