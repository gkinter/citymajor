namespace Forge.SimCore;

/// <summary>
/// Rolling window of population samples for HUD growth-rate estimation.
/// Mirrors <c>web/lib/population-growth.ts</c> for Unity + WASM parity.
/// </summary>
public sealed class PopulationGrowthTracker
{
    /// <summary>Sim seconds per game month (WasmConfig.GameMonthInterval).</summary>
    public const int GameMonthSimSeconds = 30;

    private const int MaxSamples = 24;
    private const int MinTickSpan = 8;

    private readonly List<PopulationSample> _samples = new();

    public void Push(long tick, int population)
    {
        if (_samples.Count > 0)
        {
            var last = _samples[^1];
            if (last.Tick == tick && last.Population == population)
                return;
        }

        _samples.Add(new PopulationSample(tick, population));
        if (_samples.Count > MaxSamples)
            _samples.RemoveAt(0);
    }

    /// <summary>Estimated net population change per game month, or null when unstable.</summary>
    public int? EstimatePerMonth()
    {
        if (_samples.Count < 2)
            return null;

        var first = _samples[0];
        var last = _samples[^1];
        var deltaTick = last.Tick - first.Tick;
        if (deltaTick < MinTickSpan)
            return null;

        var deltaPop = last.Population - first.Population;
        return (int)Math.Round((double)deltaPop / deltaTick * GameMonthSimSeconds);
    }

    public void Reset() => _samples.Clear();
}

public readonly record struct PopulationSample(long Tick, int Population);

public static class PopulationGrowthFormat
{
    public static string FormatGrowthPerMonth(int rate)
    {
        if (rate == 0)
            return "0/mo";

        var sign = rate > 0 ? "+" : "";
        return $"{sign}{rate:N0}/mo";
    }
}
