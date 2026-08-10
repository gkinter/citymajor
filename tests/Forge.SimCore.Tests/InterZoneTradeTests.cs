using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Forge.SimCore;
using Xunit;

namespace Forge.SimCore.Tests;

[Collection("SimHost")]
public sealed class InterZoneTradeTests
{
    [Fact]
    public void TradeCoefficients_SameZone_IsUnity()
    {
        Assert.Equal(1.0f, EconomySystem.TradeCoefficients[0, 0], 4);
        Assert.Equal(1.0f, EconomySystem.GetTradeFriction(0, 0, 2), 4);
    }

    [Fact]
    public void TradeCoefficients_OrthogonalNeighbor_IsAboutOnePointZeroFive()
    {
        Assert.Equal(1.05f, EconomySystem.TradeCoefficients[0, 1], 3);
        Assert.Equal(1.05f, EconomySystem.GetTradeFriction(0, 1, 2), 3);
    }

    [Fact]
    public void TradeCoefficients_DiagonalNeighbor_IsAboutOnePointOneFive()
    {
        // 4×4 grid: zone 0 (0,0) → zone 5 (1,1)
        Assert.Equal(1.15f, EconomySystem.TradeCoefficients[0, 5], 3);
        // 2×2 grid: zone 0 (0,0) → zone 3 (1,1)
        Assert.Equal(1.15f, EconomySystem.GetTradeFriction(0, 3, 2), 3);
    }

    [Fact]
    public void CrossZoneTrade_MovesGoods_WhenPriceSpreadCoversTransport()
    {
        var economy = new EconomySystem();
        economy.SetActiveZoneCount(4);

        economy.SetZoneSupply(0, Good.Wheat, 100f);
        economy.SetZoneDemand(0, Good.Wheat, 10f);
        economy.SetZoneSupply(3, Good.Wheat, 5f);
        economy.SetZoneDemand(3, Good.Wheat, 80f);

        economy.RecalculatePrices();
        float supplyBefore = economy.GetZoneSupply(3, Good.Wheat);

        economy.RunCrossZoneTradeOnly();

        float supplyAfter = economy.GetZoneSupply(3, Good.Wheat);
        Assert.True(supplyAfter > supplyBefore,
            $"Expected wheat to flow into deficit zone 3. Before={supplyBefore}, after={supplyAfter}");
        Assert.True(economy.LastInterZoneTradeVolume > 0f);
        Assert.True(economy.LastMeanInterZoneFriction >= 1.0f);
    }

    [Fact]
    public void CrossZoneTrade_NoVolume_WhenZonesAreBalanced()
    {
        var economy = new EconomySystem();
        economy.SetActiveZoneCount(4);

        for (int z = 0; z < 4; z++)
        {
            economy.SetZoneSupply(z, Good.Wheat, 50f);
            economy.SetZoneDemand(z, Good.Wheat, 50f);
        }

        economy.RecalculatePrices();
        economy.RunCrossZoneTradeOnly();

        Assert.Equal(0f, economy.LastInterZoneTradeVolume, 3);
        Assert.Equal(1.0f, economy.LastMeanInterZoneFriction, 3);
    }

    [Fact]
    public void PublishImbalancesTo_ExportsInterZoneTradeMetrics()
    {
        var economy = new EconomySystem();
        var state = new WorldState(32);
        economy.SetActiveZoneCount(4);

        economy.SetZoneSupply(0, Good.Food, 200f);
        economy.SetZoneDemand(0, Good.Food, 20f);
        economy.SetZoneSupply(3, Good.Food, 10f);
        economy.SetZoneDemand(3, Good.Food, 150f);

        economy.RecalculatePrices();
        economy.RunCrossZoneTradeOnly();
        economy.PublishImbalancesTo(state);

        Assert.True(state.InterZoneTradeVolume > 0f);
        Assert.InRange(state.MeanInterZoneFriction, 1.0f, 2.0f);
    }

    [Fact]
    public void SimHost_SnapshotExportsInterZoneTradeFields()
    {
        var host = new SimHost();
        host.Init(128);
        host.RestoreHouseholds(600, 150);

        for (int i = 0; i < 10; i++)
            host.Tick(1.0);

        var snap = host.GetSnapshot();

        Assert.False(float.IsNaN(snap.InterZoneTradeVolume));
        Assert.False(float.IsNaN(snap.MeanInterZoneFriction));
        Assert.InRange(snap.MeanInterZoneFriction, 1.0f, 2.5f);
        Assert.True(snap.InterZoneTradeVolume >= 0f);
    }
}
