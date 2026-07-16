using System.Diagnostics;
using Forge.SimCore;
using Forge.SimWasm;
using Xunit;
using Xunit.Abstractions;

namespace Forge.SimCore.Tests;

/// <summary>
/// Wall-clock tick budget characterization for native <see cref="SimHost"/> (SB-3685 / WP-D).
/// CI gate: median &lt; 25 ms on shared ubuntu runners. Local dev target: ≤ 10 ms.
/// </summary>
public sealed class TickBudgetTests
{
    private const double FrameDt = 0.125; // 8 Hz sim frame
    private const int WarmupTicks = 40;
    private const int SampleTicks = 100;

    /// <summary>Generous CI ceiling on shared ubuntu-latest runners.</summary>
    private const double CiMedianTickMsLimit = 25.0;

    private readonly ITestOutputHelper _output;

    public TickBudgetTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void StarterCity_256x256_TrafficLite64_MedianFrameTick_UnderCiBudget()
    {
        MeasureAndAssert(
            worldSize: 256,
            options: new SimHostInitOptions(),
            trafficLabel: "lite-64",
            ciLimitMs: CiMedianTickMsLimit);
    }

    [Fact]
    public void StarterCity_128x128_TrafficLite64_MedianFrameTick_UnderCiBudget()
    {
        MeasureAndAssert(
            worldSize: 128,
            options: new SimHostInitOptions(),
            trafficLabel: "lite-64",
            ciLimitMs: CiMedianTickMsLimit);
    }

    /// <summary>
    /// Documents starter-city budget after a short warm sim — not yet at 10K HH / 5K buildings scale.
    /// </summary>
    [Fact]
    public void StarterCity_AfterWarmSim_ReportsPopulationAndTickBudget()
    {
        var host = new SimHost();
        host.Init(256, new SimHostInitOptions());

        for (int i = 0; i < 200; i++)
            host.Tick(FrameDt);

        var snap = host.GetSnapshot();
        double medianMs = MeasureMedianTickMs(host, warmupTicks: 20, sampleTicks: 50);

        _output.WriteLine(
            $"starter-after-warm: world=256 HH={host.State.Households.Count} pop={snap.Population} " +
            $"buildings={snap.BuildingCount} median_tick_ms={medianMs:F2}");

        Assert.True(snap.Population > 0);
        Assert.True(medianMs < CiMedianTickMsLimit,
            $"median frame tick {medianMs:F2} ms exceeds CI budget {CiMedianTickMsLimit} ms");
    }

    private void MeasureAndAssert(
        int worldSize,
        SimHostInitOptions options,
        string trafficLabel,
        double ciLimitMs)
    {
        var host = new SimHost();
        host.Init(worldSize, options);

        double medianMs = MeasureMedianTickMs(host, WarmupTicks, SampleTicks);
        var snap = host.GetSnapshot();

        _output.WriteLine(
            $"tick-budget: world={worldSize} traffic={trafficLabel} " +
            $"HH={host.State.Households.Count} pop={snap.Population} buildings={snap.BuildingCount} " +
            $"median_tick_ms={medianMs:F2} ci_limit_ms={ciLimitMs:F0}");

        Assert.True(medianMs < ciLimitMs,
            $"median frame tick {medianMs:F2} ms exceeds CI budget {ciLimitMs} ms " +
            $"(world={worldSize}, traffic={trafficLabel})");
    }

    private static double MeasureMedianTickMs(SimHost host, int warmupTicks, int sampleTicks)
    {
        for (int i = 0; i < warmupTicks; i++)
            host.Tick(FrameDt);

        var samples = new double[sampleTicks];
        for (int i = 0; i < sampleTicks; i++)
        {
            var sw = Stopwatch.StartNew();
            host.Tick(FrameDt);
            sw.Stop();
            samples[i] = sw.Elapsed.TotalMilliseconds;
        }

        Array.Sort(samples);
        return samples[sampleTicks / 2];
    }
}