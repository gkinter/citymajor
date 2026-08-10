using Forge.SimCore;
using Xunit;
using Xunit.Abstractions;

namespace Forge.SimCore.Tests;

/// <summary>
/// Desktop opt-in full <see cref="Forge.Game.Simulation.TrafficSystem"/> (WP-E).
/// </summary>
[Collection("SimHost")]
public sealed class FullTrafficTests
{
    private const double FrameDt = 0.125;

    private readonly ITestOutputHelper _output;

    public FullTrafficTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void FullTraffic_128x128_StarterCity_MeanTrafficDensity_IsFinite()
    {
        var host = new SimHost();
        host.Init(128, new SimHostInitOptions { UseFullTraffic = true });

        for (int i = 0; i < 20; i++)
            host.Tick(FrameDt);

        var snap = host.GetSnapshot();

        _output.WriteLine(
            $"full-traffic: world=128 pop={snap.Population} mean_traffic={snap.MeanTrafficDensity:F3}");

        Assert.True(snap.Population > 0);
        Assert.False(float.IsNaN(snap.MeanTrafficDensity));
        Assert.False(float.IsInfinity(snap.MeanTrafficDensity));
    }

    [Fact]
    public void FullTraffic_128x128_StarterCity_MedianTick_UnderCiBudget()
    {
        const double ciLimitMs = 25.0;
        var host = new SimHost();
        host.Init(128, new SimHostInitOptions { UseFullTraffic = true });

        for (int i = 0; i < 30; i++)
            host.Tick(FrameDt);

        var samples = new double[40];
        for (int i = 0; i < samples.Length; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            host.Tick(FrameDt);
            sw.Stop();
            samples[i] = sw.Elapsed.TotalMilliseconds;
        }

        Array.Sort(samples);
        double median = samples[samples.Length / 2];

        _output.WriteLine($"full-traffic tick budget: median_ms={median:F2} ci_limit={ciLimitMs}");

        Assert.True(median < ciLimitMs,
            $"full TrafficSystem median tick {median:F2} ms exceeds CI budget {ciLimitMs} ms");
    }
}
