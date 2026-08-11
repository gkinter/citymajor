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

    [Fact]
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
    public void MarketZonePrices_RemainDistinct_AfterTradeShock()
    {
        var economy = new EconomySystem();
        economy.SetActiveZoneCount(9);

        economy.SetZoneSupply(0, Good.Food, 200f);
        economy.SetZoneDemand(0, Good.Food, 10f);
        economy.SetZoneSupply(8, Good.Food, 2f);
        economy.SetZoneDemand(8, Good.Food, 160f);

        economy.RecalculatePrices();
        float before0 = economy.GetPrice(0, Good.Food);
        float before8 = economy.GetPrice(8, Good.Food);
        Assert.True(before8 > before0 * 1.2f,
            $"Pre-trade scarcity should price above surplus. z0={before0:F2} z8={before8:F2}");

        economy.RunCrossZoneTradeOnly();
        economy.RecalculatePrices();

        float after0 = economy.GetPrice(0, Good.Food);
        float after8 = economy.GetPrice(8, Good.Food);
        Assert.True(economy.LastInterZoneTradeVolume > 0f,
            "Trade shock should move goods across partitions.");
        Assert.True(after8 > after0 * 1.05f,
            $"Partition prices should stay distinct after trade. z0={after0:F2} z8={after8:F2}");

        var spreads = economy.GetAnchorZonePriceSpreads();
        var food = Assert.Single(spreads, row => row.Name == "Food");
        Assert.True(food.MaxPrice > food.MinPrice * 1.05f,
            $"Anchor spread should remain after trade. min={food.MinPrice:F2} max={food.MaxPrice:F2}");
    }

    [Fact]
    public void MarketZoneCount_ScalesThroughNineAndSixteen()
    {
        var economy = new EconomySystem();
        var state = new WorldState(64);

        state.Population = 400;
        economy.DailyTick(state, 1.0);
        Assert.Equal(1, economy.ActiveZoneCount);

        state.Population = 1500;
        economy.DailyTick(state, 1.0);
        Assert.Equal(4, economy.ActiveZoneCount);

        state.Population = 5000;
        economy.DailyTick(state, 1.0);
        Assert.Equal(9, economy.ActiveZoneCount);

        state.Population = 12000;
        economy.DailyTick(state, 1.0);
        Assert.Equal(16, economy.ActiveZoneCount);
        Assert.Equal(EconomySystem.MaxMarketZones, economy.ActiveZoneCount);
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

    [Fact]
    public void EconomySnapshot_ExportsZonePriceSpread_WhenMultiZone()
    {
        var economy = new EconomySystem();
        economy.SetActiveZoneCount(4);
        economy.SetZoneSupply(0, Good.Food, 100f);
        economy.SetZoneDemand(0, Good.Food, 10f);
        economy.SetZoneSupply(3, Good.Food, 1f);
        economy.SetZoneDemand(3, Good.Food, 80f);
        economy.RecalculatePrices();

        var snapshot = EconomySnapshotDto.From(economy);

        Assert.Equal(4, snapshot.MarketZoneCount);
        Assert.NotEmpty(snapshot.MarketZonePrices);
        var food = Assert.Single(snapshot.MarketZonePrices, row => row.Name == "Food");
        Assert.True(food.MaxPrice > food.MinPrice * 1.2f,
            $"Expected zone spread for Food. min={food.MinPrice:F2}, max={food.MaxPrice:F2}");
        Assert.Equal(economy.GetAveragePrice(Good.Food), food.CityAvgPrice);
    }

    [Fact]
    public void EconomySnapshot_ExportsProductionFlows_WithInventoryRate()
    {
        var economy = new EconomySystem();
        economy.SetActiveZoneCount(1);
        economy.SetZoneSupply(0, Good.Food, 20f);
        economy.SetZoneDemand(0, Good.Food, 80f);
        economy.SetZoneSupply(0, Good.Timber, 50f);
        economy.SetZoneDemand(0, Good.Timber, 10f);
        economy.RecalculatePrices();

        var flows = economy.GetTopGoodFlows(8);
        Assert.NotEmpty(flows);

        var food = Assert.Single(flows, row => row.Name == "Food");
        Assert.Equal(20f, food.Production);
        Assert.Equal(80f, food.Demand);
        Assert.True(food.InventoryRate < 0f, "Food deficit should yield negative inventory rate.");

        var timber = Assert.Single(flows, row => row.Name == "Timber");
        Assert.True(timber.InventoryRate > 0f, "Timber surplus should yield positive inventory rate.");

        var snapshot = EconomySnapshotDto.From(economy);
        Assert.NotEmpty(snapshot.Flows);
        Assert.Contains(snapshot.Flows, row => row.Name == "Food" && row.Production == 20f);
    }

    [Fact]
    public void GoodsTransportCostIndex_RisesWithFrictionAndCongestion()
    {
        float local = EconomySystem.ComputeGoodsTransportCostIndex(1.0f, 0f);
        float friction = EconomySystem.ComputeGoodsTransportCostIndex(1.25f, 0f);
        float congested = EconomySystem.ComputeGoodsTransportCostIndex(1.25f, 0.8f);

        Assert.Equal(0f, local, 3);
        Assert.True(friction > local + 0.2f,
            $"Friction should raise transport cost. local={local:F3} friction={friction:F3}");
        Assert.True(congested > friction,
            $"Congestion should raise transport cost further. friction={friction:F3} congested={congested:F3}");
        Assert.InRange(congested, 0f, 1f);
    }

    [Fact]
    public void FrictionCorridors_ExportHighFrictionBoundaries_WhenMultiZone()
    {
        var economy = new EconomySystem();
        economy.SetActiveZoneCount(4);

        var corridors = economy.CollectFrictionCorridors(
            worldSize: 64,
            meanInterZoneFriction: 1.2f,
            trafficDensity: null,
            stride: 2);

        Assert.NotEmpty(corridors);
        Assert.All(corridors, sample =>
        {
            Assert.InRange(sample.Friction, 0.05f, 1f);
            Assert.InRange(sample.TileX, 0, 63);
            Assert.InRange(sample.TileZ, 0, 63);
        });

        var dto = SimSnapshotDto.From(
            new SimSnapshot { TickCount = 1 },
            new WorldState(64)
            {
                MeanInterZoneFriction = 1.2f,
                GoodsTransportCostIndex = 0.4f,
                MarketZoneCount = 4,
            },
            economy: economy);

        Assert.NotEmpty(dto.FrictionCorridors);
        Assert.Equal(0.4f, dto.GoodsTransportCostIndex, 3);
    }
}
