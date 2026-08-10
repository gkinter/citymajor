using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Forge.SimCore;
using Forge.SimWasm;
using Xunit;

namespace Forge.SimCore.Tests;

/// <summary>Characterization tests for Cathedral P4.1 traffic lite BPR foundation.</summary>
public sealed class CathedralTrafficBprTests
{
    [Fact]
    public void BprFormula_ZeroFlow_ReturnsFreeFlowTime()
    {
        float t0 = 12f;
        float result = TrafficBpr.CalculateTravelTime(t0, 0f, 800f);
        Assert.Equal(t0, result);
    }

    [Fact]
    public void BprFormula_FlowEqualsCapacity_ReturnsExpectedDelay()
    {
        // t = 10 * (1 + 0.15 * (100/100)^4) = 11.5
        float result = TrafficBpr.CalculateTravelTime(10f, 100f, 100f);
        Assert.Equal(11.5f, result, precision: 3);
    }

    [Fact]
    public void BprFormula_DoubleFlow_SevereCongestion()
    {
        // t = 10 * (1 + 0.15 * (200/100)^4) = 34
        float result = TrafficBpr.CalculateTravelTime(10f, 200f, 100f);
        Assert.Equal(34f, result, precision: 1);
    }

    [Fact]
    public void EdgeCapacity_PavedTwoLane_MatchesP12TierTable()
    {
        float cap = TrafficBpr.EdgeCapacity(1, 2f);
        Assert.Equal(RoadTier.RoadCapacityForLevel(1) * 2f, cap);
        Assert.Equal(1600f, cap);
    }

    [Fact]
    public void EdgeCapacity_HighwayFourLane_UsesTierCapacity()
    {
        float cap = TrafficBpr.EdgeCapacity(2, 4f);
        Assert.Equal(2200f * 4f, cap);
    }

    [Fact]
    public void TrafficSystem_Capacity_UsesP12TierTableNotLegacyBase()
    {
        // Paved 2-lane = 1600 veh/hr (P1.2); legacy BaseLaneCapacity was 50 × 2 = 100.
        Assert.Equal(1600f, TrafficBpr.EdgeCapacity(1, 2f));
        Assert.NotEqual(100f, TrafficBpr.EdgeCapacity(1, 2f));
    }

    [Fact]
    public void TrafficLite_SingleIteration_AssignsNonZeroCongestion()
    {
        Assert.Equal(1, WasmConfig.TrafficLiteFrankWolfeIterations);

        var host = new SimHost();
        host.Init(128);

        for (int i = 0; i < 12; i++)
            host.Tick(WasmConfig.TrafficLiteInterval);

        Assert.False(float.IsNaN(host.State.MeanTrafficDensity));
        Assert.True(host.State.MeanTrafficDensity >= 0f);
        Assert.True(host.Traffic.EdgeCongestion.Length > 0);
    }

    [Fact]
    public void TrafficSystem_CalculateBprTravelTime_ForwardsToSharedBpr()
    {
        float shared = TrafficBpr.CalculateTravelTime(8f, 40f, 80f);
        float system = TrafficSystem.CalculateBprTravelTime(8f, 40f, 80f);
        Assert.Equal(shared, system);
    }
}
