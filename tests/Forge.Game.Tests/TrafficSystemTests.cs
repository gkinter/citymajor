using Forge.Engine.Data;
using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Xunit;

namespace Forge.Game.Tests;

public class TrafficSystemTests
{
    /// <summary>
    /// Create a world with a simple road network and some commuting households.
    /// </summary>
    private static (WorldState state, TrafficSystem traffic) CreateTestSetup(
        int worldSize = 64, int households = 10)
    {
        var state = new WorldState(worldSize, maxHouseholds: 256, maxBuildings: 64, maxRoadNodes: 128);
        var traffic = new TrafficSystem();

        // Create a simple road network: 4 nodes in a square
        int n0 = state.Roads.AddNode(10, 10);
        int n1 = state.Roads.AddNode(30, 10);
        int n2 = state.Roads.AddNode(30, 30);
        int n3 = state.Roads.AddNode(10, 30);

        var edges = new List<(int from, int to, float cost, byte level)>
        {
            (n0, n1, 20f, 1), (n1, n0, 20f, 1), // Paved road
            (n1, n2, 20f, 1), (n2, n1, 20f, 1),
            (n2, n3, 20f, 1), (n3, n2, 20f, 1),
            (n3, n0, 20f, 1), (n0, n3, 20f, 1),
            (n0, n2, 28f, 2), (n2, n0, 28f, 2), // Highway diagonal
        };
        state.Roads.BuildFromEdgeList(edges);

        // Place residential building at (10, 10)
        int resId = state.Buildings.Allocate();
        state.Buildings.GridX[resId] = 10;
        state.Buildings.GridY[resId] = 10;
        state.Buildings.State[resId] = 1;
        state.Buildings.MaxOccupants[resId] = 100;
        state.Tiles.ZoneType[state.Tiles.Index(10, 10)] = 1; // Residential
        state.Tiles.BuildingId[state.Tiles.Index(10, 10)] = (ushort)resId;

        // Place commercial building at (30, 30)
        int comId = state.Buildings.Allocate();
        state.Buildings.GridX[comId] = 30;
        state.Buildings.GridY[comId] = 30;
        state.Buildings.State[comId] = 1;
        state.Buildings.MaxOccupants[comId] = 50;
        state.Tiles.ZoneType[state.Tiles.Index(30, 30)] = 3; // Commercial
        state.Tiles.BuildingId[state.Tiles.Index(30, 30)] = (ushort)comId;

        // Set road flags on tiles so they are recognized
        state.Tiles.RoadFlags[state.Tiles.Index(10, 10)] = 0x10; // Paved
        state.Tiles.RoadFlags[state.Tiles.Index(30, 10)] = 0x10;
        state.Tiles.RoadFlags[state.Tiles.Index(30, 30)] = 0x10;
        state.Tiles.RoadFlags[state.Tiles.Index(10, 30)] = 0x10;

        // Add commuting households
        for (int i = 0; i < households; i++)
        {
            int slot = state.Households.Allocate();
            state.Households.MemberCount[slot] = 3;
            state.Households.AgeGroup[slot] = 1; // Working
            state.Households.Education[slot] = 1;
            state.Households.Income[slot] = 1500;
            state.Households.HomeBuildingId[slot] = (ushort)resId;
            state.Households.WorkBuildingId[slot] = (ushort)comId;
            state.Households.Flags[slot] = 1; // Active, employed
            state.Buildings.Occupants[resId]++;
            state.Buildings.Occupants[comId]++;
        }

        state.Population = households * 3;
        state.Happiness = 0.6f;

        return (state, traffic);
    }

    // =========================================================================
    // BPR Formula Tests
    // =========================================================================

    [Fact]
    public void BprFormula_ZeroVolume_ReturnsFreeFlowTime()
    {
        float result = TrafficSystem.CalculateBprTravelTime(10f, 0f, 100f);
        Assert.Equal(10f, result);
    }

    [Fact]
    public void BprFormula_VolumeEqualsCapacity_ReturnsExpectedCongestion()
    {
        // BPR: t = 10 * (1 + 0.15 * (100/100)^4) = 10 * 1.15 = 11.5
        float result = TrafficSystem.CalculateBprTravelTime(10f, 100f, 100f);
        Assert.Equal(11.5f, result, precision: 2);
    }

    [Fact]
    public void BprFormula_DoubleCapacity_SeverelyCongestedTime()
    {
        // BPR: t = 10 * (1 + 0.15 * (200/100)^4) = 10 * (1 + 0.15 * 16) = 10 * 3.4 = 34
        float result = TrafficSystem.CalculateBprTravelTime(10f, 200f, 100f);
        Assert.Equal(34f, result, precision: 1);
    }

    [Fact]
    public void BprFormula_HalfVolume_ModerateIncrease()
    {
        // BPR: t = 10 * (1 + 0.15 * (50/100)^4) = 10 * (1 + 0.15 * 0.0625) = 10 * 1.009375
        float result = TrafficSystem.CalculateBprTravelTime(10f, 50f, 100f);
        Assert.InRange(result, 10f, 10.1f);
    }

    [Fact]
    public void BprFormula_ZeroCapacity_ReturnsHighTime()
    {
        float result = TrafficSystem.CalculateBprTravelTime(10f, 50f, 0f);
        Assert.True(result > 10f, "Zero capacity should produce very high travel time");
    }

    // =========================================================================
    // Mode Choice Tests
    // =========================================================================

    [Fact]
    public void ModeChoice_ProbabilitiesSumToOne()
    {
        TrafficSystem.ComputeLogitProbabilities(
            1.0f, 0.5f, -1.0f, -0.5f,
            out float pCar, out float pTransit, out float pWalk, out float pCycle);

        float sum = pCar + pTransit + pWalk + pCycle;
        Assert.InRange(sum, 0.999f, 1.001f);
    }

    [Fact]
    public void ModeChoice_AllEqual_UniformDistribution()
    {
        TrafficSystem.ComputeLogitProbabilities(
            0f, 0f, 0f, 0f,
            out float pCar, out float pTransit, out float pWalk, out float pCycle);

        Assert.InRange(pCar, 0.24f, 0.26f);
        Assert.InRange(pTransit, 0.24f, 0.26f);
        Assert.InRange(pWalk, 0.24f, 0.26f);
        Assert.InRange(pCycle, 0.24f, 0.26f);
    }

    [Fact]
    public void ModeChoice_HighCarUtility_CarDominates()
    {
        TrafficSystem.ComputeLogitProbabilities(
            5.0f, 0f, 0f, 0f,
            out float pCar, out float pTransit, out float pWalk, out float pCycle);

        Assert.True(pCar > 0.9f, $"Car share {pCar} should dominate with high utility");
        Assert.True(pCar > pTransit);
        Assert.True(pCar > pWalk);
        Assert.True(pCar > pCycle);
    }

    [Fact]
    public void ModeChoice_VeryNegativeValues_NoCrash()
    {
        TrafficSystem.ComputeLogitProbabilities(
            -100f, -100f, -100f, -100f,
            out float pCar, out float pTransit, out float pWalk, out float pCycle);

        float sum = pCar + pTransit + pWalk + pCycle;
        Assert.InRange(sum, 0.999f, 1.001f);
    }

    [Fact]
    public void ModeChoice_MixedValues_ProbabilitiesAreNonNegative()
    {
        TrafficSystem.ComputeLogitProbabilities(
            -2f, 3f, -5f, 1f,
            out float pCar, out float pTransit, out float pWalk, out float pCycle);

        Assert.True(pCar >= 0f);
        Assert.True(pTransit >= 0f);
        Assert.True(pWalk >= 0f);
        Assert.True(pCycle >= 0f);
    }

    // =========================================================================
    // Cost Scaling Tests
    // =========================================================================

    [Fact]
    public void ScaleCostByIncome_HighIncome_ReducesCost()
    {
        float richPerceived = TrafficSystem.ScaleCostByIncome(10f, 10000f);
        float poorPerceived = TrafficSystem.ScaleCostByIncome(10f, 100f);

        Assert.True(richPerceived < poorPerceived,
            $"Rich ({richPerceived:F3}) should perceive less cost than poor ({poorPerceived:F3})");
    }

    [Fact]
    public void ScaleCostByIncome_ZeroIncome_ReturnsCost()
    {
        float result = TrafficSystem.ScaleCostByIncome(10f, 0f);
        Assert.Equal(10f, result);
    }

    // =========================================================================
    // Zone Assignment Tests
    // =========================================================================

    [Fact]
    public void ZoneAssignment_CoversAllTiles()
    {
        var (state, traffic) = CreateTestSetup();

        // Run a tick to initialize zones
        traffic.Tick(state, 0);

        int zoneCount = traffic.ZoneCount;
        Assert.True(zoneCount > 0, "Zone count should be positive after initialization");

        // Every in-bounds tile should map to a valid zone
        var seenZones = new HashSet<int>();
        for (int y = 0; y < state.Tiles.Size; y++)
        {
            for (int x = 0; x < state.Tiles.Size; x++)
            {
                int zone = traffic.GetZoneForTile(x, y);
                Assert.InRange(zone, 0, zoneCount - 1);
                seenZones.Add(zone);
            }
        }

        // All zones should be reachable from tile coordinates
        Assert.Equal(zoneCount, seenZones.Count);
    }

    // =========================================================================
    // O-D Matrix Tests
    // =========================================================================

    [Fact]
    public void OdMatrix_IsSymmetric_ForReturnTrips()
    {
        var (state, traffic) = CreateTestSetup(households: 5);

        // Tick to build O-D matrix
        traffic.Tick(state, 0);

        int zones = traffic.ZoneCount;
        if (zones == 0) return; // Skip if no zones

        // Check symmetry: for each O-D pair, there should be equal return trips
        // (since we add both directions in BuildOdMatrix)
        for (int o = 0; o < zones; o++)
        {
            for (int d = 0; d < zones; d++)
            {
                float od = traffic.GetOdValue(o, d);
                float doVal = traffic.GetOdValue(d, o);
                Assert.Equal(od, doVal, precision: 2);
            }
        }
    }

    // =========================================================================
    // Integration Tests
    // =========================================================================

    [Fact]
    public void Tick_WithCommuters_ProducesValidModeShares()
    {
        var (state, traffic) = CreateTestSetup(households: 20);

        traffic.Tick(state, 0);

        float totalShare = traffic.CarModeShare + traffic.TransitModeShare +
                           traffic.WalkModeShare + traffic.CycleModeShare;

        // Mode shares should sum to ~1.0 (or 0 if no trips)
        if (totalShare > 0)
        {
            Assert.InRange(totalShare, 0.99f, 1.01f);
        }

        // All shares should be non-negative
        Assert.True(traffic.CarModeShare >= 0f);
        Assert.True(traffic.TransitModeShare >= 0f);
        Assert.True(traffic.WalkModeShare >= 0f);
        Assert.True(traffic.CycleModeShare >= 0f);
    }

    [Fact]
    public void Tick_WithCommuters_CalculatesCommuteTime()
    {
        var (state, traffic) = CreateTestSetup(households: 10);

        traffic.Tick(state, 0);

        Assert.True(traffic.AverageCommuteMinutes >= 0f,
            "Average commute should be non-negative");
    }

    [Fact]
    public void Tick_EmptyWorld_DoesNotCrash()
    {
        var state = new WorldState(32);
        var traffic = new TrafficSystem();

        traffic.Tick(state, 0);

        Assert.Equal(0f, traffic.AverageCommuteMinutes);
        Assert.Equal(0f, traffic.CarModeShare);
    }

    [Fact]
    public void Tick_NoRoads_DoesNotCrash()
    {
        var state = new WorldState(32);
        var traffic = new TrafficSystem();

        // Add household but no roads
        int slot = state.Households.Allocate();
        state.Households.MemberCount[slot] = 2;
        state.Households.Flags[slot] = 1;

        traffic.Tick(state, 0);

        Assert.True(true, "Should not crash with no roads");
    }

    [Fact]
    public void InvalidateCache_ForcesReinitialization()
    {
        var (state, traffic) = CreateTestSetup(households: 5);

        traffic.Tick(state, 0);
        int initialZones = traffic.ZoneCount;

        traffic.InvalidateCache();
        traffic.Tick(state, 1);

        // Should still work after cache invalidation
        Assert.Equal(initialZones, traffic.ZoneCount);
    }

    [Fact]
    public void EdgeCongestion_WithTraffic_ProducesValues()
    {
        var (state, traffic) = CreateTestSetup(households: 20);

        traffic.Tick(state, 0);

        // Edge congestion array should exist
        Assert.NotNull(traffic.EdgeCongestion);

        // With 20 households commuting, some congestion should appear
        // (but may be zero if all trips are walk/cycle)
        foreach (float c in traffic.EdgeCongestion)
        {
            Assert.True(c >= 0f, "Congestion should be non-negative");
        }
    }
}
