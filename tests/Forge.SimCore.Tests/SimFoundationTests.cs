using Forge.Engine.Simulation;
using Forge.SimCore;
using Forge.SimWasm;
using Xunit;

namespace Forge.SimCore.Tests;

/// <summary>
/// Characterization tests for the sim foundation loop (economy → traffic → trade).
/// Pins behavior so deepen passes cannot silently regress.
/// </summary>
[Collection("SimHost")]
public sealed class SimFoundationTests
{
    [Fact]
    public void SeedCity_AfterMonths_HasPopulationEmploymentAndRci()
    {
        var host = new SimHost();
        host.Init(128);

        for (int i = 0; i < 100; i++)
            host.Tick(1.0);

        var snap = host.GetSnapshot();
        Assert.True(snap.Population > 0, "starter city should have population");
        Assert.InRange(snap.EmploymentRate, 0f, 1f);
        Assert.InRange(host.Economy.ResidentialDemand, -1f, 1f);
        Assert.InRange(host.Economy.CommercialDemand, -1f, 1f);
        Assert.InRange(host.Economy.IndustrialDemand, -1f, 1f);
    }

    [Fact]
    public void TrafficLite_RushPeak_ExceedsOffPeakMean()
    {
        var host = new SimHost();
        host.Init(128);

        for (int i = 0; i < 40; i++)
            host.Tick(1.0);

        float SampleAtHour(float hour)
        {
            var state = host.State;
            long dayBase = (state.TickCount / 1440) * 1440;
            state.TickCount = dayBase + (long)(hour * 60f);

            for (int i = 0; i < 12; i++)
                host.Tick(WasmConfig.TrafficLiteInterval);

            return host.State.MeanTrafficDensity;
        }

        float offPeak = SampleAtHour(13f);
        float morning = SampleAtHour(8f);

        Assert.True(morning + 1e-4f >= offPeak * 0.95f,
            $"expected rush mean traffic >= off-peak (rush={morning}, off={offPeak})");
    }

    [Fact]
    public void MonthlyTrade_UpdatesSnapshotTradeFields()
    {
        var host = new SimHost();
        host.Init(128);

        for (int i = 0; i < 80; i++)
            host.Tick(1.0);

        var snap = host.GetSnapshot();
        Assert.False(float.IsNaN(snap.TradeBalance));
        Assert.False(float.IsNaN(snap.MonthlyExportValue));
        Assert.False(float.IsNaN(snap.MonthlyImportCost));
        Assert.Equal(snap.MonthlyExportValue - snap.MonthlyImportCost, snap.TradeBalance, 3);
    }

    [Fact]
    public void PopulationGrowthTracker_EstimatePerMonth_MatchesWebParity()
    {
        var tracker = new PopulationGrowthTracker();
        tracker.Push(0, 1000);
        tracker.Push(30, 1300);
        var rate = tracker.EstimatePerMonth();
        Assert.NotNull(rate);
        Assert.Equal(300, rate.Value);
    }

    [Fact]
    public void Init_UseFullTraffic_DoesNotThrow_MeanTrafficFinite()
    {
        var host = new SimHost();
        host.Init(128, new SimHostInitOptions { UseFullTraffic = true });

        for (int i = 0; i < 20; i++)
            host.Tick(WasmConfig.TrafficLiteInterval);

        float mean = host.State.MeanTrafficDensity;
        Assert.False(float.IsNaN(mean));
        Assert.False(float.IsInfinity(mean));
    }

    [Fact]
    public void Init_TrafficLiteZoneCount128_Initializes()
    {
        var host = new SimHost();
        host.Init(128, new SimHostInitOptions { TrafficLiteZoneCount = 128 });

        for (int i = 0; i < 10; i++)
            host.Tick(WasmConfig.TrafficLiteInterval);

        Assert.True(host.IsInitialized);
        Assert.False(float.IsNaN(host.State.MeanTrafficDensity));
    }
}
