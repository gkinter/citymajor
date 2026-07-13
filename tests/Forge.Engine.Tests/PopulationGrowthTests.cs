using System.Globalization;
using Forge.SimCore;
using Xunit;

namespace Forge.Engine.Tests;

public class PopulationGrowthTests
{
    public PopulationGrowthTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    [Fact]
    public void EstimatePerMonth_ReturnsNull_WithFewerThanTwoSamples()
    {
        var tracker = new PopulationGrowthTracker();

        Assert.Null(tracker.EstimatePerMonth());

        tracker.Push(0, 1_000);
        Assert.Null(tracker.EstimatePerMonth());
    }

    [Fact]
    public void EstimatePerMonth_ReturnsNull_WhenTickSpanBelowMinGate()
    {
        var tracker = new PopulationGrowthTracker();
        tracker.Push(0, 1_000);
        tracker.Push(5, 1_050);

        Assert.Null(tracker.EstimatePerMonth());
    }

    [Fact]
    public void EstimatePerMonth_ComputesKnownRate_FromTickAndPopulationSamples()
    {
        var tracker = new PopulationGrowthTracker();
        tracker.Push(0, 1_000);
        tracker.Push(10, 1_100);

        Assert.Equal(300, tracker.EstimatePerMonth());
    }

    [Fact]
    public void EstimatePerMonth_UsesFirstAndLastSample_InRollingWindow()
    {
        var tracker = new PopulationGrowthTracker();
        tracker.Push(0, 500);
        tracker.Push(4, 520);
        tracker.Push(12, 620);

        Assert.Equal(300, tracker.EstimatePerMonth());
    }

    [Fact]
    public void Push_DeduplicatesIdenticalTickAndPopulation()
    {
        var tracker = new PopulationGrowthTracker();
        tracker.Push(0, 1_000);
        tracker.Push(10, 1_100);
        tracker.Push(10, 1_100);

        Assert.Equal(300, tracker.EstimatePerMonth());
    }

    [Fact]
    public void Push_EvictsOldestSample_AfterMaxWindow()
    {
        var tracker = new PopulationGrowthTracker();

        for (var i = 0; i < 25; i++)
            tracker.Push(i, 1_000 + i * 10);

        Assert.Equal(300, tracker.EstimatePerMonth());
    }

    [Fact]
    public void Reset_ClearsSamples()
    {
        var tracker = new PopulationGrowthTracker();
        tracker.Push(0, 1_000);
        tracker.Push(10, 1_100);

        tracker.Reset();

        Assert.Null(tracker.EstimatePerMonth());
    }

    [Theory]
    [InlineData(0, "0/mo")]
    [InlineData(1500, "+1,500/mo")]
    [InlineData(-200, "-200/mo")]
    public void FormatGrowthPerMonth_FormatsSignedMonthlyRate(int rate, string expected)
    {
        Assert.Equal(expected, PopulationGrowthFormat.FormatGrowthPerMonth(rate));
    }

    [Fact]
    public void GameMonthSimSeconds_MatchesWasmConfig()
    {
        Assert.Equal(30, PopulationGrowthTracker.GameMonthSimSeconds);
    }
}
