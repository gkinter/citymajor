using System.Collections.Generic;

namespace CityMajor.Sim
{
    /// <summary>
    /// Rolling population samples for HUD growth-rate estimation (mirrors web population-growth.ts).
    /// </summary>
    public sealed class PopulationGrowthTracker
    {
        public const int GameMonthSimSeconds = 30;
        const int MaxSamples = 24;
        const int MinTickSpan = 8;

        readonly List<Sample> _samples = new();

        struct Sample
        {
            public long Tick;
            public int Population;
        }

        public void Push(long tick, int population)
        {
            if (_samples.Count > 0)
            {
                var last = _samples[_samples.Count - 1];
                if (last.Tick == tick && last.Population == population)
                    return;
            }

            _samples.Add(new Sample { Tick = tick, Population = population });
            if (_samples.Count > MaxSamples)
                _samples.RemoveAt(0);
        }

        public int? EstimatePerMonth()
        {
            if (_samples.Count < 2)
                return null;

            var first = _samples[0];
            var last = _samples[_samples.Count - 1];
            var deltaTick = last.Tick - first.Tick;
            if (deltaTick < MinTickSpan)
                return null;

            var deltaPop = last.Population - first.Population;
            return (int)System.Math.Round(deltaPop / (double)deltaTick * GameMonthSimSeconds);
        }

        public void Reset() => _samples.Clear();
    }

    public static class PopulationGrowthFormat
    {
        public static string PerMonth(int rate)
        {
            if (rate == 0)
                return "0/mo";

            return rate > 0 ? $"+{rate:N0}/mo" : $"{rate:N0}/mo";
        }
    }
}
