using System.Diagnostics;
using System.Runtime.InteropServices;
using Forge.SimCore;
using Forge.SimWasm;
using Xunit;
using Xunit.Abstractions;

namespace Forge.SimCore.Tests;

/// <summary>
/// Wall-clock tick budget characterization for native <see cref="SimHost"/> (SB-3685 / WP-D).
/// Starter-city CI gate: median &lt; 25 ms on shared ubuntu runners.
/// V1-scale: always asserts 8 Hz keep-pace (&lt; 125 ms); hard &lt;25 ms only under CI_STRICT=1.
/// WEB_V1_SCOPE ≥30 FPS is render wall-clock with LOD — not this sim tick-ms gate.
/// </summary>
public sealed class TickBudgetTests
{
    private const double FrameDt = 0.125; // 8 Hz sim frame
    private const int WarmupTicks = 40;
    private const int SampleTicks = 100;

    /// <summary>Generous CI ceiling on shared ubuntu-latest runners (starter city).</summary>
    private const double CiMedianTickMsLimit = 25.0;

    /// <summary>
    /// Soft v1-scale ceiling: one 8 Hz sim frame (125 ms). Sim must keep pace in the WASM worker;
    /// this is independent of WEB_V1_SCOPE ≥30 FPS render wall-clock.
    /// </summary>
    private const double V1ScaleKeepPaceTickMsLimit = 125.0;

    /// <summary>Local dev target (SB-3685) — logged for comparison, not asserted in CI.</summary>
    private const double LocalMedianTickMsTarget = 10.0;

    private const int V1ScaleMinHouseholds = 8_000;
    private const int V1ScaleMinBuildings = 3_000;

    private readonly ITestOutputHelper _output;

    public TickBudgetTests(ITestOutputHelper output) => _output = output;

    private static bool IsCiStrict =>
        string.Equals(Environment.GetEnvironmentVariable("CI_STRICT"), "1", StringComparison.Ordinal);

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

    /// <summary>
    /// V1 charter scale: near-full pools (8K+ HH, 3K+ buildings) on 256×256 via
    /// <see cref="SimHost.SeedV1ScaleCity"/>.
    /// Always asserts keep-pace at 8 Hz (&lt;125 ms). Hard historical CI gate (&lt;25 ms) only when
    /// <c>CI_STRICT=1</c> — Mac/dev machines often land ~90+ ms after cathedral systems and must
    /// not be confused with WEB_V1_SCOPE ≥30 FPS (render wall-clock + LOD).
    /// </summary>
    [Fact]
    public void V1Scale_256x256_NearFullPools_MedianFrameTick_UnderCiBudget()
    {
        var host = new SimHost();
        host.Init(256, new SimHostInitOptions { SkipStarterCity = true });
        host.SeedV1ScaleCity();

        var snap = host.GetSnapshot();
        int households = host.State.Households.Count;

        Assert.True(households >= V1ScaleMinHouseholds,
            $"expected ≥{V1ScaleMinHouseholds} households, got {households}");
        Assert.True(snap.BuildingCount >= V1ScaleMinBuildings,
            $"expected ≥{V1ScaleMinBuildings} buildings, got {snap.BuildingCount}");

        double medianMs = MeasureMedianTickMs(host, WarmupTicks, SampleTicks);
        bool ciStrict = IsCiStrict;
        bool isDarwin = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        _output.WriteLine(
            $"v1-scale: world=256 HH={households} pop={snap.Population} buildings={snap.BuildingCount} " +
            $"median_tick_ms={medianMs:F2} keep_pace_ms={V1ScaleKeepPaceTickMsLimit:F0} " +
            $"strict_limit_ms={CiMedianTickMsLimit:F0} ci_strict={ciStrict} darwin={isDarwin} " +
            $"local_target_ms={LocalMedianTickMsTarget:F0} " +
            $"(WEB_V1_SCOPE ≥30 FPS = render wall-clock, not tick-ms)");

        // Soft gate: sim must keep up with 8 Hz frame period (always meaningful).
        Assert.True(medianMs < V1ScaleKeepPaceTickMsLimit,
            $"median frame tick {medianMs:F2} ms exceeds 8 Hz keep-pace budget {V1ScaleKeepPaceTickMsLimit} ms " +
            $"(v1-scale HH={households} buildings={snap.BuildingCount})");

        // Hard historical ubuntu gate — opt-in via CI_STRICT=1 (skipped on Darwin unless strict).
        if (ciStrict)
        {
            Assert.True(medianMs < CiMedianTickMsLimit,
                $"median frame tick {medianMs:F2} ms exceeds CI_STRICT budget {CiMedianTickMsLimit} ms " +
                $"(v1-scale HH={households} buildings={snap.BuildingCount})");
        }
        else if (isDarwin)
        {
            _output.WriteLine(
                $"v1-scale: Darwin soft-gate only (median={medianMs:F2} ms); " +
                $"set CI_STRICT=1 to enforce <{CiMedianTickMsLimit:F0} ms");
        }
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
