using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Xunit;

namespace Forge.Engine.Tests;

public class CulturalDNASystemTests
{
    private static WorldState CreateTestWorld(int size = 16)
    {
        return new WorldState(size);
    }

    // =========================================================================
    // Scale conversion tests
    // =========================================================================

    [Theory]
    [InlineData(0f, -1f)]
    [InlineData(50f, 0f)]
    [InlineData(100f, 1f)]
    [InlineData(25f, -0.5f)]
    [InlineData(75f, 0.5f)]
    public void ToNormalized_ConvertsCorrectly(float input, float expected)
    {
        float result = CulturalDNASystem.ToNormalized(input);
        Assert.Equal(expected, result, 0.001f);
    }

    [Theory]
    [InlineData(-1f, 0f)]
    [InlineData(0f, 50f)]
    [InlineData(1f, 100f)]
    [InlineData(-0.5f, 25f)]
    [InlineData(0.5f, 75f)]
    public void FromNormalized_ConvertsCorrectly(float input, float expected)
    {
        float result = CulturalDNASystem.FromNormalized(input);
        Assert.Equal(expected, result, 0.001f);
    }

    [Fact]
    public void RoundTrip_ToNormalized_FromNormalized()
    {
        for (float v = 0f; v <= 100f; v += 5f)
        {
            float normalized = CulturalDNASystem.ToNormalized(v);
            float roundTrip = CulturalDNASystem.FromNormalized(normalized);
            Assert.Equal(v, roundTrip, 0.01f);
        }
    }

    // =========================================================================
    // Regional preset tests
    // =========================================================================

    [Fact]
    public void GetPreset_ReturnsEightValues()
    {
        string[] regions = {
            "north_american", "western_european", "japanese_korean",
            "se_asian", "south_asian", "gulf_middle_eastern",
            "sub_saharan_african", "south_american", "scandinavian",
            "eastern_european", "caribbean", "oceanian"
        };

        foreach (string region in regions)
        {
            float[] preset = CulturalDNASystem.GetPreset(region);
            Assert.Equal(CulturalDNASystem.DimensionCount, preset.Length);
        }
    }

    [Fact]
    public void GetPreset_ValuesInRange()
    {
        string[] regions = {
            "north_american", "western_european", "japanese_korean",
            "se_asian", "south_asian", "gulf_middle_eastern",
            "sub_saharan_african", "south_american", "scandinavian",
            "eastern_european", "caribbean", "oceanian"
        };

        foreach (string region in regions)
        {
            float[] preset = CulturalDNASystem.GetPreset(region);
            for (int i = 0; i < preset.Length; i++)
            {
                Assert.InRange(preset[i], 0f, 100f);
            }
        }
    }

    [Fact]
    public void GetPreset_UnknownRegion_ReturnsNeutralDefaults()
    {
        float[] preset = CulturalDNASystem.GetPreset("unknown_region");
        for (int i = 0; i < preset.Length; i++)
        {
            Assert.Equal(50f, preset[i]);
        }
    }

    [Fact]
    public void GetPreset_Scandinavian_HasHighEnvironmentalValues()
    {
        float[] preset = CulturalDNASystem.GetPreset("scandinavian");
        // EnvironmentalValues is index 3
        Assert.True(preset[3] >= 75f, "Scandinavian preset should have high environmental values");
    }

    [Fact]
    public void GetPreset_JapaneseKorean_HasHighWorkEthic()
    {
        float[] preset = CulturalDNASystem.GetPreset("japanese_korean");
        // WorkEthic is index 0
        Assert.True(preset[0] >= 80f, "Japanese/Korean preset should have high work ethic");
    }

    [Fact]
    public void GetPreset_VariantNameFormats_Match()
    {
        // Test that alternate name formats produce the same result
        float[] a = CulturalDNASystem.GetPreset("north_american");
        float[] b = CulturalDNASystem.GetPreset("north american");
        Assert.Equal(a, b);

        float[] c = CulturalDNASystem.GetPreset("western_european");
        float[] d = CulturalDNASystem.GetPreset("western european");
        Assert.Equal(c, d);
    }

    [Fact]
    public void ApplyPreset_WritesToWorldState()
    {
        var state = CreateTestWorld();
        CulturalDNASystem.ApplyPreset(state, "scandinavian");

        // Scandinavian preset: WorkEthic=60, so normalized = (60/50)-1 = 0.2
        float workEthicNorm = CulturalDNASystem.ToNormalized(60f);
        Assert.Equal(workEthicNorm, state.CulturalDna[0], 0.01f);

        // EnvironmentalValues=80, normalized = (80/50)-1 = 0.6
        float envNorm = CulturalDNASystem.ToNormalized(80f);
        Assert.Equal(envNorm, state.CulturalDna[3], 0.01f);
    }

    // =========================================================================
    // Cultural drift formula tests
    // =========================================================================

    [Fact]
    public void YearlyTick_DriftsCulturalValues()
    {
        var system = new CulturalDNASystem();
        var state = CreateTestWorld();
        state.Population = 10000;
        state.Happiness = 0.7f; // High prosperity
        state.CityFunds = 100_000;

        CulturalDNASystem.ApplyPreset(state, "north_american");

        float[] before = new float[CulturalDNASystem.DimensionCount];
        Array.Copy(state.CulturalDna, before, CulturalDNASystem.DimensionCount);

        system.YearlyTick(state);

        // At least some dimensions should have changed
        bool anyChanged = false;
        for (int i = 0; i < CulturalDNASystem.DimensionCount; i++)
        {
            if (global::System.Math.Abs(state.CulturalDna[i] - before[i]) > 0.0001f)
            {
                anyChanged = true;
                break;
            }
        }
        Assert.True(anyChanged, "Cultural values should drift after yearly tick");
    }

    [Fact]
    public void YearlyTick_LargerPopulation_SlowerDrift()
    {
        // Small city
        var systemSmall = new CulturalDNASystem();
        var stateSmall = CreateTestWorld();
        stateSmall.Population = 1000;
        stateSmall.Happiness = 0.8f;
        stateSmall.CityFunds = 100_000;
        stateSmall.Year = 2024;
        CulturalDNASystem.ApplyPreset(stateSmall, "north_american");
        float[] beforeSmall = (float[])stateSmall.CulturalDna.Clone();
        systemSmall.YearlyTick(stateSmall);

        // Large city
        var systemLarge = new CulturalDNASystem();
        var stateLarge = CreateTestWorld();
        stateLarge.Population = 1_000_000;
        stateLarge.Happiness = 0.8f;
        stateLarge.CityFunds = 100_000;
        stateLarge.Year = 2024;
        CulturalDNASystem.ApplyPreset(stateLarge, "north_american");
        float[] beforeLarge = (float[])stateLarge.CulturalDna.Clone();
        systemLarge.YearlyTick(stateLarge);

        // Calculate total absolute drift
        float driftSmall = 0f, driftLarge = 0f;
        for (int i = 0; i < CulturalDNASystem.DimensionCount; i++)
        {
            driftSmall += global::System.Math.Abs(stateSmall.CulturalDna[i] - beforeSmall[i]);
            driftLarge += global::System.Math.Abs(stateLarge.CulturalDna[i] - beforeLarge[i]);
        }

        Assert.True(driftSmall > driftLarge,
            $"Small city drift ({driftSmall:F4}) should exceed large city drift ({driftLarge:F4}) due to population inertia");
    }

    [Fact]
    public void YearlyTick_ValuesStayInRange()
    {
        var system = new CulturalDNASystem();
        var state = CreateTestWorld();
        state.Population = 5000;
        state.Happiness = 0.9f;
        state.CityFunds = 200_000;

        // Set extreme values
        for (int i = 0; i < CulturalDNASystem.DimensionCount; i++)
            state.CulturalDna[i] = 1.0f; // Max normalized (100 on 0-100 scale)

        for (int year = 0; year < 50; year++)
        {
            state.Year = 2024 + year;
            system.YearlyTick(state);
        }

        for (int i = 0; i < CulturalDNASystem.DimensionCount; i++)
        {
            Assert.InRange(state.CulturalDna[i], -1f, 1f);
        }
    }

    [Fact]
    public void YearlyTick_HighCrime_ErrodesSocialTrust()
    {
        var system = new CulturalDNASystem();
        var state = CreateTestWorld();
        state.Population = 10000;
        state.Happiness = 0.5f;
        state.CityFunds = 50_000;

        CulturalDNASystem.ApplyPreset(state, "scandinavian"); // Start with high trust (80)
        float trustBefore = state.CulturalDna[(int)CulturalDNASystem.Dimension.SocialTrust];

        // Set very high crime across all tiles
        for (int i = 0; i < state.Tiles.Count; i++)
            state.Tiles.Crime[i] = 0.8f;

        system.YearlyTick(state);
        float trustAfter = state.CulturalDna[(int)CulturalDNASystem.Dimension.SocialTrust];

        Assert.True(trustAfter < trustBefore,
            "High crime should erode social trust");
    }

    [Fact]
    public void YearlyTick_Prosperity_IncreasesProgressivism()
    {
        var system = new CulturalDNASystem();
        var state = CreateTestWorld();
        state.Population = 10000;
        state.Happiness = 0.9f; // Very prosperous
        state.CityFunds = 500_000;

        // Start with moderate progressivism
        CulturalDNASystem.ApplyPreset(state, "eastern_european"); // Progressivism=35
        float progBefore = state.CulturalDna[(int)CulturalDNASystem.Dimension.Progressivism];

        system.YearlyTick(state);
        float progAfter = state.CulturalDna[(int)CulturalDNASystem.Dimension.Progressivism];

        Assert.True(progAfter > progBefore,
            "High prosperity should increase progressivism");
    }

    // =========================================================================
    // Archetype detection tests
    // =========================================================================

    [Fact]
    public void DetectArchetypes_SiliconValley()
    {
        var system = new CulturalDNASystem();
        var state = CreateTestWorld();

        // Set dimensions to match Silicon Valley archetype
        state.CulturalDna[(int)CulturalDNASystem.Dimension.RiskTolerance] = CulturalDNASystem.ToNormalized(85);
        state.CulturalDna[(int)CulturalDNASystem.Dimension.Progressivism] = CulturalDNASystem.ToNormalized(80);
        state.CulturalDna[(int)CulturalDNASystem.Dimension.WorkEthic] = CulturalDNASystem.ToNormalized(75);

        system.DetectArchetypes(state);

        Assert.True(system.ActiveArchetypes.Any(a => a.Id == "silicon_valley"),
            "City with high risk tolerance, progressivism, and work ethic should activate Silicon Valley archetype");
    }

    [Fact]
    public void DetectArchetypes_GreenUtopia()
    {
        var system = new CulturalDNASystem();
        var state = CreateTestWorld();

        state.CulturalDna[(int)CulturalDNASystem.Dimension.EnvironmentalValues] = CulturalDNASystem.ToNormalized(85);
        state.CulturalDna[(int)CulturalDNASystem.Dimension.Collectivism] = CulturalDNASystem.ToNormalized(70);
        state.CulturalDna[(int)CulturalDNASystem.Dimension.SocialTrust] = CulturalDNASystem.ToNormalized(65);

        system.DetectArchetypes(state);

        Assert.True(system.ActiveArchetypes.Any(a => a.Id == "green_utopia"),
            "City with high env values, collectivism, and trust should activate Green Utopia");
    }

    [Fact]
    public void DetectArchetypes_MaxThreeArchetypes()
    {
        var system = new CulturalDNASystem();
        var state = CreateTestWorld();

        // Set values that could match many archetypes
        state.CulturalDna[(int)CulturalDNASystem.Dimension.WorkEthic] = CulturalDNASystem.ToNormalized(80);
        state.CulturalDna[(int)CulturalDNASystem.Dimension.Collectivism] = CulturalDNASystem.ToNormalized(80);
        state.CulturalDna[(int)CulturalDNASystem.Dimension.RiskTolerance] = CulturalDNASystem.ToNormalized(80);
        state.CulturalDna[(int)CulturalDNASystem.Dimension.EnvironmentalValues] = CulturalDNASystem.ToNormalized(80);
        state.CulturalDna[(int)CulturalDNASystem.Dimension.SocialTrust] = CulturalDNASystem.ToNormalized(80);
        state.CulturalDna[(int)CulturalDNASystem.Dimension.Progressivism] = CulturalDNASystem.ToNormalized(80);
        state.CulturalDna[(int)CulturalDNASystem.Dimension.HierarchyAcceptance] = CulturalDNASystem.ToNormalized(80);
        state.CulturalDna[(int)CulturalDNASystem.Dimension.CulturalPride] = CulturalDNASystem.ToNormalized(80);

        system.DetectArchetypes(state);

        Assert.True(system.ActiveArchetypes.Count <= CulturalDNASystem.MaxActiveArchetypes,
            $"Should have at most {CulturalDNASystem.MaxActiveArchetypes} active archetypes, got {system.ActiveArchetypes.Count}");
    }

    [Fact]
    public void DetectArchetypes_NoMatchWhenOutOfRange()
    {
        var system = new CulturalDNASystem();
        var state = CreateTestWorld();

        // Set all dimensions to 50 (middle) -- should match some but not extreme archetypes
        for (int i = 0; i < CulturalDNASystem.DimensionCount; i++)
            state.CulturalDna[i] = CulturalDNASystem.ToNormalized(50);

        system.DetectArchetypes(state);

        // Silicon Valley requires RiskTolerance >= 70, so it should not be active
        Assert.False(system.ActiveArchetypes.Any(a => a.Id == "silicon_valley"),
            "Silicon Valley should not activate when risk tolerance is only 50");
    }

    [Fact]
    public void DetectArchetypes_AllConditionsMustBeMet()
    {
        var system = new CulturalDNASystem();
        var state = CreateTestWorld();

        // Silicon Valley: needs Risk >= 70, Progressivism >= 65, WorkEthic >= 60
        // Set 2 of 3 conditions
        state.CulturalDna[(int)CulturalDNASystem.Dimension.RiskTolerance] = CulturalDNASystem.ToNormalized(85);
        state.CulturalDna[(int)CulturalDNASystem.Dimension.Progressivism] = CulturalDNASystem.ToNormalized(80);
        state.CulturalDna[(int)CulturalDNASystem.Dimension.WorkEthic] = CulturalDNASystem.ToNormalized(30); // Too low

        system.DetectArchetypes(state);

        Assert.False(system.ActiveArchetypes.Any(a => a.Id == "silicon_valley"),
            "Silicon Valley should not activate when work ethic is below threshold");
    }

    // =========================================================================
    // Gameplay modifier tests
    // =========================================================================

    [Fact]
    public void GetProductivityModifier_ScalesWithWorkEthic()
    {
        var system = new CulturalDNASystem();
        var state = CreateTestWorld();

        // Low work ethic
        state.CulturalDna[(int)CulturalDNASystem.Dimension.WorkEthic] = CulturalDNASystem.ToNormalized(0);
        float lowProd = system.GetProductivityModifier(state);

        // High work ethic
        state.CulturalDna[(int)CulturalDNASystem.Dimension.WorkEthic] = CulturalDNASystem.ToNormalized(100);
        float highProd = system.GetProductivityModifier(state);

        Assert.True(highProd > lowProd,
            "High work ethic should produce higher productivity modifier");
        Assert.InRange(lowProd, 0.6f, 0.8f);
        Assert.InRange(highProd, 1.2f, 1.4f);
    }

    [Fact]
    public void GetTransitAcceptance_ScalesWithCollectivism()
    {
        var system = new CulturalDNASystem();
        var state = CreateTestWorld();

        state.CulturalDna[(int)CulturalDNASystem.Dimension.Collectivism] = CulturalDNASystem.ToNormalized(0);
        float lowTransit = system.GetTransitAcceptance(state);

        state.CulturalDna[(int)CulturalDNASystem.Dimension.Collectivism] = CulturalDNASystem.ToNormalized(100);
        float highTransit = system.GetTransitAcceptance(state);

        Assert.True(highTransit > lowTransit);
        Assert.InRange(lowTransit, 0.2f, 0.4f);
        Assert.InRange(highTransit, 0.9f, 1.1f);
    }

    [Fact]
    public void GetStartupRate_ScalesWithRiskTolerance()
    {
        var system = new CulturalDNASystem();
        var state = CreateTestWorld();

        state.CulturalDna[(int)CulturalDNASystem.Dimension.RiskTolerance] = CulturalDNASystem.ToNormalized(0);
        float low = system.GetStartupRate(state);

        state.CulturalDna[(int)CulturalDNASystem.Dimension.RiskTolerance] = CulturalDNASystem.ToNormalized(100);
        float high = system.GetStartupRate(state);

        Assert.True(high > low);
        Assert.InRange(low, 0.4f, 0.6f);
        Assert.InRange(high, 1.4f, 1.6f);
    }

    [Fact]
    public void GetPollutionTolerance_InverseWithEnvironmentalValues()
    {
        var system = new CulturalDNASystem();
        var state = CreateTestWorld();

        // High env values = LOW pollution tolerance
        state.CulturalDna[(int)CulturalDNASystem.Dimension.EnvironmentalValues] = CulturalDNASystem.ToNormalized(100);
        float highEnv = system.GetPollutionTolerance(state);

        state.CulturalDna[(int)CulturalDNASystem.Dimension.EnvironmentalValues] = CulturalDNASystem.ToNormalized(0);
        float lowEnv = system.GetPollutionTolerance(state);

        Assert.True(lowEnv > highEnv,
            "Low environmental values should produce higher pollution tolerance");
    }

    [Fact]
    public void GetCrimeBaseline_InverseWithSocialTrust()
    {
        var system = new CulturalDNASystem();
        var state = CreateTestWorld();

        // High trust = low crime
        state.CulturalDna[(int)CulturalDNASystem.Dimension.SocialTrust] = CulturalDNASystem.ToNormalized(100);
        float highTrust = system.GetCrimeBaseline(state);

        state.CulturalDna[(int)CulturalDNASystem.Dimension.SocialTrust] = CulturalDNASystem.ToNormalized(0);
        float lowTrust = system.GetCrimeBaseline(state);

        Assert.True(lowTrust > highTrust, "Low trust should produce higher crime baseline");
        Assert.True(highTrust >= 0f, "Crime baseline should never go negative");
    }

    [Fact]
    public void GetTechAdoptionSpeed_ScalesWithProgressivism()
    {
        var system = new CulturalDNASystem();
        var state = CreateTestWorld();

        state.CulturalDna[(int)CulturalDNASystem.Dimension.Progressivism] = CulturalDNASystem.ToNormalized(0);
        float low = system.GetTechAdoptionSpeed(state);

        state.CulturalDna[(int)CulturalDNASystem.Dimension.Progressivism] = CulturalDNASystem.ToNormalized(100);
        float high = system.GetTechAdoptionSpeed(state);

        Assert.True(high > low);
    }

    [Fact]
    public void GetInequalityTolerance_ScalesWithHierarchyAcceptance()
    {
        var system = new CulturalDNASystem();
        var state = CreateTestWorld();

        state.CulturalDna[(int)CulturalDNASystem.Dimension.HierarchyAcceptance] = CulturalDNASystem.ToNormalized(0);
        float low = system.GetInequalityTolerance(state);

        state.CulturalDna[(int)CulturalDNASystem.Dimension.HierarchyAcceptance] = CulturalDNASystem.ToNormalized(100);
        float high = system.GetInequalityTolerance(state);

        Assert.True(high > low);
        Assert.InRange(low, 0.1f, 0.3f);
        Assert.InRange(high, 0.8f, 1.0f);
    }

    [Fact]
    public void GetImmigrationAcceptance_InverseWithCulturalPride()
    {
        var system = new CulturalDNASystem();
        var state = CreateTestWorld();

        // High pride = low immigration acceptance
        state.CulturalDna[(int)CulturalDNASystem.Dimension.CulturalPride] = CulturalDNASystem.ToNormalized(100);
        float highPride = system.GetImmigrationAcceptance(state);

        state.CulturalDna[(int)CulturalDNASystem.Dimension.CulturalPride] = CulturalDNASystem.ToNormalized(0);
        float lowPride = system.GetImmigrationAcceptance(state);

        Assert.True(lowPride > highPride,
            "Low cultural pride should produce higher immigration acceptance");
    }

    // =========================================================================
    // Archetype modifier integration tests
    // =========================================================================

    [Fact]
    public void ArchetypeModifiers_AffectGameplayValues()
    {
        var system = new CulturalDNASystem();
        var state = CreateTestWorld();

        // Get baseline productivity with medium work ethic and no archetypes
        state.CulturalDna[(int)CulturalDNASystem.Dimension.WorkEthic] = CulturalDNASystem.ToNormalized(70);
        float baselineProd = system.GetProductivityModifier(state);

        // Now activate Industrial Powerhouse archetype (WorkEthic 70-100, EnvValues 0-35, Hierarchy 55-100)
        state.CulturalDna[(int)CulturalDNASystem.Dimension.EnvironmentalValues] = CulturalDNASystem.ToNormalized(20);
        state.CulturalDna[(int)CulturalDNASystem.Dimension.HierarchyAcceptance] = CulturalDNASystem.ToNormalized(70);
        system.DetectArchetypes(state);

        float boostedProd = system.GetProductivityModifier(state);

        if (system.ActiveArchetypes.Any(a => a.Id == "industrial_powerhouse"))
        {
            Assert.True(boostedProd > baselineProd,
                "Industrial Powerhouse archetype should boost productivity");
        }
    }

    [Fact]
    public void AllArchetypes_HaveTwentyDefined()
    {
        var system = new CulturalDNASystem();
        Assert.Equal(20, system.AllArchetypes.Count);
    }

    [Fact]
    public void AllArchetypes_HaveUniqueIds()
    {
        var system = new CulturalDNASystem();
        var ids = system.AllArchetypes.Select(a => a.Id).ToList();
        Assert.Equal(ids.Distinct().Count(), ids.Count);
    }

    [Fact]
    public void AllArchetypes_HaveConditionsAndModifiers()
    {
        var system = new CulturalDNASystem();
        foreach (var archetype in system.AllArchetypes)
        {
            Assert.True(archetype.Conditions.Count > 0,
                $"Archetype '{archetype.Id}' must have at least one condition");
            Assert.True(archetype.Modifiers.Count > 0,
                $"Archetype '{archetype.Id}' must have at least one modifier");
            Assert.False(string.IsNullOrEmpty(archetype.Name),
                $"Archetype '{archetype.Id}' must have a name");
            Assert.False(string.IsNullOrEmpty(archetype.Description),
                $"Archetype '{archetype.Id}' must have a description");
        }
    }

    // =========================================================================
    // 12 regional presets completeness
    // =========================================================================

    [Fact]
    public void AllTwelvePresets_ProduceDifferentValues()
    {
        string[] regions = {
            "north_american", "western_european", "japanese_korean",
            "se_asian", "south_asian", "gulf_middle_eastern",
            "sub_saharan_african", "south_american", "scandinavian",
            "eastern_european", "caribbean", "oceanian"
        };

        var presets = regions.Select(r => CulturalDNASystem.GetPreset(r)).ToList();

        // Each preset should be distinct
        for (int i = 0; i < presets.Count; i++)
        {
            for (int j = i + 1; j < presets.Count; j++)
            {
                bool same = true;
                for (int k = 0; k < CulturalDNASystem.DimensionCount; k++)
                {
                    if (global::System.Math.Abs(presets[i][k] - presets[j][k]) > 0.001f)
                    {
                        same = false;
                        break;
                    }
                }
                Assert.False(same,
                    $"Presets for '{regions[i]}' and '{regions[j]}' should be different");
            }
        }
    }
}
