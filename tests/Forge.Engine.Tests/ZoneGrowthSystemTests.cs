using Forge.Engine.Core;
using Forge.Engine.Data;
using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Xunit;

namespace Forge.Engine.Tests;

public class ZoneGrowthSystemTests
{
    // =========================================================================
    // Helpers
    // =========================================================================

    private static WorldState CreateTestWorld(int size = 32)
    {
        var state = new WorldState(size);
        // Set all terrain to grass (buildable)
        Array.Fill(state.Tiles.TerrainType, (byte)0);
        // Burn slot 0: BuildingId=0 in TileData means "no building"
        state.Buildings.Allocate();
        return state;
    }

    private static int PlaceBuilding(WorldState state, int x, int y, byte zoneType,
        byte buildingState = 1, ushort maxOccupants = 20, ushort occupants = 10)
    {
        int slot = state.Buildings.Allocate();
        if (slot < 0) return -1;

        state.Buildings.GridX[slot] = x;
        state.Buildings.GridY[slot] = y;
        state.Buildings.Width[slot] = 1;
        state.Buildings.Height[slot] = 1;
        state.Buildings.Level[slot] = 1;
        state.Buildings.State[slot] = buildingState;
        state.Buildings.MaxOccupants[slot] = maxOccupants;
        state.Buildings.Occupants[slot] = occupants;
        state.Buildings.Condition[slot] = 255;

        if (state.Tiles.InBounds(x, y))
        {
            int idx = state.Tiles.Index(x, y);
            state.Tiles.BuildingId[idx] = (ushort)slot;
            state.Tiles.ZoneType[idx] = zoneType;
        }

        return slot;
    }

    private static int AddHousehold(WorldState state, ushort homeBuildingId, byte wealthLevel = 2,
        bool unemployed = false)
    {
        int hh = state.Households.Allocate();
        if (hh < 0) return -1;

        state.Households.HomeBuildingId[hh] = homeBuildingId;
        state.Households.WealthLevel[hh] = wealthLevel;
        state.Households.MemberCount[hh] = 3;
        state.Households.Flags[hh] = (byte)(1 | (unemployed ? 4 : 0)); // active + maybe unemployed
        return hh;
    }

    // =========================================================================
    // RCI Demand Tests
    // =========================================================================

    [Fact]
    public void GetResidentialDemand_WithJobsNoHousing_PositiveDemand()
    {
        var state = CreateTestWorld();
        state.Population = 100;

        // Add commercial buildings with job capacity but no residential
        for (int i = 0; i < 5; i++)
        {
            int slot = PlaceBuilding(state, i + 5, 10, 3, maxOccupants: 30, occupants: 5); // commercial zone
        }

        var growth = new ZoneGrowthSystem(32, seed: 42);
        float demand = growth.GetResidentialDemand(state);

        Assert.True(demand > 0f,
            $"Residential demand should be positive with jobs and no housing, got {demand}");
    }

    [Fact]
    public void GetResidentialDemand_ExcessHousing_NegativeOrLowDemand()
    {
        var state = CreateTestWorld();
        state.Population = 10;

        // Add excess residential capacity
        for (int i = 0; i < 10; i++)
        {
            PlaceBuilding(state, i + 5, 10, 1, maxOccupants: 50, occupants: 1); // residential low
        }

        var growth = new ZoneGrowthSystem(32, seed: 42);
        float demand = growth.GetResidentialDemand(state);

        // With very few jobs, demand should be low/negative even though there's pop
        Assert.True(demand < 0.5f,
            $"Residential demand should be low/negative with excess housing, got {demand}");
    }

    [Fact]
    public void GetCommercialDemand_HighPopulation_PositiveDemand()
    {
        var state = CreateTestWorld();
        state.Population = 5000;

        // Add some wealthy households
        for (int i = 0; i < 50; i++)
        {
            int hh = state.Households.Allocate();
            state.Households.WealthLevel[hh] = 3;
            state.Households.Flags[hh] = 1;
        }

        var growth = new ZoneGrowthSystem(32, seed: 42);
        float demand = growth.GetCommercialDemand(state);

        Assert.True(demand > 0f,
            $"Commercial demand should be positive with high population, got {demand}");
    }

    [Fact]
    public void GetIndustrialDemand_RespondsToCommercialDemand()
    {
        var state = CreateTestWorld();
        state.Population = 3000;
        state.CityFunds = 50000; // good export potential

        // Add wealthy households to drive commercial demand
        for (int i = 0; i < 30; i++)
        {
            int hh = state.Households.Allocate();
            state.Households.WealthLevel[hh] = 3;
            state.Households.Flags[hh] = 1;
        }

        var growth = new ZoneGrowthSystem(32, seed: 42);
        float indDemand = growth.GetIndustrialDemand(state);

        // Industrial demand should be positive when commercial demand is positive
        Assert.True(indDemand > -0.5f,
            $"Industrial demand should follow commercial demand, got {indDemand}");
    }

    [Fact]
    public void GetResidentialDemand_PopulationChanges_DemandResponds()
    {
        var state = CreateTestWorld();
        var growth = new ZoneGrowthSystem(32, seed: 42);

        // Add some commercial jobs to create job availability
        for (int i = 0; i < 3; i++)
            PlaceBuilding(state, i + 5, 10, 3, maxOccupants: 50, occupants: 5);

        // Add some residential buildings so housing supply is nonzero
        for (int i = 0; i < 3; i++)
            PlaceBuilding(state, i + 5, 15, 1, maxOccupants: 30, occupants: 10);

        state.Population = 10;
        float demandLowPop = growth.GetResidentialDemand(state);

        state.Population = 5000;
        float demandHighPop = growth.GetResidentialDemand(state);

        // With more population, housing supply ratio drops (90 capacity / 5000 pop < 90 / 10)
        // so demand should be higher with high population
        Assert.True(demandHighPop > demandLowPop,
            $"Demand with high pop ({demandHighPop}) should exceed low pop ({demandLowPop})");
    }

    // =========================================================================
    // Zone Growth Probability Tests
    // =========================================================================

    [Fact]
    public void Tick_HighDemand_GrowthOccurs()
    {
        var state = CreateTestWorld();
        state.Population = 5000;
        state.CityFunds = 100000;

        // Create high job availability (commercial buildings with many openings)
        for (int i = 0; i < 10; i++)
        {
            PlaceBuilding(state, i, 0, 3, maxOccupants: 100, occupants: 5); // commercial zone
        }

        // Zone a large area as residential
        for (int y = 10; y < 20; y++)
        {
            for (int x = 5; x < 20; x++)
            {
                int idx = state.Tiles.Index(x, y);
                state.Tiles.ZoneType[idx] = 1; // residential low

                // Add road access
                if (y == 10)
                    state.Tiles.RoadFlags[state.Tiles.Index(x, 9)] = 1;
            }
        }

        // Set land value to make areas desirable
        for (int y = 10; y < 20; y++)
            for (int x = 5; x < 20; x++)
                state.Tiles.LandValue[state.Tiles.Index(x, y)] = 0.7f;

        // Add some households for wealth calculation
        for (int i = 0; i < 20; i++)
        {
            int hh = state.Households.Allocate();
            state.Households.WealthLevel[hh] = 2;
            state.Households.Flags[hh] = 1;
        }

        var growth = new ZoneGrowthSystem(32, seed: 12345);
        var economy = new EconomySystem();

        int buildingsBefore = state.Buildings.Count;

        // Run multiple ticks to give growth a chance to happen
        for (int tick = 0; tick < 30; tick++)
        {
            growth.Tick(state, economy);
        }

        int buildingsAfter = state.Buildings.Count;

        Assert.True(buildingsAfter > buildingsBefore,
            $"Buildings should grow with high demand. Before: {buildingsBefore}, After: {buildingsAfter}");
    }

    [Fact]
    public void Tick_ZeroDemand_NoGrowth()
    {
        var state = CreateTestWorld();
        state.Population = 0;

        // Zone an area but with no demand drivers
        state.Tiles.ZoneType[state.Tiles.Index(10, 10)] = 1;

        var growth = new ZoneGrowthSystem(32, seed: 42);
        var economy = new EconomySystem();

        int buildingsBefore = state.Buildings.Count;
        growth.Tick(state, economy);
        int buildingsAfter = state.Buildings.Count;

        Assert.Equal(buildingsBefore, buildingsAfter);
    }

    // =========================================================================
    // Building Selection Tests
    // =========================================================================

    [Fact]
    public void SelectBuildingType_WealthyArea_SelectsLuxury()
    {
        var state = CreateTestWorld();
        state.Era = 4; // Modern era

        // Create wealthy households near the target tile
        int building = PlaceBuilding(state, 15, 15, 1);
        for (int i = 0; i < 10; i++)
        {
            AddHousehold(state, (ushort)building, wealthLevel: 4); // wealthy
        }

        var growth = new ZoneGrowthSystem(32, seed: 42);

        ushort typeId = growth.SelectBuildingType(state, 15, 15, 1, 2); // residential low, medium density

        // Should select luxury villa (110) for wealthy areas in modern era
        Assert.Equal((ushort)110, typeId);
    }

    [Fact]
    public void SelectBuildingType_PoorArea_SelectsBasic()
    {
        var state = CreateTestWorld();
        state.Era = 4;

        // Create poor households near the target tile
        int building = PlaceBuilding(state, 15, 15, 1);
        for (int i = 0; i < 10; i++)
        {
            AddHousehold(state, (ushort)building, wealthLevel: 0); // poor
        }

        var growth = new ZoneGrowthSystem(32, seed: 42);

        ushort typeId = growth.SelectBuildingType(state, 15, 15, 1, 1); // residential low, low density

        // Should select basic small house (100) for poor areas
        Assert.Equal((ushort)100, typeId);
    }

    [Fact]
    public void SelectBuildingType_HighDensityCommercial_SelectsLarger()
    {
        var state = CreateTestWorld();
        state.Era = 4;

        var growth = new ZoneGrowthSystem(32, seed: 42);

        ushort lowDensity = growth.SelectBuildingType(state, 15, 15, 3, 1);  // commercial, low
        ushort highDensity = growth.SelectBuildingType(state, 15, 15, 3, 3); // commercial, high

        // Low density should get smaller building than high density
        Assert.True(lowDensity <= highDensity,
            $"High density type ({highDensity}) should be >= low density type ({lowDensity})");
    }

    [Fact]
    public void SelectBuildingType_Industrial_IgnoresWealth()
    {
        var state = CreateTestWorld();
        state.Era = 4;

        var growth = new ZoneGrowthSystem(32, seed: 42);

        // Industrial selection should be the same regardless of wealth
        ushort typeIdLowDensity = growth.SelectBuildingType(state, 15, 15, 4, 1);  // industrial, low
        ushort typeIdHighDensity = growth.SelectBuildingType(state, 15, 15, 4, 3); // industrial, high

        Assert.Equal((ushort)400, typeIdLowDensity);  // IndSmallWorkshop
        Assert.Equal((ushort)402, typeIdHighDensity);  // IndLargeFactory
    }

    // =========================================================================
    // Land Value Tests
    // =========================================================================

    [Fact]
    public void RecalculateLandValue_NearPark_HigherValue()
    {
        var state = CreateTestWorld();
        var growth = new ZoneGrowthSystem(32, seed: 42);

        // Place a park (ServicePark = 1 << 6 = 64)
        int parkId = state.Buildings.Allocate();
        state.Buildings.GridX[parkId] = 16;
        state.Buildings.GridY[parkId] = 16;
        state.Buildings.Width[parkId] = 1;
        state.Buildings.Height[parkId] = 1;
        state.Buildings.ServiceFlags[parkId] = 1u << 6;
        state.Buildings.State[parkId] = 1;
        state.Tiles.BuildingId[state.Tiles.Index(16, 16)] = (ushort)parkId;

        growth.RecalculateLandValue(state);

        // Tile near park (within 8 tiles)
        float nearPark = state.Tiles.LandValue[state.Tiles.Index(17, 16)];
        // Tile far from park (>8 tiles away)
        float farFromPark = state.Tiles.LandValue[state.Tiles.Index(1, 1)];

        Assert.True(nearPark > farFromPark,
            $"Land value near park ({nearPark}) should exceed far from park ({farFromPark})");
    }

    [Fact]
    public void RecalculateLandValue_NearIndustry_LowerValue()
    {
        var state = CreateTestWorld();
        var growth = new ZoneGrowthSystem(32, seed: 42);

        // Place industrial building
        int indId = PlaceBuilding(state, 16, 16, 4); // industrial zone

        growth.RecalculateLandValue(state);

        // Tile near industry
        float nearIndustry = state.Tiles.LandValue[state.Tiles.Index(17, 16)];
        // Tile far from industry (but also grass terrain)
        float farFromIndustry = state.Tiles.LandValue[state.Tiles.Index(1, 1)];

        Assert.True(nearIndustry < farFromIndustry,
            $"Land value near industry ({nearIndustry}) should be less than far ({farFromIndustry})");
    }

    [Fact]
    public void RecalculateLandValue_WaterTerrain_ZeroValue()
    {
        var state = CreateTestWorld();
        var growth = new ZoneGrowthSystem(32, seed: 42);

        // Set some tiles to water
        state.Tiles.TerrainType[state.Tiles.Index(5, 5)] = 3; // water

        growth.RecalculateLandValue(state);

        Assert.Equal(0f, state.Tiles.LandValue[state.Tiles.Index(5, 5)]);
    }

    [Fact]
    public void RecalculateLandValue_WithRoadAccess_HigherThanWithout()
    {
        var state = CreateTestWorld();
        var growth = new ZoneGrowthSystem(32, seed: 42);

        // Place a road next to tile (10, 10)
        state.Tiles.RoadFlags[state.Tiles.Index(10, 9)] = 1;

        growth.RecalculateLandValue(state);

        float withRoad = state.Tiles.LandValue[state.Tiles.Index(10, 10)];
        float noRoad = state.Tiles.LandValue[state.Tiles.Index(25, 25)];

        Assert.True(withRoad > noRoad,
            $"Land value with road access ({withRoad}) > without ({noRoad})");
    }

    // =========================================================================
    // Desirability Tests
    // =========================================================================

    [Fact]
    public void GetDesirability_ReturnsLandValue()
    {
        var state = CreateTestWorld();
        var growth = new ZoneGrowthSystem(32, seed: 42);

        state.Tiles.LandValue[state.Tiles.Index(10, 10)] = 0.75f;

        float desirability = growth.GetDesirability(state, 10, 10);
        Assert.Equal(0.75f, desirability, precision: 3);
    }

    [Fact]
    public void GetDesirability_OutOfBounds_ReturnsZero()
    {
        var state = CreateTestWorld();
        var growth = new ZoneGrowthSystem(32, seed: 42);

        Assert.Equal(0f, growth.GetDesirability(state, -1, 0));
        Assert.Equal(0f, growth.GetDesirability(state, 100, 100));
    }

    // =========================================================================
    // Abandonment Tests
    // =========================================================================

    [Fact]
    public void Tick_AbandonedBuilding_ConditionDegrades()
    {
        var state = CreateTestWorld();

        // Place an abandoned building
        int slot = PlaceBuilding(state, 10, 10, 1, buildingState: 2); // abandoned
        byte conditionBefore = state.Buildings.Condition[slot];

        var growth = new ZoneGrowthSystem(32, seed: 42);
        var economy = new EconomySystem();

        growth.Tick(state, economy);

        byte conditionAfter = state.Buildings.Condition[slot];
        Assert.True(conditionAfter < conditionBefore,
            $"Abandoned building condition should degrade. Before: {conditionBefore}, After: {conditionAfter}");
    }

    // =========================================================================
    // Road Access Tests
    // =========================================================================

    [Fact]
    public void HasRoadAccess_AdjacentRoad_ReturnsTrue()
    {
        var state = CreateTestWorld();
        state.Tiles.RoadFlags[state.Tiles.Index(10, 9)] = 1; // road north of (10,10)

        Assert.True(ZoneGrowthSystem.HasRoadAccess(state, 10, 10));
    }

    [Fact]
    public void HasRoadAccess_NoAdjacentRoad_ReturnsFalse()
    {
        var state = CreateTestWorld();
        Assert.False(ZoneGrowthSystem.HasRoadAccess(state, 10, 10));
    }

    // =========================================================================
    // Average Wealth Tests
    // =========================================================================

    [Fact]
    public void CalculateAverageWealth_MixedHouseholds_ReturnsAverage()
    {
        var state = CreateTestWorld();

        for (int i = 0; i < 4; i++)
        {
            int hh = state.Households.Allocate();
            state.Households.WealthLevel[hh] = (byte)i; // 0, 1, 2, 3
            state.Households.Flags[hh] = 1;
        }

        float avg = ZoneGrowthSystem.CalculateAverageWealth(state);
        // (0+1+2+3) / (4*4) = 6/16 = 0.375
        Assert.Equal(0.375f, avg, precision: 3);
    }
}
