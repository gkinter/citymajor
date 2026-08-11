using Forge.Engine.Data;
using Forge.Engine.Simulation;

namespace Forge.SimCore;

/// <summary>
/// Manning pipe capacity + combined-sewer overflow (CSO) during storms
/// (Cathedral P5 — extends Tier-2 <see cref="SewageTreatment"/>).
/// Gravity velocity from Manning's equation; storm inflow from the rational
/// method (C·i·A) with surface runoff coefficients. When dry-weather sewage
/// + combined storm runoff exceeds pipe capacity, overflow dirties tiles
/// (feeds pollution → water quality / health / immigration). Better plants
/// raise capacity and partially separate stormwater.
/// Aggregates: <see cref="WorldState.MeanPipeUtilization"/>,
/// <see cref="WorldState.CsoOverflowRate"/>, <see cref="WorldState.StormRunoffLoad"/>.
/// </summary>
public static class StormOverflow
{
    /// <summary>Manning roughness n for concrete sewer pipe.</summary>
    public const float ManningN = 0.013f;

    /// <summary>Hydraulic radius proxy (m) for a typical municipal combined sewer.</summary>
    public const float DefaultHydraulicRadiusM = 0.35f;

    /// <summary>Minimum sewer slope (0.5%) for gravity flow.</summary>
    public const float MinSlope = 0.005f;

    /// <summary>
    /// Reference Manning velocity (m/s) at <see cref="MinSlope"/> and
    /// <see cref="DefaultHydraulicRadiusM"/> — used to normalize capacity.
    /// </summary>
    public const float ReferenceVelocityMs = 2.70f;

    /// <summary>Normalized dry-weather sewage load at full residential coverage.</summary>
    public const float DryWeatherSewageLoad = 0.35f;

    /// <summary>Per-day pollution added per unit of CSO overflow (raw sewage dump).</summary>
    public const float CsoPollutionPerDay = 0.12f;

    /// <summary>Tile edge length (m) for slope ≈ Δelevation / length.</summary>
    public const float TileLengthM = 10f;

    /// <summary>
    /// Manning velocity V = (1/n)·R^(2/3)·S^(1/2). Slope is clamped to ≥ 0.5%.
    /// </summary>
    public static float ManningVelocity(
        float slope,
        float hydraulicRadiusM = DefaultHydraulicRadiusM,
        float n = ManningN)
    {
        float s = Math.Max(slope, MinSlope);
        float r = Math.Max(hydraulicRadiusM, 0.05f);
        float nn = Math.Max(n, 0.008f);
        return (1f / nn) * MathF.Pow(r, 2f / 3f) * MathF.Sqrt(s);
    }

    /// <summary>
    /// Normalized pipe capacity from Manning velocity × plant quality.
    /// Poor plants (~0.3) ≈ 72% of reference; excellent (1.0) = full + quality headroom.
    /// </summary>
    public static float PipeCapacity(float slope, float plantQuality)
    {
        float v = ManningVelocity(slope);
        float q = Math.Clamp(plantQuality, 0.3f, 1f);
        float qualityScale = 0.6f + 0.4f * q;
        float capacity = (v / ReferenceVelocityMs) * qualityScale;
        return Math.Clamp(capacity, 0.15f, 2.5f);
    }

    /// <summary>
    /// Fraction of storm runoff that enters the combined sewer (vs separated).
    /// Poor/old plants ≈ fully combined (1.0); excellent ≈ mostly separated (0.2).
    /// </summary>
    public static float CombinedStormFraction(float plantQuality)
    {
        float q = Math.Clamp(plantQuality, 0.3f, 1f);
        return Math.Clamp(1.2f - q, 0.2f, 1f);
    }

    /// <summary>
    /// Rational-method intensity 0–1 from precipitation + weather.
    /// Rain (≥2) floors at 0.45; Storm (3) at 0.85; Blizzard (7) uses precip only.
    /// </summary>
    public static float StormIntensity(float precipitation, int weatherCondition)
    {
        float i = Math.Clamp(precipitation, 0f, 1.5f);
        // WeatherCondition: Clear=0 Cloudy=1 Rain=2 Storm=3 Snow=4 Fog=5 Heatwave=6 Blizzard=7
        if (weatherCondition == 3)
            i = Math.Max(i, 0.85f);
        else if (weatherCondition == 2)
            i = Math.Max(i, 0.45f);
        return Math.Clamp(i, 0f, 1.5f);
    }

    /// <summary>
    /// Runoff coefficient C by surface (rational method). Roads dominate;
    /// parks / forest / water are permeable.
    /// </summary>
    public static float RunoffCoefficient(byte zoneType, byte terrainType, bool hasRoad)
    {
        if (hasRoad)
            return 0.90f;

        // Terrain: 0=grass, 1=dirt, 2=sand, 3=water, 4=rock, 5=forest, 6=marsh, 7=snow
        if (terrainType is 3 or 6)
            return 0.05f;
        if (terrainType == 5)
            return 0.15f;

        return zoneType switch
        {
            0 => terrainType is 0 or 1 ? 0.25f : 0.35f, // unzoned
            1 or 2 => 0.45f,  // Residential
            3 or 5 => 0.70f,  // Commercial / office
            4 => 0.75f,       // Industrial
            6 => 0.55f,       // Mixed-use
            8 => 0.20f,       // Park
            _ => 0.30f,
        };
    }

    /// <summary>
    /// Local sewer slope from neighbor elevation drops (elevation is half-meters).
    /// </summary>
    public static float TileSlope(TileData tiles, int x, int y)
    {
        int idx = tiles.Index(x, y);
        float elevM = tiles.Elevation[idx] * 0.5f;
        float maxDrop = 0f;

        void Consider(int nx, int ny)
        {
            if (!tiles.InBounds(nx, ny)) return;
            float nElev = tiles.Elevation[tiles.Index(nx, ny)] * 0.5f;
            float drop = elevM - nElev;
            if (drop > maxDrop) maxDrop = drop;
        }

        Consider(x - 1, y);
        Consider(x + 1, y);
        Consider(x, y - 1);
        Consider(x, y + 1);

        float slope = maxDrop / TileLengthM;
        return Math.Max(slope, MinSlope);
    }

    /// <summary>
    /// Dry-weather sewage inflow (normalized) for a covered tile.
    /// </summary>
    public static float DrySewageInflow(float coverage, byte zoneType)
    {
        float load = SewageTreatment.ZoneSewageLoad(zoneType);
        if (load <= 0f) return 0f;
        float cov = Math.Clamp(coverage, 0f, 1f);
        return DryWeatherSewageLoad * load * Math.Max(cov, 0.15f);
    }

    /// <summary>
    /// Storm runoff entering the combined sewer (normalized).
    /// </summary>
    public static float StormRunoffInflow(
        float runoffCoefficient,
        float stormIntensity,
        float combinedStormFraction)
    {
        float c = Math.Clamp(runoffCoefficient, 0f, 1f);
        float i = Math.Max(0f, stormIntensity);
        float combined = Math.Clamp(combinedStormFraction, 0f, 1f);
        return c * i * combined;
    }

    /// <summary>
    /// Pipe utilization 0–1+ (may exceed 1 when overflowing).
    /// </summary>
    public static float PipeUtilization(float drySewage, float stormRunoff, float capacity)
    {
        float cap = Math.Max(capacity, 0.01f);
        return Math.Max(0f, (drySewage + stormRunoff) / cap);
    }

    /// <summary>
    /// Overflow volume (normalized) when inflow exceeds capacity.
    /// </summary>
    public static float OverflowAmount(float drySewage, float stormRunoff, float capacity)
        => Math.Max(0f, drySewage + stormRunoff - Math.Max(capacity, 0f));

    /// <summary>
    /// Disease / contamination pressure 0–1 from city CSO rate (logistic-ish).
    /// Soft curve: 0 at no overflow → ~0.73 at rate=0.5 → ~0.95 at rate=1.
    /// </summary>
    public static float DiseasePressureFromCso(float csoOverflowRate)
    {
        float r = Math.Clamp(csoOverflowRate, 0f, 1f);
        return Math.Clamp(1f - MathF.Exp(-2.6f * r), 0f, 1f);
    }

    /// <summary>
    /// Apply Manning/CSO for <paramref name="days"/> on zoned tiles with sewage
    /// load. Overflow raises tile pollution; aggregates feed Sewage HUD + snapshot.
    /// Returns count of tiles that overflowed this pass.
    /// </summary>
    public static int Tick(
        WorldState state,
        InfluenceMap sewageCoverage,
        float days = 1f)
    {
        float daysClamped = Math.Max(0f, days);
        float quality = SewageTreatment.MeanPlantQuality(state);
        float combinedFrac = CombinedStormFraction(quality);
        float intensity = StormIntensity(state.Precipitation, state.WeatherCondition);

        double utilSum = 0;
        double overflowSum = 0;
        double runoffSum = 0;
        int sampleCount = 0;
        int overflowTiles = 0;

        var tiles = state.Tiles;
        for (int y = 0; y < tiles.Size; y++)
        for (int x = 0; x < tiles.Size; x++)
        {
            int idx = tiles.Index(x, y);
            byte zone = tiles.ZoneType[idx];
            float sewageLoad = SewageTreatment.ZoneSewageLoad(zone);
            bool hasRoad = tiles.RoadFlags[idx] != 0;

            // Sample roads + sewage-generating zones (CSO risk surface).
            if (sewageLoad <= 0f && !hasRoad)
                continue;

            float coverage = Math.Clamp(sewageCoverage.GetValue(x, y), 0f, 1f);
            // Uncovered tiles still shed storm runoff into nearby combined network
            // at a reduced dry-sewage contribution.
            float dry = DrySewageInflow(Math.Max(coverage, hasRoad ? 0.25f : 0f), zone);
            if (sewageLoad <= 0f && hasRoad)
                dry = DryWeatherSewageLoad * 0.2f; // road wash / residual

            float c = RunoffCoefficient(zone, tiles.TerrainType[idx], hasRoad);
            float storm = StormRunoffInflow(c, intensity, combinedFrac);
            float slope = TileSlope(tiles, x, y);
            float capacity = PipeCapacity(slope, quality);
            float util = PipeUtilization(dry, storm, capacity);
            float overflow = OverflowAmount(dry, storm, capacity);

            utilSum += Math.Min(util, 2f);
            runoffSum += storm;
            sampleCount++;

            if (overflow <= 0f || daysClamped <= 0f)
                continue;

            overflowSum += overflow;
            overflowTiles++;

            float pollutionDelta = overflow * CsoPollutionPerDay * daysClamped;
            float before = tiles.Pollution[idx];
            tiles.Pollution[idx] = Math.Clamp(before + pollutionDelta, 0f, 1f);
        }

        float meanUtil = sampleCount == 0 ? 0f : (float)(utilSum / sampleCount);
        float meanRunoff = sampleCount == 0 ? 0f : (float)(runoffSum / sampleCount);
        // Normalize overflow rate: mean overflow / (mean capacity proxy ~1) clamped.
        float meanOverflow = sampleCount == 0 ? 0f : (float)(overflowSum / sampleCount);
        float csoRate = Math.Clamp(meanOverflow, 0f, 1f);
        if (sampleCount > 0 && overflowTiles > 0)
        {
            // Blend volume rate with fraction of tiles overflowing for HUD signal.
            float tileFrac = overflowTiles / (float)sampleCount;
            csoRate = Math.Clamp(0.55f * meanOverflow + 0.45f * tileFrac, 0f, 1f);
        }

        state.MeanPipeUtilization = Math.Clamp(meanUtil, 0f, 2f);
        state.CsoOverflowRate = csoRate;
        state.StormRunoffLoad = Math.Clamp(meanRunoff, 0f, 1.5f);

        if (overflowTiles > 0)
            SewageTreatment.RefreshAggregates(state, sewageCoverage);

        return overflowTiles;
    }
}
