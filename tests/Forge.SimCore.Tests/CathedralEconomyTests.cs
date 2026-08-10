using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Forge.SimCore;
using Forge.SimWasm;
using Xunit;

namespace Forge.SimCore.Tests;

/// <summary>Characterization tests for Cathedral P3 economy / market zones.</summary>
public sealed class CathedralEconomyTests
{
    [Fact]
    public void GoodsShortageIndex_CorrelatesWithIndustrialCommercialImbalance()
    {
        var economy = new EconomySystem();
        var state = new WorldState(32);
        state.Population = 800;

        economy.SetActiveZoneCount(1);
        economy.SetZoneSupply(0, Good.Food, 2f);
        economy.SetZoneDemand(0, Good.Food, 80f);
        economy.SetZoneSupply(0, Good.Steel, 0.5f);
        economy.SetZoneDemand(0, Good.Steel, 40f);

        economy.RecalculatePrices();
        economy.PublishImbalancesTo(state);

        float shortageIndex = economy.ComputeShortageIndex();
        Assert.True(shortageIndex > 0.25f,
            $"Expected high shortage index when industrial inputs lag demand. Got {shortageIndex:F3}");

        economy.DailyTick(state, 1.0);
        Assert.True(economy.IndustrialDemand > 0.1f,
            $"Industrial demand should rise when food supply lags. Got {economy.IndustrialDemand:F3}");
    }

    [Fact(Skip = "P3.3 not implemented")]
    public void MarketZonePrices_DifferAcrossPartitions()
    {
        var economy = new EconomySystem();
        economy.SetActiveZoneCount(4);

        economy.SetZoneSupply(0, Good.Food, 100f);
        economy.SetZoneDemand(0, Good.Food, 10f);
        economy.SetZoneSupply(3, Good.Food, 1f);
        economy.SetZoneDemand(3, Good.Food, 80f);

        economy.RecalculatePrices();

        float priceZone0 = economy.GetPrice(0, Good.Food);
        float priceZone3 = economy.GetPrice(3, Good.Food);
        Assert.True(priceZone3 > priceZone0 * 1.2f,
            $"Expected scarcity zone price > surplus zone. zone0={priceZone0:F2}, zone3={priceZone3:F2}");
    }

    [Fact]
    public void EconomySnapshot_ExportsPricePerTopImbalance()
    {
        var economy = new EconomySystem();
        economy.SetActiveZoneCount(1);
        economy.SetZoneSupply(0, Good.Food, 2f);
        economy.SetZoneDemand(0, Good.Food, 80f);
        economy.RecalculatePrices();

        var snapshot = EconomySnapshotDto.From(economy);

        Assert.NotEmpty(snapshot.Shortages);
        var food = Assert.Single(snapshot.Shortages, row => row.Name == "Food");
        Assert.Equal((byte)Good.Food, food.GoodId);
        Assert.True(food.Price > economy.GetAveragePrice(Good.Water),
            $"Scarce food should price above a stable good. food={food.Price:F2}");
        Assert.True(food.Magnitude > 0f);
    }

    [Fact]
    public void Snapshot_ExportsMarketZoneCount_FromEconomy()
    {
        var host = new SimHost();
        host.Init(128);
        host.RestoreHouseholds(2500, 600);

        for (int i = 0; i < 30; i++)
            host.Tick(1.0);

        var snap = host.GetSnapshot();
        Assert.True(snap.MarketZoneCount >= 4,
            $"Population 2500 should activate multiple market zones. Got {snap.MarketZoneCount}");
        Assert.Equal(host.Economy.ActiveZoneCount, snap.MarketZoneCount);
    }
}
