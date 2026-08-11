using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Forge.SimCore;
using Forge.SimWasm;
using Xunit;

namespace Forge.SimCore.Tests;

/// <summary>Cathedral P3.5 — bilateral trade routes: partner, contract, freight time.</summary>
public sealed class BilateralTradeRoutesTests
{
    [Fact]
    public void ComputeFreightMonths_GlobalMarket_IsOne()
    {
        Assert.Equal(1, TradeSystem.ComputeFreightMonths(-1, 1f));
        Assert.Equal(1, TradeSystem.ComputeFreightMonths(-1, 0f));
    }

    [Fact]
    public void ComputeFreightMonths_PartnerPlusCongestion_ClampsToFive()
    {
        // partner 1 → base 1+(1%3)=2; delay 1 → +2 → 4
        Assert.Equal(4, TradeSystem.ComputeFreightMonths(1, 1f));
        // partner 3 → base 1+0=1; delay 0 → 1
        Assert.Equal(1, TradeSystem.ComputeFreightMonths(3, 0f));
        // partner 2 → base 1+2=3; delay 1 → +2 → 5 (clamp)
        Assert.Equal(5, TradeSystem.ComputeFreightMonths(2, 1f));
    }

    [Fact]
    public void CreateTradeRoute_Bilateral_SetsFreightFromDeliveryDelay()
    {
        var trade = new TradeSystem();
        Assert.True(trade.CreateTradeRoute(1, Good.Steel, 50f, 10f, 12, deliveryDelay: 1f));
        Assert.Single(trade.Routes);
        Assert.Equal(1, trade.Routes[0].PartnerCityId);
        Assert.Equal(4, trade.Routes[0].FreightMonths);
    }

    [Fact]
    public void ProcessTrade_BilateralFreight_SettlesSlowerThanGlobal()
    {
        var economy = new EconomySystem();
        var stateGlobal = CreateWorld();
        var stateBilateral = CreateWorld();
        var global = new TradeSystem();
        var bilateral = new TradeSystem();

        global.CreateTradeRoute(-1, Good.Steel, 50f, 10f, 6, deliveryDelay: 0f);
        bilateral.CreateTradeRoute(1, Good.Steel, 50f, 10f, 6, deliveryDelay: 1f); // freight 4

        global.ProcessTrade(stateGlobal, economy);
        bilateral.ProcessTrade(stateBilateral, economy);

        Assert.Equal(500f, global.MonthlyExportValue, precision: 1); // 50*10 / 1
        Assert.Equal(125f, bilateral.MonthlyExportValue, precision: 1); // 50*10 / 4
        Assert.True(bilateral.MonthlyExportValue < global.MonthlyExportValue);
        Assert.Equal(1, bilateral.BilateralRouteCount);
        Assert.Equal(500f, bilateral.BilateralTradeValue, precision: 1); // notional pre-freight
        Assert.Equal(4f, bilateral.MeanFreightMonths, precision: 2);
        Assert.Equal(1, stateBilateral.BilateralRouteCount);
        Assert.Equal(4f, stateBilateral.MeanFreightMonths, precision: 2);
    }

    [Fact]
    public void SimHost_ProcessTrade_PreservesBilateralRoutes()
    {
        var host = new SimHost();
        host.Init(32, new SimHostInitOptions { SkipStarterCity = true });

        Assert.True(host.Trade.CreateTradeRoute(2, Good.Coal, 40f, 5f, 8, deliveryDelay: 0.5f));
        Assert.Single(host.Trade.Routes);
        Assert.True(host.Trade.Routes[0].PartnerCityId >= 0);

        // Force a monthly trade tick via public Process path used by sim.
        // ProcessGlobalMarketTrade is private — tick until trade runs, or call Trade.ProcessTrade.
        host.Trade.ProcessTrade(host.State!, host.Economy);
        Assert.Single(host.Trade.Routes);
        Assert.Equal(2, host.Trade.Routes[0].PartnerCityId);
        Assert.True(host.State!.BilateralRouteCount >= 1);
        Assert.True(host.State.MeanFreightMonths >= 1f);

        var snap = host.GetSnapshot();
        Assert.Equal(host.State.BilateralRouteCount, snap.BilateralRouteCount);
        Assert.Equal(host.State.BilateralTradeValue, snap.BilateralTradeValue, precision: 2);
        Assert.Equal(host.State.MeanFreightMonths, snap.MeanFreightMonths, precision: 2);

        var dto = SimSnapshotDto.From(snap, host.State);
        Assert.Equal(host.State.BilateralRouteCount, dto.BilateralRouteCount);
        Assert.Equal(host.State.MeanFreightMonths, dto.MeanFreightMonths, precision: 2);
    }

    static WorldState CreateWorld()
    {
        var state = new WorldState(16);
        state.CityFunds = 100_000;
        return state;
    }
}
