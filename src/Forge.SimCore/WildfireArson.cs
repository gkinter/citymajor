using Forge.Engine.Data;
using Forge.Engine.Simulation;

namespace Forge.SimCore;

/// <summary>
/// Tier-2 wildfire + arson rings + lookout / aerial / fire-rating depth
/// (<c>MISSING_SYSTEMS.md</c> §1.1 / <c>AGENT_06_SERVICES</c> / <c>CATHEDRAL_P5_SERVICES</c>).
/// Forest / park / ag fuel tiles ignite under drought, spread until firebreaks (roads /
/// water / rock), and can torch adjacent buildings. High-crime clusters can spark
/// arson ignitions; ≥2 concurrent high-crime building fires flag an arson ring.
/// Lookout towers cut spark chance in radius; aerial firefighting suppresses burning
/// fuel; city fire safety rating (1–10) drives insurance premium multiplier.
/// </summary>
public static class WildfireArson
{
    public const byte TerrainForest = (byte)TerrainId.Forest;
    public const byte TerrainWater = (byte)TerrainId.Water;
    public const byte TerrainRock = (byte)TerrainId.Rock;
    public const byte ZoneAgricultural = 7;
    public const byte ZonePark = 8;

    public const byte BurningThreshold = 1;
    public const byte IgniteIntensity = 64;

    /// <summary>ServiceFlags bit — fire lookout tower (MISSING_SYSTEMS §1.1).</summary>
    public const uint ServiceLookout = 1u << 10;

    /// <summary>ServiceFlags bit — aerial firefighting / water-bomber base (Modern+).</summary>
    public const uint ServiceAerialFire = 1u << 11;

    /// <summary>Heatwave (<see cref="WorldState.WeatherCondition"/> = 6) drought index.</summary>
    public const float HeatwaveDroughtRisk = 1.0f;

    /// <summary>Dry summer (Season 1 + low precipitation) drought index.</summary>
    public const float DrySummerDroughtRisk = 0.7f;

    /// <summary>Baseline drought when conditions are mild.</summary>
    public const float BaseDroughtRisk = 0.05f;

    public const float DrySummerPrecipThreshold = 0.1f;

    /// <summary>Max random fuel-tile spark probes per hour when drought risk is 1.0.</summary>
    public const int MaxSparkAttemptsPerHour = 4;

    /// <summary>Per-probe spark success chance at drought risk 1.0.</summary>
    public const float SparkSuccessChance = 0.35f;

    /// <summary>Spark chance multiplier when a lookout covers the probed tile.</summary>
    public const float LookoutSparkMultiplier = 0.25f;

    /// <summary>Lookout early-detection radius in tiles.</summary>
    public const float LookoutRadiusTiles = 20f;

    /// <summary>Max wildfire tiles an aerial unit can extinguish per hour.</summary>
    public const int AerialSuppressPerHour = 3;

    /// <summary>Per-hour chance a burning fuel tile ignites an adjacent fuel tile.</summary>
    public const float BaseWildfireSpreadChance = 0.45f;

    /// <summary>Crime at or above this is "high crime" for arson / ring detection.</summary>
    public const float HighCrimeThreshold = 0.55f;

    /// <summary>Per-hour arson chance at crime = 1.0 (scaled by excess over threshold).</summary>
    public const float BaseArsonChancePerHour = 0.015f;

    /// <summary>Concurrent high-crime building fires required to flag an arson ring.</summary>
    public const int ArsonRingMinFires = 2;

    public const byte MinFireSafetyRating = 1;
    public const byte MaxFireSafetyRating = 10;
    public const byte DefaultFireSafetyRating = 5;

    /// <summary>Insurance premium at rating 1 (MISSING_SYSTEMS — higher rating → lower premium).</summary>
    public const float MaxInsurancePremiumMult = 1.8f;

    /// <summary>Insurance premium at rating 10.</summary>
    public const float MinInsurancePremiumMult = 0.7f;

    private static readonly (int Dx, int Dy)[] Cardinal =
    {
        (1, 0), (-1, 0), (0, 1), (0, -1),
    };

    /// <summary>
    /// Drought / wildfire risk index 0–1 from weather + season + precipitation.
    /// </summary>
    public static float CalculateDroughtRisk(WorldState state)
    {
        if (state.WeatherCondition == 6) // heatwave (ServiceSystem fire risk)
            return HeatwaveDroughtRisk;

        if (state.Season == 1 && state.Precipitation < DrySummerPrecipThreshold)
            return DrySummerDroughtRisk;

        return BaseDroughtRisk;
    }

    /// <summary>
    /// True when the tile can carry wildfire: forest terrain or park/ag zone,
    /// no building, not a firebreak.
    /// </summary>
    public static bool IsFuelTile(WorldState state, int tileX, int tileY)
    {
        if (!state.Tiles.InBounds(tileX, tileY)) return false;
        if (IsFirebreak(state, tileX, tileY)) return false;

        int idx = state.Tiles.Index(tileX, tileY);
        if (state.Tiles.BuildingId[idx] != 0) return false;

        byte terrain = state.Tiles.TerrainType[idx];
        if (terrain == TerrainForest) return true;

        byte zone = state.Tiles.ZoneType[idx];
        return zone == ZonePark || zone == ZoneAgricultural;
    }

    /// <summary>Roads, water, and rock stop wildfire spread.</summary>
    public static bool IsFirebreak(WorldState state, int tileX, int tileY)
    {
        if (!state.Tiles.InBounds(tileX, tileY)) return true;

        int idx = state.Tiles.Index(tileX, tileY);
        if (state.Tiles.RoadFlags[idx] != 0) return true;

        byte terrain = state.Tiles.TerrainType[idx];
        return terrain == TerrainWater || terrain == TerrainRock;
    }

    /// <summary>Ensure <see cref="WorldState.WildfireIntensity"/> matches tile count.</summary>
    public static byte[] EnsureIntensityBuffer(WorldState state)
    {
        int count = state.Tiles.Count;
        if (state.WildfireIntensity is null || state.WildfireIntensity.Length != count)
            state.WildfireIntensity = new byte[count];
        return state.WildfireIntensity;
    }

    public static bool IsWildfireBurning(WorldState state, int tileX, int tileY)
    {
        if (!state.Tiles.InBounds(tileX, tileY)) return false;
        var buf = state.WildfireIntensity;
        if (buf is null || buf.Length != state.Tiles.Count) return false;
        return buf[state.Tiles.Index(tileX, tileY)] >= BurningThreshold;
    }

    public static bool TryIgniteWildfireTile(
        WorldState state,
        int tileX,
        int tileY,
        byte intensity = IgniteIntensity)
    {
        if (!IsFuelTile(state, tileX, tileY)) return false;

        var buf = EnsureIntensityBuffer(state);
        int idx = state.Tiles.Index(tileX, tileY);
        byte next = Math.Max(buf[idx], intensity);
        if (next < BurningThreshold)
            next = BurningThreshold;
        bool wasCold = buf[idx] < BurningThreshold;
        buf[idx] = next;
        return wasCold;
    }

    public static int CountActiveWildfireTiles(WorldState state)
    {
        var buf = state.WildfireIntensity;
        if (buf is null || buf.Length != state.Tiles.Count) return 0;

        int count = 0;
        for (int i = 0; i < buf.Length; i++)
        {
            if (buf[i] >= BurningThreshold)
                count++;
        }

        return count;
    }

    /// <summary>
    /// Mean arson risk over zoned tiles with buildings (crime excess over threshold).
    /// Empty cities report 0.
    /// </summary>
    public static float CalculateArsonRiskIndex(WorldState state, int sampleStride = 8)
    {
        int stride = Math.Max(1, sampleStride);
        var tiles = state.Tiles;
        double sum = 0;
        int count = 0;
        int seen = 0;

        for (int y = 0; y < tiles.Size; y++)
        for (int x = 0; x < tiles.Size; x++)
        {
            int idx = tiles.Index(x, y);
            if (tiles.ZoneType[idx] == 0) continue;
            if (tiles.BuildingId[idx] == 0) continue;
            if ((seen++ % stride) != 0) continue;

            count++;
            sum += ArsonRiskFromCrime(tiles.Crime[idx]);
        }

        return count == 0 ? 0f : (float)(sum / count);
    }

    /// <summary>0–1 risk from tile crime (0 below threshold, 1 at crime = 1).</summary>
    public static float ArsonRiskFromCrime(float crime)
    {
        float c = Math.Clamp(crime, 0f, 1f);
        if (c < HighCrimeThreshold) return 0f;
        float span = 1f - HighCrimeThreshold;
        if (span <= 0f) return 1f;
        return Math.Clamp((c - HighCrimeThreshold) / span, 0f, 1f);
    }

    /// <summary>
    /// True when ≥ <see cref="ArsonRingMinFires"/> active building fires sit on
    /// high-crime tiles (suspicious cluster / ring).
    /// </summary>
    public static bool DetectArsonRing(WorldState state)
    {
        var buildings = state.Buildings;
        var tiles = state.Tiles;
        int suspicious = 0;

        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!FireResponse.IsBurning(buildings, i)) continue;

            int x = buildings.GridX[i];
            int y = buildings.GridY[i];
            if (!tiles.InBounds(x, y)) continue;

            float crime = tiles.Crime[tiles.Index(x, y)];
            if (crime >= HighCrimeThreshold)
                suspicious++;

            if (suspicious >= ArsonRingMinFires)
                return true;
        }

        return false;
    }

    /// <summary>Count active lookout towers (<see cref="ServiceLookout"/>).</summary>
    public static int CountLookoutTowers(WorldState state)
    {
        var buildings = state.Buildings;
        int count = 0;
        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if ((buildings.ServiceFlags[i] & ServiceLookout) != 0)
                count++;
        }

        return count;
    }

    /// <summary>True when any active aerial firefighting base exists.</summary>
    public static bool HasAerialFirefighting(WorldState state)
    {
        var buildings = state.Buildings;
        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if ((buildings.ServiceFlags[i] & ServiceAerialFire) != 0)
                return true;
        }

        return false;
    }

    /// <summary>
    /// True when an active lookout lies within <paramref name="radiusTiles"/> of the tile.
    /// </summary>
    public static bool HasLookoutCoverage(
        WorldState state,
        int tileX,
        int tileY,
        float radiusTiles = LookoutRadiusTiles)
    {
        if (!state.Tiles.InBounds(tileX, tileY))
            return false;

        float radiusSq = radiusTiles * radiusTiles;
        var buildings = state.Buildings;

        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if ((buildings.ServiceFlags[i] & ServiceLookout) == 0) continue;

            float dx = buildings.GridX[i] - tileX;
            float dy = buildings.GridY[i] - tileY;
            if (dx * dx + dy * dy <= radiusSq)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Extinguish up to <see cref="AerialSuppressPerHour"/> × hours burning fuel tiles
    /// when aerial firefighting is available. Returns tiles suppressed.
    /// </summary>
    public static int TickAerialSuppression(WorldState state, float hours = 1f)
    {
        if (!HasAerialFirefighting(state)) return 0;

        float hoursClamped = Math.Max(0f, hours);
        if (hoursClamped <= 0f) return 0;

        int budget = Math.Max(0, (int)Math.Ceiling(AerialSuppressPerHour * hoursClamped));
        if (budget <= 0) return 0;

        var buf = state.WildfireIntensity;
        if (buf is null || buf.Length != state.Tiles.Count) return 0;

        int suppressed = 0;
        for (int i = 0; i < buf.Length && suppressed < budget; i++)
        {
            if (buf[i] < BurningThreshold) continue;
            buf[i] = 0;
            suppressed++;
        }

        return suppressed;
    }

    /// <summary>
    /// City fire safety rating 1–10 from hydrants, stations, lookouts, aerial,
    /// and penalties for active fires / wildfire / arson / drought
    /// (MISSING_SYSTEMS §1.1 fire rating → insurance).
    /// </summary>
    public static byte CalculateFireSafetyRating(WorldState state)
    {
        float score = DefaultFireSafetyRating;

        score += 2f * Math.Clamp(state.HydrantCoverageFraction, 0f, 1f);

        int fireStations = 0;
        var buildings = state.Buildings;
        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if ((buildings.ServiceFlags[i] & EmergencyResponseTime.ServiceFire) != 0)
                fireStations++;
        }

        score += Math.Min(2f, fireStations * 0.5f);
        score += Math.Min(1.5f, CountLookoutTowers(state) * 0.5f);
        if (HasAerialFirefighting(state))
            score += 1.5f;

        score -= Math.Min(2f, Math.Max(0, state.ActiveFireCount) * 0.4f);
        score -= Math.Min(2f, Math.Max(0, state.ActiveWildfireTileCount) * 0.3f);
        if (state.ArsonRingActive)
            score -= 1.5f;
        score -= Math.Clamp(state.WildfireRiskIndex, 0f, 1f) * 0.5f;
        score -= Math.Clamp(state.ArsonRiskIndex, 0f, 1f) * 0.5f;

        int rounded = (int)Math.Round(score, MidpointRounding.AwayFromZero);
        return (byte)Math.Clamp(rounded, MinFireSafetyRating, MaxFireSafetyRating);
    }

    /// <summary>
    /// Insurance premium multiplier from fire safety rating (1 → 1.8, 10 → 0.7).
    /// </summary>
    public static float InsurancePremiumMultFromRating(byte rating)
    {
        int r = Math.Clamp((int)rating, MinFireSafetyRating, MaxFireSafetyRating);
        float t = (r - MinFireSafetyRating) / (float)(MaxFireSafetyRating - MinFireSafetyRating);
        return MaxInsurancePremiumMult + (MinInsurancePremiumMult - MaxInsurancePremiumMult) * t;
    }

    /// <summary>
    /// Drought sparks + wildfire spread across fuel + edge building ignition.
    /// Returns newly ignited wildfire tiles (not building ignitions).
    /// </summary>
    public static int TickWildfire(
        WorldState state,
        float hours = 1f,
        Random? rng = null)
    {
        rng ??= new Random();
        float drought = CalculateDroughtRisk(state);
        float hoursClamped = Math.Max(0f, hours);
        var tiles = state.Tiles;
        var buf = EnsureIntensityBuffer(state);

        int ignited = 0;

        // Sparks: bounded random probes under drought (avoids torching every forest tile).
        if (drought > BaseDroughtRisk && hoursClamped > 0f)
        {
            int attempts = Math.Max(0, (int)Math.Ceiling(MaxSparkAttemptsPerHour * drought * hoursClamped));
            float sparkChance = Math.Clamp(SparkSuccessChance * drought, 0f, 1f);
            int size = tiles.Size;

            for (int a = 0; a < attempts; a++)
            {
                int x = rng.Next(size);
                int y = rng.Next(size);
                if (!IsFuelTile(state, x, y)) continue;
                if (buf[tiles.Index(x, y)] >= BurningThreshold) continue;

                float chance = sparkChance;
                if (HasLookoutCoverage(state, x, y))
                    chance *= LookoutSparkMultiplier;

                if (rng.NextDouble() < chance && TryIgniteWildfireTile(state, x, y))
                    ignited++;
            }
        }

        // Snapshot burners so same-tick chains stay deterministic.
        var burners = new List<(int X, int Y)>();
        for (int y = 0; y < tiles.Size; y++)
        for (int x = 0; x < tiles.Size; x++)
        {
            if (buf[tiles.Index(x, y)] >= BurningThreshold)
                burners.Add((x, y));
        }

        float wind = FireResponse.WindFactor(state.WindSpeed);
        float spreadBase = BaseWildfireSpreadChance * wind * hoursClamped;

        foreach (var (sx, sy) in burners)
        {
            foreach (var (dx, dy) in Cardinal)
            {
                int nx = sx + dx;
                int ny = sy + dy;
                if (!tiles.InBounds(nx, ny)) continue;
                if (IsFirebreak(state, nx, ny)) continue;

                int nIdx = tiles.Index(nx, ny);

                // Adjacent building → structural fire.
                if (tiles.BuildingId[nIdx] != 0)
                {
                    int buildingId = FireResponse.FindBuildingAt(state.Buildings, nx, ny);
                    if (buildingId >= 0 && !FireResponse.IsBurning(state.Buildings, buildingId))
                    {
                        float chance = Math.Clamp(spreadBase * 0.8f, 0f, 1f);
                        if (rng.NextDouble() < chance)
                            FireResponse.TryIgnite(state, buildingId);
                    }

                    continue;
                }

                if (!IsFuelTile(state, nx, ny)) continue;
                if (buf[nIdx] >= BurningThreshold) continue;

                float chanceFuel = Math.Clamp(spreadBase, 0f, 1f);
                if (rng.NextDouble() < chanceFuel && TryIgniteWildfireTile(state, nx, ny))
                    ignited++;
            }
        }

        return ignited;
    }

    /// <summary>
    /// Probabilistic arson ignitions on buildings in high-crime tiles.
    /// Returns newly ignited building count.
    /// </summary>
    public static int TickArson(
        WorldState state,
        float hours = 1f,
        Random? rng = null)
    {
        rng ??= new Random();
        float hoursClamped = Math.Max(0f, hours);
        if (hoursClamped <= 0f) return 0;

        var buildings = state.Buildings;
        var tiles = state.Tiles;
        int ignited = 0;

        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if (FireResponse.IsBurning(buildings, i)) continue;

            int x = buildings.GridX[i];
            int y = buildings.GridY[i];
            if (!tiles.InBounds(x, y)) continue;

            float crime = tiles.Crime[tiles.Index(x, y)];
            float risk = ArsonRiskFromCrime(crime);
            if (risk <= 0f) continue;

            float chance = Math.Clamp(BaseArsonChancePerHour * risk * hoursClamped, 0f, 1f);
            if (rng.NextDouble() < chance && FireResponse.TryIgnite(state, i))
                ignited++;
        }

        return ignited;
    }

    /// <summary>
    /// Full Tier-2 tick: wildfire sparks/spread, aerial suppression, arson ignitions,
    /// lookout/aerial counts, fire safety rating + insurance premium, HUD aggregates.
    /// Recounts <see cref="WorldState.ActiveFireCount"/> after building ignitions.
    /// </summary>
    public static void Tick(WorldState state, float hours = 1f, Random? rng = null)
    {
        rng ??= new Random();

        state.WildfireRiskIndex = CalculateDroughtRisk(state);
        TickWildfire(state, hours, rng);
        TickAerialSuppression(state, hours);
        TickArson(state, hours, rng);

        state.ActiveWildfireTileCount = CountActiveWildfireTiles(state);
        state.ArsonRiskIndex = CalculateArsonRiskIndex(state);
        state.ArsonRingActive = DetectArsonRing(state);
        state.ActiveFireCount = FireResponse.CountActiveFires(state);

        state.LookoutTowerCount = CountLookoutTowers(state);
        state.AerialFirefightingAvailable = HasAerialFirefighting(state);
        state.FireSafetyRating = CalculateFireSafetyRating(state);
        state.FireInsurancePremiumMult = InsurancePremiumMultFromRating(state.FireSafetyRating);
    }
}
