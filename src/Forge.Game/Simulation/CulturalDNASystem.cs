using Forge.Engine.Simulation;

namespace Forge.Game.Simulation;

/// <summary>
/// Cultural DNA simulation: 8 cultural dimensions that define a city's identity,
/// drift over time based on events/policies/demographics, produce gameplay modifiers,
/// and trigger archetype detection for emergent city personalities.
///
/// Dimensions are stored in WorldState.CulturalDna[0..7] as floats in range 0-100.
/// The system maps them to the WorldState's existing -1..+1 representation internally
/// using a 0-100 working scale for clearer formulas, then writes back normalized.
///
/// Drift formula (yearly):
///   new_value = old_value + sum(drift_sources) * (1.0 / log2(population/1000 + 1))
///   Population inertia: larger cities change culture more slowly.
///
/// Archetypes are detected yearly. A city can hold 1-3 simultaneous archetypes
/// based on dimension thresholds and combination rules.
/// </summary>
public sealed class CulturalDNASystem
{
    // =========================================================================
    // Dimension enum -- maps to WorldState.CulturalDna indices
    // =========================================================================

    /// <summary>
    /// 8 cultural dimensions, each representing a spectrum from 0 (low) to 100 (high).
    /// Stored in WorldState.CulturalDna[0..7] normalized to -1..+1.
    /// </summary>
    public enum Dimension : byte
    {
        /// <summary>Work ethic: productivity bonus, overtime acceptance, inverse leisure demand.</summary>
        WorkEthic = 0,
        /// <summary>Collectivism: transit acceptance, tax tolerance, community event frequency.</summary>
        Collectivism = 1,
        /// <summary>Risk tolerance: startup rate, R&amp;D enthusiasm, experimental tech adoption.</summary>
        RiskTolerance = 2,
        /// <summary>Environmental values: green policy acceptance, recycling rate, industry tolerance (inverse).</summary>
        EnvironmentalValues = 3,
        /// <summary>Social trust: tax compliance, crime baseline (inverse), corruption tolerance (inverse).</summary>
        SocialTrust = 4,
        /// <summary>Progressivism: social law acceptance, tech adoption speed, diversity demand.</summary>
        Progressivism = 5,
        /// <summary>Hierarchy acceptance: wealth inequality tolerance, authority acceptance, union strength (inverse).</summary>
        HierarchyAcceptance = 6,
        /// <summary>Cultural pride: immigration acceptance (inverse), heritage preservation, chain store resistance.</summary>
        CulturalPride = 7,
    }

    /// <summary>Total number of cultural dimensions.</summary>
    public const int DimensionCount = 8;

    // =========================================================================
    // Archetype definitions
    // =========================================================================

    /// <summary>A cultural archetype represents an emergent city personality.</summary>
    public sealed class Archetype
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        /// <summary>Required dimension thresholds. Key = dimension, Value = (min, max) in 0-100 scale.</summary>
        public Dictionary<Dimension, (float Min, float Max)> Conditions { get; }
        /// <summary>Gameplay effect modifiers applied when this archetype is active.</summary>
        public Dictionary<string, float> Modifiers { get; }

        public Archetype(string id, string name, string description,
            Dictionary<Dimension, (float Min, float Max)> conditions,
            Dictionary<string, float> modifiers)
        {
            Id = id;
            Name = name;
            Description = description;
            Conditions = conditions;
            Modifiers = modifiers;
        }
    }

    // =========================================================================
    // State
    // =========================================================================

    private readonly List<Archetype> _allArchetypes = new();
    private readonly List<Archetype> _activeArchetypes = new();

    /// <summary>Maximum simultaneous archetypes a city can have.</summary>
    public const int MaxActiveArchetypes = 3;

    /// <summary>Currently active archetypes (read-only view).</summary>
    public IReadOnlyList<Archetype> ActiveArchetypes => _activeArchetypes;

    /// <summary>All defined archetypes (read-only view).</summary>
    public IReadOnlyList<Archetype> AllArchetypes => _allArchetypes;

    // =========================================================================
    // Construction
    // =========================================================================

    public CulturalDNASystem()
    {
        RegisterArchetypes();
    }

    // =========================================================================
    // Regional presets
    // =========================================================================

    /// <summary>
    /// Get starting cultural DNA values for a region. Returns 8 floats in 0-100 scale.
    /// Order: WorkEthic, Collectivism, RiskTolerance, EnvironmentalValues,
    ///        SocialTrust, Progressivism, HierarchyAcceptance, CulturalPride.
    /// </summary>
    public static float[] GetPreset(string regionName)
    {
        return (regionName?.ToLowerInvariant()) switch
        {
            "north_american" or "north american" =>
                new float[] { 65, 35, 70, 45, 55, 60, 35, 40 },
            "western_european" or "western european" =>
                new float[] { 55, 55, 50, 65, 65, 65, 40, 55 },
            "japanese_korean" or "japanese/korean" or "east asian" =>
                new float[] { 85, 75, 40, 55, 70, 45, 70, 75 },
            "se_asian" or "southeast asian" or "se asian" =>
                new float[] { 60, 65, 50, 40, 50, 45, 55, 60 },
            "south_asian" or "south asian" =>
                new float[] { 65, 60, 45, 35, 45, 40, 65, 70 },
            "gulf_middle_eastern" or "gulf/middle eastern" or "middle eastern" =>
                new float[] { 55, 50, 45, 30, 45, 25, 75, 80 },
            "sub_saharan_african" or "sub-saharan african" =>
                new float[] { 55, 70, 45, 40, 50, 40, 50, 65 },
            "south_american" or "south american" =>
                new float[] { 50, 55, 55, 45, 40, 55, 45, 60 },
            "scandinavian" =>
                new float[] { 60, 75, 55, 80, 80, 75, 25, 50 },
            "eastern_european" or "eastern european" =>
                new float[] { 60, 50, 40, 35, 40, 35, 60, 70 },
            "caribbean" =>
                new float[] { 45, 60, 50, 50, 45, 55, 40, 65 },
            "oceanian" =>
                new float[] { 55, 45, 55, 60, 65, 65, 30, 45 },
            _ => new float[] { 50, 50, 50, 50, 50, 50, 50, 50 }, // Neutral default
        };
    }

    /// <summary>
    /// Apply a regional preset to a WorldState, writing values into CulturalDna[].
    /// Converts from 0-100 scale to the WorldState's -1..+1 representation.
    /// </summary>
    public static void ApplyPreset(WorldState state, string regionName)
    {
        float[] preset = GetPreset(regionName);
        for (int i = 0; i < DimensionCount && i < preset.Length; i++)
        {
            state.CulturalDna[i] = ToNormalized(preset[i]);
        }
    }

    // =========================================================================
    // Yearly tick -- cultural drift + archetype detection
    // =========================================================================

    /// <summary>
    /// Advance cultural drift once per game year. Calculates drift from multiple sources,
    /// applies population inertia, clamps values, and re-detects archetypes.
    /// </summary>
    public void YearlyTick(WorldState state)
    {
        // Population inertia: larger cities resist cultural change
        float inertia = 1.0f / ((float)Math.Log(state.Population / 1000f + 1f + 1f, 2));
        // The extra +1 prevents division by zero at pop=0 and ensures log2 >= ~1

        float[] drifts = new float[DimensionCount];

        // --- Drift source: prosperity ---
        // High city funds and happiness push toward progressivism and environmental values
        float prosperitySignal = (state.Happiness - 0.5f) * 2f; // -1 to +1
        drifts[(int)Dimension.Progressivism] += prosperitySignal * 3f;
        drifts[(int)Dimension.EnvironmentalValues] += prosperitySignal * 2f;
        drifts[(int)Dimension.RiskTolerance] += prosperitySignal * 1.5f;

        // --- Drift source: economic pressure ---
        // Low funds push toward work ethic and hierarchy acceptance
        if (state.CityFunds < 10_000)
        {
            float pressure = Math.Clamp(1f - state.CityFunds / 10_000f, 0f, 1f);
            drifts[(int)Dimension.WorkEthic] += pressure * 2f;
            drifts[(int)Dimension.HierarchyAcceptance] += pressure * 1.5f;
            drifts[(int)Dimension.Collectivism] += pressure * 1f;
        }

        // --- Drift source: population density ---
        // Dense populations push toward collectivism and social trust
        float density = state.Population / (float)(state.Tiles.Size * state.Tiles.Size + 1);
        float densitySignal = Math.Clamp(density * 100f, 0f, 1f);
        drifts[(int)Dimension.Collectivism] += densitySignal * 2f;
        drifts[(int)Dimension.SocialTrust] += densitySignal * 1f;

        // --- Drift source: crime level ---
        // High crime erodes social trust
        float avgCrime = GetAverageTileValue(state.Tiles.Crime, state.Tiles.Count);
        drifts[(int)Dimension.SocialTrust] -= avgCrime * 5f;

        // --- Drift source: pollution level ---
        // High pollution pushes environmental values up (awareness)
        float avgPollution = GetAverageTileValue(state.Tiles.Pollution, state.Tiles.Count);
        drifts[(int)Dimension.EnvironmentalValues] += avgPollution * 3f;

        // --- Drift source: education ---
        // Education coverage pushes progressivism and risk tolerance
        float avgEducation = GetAverageServiceLevel(state, 3); // education is bits 6-7
        drifts[(int)Dimension.Progressivism] += avgEducation * 2f;
        drifts[(int)Dimension.RiskTolerance] += avgEducation * 1.5f;

        // --- Drift source: approval rating ---
        // Low approval erodes hierarchy acceptance
        float approvalSignal = (state.ApprovalRating - 0.5f) * 2f;
        drifts[(int)Dimension.HierarchyAcceptance] += approvalSignal * 1.5f;

        // --- Drift source: era ---
        // Later eras tend toward progressivism and risk tolerance
        float eraSignal = state.Era / 5f; // 0 to 1
        drifts[(int)Dimension.Progressivism] += eraSignal * 1f;
        drifts[(int)Dimension.RiskTolerance] += eraSignal * 0.5f;

        // --- Drift source: generational shift ---
        // Small random drift representing generational change
        // Use a deterministic seed per year for reproducibility
        var yearRng = new Random(state.Year * 31337);
        for (int i = 0; i < DimensionCount; i++)
        {
            drifts[i] += ((float)yearRng.NextDouble() - 0.5f) * 1f;
        }

        // Apply drifts with inertia, clamp to 0-100 (stored as -1..+1)
        for (int i = 0; i < DimensionCount; i++)
        {
            float currentValue = FromNormalized(state.CulturalDna[i]);
            float newValue = currentValue + drifts[i] * inertia;
            newValue = Math.Clamp(newValue, 0f, 100f);
            state.CulturalDna[i] = ToNormalized(newValue);
        }

        DetectArchetypes(state);
    }

    // =========================================================================
    // Archetype detection
    // =========================================================================

    /// <summary>
    /// Re-evaluate which archetypes are active based on current cultural dimensions.
    /// A city can have at most MaxActiveArchetypes simultaneous archetypes.
    /// Archetypes are sorted by match quality (how many conditions are met strongly).
    /// </summary>
    public void DetectArchetypes(WorldState state)
    {
        _activeArchetypes.Clear();

        // Score each archetype by how well the city matches
        var candidates = new List<(Archetype archetype, float score)>();

        foreach (var archetype in _allArchetypes)
        {
            float score = ScoreArchetype(archetype, state);
            if (score > 0f)
            {
                candidates.Add((archetype, score));
            }
        }

        // Sort by score descending, take top N
        candidates.Sort((a, b) => b.score.CompareTo(a.score));

        int count = Math.Min(candidates.Count, MaxActiveArchetypes);
        for (int i = 0; i < count; i++)
        {
            _activeArchetypes.Add(candidates[i].archetype);
        }
    }

    private float ScoreArchetype(Archetype archetype, WorldState state)
    {
        float totalScore = 0f;
        int conditionCount = archetype.Conditions.Count;
        if (conditionCount == 0) return 0f;

        foreach (var kvp in archetype.Conditions)
        {
            float value = FromNormalized(state.CulturalDna[(int)kvp.Key]);
            float min = kvp.Value.Min;
            float max = kvp.Value.Max;

            if (value < min || value > max)
                return 0f; // Fail -- all conditions must be met

            // Score based on how deeply within the range the value sits
            float range = max - min;
            if (range <= 0f) continue;
            float center = (min + max) / 2f;
            float distanceFromCenter = Math.Abs(value - center);
            float normalizedFit = 1f - (distanceFromCenter / (range / 2f));
            totalScore += normalizedFit;
        }

        return totalScore / conditionCount;
    }

    // =========================================================================
    // Gameplay modifier queries
    // =========================================================================

    /// <summary>
    /// Productivity modifier from WorkEthic dimension.
    /// Range: 0.7 (WorkEthic=0) to 1.3 (WorkEthic=100).
    /// </summary>
    public float GetProductivityModifier(WorldState state)
    {
        float workEthic = FromNormalized(state.CulturalDna[(int)Dimension.WorkEthic]);
        float baseMod = 0.7f + (workEthic / 100f) * 0.6f;
        return baseMod + GetArchetypeModifier("productivity");
    }

    /// <summary>
    /// Transit acceptance from Collectivism dimension.
    /// Range: 0.3 (Collectivism=0) to 1.0 (Collectivism=100).
    /// </summary>
    public float GetTransitAcceptance(WorldState state)
    {
        float collectivism = FromNormalized(state.CulturalDna[(int)Dimension.Collectivism]);
        float baseMod = 0.3f + (collectivism / 100f) * 0.7f;
        return baseMod + GetArchetypeModifier("transit_acceptance");
    }

    /// <summary>
    /// Startup rate modifier from RiskTolerance dimension.
    /// Range: 0.5 (RiskTolerance=0) to 1.5 (RiskTolerance=100).
    /// </summary>
    public float GetStartupRate(WorldState state)
    {
        float riskTolerance = FromNormalized(state.CulturalDna[(int)Dimension.RiskTolerance]);
        float baseMod = 0.5f + (riskTolerance / 100f) * 1.0f;
        return baseMod + GetArchetypeModifier("startup_rate");
    }

    /// <summary>
    /// Pollution tolerance from EnvironmentalValues dimension.
    /// Range: 0.8 (EnvironmentalValues=100) to 0.2 (EnvironmentalValues=0).
    /// High environmental values = LOW pollution tolerance.
    /// </summary>
    public float GetPollutionTolerance(WorldState state)
    {
        float envValues = FromNormalized(state.CulturalDna[(int)Dimension.EnvironmentalValues]);
        float baseMod = 0.8f - (envValues / 100f) * 0.6f;
        return baseMod + GetArchetypeModifier("pollution_tolerance");
    }

    /// <summary>
    /// Crime baseline from SocialTrust dimension.
    /// Range: 0.3 (SocialTrust=0) to 0.02 (SocialTrust=100).
    /// High trust = low baseline crime.
    /// </summary>
    public float GetCrimeBaseline(WorldState state)
    {
        float trust = FromNormalized(state.CulturalDna[(int)Dimension.SocialTrust]);
        float baseMod = 0.3f - (trust / 100f) * 0.28f;
        return Math.Max(0f, baseMod + GetArchetypeModifier("crime_baseline"));
    }

    /// <summary>
    /// Technology adoption speed from Progressivism dimension.
    /// Range: 0.5 (Progressivism=0) to 1.5 (Progressivism=100).
    /// </summary>
    public float GetTechAdoptionSpeed(WorldState state)
    {
        float progressivism = FromNormalized(state.CulturalDna[(int)Dimension.Progressivism]);
        float baseMod = 0.5f + (progressivism / 100f) * 1.0f;
        return baseMod + GetArchetypeModifier("tech_adoption");
    }

    /// <summary>
    /// Inequality tolerance from HierarchyAcceptance dimension.
    /// Range: 0.2 (HierarchyAcceptance=0) to 0.9 (HierarchyAcceptance=100).
    /// </summary>
    public float GetInequalityTolerance(WorldState state)
    {
        float hierarchy = FromNormalized(state.CulturalDna[(int)Dimension.HierarchyAcceptance]);
        float baseMod = 0.2f + (hierarchy / 100f) * 0.7f;
        return baseMod + GetArchetypeModifier("inequality_tolerance");
    }

    /// <summary>
    /// Immigration acceptance from CulturalPride dimension (inverse relationship).
    /// Range: 0.3 (CulturalPride=100) to 1.0 (CulturalPride=0).
    /// High cultural pride = lower immigration acceptance.
    /// </summary>
    public float GetImmigrationAcceptance(WorldState state)
    {
        float pride = FromNormalized(state.CulturalDna[(int)Dimension.CulturalPride]);
        float baseMod = 1.0f - (pride / 100f) * 0.7f;
        return baseMod + GetArchetypeModifier("immigration_acceptance");
    }

    // =========================================================================
    // Scale conversion helpers
    // =========================================================================

    /// <summary>Convert from 0-100 working scale to WorldState -1..+1 representation.</summary>
    internal static float ToNormalized(float value0to100)
    {
        return (value0to100 / 50f) - 1f;
    }

    /// <summary>Convert from WorldState -1..+1 representation to 0-100 working scale.</summary>
    internal static float FromNormalized(float normalizedValue)
    {
        return (normalizedValue + 1f) * 50f;
    }

    // =========================================================================
    // Archetype modifier aggregation
    // =========================================================================

    private float GetArchetypeModifier(string modifierKey)
    {
        float total = 0f;
        for (int i = 0; i < _activeArchetypes.Count; i++)
        {
            if (_activeArchetypes[i].Modifiers.TryGetValue(modifierKey, out float val))
                total += val;
        }
        return total;
    }

    // =========================================================================
    // Tile value helpers
    // =========================================================================

    private static float GetAverageTileValue(float[] values, int count)
    {
        if (count == 0) return 0f;
        double sum = 0;
        for (int i = 0; i < count; i++)
            sum += values[i];
        return (float)(sum / count);
    }

    private static float GetAverageServiceLevel(WorldState state, int serviceShift)
    {
        // serviceShift: 0=fire, 2=police, 4=health, 6=education
        int tileCount = state.Tiles.Count;
        if (tileCount == 0) return 0f;
        int bitShift = serviceShift * 2;
        double sum = 0;
        for (int i = 0; i < tileCount; i++)
        {
            sum += (state.Tiles.ServiceCoverage[i] >> bitShift) & 0x03;
        }
        return (float)(sum / (tileCount * 3.0)); // Normalize 0-3 to 0-1
    }

    // =========================================================================
    // 20 archetype definitions
    // =========================================================================

    private void RegisterArchetypes()
    {
        _allArchetypes.Add(new Archetype(
            "silicon_valley", "Silicon Valley",
            "A tech-obsessed innovation hub with sky-high risk tolerance and progressive values.",
            new Dictionary<Dimension, (float, float)>
            {
                [Dimension.RiskTolerance] = (70, 100),
                [Dimension.Progressivism] = (65, 100),
                [Dimension.WorkEthic] = (60, 100),
            },
            new Dictionary<string, float>
            {
                ["startup_rate"] = 0.2f,
                ["tech_adoption"] = 0.15f,
                ["productivity"] = 0.05f,
            }));

        _allArchetypes.Add(new Archetype(
            "green_utopia", "Green Utopia",
            "An environmentally conscious city with strong communal values.",
            new Dictionary<Dimension, (float, float)>
            {
                [Dimension.EnvironmentalValues] = (75, 100),
                [Dimension.Collectivism] = (60, 100),
                [Dimension.SocialTrust] = (55, 100),
            },
            new Dictionary<string, float>
            {
                ["pollution_tolerance"] = -0.1f,
                ["transit_acceptance"] = 0.15f,
                ["crime_baseline"] = -0.03f,
            }));

        _allArchetypes.Add(new Archetype(
            "industrial_powerhouse", "Industrial Powerhouse",
            "A hard-working manufacturing city that prioritizes output over environment.",
            new Dictionary<Dimension, (float, float)>
            {
                [Dimension.WorkEthic] = (70, 100),
                [Dimension.EnvironmentalValues] = (0, 35),
                [Dimension.HierarchyAcceptance] = (55, 100),
            },
            new Dictionary<string, float>
            {
                ["productivity"] = 0.15f,
                ["pollution_tolerance"] = 0.15f,
                ["inequality_tolerance"] = 0.1f,
            }));

        _allArchetypes.Add(new Archetype(
            "cultural_capital", "Cultural Capital",
            "A city proud of its heritage that resists homogenization.",
            new Dictionary<Dimension, (float, float)>
            {
                [Dimension.CulturalPride] = (75, 100),
                [Dimension.Progressivism] = (40, 100),
            },
            new Dictionary<string, float>
            {
                ["immigration_acceptance"] = -0.1f,
                ["transit_acceptance"] = 0.05f,
            }));

        _allArchetypes.Add(new Archetype(
            "socialist_haven", "Socialist Haven",
            "A highly collectivist city with strong social safety nets and low inequality.",
            new Dictionary<Dimension, (float, float)>
            {
                [Dimension.Collectivism] = (75, 100),
                [Dimension.HierarchyAcceptance] = (0, 30),
                [Dimension.SocialTrust] = (60, 100),
            },
            new Dictionary<string, float>
            {
                ["transit_acceptance"] = 0.2f,
                ["inequality_tolerance"] = -0.15f,
                ["crime_baseline"] = -0.05f,
            }));

        _allArchetypes.Add(new Archetype(
            "free_market", "Free Market Hub",
            "A laissez-faire economy with minimal regulation and high risk tolerance.",
            new Dictionary<Dimension, (float, float)>
            {
                [Dimension.RiskTolerance] = (65, 100),
                [Dimension.HierarchyAcceptance] = (55, 100),
                [Dimension.Collectivism] = (0, 35),
            },
            new Dictionary<string, float>
            {
                ["startup_rate"] = 0.15f,
                ["inequality_tolerance"] = 0.15f,
                ["productivity"] = 0.05f,
            }));

        _allArchetypes.Add(new Archetype(
            "fortress_city", "Fortress City",
            "An insular, high-pride city skeptical of outsiders.",
            new Dictionary<Dimension, (float, float)>
            {
                [Dimension.CulturalPride] = (80, 100),
                [Dimension.SocialTrust] = (0, 40),
                [Dimension.Progressivism] = (0, 35),
            },
            new Dictionary<string, float>
            {
                ["immigration_acceptance"] = -0.2f,
                ["crime_baseline"] = 0.05f,
            }));

        _allArchetypes.Add(new Archetype(
            "melting_pot", "Melting Pot",
            "A cosmopolitan city that welcomes immigrants and embraces diversity.",
            new Dictionary<Dimension, (float, float)>
            {
                [Dimension.CulturalPride] = (0, 35),
                [Dimension.Progressivism] = (60, 100),
                [Dimension.SocialTrust] = (50, 100),
            },
            new Dictionary<string, float>
            {
                ["immigration_acceptance"] = 0.2f,
                ["tech_adoption"] = 0.05f,
                ["startup_rate"] = 0.05f,
            }));

        _allArchetypes.Add(new Archetype(
            "academic_enclave", "Academic Enclave",
            "A city centered around education and research institutions.",
            new Dictionary<Dimension, (float, float)>
            {
                [Dimension.Progressivism] = (65, 100),
                [Dimension.RiskTolerance] = (50, 100),
                [Dimension.SocialTrust] = (55, 100),
            },
            new Dictionary<string, float>
            {
                ["tech_adoption"] = 0.1f,
                ["startup_rate"] = 0.1f,
                ["crime_baseline"] = -0.02f,
            }));

        _allArchetypes.Add(new Archetype(
            "resort_town", "Resort Town",
            "A relaxed, tourism-focused city with low work ethic but high quality of life.",
            new Dictionary<Dimension, (float, float)>
            {
                [Dimension.WorkEthic] = (0, 40),
                [Dimension.EnvironmentalValues] = (55, 100),
                [Dimension.CulturalPride] = (50, 100),
            },
            new Dictionary<string, float>
            {
                ["productivity"] = -0.1f,
                ["pollution_tolerance"] = -0.1f,
                ["immigration_acceptance"] = 0.05f,
            }));

        _allArchetypes.Add(new Archetype(
            "surveillance_state", "Surveillance State",
            "A high-order city with extensive monitoring and low crime.",
            new Dictionary<Dimension, (float, float)>
            {
                [Dimension.HierarchyAcceptance] = (75, 100),
                [Dimension.SocialTrust] = (0, 40),
                [Dimension.Collectivism] = (55, 100),
            },
            new Dictionary<string, float>
            {
                ["crime_baseline"] = -0.1f,
                ["immigration_acceptance"] = -0.1f,
            }));

        _allArchetypes.Add(new Archetype(
            "bohemian_quarter", "Bohemian Quarter",
            "An artsy, progressive city with high risk tolerance and low hierarchy.",
            new Dictionary<Dimension, (float, float)>
            {
                [Dimension.Progressivism] = (70, 100),
                [Dimension.HierarchyAcceptance] = (0, 30),
                [Dimension.RiskTolerance] = (55, 100),
            },
            new Dictionary<string, float>
            {
                ["startup_rate"] = 0.1f,
                ["immigration_acceptance"] = 0.1f,
                ["productivity"] = -0.05f,
            }));

        _allArchetypes.Add(new Archetype(
            "company_town", "Company Town",
            "A city dominated by a single industry with high work ethic.",
            new Dictionary<Dimension, (float, float)>
            {
                [Dimension.WorkEthic] = (75, 100),
                [Dimension.HierarchyAcceptance] = (65, 100),
                [Dimension.RiskTolerance] = (0, 35),
            },
            new Dictionary<string, float>
            {
                ["productivity"] = 0.15f,
                ["startup_rate"] = -0.1f,
                ["inequality_tolerance"] = 0.1f,
            }));

        _allArchetypes.Add(new Archetype(
            "eco_warrior", "Eco Warrior",
            "An aggressively green city that prioritizes environment over economy.",
            new Dictionary<Dimension, (float, float)>
            {
                [Dimension.EnvironmentalValues] = (80, 100),
                [Dimension.Progressivism] = (60, 100),
            },
            new Dictionary<string, float>
            {
                ["pollution_tolerance"] = -0.15f,
                ["transit_acceptance"] = 0.2f,
                ["productivity"] = -0.05f,
            }));

        _allArchetypes.Add(new Archetype(
            "rust_belt", "Rust Belt",
            "A declining industrial city struggling with economic transition.",
            new Dictionary<Dimension, (float, float)>
            {
                [Dimension.WorkEthic] = (55, 100),
                [Dimension.RiskTolerance] = (0, 35),
                [Dimension.Progressivism] = (0, 40),
            },
            new Dictionary<string, float>
            {
                ["productivity"] = -0.1f,
                ["startup_rate"] = -0.1f,
                ["crime_baseline"] = 0.05f,
            }));

        _allArchetypes.Add(new Archetype(
            "tax_haven", "Tax Haven",
            "A business-friendly city with low collective spirit and high risk tolerance.",
            new Dictionary<Dimension, (float, float)>
            {
                [Dimension.RiskTolerance] = (60, 100),
                [Dimension.Collectivism] = (0, 30),
                [Dimension.HierarchyAcceptance] = (50, 100),
            },
            new Dictionary<string, float>
            {
                ["startup_rate"] = 0.15f,
                ["inequality_tolerance"] = 0.2f,
                ["immigration_acceptance"] = 0.1f,
            }));

        _allArchetypes.Add(new Archetype(
            "spiritual_center", "Spiritual Center",
            "A city with deep cultural and spiritual traditions.",
            new Dictionary<Dimension, (float, float)>
            {
                [Dimension.CulturalPride] = (70, 100),
                [Dimension.HierarchyAcceptance] = (55, 100),
                [Dimension.Progressivism] = (0, 40),
            },
            new Dictionary<string, float>
            {
                ["crime_baseline"] = -0.03f,
                ["immigration_acceptance"] = -0.05f,
                ["tech_adoption"] = -0.05f,
            }));

        _allArchetypes.Add(new Archetype(
            "frontier_town", "Frontier Town",
            "A small, risk-taking settlement on the edge of civilization.",
            new Dictionary<Dimension, (float, float)>
            {
                [Dimension.RiskTolerance] = (70, 100),
                [Dimension.Collectivism] = (0, 40),
                [Dimension.SocialTrust] = (0, 45),
            },
            new Dictionary<string, float>
            {
                ["startup_rate"] = 0.1f,
                ["crime_baseline"] = 0.05f,
                ["productivity"] = 0.05f,
            }));

        _allArchetypes.Add(new Archetype(
            "welfare_state", "Welfare State",
            "A caring society with strong social programs and high trust.",
            new Dictionary<Dimension, (float, float)>
            {
                [Dimension.Collectivism] = (65, 100),
                [Dimension.SocialTrust] = (65, 100),
                [Dimension.HierarchyAcceptance] = (0, 40),
            },
            new Dictionary<string, float>
            {
                ["crime_baseline"] = -0.05f,
                ["transit_acceptance"] = 0.15f,
                ["inequality_tolerance"] = -0.1f,
            }));

        _allArchetypes.Add(new Archetype(
            "party_city", "Party City",
            "A lively, entertainment-focused city with relaxed attitudes.",
            new Dictionary<Dimension, (float, float)>
            {
                [Dimension.WorkEthic] = (0, 35),
                [Dimension.Progressivism] = (55, 100),
                [Dimension.RiskTolerance] = (55, 100),
            },
            new Dictionary<string, float>
            {
                ["productivity"] = -0.1f,
                ["immigration_acceptance"] = 0.1f,
                ["crime_baseline"] = 0.03f,
            }));
    }
}
