using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Xunit;

namespace Forge.Game.Tests;

public class ResearchSystemTests
{
    private static WorldState CreateTestState() => new(64);

    /// <summary>
    /// Create a research system with a minimal tech tree for testing.
    /// Tech 0: no prereqs, cost 100
    /// Tech 1: no prereqs, cost 80
    /// Tech 2: requires [0, 1], cost 200
    /// Tech 3: requires [2], cost 300, branching decision 0 option A
    /// Tech 4: requires [2], cost 300, branching decision 0 option B
    /// </summary>
    private static ResearchSystem CreateTestSystem()
    {
        var rs = new ResearchSystem();
        rs.LoadDirect(new[]
        {
            new ResearchSystem.TechDefinition
            {
                Id = 0, Name = "Tech Alpha", Category = "Infrastructure", Era = 0,
                Cost = 100, Prerequisites = Array.Empty<int>(),
                Effects = new[] { new ResearchSystem.TechEffect { Parameter = "food", Modifier = 0.2f } },
                UnlocksBuildings = new[] { "Farm" },
            },
            new ResearchSystem.TechDefinition
            {
                Id = 1, Name = "Tech Beta", Category = "Education", Era = 0,
                Cost = 80, Prerequisites = Array.Empty<int>(),
            },
            new ResearchSystem.TechDefinition
            {
                Id = 2, Name = "Tech Gamma", Category = "Infrastructure", Era = 1,
                Cost = 200, Prerequisites = new[] { 0, 1 },
            },
            new ResearchSystem.TechDefinition
            {
                Id = 3, Name = "Tech Delta", Category = "Energy", Era = 2,
                Cost = 300, Prerequisites = new[] { 2 },
                BranchingDecisionId = 0,
            },
            new ResearchSystem.TechDefinition
            {
                Id = 4, Name = "Tech Epsilon", Category = "Energy", Era = 2,
                Cost = 300, Prerequisites = new[] { 2 },
                BranchingDecisionId = 0,
            },
        },
        new[]
        {
            new ResearchSystem.BranchingDecision
            {
                Id = 0, Name = "Energy Choice", OptionA = 3, OptionB = 4,
            },
        });
        return rs;
    }

    // =========================================================================
    // RP generation
    // =========================================================================

    [Fact]
    public void CalculateBaseRP_Libraries()
    {
        var rs = new ResearchSystem();
        rs.LibraryCount = 3;
        Assert.Equal(3f, rs.CalculateBaseRP(), precision: 2);
    }

    [Fact]
    public void CalculateBaseRP_UniversitiesAndProfessors()
    {
        var rs = new ResearchSystem();
        rs.UniversityCount = 2;
        rs.ProfessorCount = 4;
        // 2*5 + 4*1 = 14
        Assert.Equal(14f, rs.CalculateBaseRP(), precision: 2);
    }

    [Fact]
    public void CalculateBaseRP_FullFormula()
    {
        var rs = new ResearchSystem();
        rs.LibraryCount = 2;        // 2*1 = 2
        rs.UniversityCount = 1;     // 1*5 = 5
        rs.ProfessorCount = 3;      // 3*1 = 3
        rs.ResearchLabCount = 1;    // 1*10 = 10
        rs.TechCampusCount = 1;     // 1*20 = 20
        rs.EducatedPopulation = 500; // 500*0.01 = 5
        rs.HeavyIndustryCount = 2;  // 2*2 = 4
        // Total: 2 + 5 + 3 + 10 + 20 + 5 + 4 = 49
        Assert.Equal(49f, rs.CalculateBaseRP(), precision: 2);
    }

    [Fact]
    public void CalculateEffectiveRP_AppliesMultipliers()
    {
        var rs = new ResearchSystem();
        rs.LibraryCount = 10; // base = 10
        rs.FundingMultiplier = 2.0f;
        rs.EducationLevelMultiplier = 1.5f;
        rs.SpecializationMultiplier = 1.0f;
        rs.CollaborationMultiplier = 1.0f;
        // effective = 10 * 2.0 * 1.5 * 1.0 * 1.0 = 30
        Assert.Equal(30f, rs.CalculateEffectiveRP(), precision: 2);
    }

    [Fact]
    public void CalculateEffectiveRP_ZeroBase_ReturnsZero()
    {
        var rs = new ResearchSystem();
        rs.FundingMultiplier = 5f;
        Assert.Equal(0f, rs.CalculateEffectiveRP());
    }

    // =========================================================================
    // Prerequisites
    // =========================================================================

    [Fact]
    public void ArePrerequisitesMet_NoPrereqs_ReturnsTrue()
    {
        var rs = CreateTestSystem();
        var state = CreateTestState();
        Assert.True(rs.ArePrerequisitesMet(0, state));
    }

    [Fact]
    public void ArePrerequisitesMet_MissingPrereq_ReturnsFalse()
    {
        var rs = CreateTestSystem();
        var state = CreateTestState();
        // Tech 2 requires [0, 1], neither unlocked
        Assert.False(rs.ArePrerequisitesMet(2, state));
    }

    [Fact]
    public void ArePrerequisitesMet_PartialPrereq_ReturnsFalse()
    {
        var rs = CreateTestSystem();
        var state = CreateTestState();
        state.UnlockTech(0); // Only one of two prereqs
        Assert.False(rs.ArePrerequisitesMet(2, state));
    }

    [Fact]
    public void ArePrerequisitesMet_AllPrereqs_ReturnsTrue()
    {
        var rs = CreateTestSystem();
        var state = CreateTestState();
        state.UnlockTech(0);
        state.UnlockTech(1);
        Assert.True(rs.ArePrerequisitesMet(2, state));
    }

    [Fact]
    public void CrossCategoryDependency_ElectricTramway()
    {
        // This tests that techs can have prerequisites from different categories
        // In our JSON: Electric Tramway (index 4) needs Horse-Drawn Transit (index 2, transport)
        // + Basic Electrical Grid (index 19, energy)
        var rs = new ResearchSystem();
        string json = File.ReadAllText(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "..", "base", "data", "tech", "technologies.json"));
        rs.LoadFromJson(json);

        var state = CreateTestState();
        // Tech 4 (Electric Tramway) needs 2 and 19
        Assert.False(rs.ArePrerequisitesMet(4, state));
        state.UnlockTech(2);
        Assert.False(rs.ArePrerequisitesMet(4, state));
        state.UnlockTech(19);
        Assert.True(rs.ArePrerequisitesMet(4, state));
    }

    // =========================================================================
    // Research queue
    // =========================================================================

    [Fact]
    public void EnqueueResearch_EmptyQueue_Succeeds()
    {
        var rs = CreateTestSystem();
        var state = CreateTestState();
        bool ok = rs.EnqueueResearch(0, state);
        Assert.True(ok);
        Assert.Equal(0, rs.ResearchQueue[0]);
    }

    [Fact]
    public void EnqueueResearch_AlreadyUnlocked_Fails()
    {
        var rs = CreateTestSystem();
        var state = CreateTestState();
        state.UnlockTech(0);
        Assert.False(rs.EnqueueResearch(0, state));
    }

    [Fact]
    public void EnqueueResearch_MissingPrereqs_Fails()
    {
        var rs = CreateTestSystem();
        var state = CreateTestState();
        Assert.False(rs.EnqueueResearch(2, state));
    }

    [Fact]
    public void EnqueueResearch_QueueFull_Fails()
    {
        var rs = CreateTestSystem();
        var state = CreateTestState();
        // Need enough techs with no prereqs
        rs.LoadDirect(new[]
        {
            new ResearchSystem.TechDefinition { Id = 0, Name = "A", Cost = 100 },
            new ResearchSystem.TechDefinition { Id = 1, Name = "B", Cost = 100 },
            new ResearchSystem.TechDefinition { Id = 2, Name = "C", Cost = 100 },
            new ResearchSystem.TechDefinition { Id = 3, Name = "D", Cost = 100 },
        });

        Assert.True(rs.EnqueueResearch(0, state));
        Assert.True(rs.EnqueueResearch(1, state));
        Assert.True(rs.EnqueueResearch(2, state));
        Assert.False(rs.EnqueueResearch(3, state)); // Queue full (max 3)
    }

    [Fact]
    public void DequeueResearch_RemovesAndCompacts()
    {
        var rs = CreateTestSystem();
        rs.LoadDirect(new[]
        {
            new ResearchSystem.TechDefinition { Id = 0, Name = "A", Cost = 100 },
            new ResearchSystem.TechDefinition { Id = 1, Name = "B", Cost = 100 },
            new ResearchSystem.TechDefinition { Id = 2, Name = "C", Cost = 100 },
        });
        var state = CreateTestState();

        rs.EnqueueResearch(0, state);
        rs.EnqueueResearch(1, state);
        rs.EnqueueResearch(2, state);

        rs.DequeueResearch(1);
        Assert.Equal(0, rs.ResearchQueue[0]);
        Assert.Equal(2, rs.ResearchQueue[1]);
        Assert.Equal(-1, rs.ResearchQueue[2]);
    }

    // =========================================================================
    // Eureka bonuses
    // =========================================================================

    [Fact]
    public void GrantEureka_ReducesCost()
    {
        var rs = CreateTestSystem();
        float baseCost = rs.GetEffectiveCost(0); // 100
        Assert.Equal(100f, baseCost, precision: 1);

        rs.GrantEureka(0, 0.25f);
        float reducedCost = rs.GetEffectiveCost(0);
        Assert.Equal(75f, reducedCost, precision: 1);
    }

    [Fact]
    public void GrantEureka_ClampsToRange()
    {
        var rs = CreateTestSystem();

        rs.GrantEureka(0, 0.10f); // below minimum
        float cost1 = rs.GetEffectiveCost(0);
        Assert.Equal(75f, cost1, precision: 1); // clamped to 0.25

        rs.GrantEureka(0, 0.90f); // above maximum
        float cost2 = rs.GetEffectiveCost(0);
        Assert.Equal(50f, cost2, precision: 1); // clamped to 0.50
    }

    [Fact]
    public void GetEffectiveCost_NoEureka_ReturnsBaseCost()
    {
        var rs = CreateTestSystem();
        Assert.Equal(100f, rs.GetEffectiveCost(0), precision: 1);
    }

    [Fact]
    public void GetEffectiveCost_InvalidTech_ReturnsMaxValue()
    {
        var rs = CreateTestSystem();
        Assert.Equal(float.MaxValue, rs.GetEffectiveCost(999));
    }

    // =========================================================================
    // Era transitions
    // =========================================================================

    [Fact]
    public void CheckEraTransition_InsufficientPopulation_ReturnsNegative()
    {
        var rs = CreateTestSystem();
        var state = CreateTestState();
        state.Era = 0;
        state.Population = 100; // Medieval needs 500
        // Unlock enough techs
        for (int i = 0; i < 10; i++) state.UnlockTech(i);
        Assert.Equal(-1, rs.CheckEraTransition(state));
    }

    [Fact]
    public void CheckEraTransition_InsufficientTechs_ReturnsNegative()
    {
        var rs = CreateTestSystem();
        var state = CreateTestState();
        state.Era = 0;
        state.Population = 1000; // Enough for Medieval
        // Only 2 techs unlocked, need 5
        state.UnlockTech(0);
        state.UnlockTech(1);
        Assert.Equal(-1, rs.CheckEraTransition(state));
    }

    [Fact]
    public void CheckEraTransition_MeetsRequirements_ReturnsNextEra()
    {
        var rs = CreateTestSystem();
        var state = CreateTestState();
        state.Era = 0;
        state.Population = 600;
        // Unlock 5 techs (Medieval requirement)
        for (int i = 0; i < 5; i++) state.UnlockTech(i);
        Assert.Equal(1, rs.CheckEraTransition(state));
    }

    [Fact]
    public void CheckEraTransition_AlreadyMaxEra_ReturnsNegative()
    {
        var rs = CreateTestSystem();
        var state = CreateTestState();
        state.Era = 5; // Future (max)
        state.Population = 1_000_000;
        for (int i = 0; i < 200; i++) state.UnlockTech(i);
        Assert.Equal(-1, rs.CheckEraTransition(state));
    }

    [Fact]
    public void CountUnlockedTechs_CountsCorrectly()
    {
        var state = CreateTestState();
        Assert.Equal(0, ResearchSystem.CountUnlockedTechs(state));

        state.UnlockTech(0);
        state.UnlockTech(5);
        state.UnlockTech(64); // Second ulong
        Assert.Equal(3, ResearchSystem.CountUnlockedTechs(state));
    }

    // =========================================================================
    // Branching decisions
    // =========================================================================

    [Fact]
    public void MakeBranchingChoice_ExcludesOtherOption()
    {
        var rs = CreateTestSystem();
        var state = CreateTestState();

        bool ok = rs.MakeBranchingChoice(0, 3); // Choose Tech 3 (optionA)
        Assert.True(ok);

        Assert.False(rs.IsBranchExcluded(3)); // Chosen, not excluded
        Assert.True(rs.IsBranchExcluded(4));   // Other option, excluded
    }

    [Fact]
    public void MakeBranchingChoice_CannotChooseTwice()
    {
        var rs = CreateTestSystem();
        rs.MakeBranchingChoice(0, 3);
        Assert.False(rs.MakeBranchingChoice(0, 4)); // Already decided
    }

    [Fact]
    public void MakeBranchingChoice_InvalidOption_Fails()
    {
        var rs = CreateTestSystem();
        Assert.False(rs.MakeBranchingChoice(0, 99)); // Not a valid option
    }

    [Fact]
    public void EnqueueResearch_BranchExcluded_Fails()
    {
        var rs = CreateTestSystem();
        var state = CreateTestState();
        state.UnlockTech(0);
        state.UnlockTech(1);
        state.UnlockTech(2);

        rs.MakeBranchingChoice(0, 3); // Choose Tech 3
        Assert.False(rs.EnqueueResearch(4, state)); // Tech 4 excluded
        Assert.True(rs.EnqueueResearch(3, state));  // Tech 3 allowed
    }

    // =========================================================================
    // MonthlyTick integration
    // =========================================================================

    [Fact]
    public void MonthlyTick_CompletesResearch_WhenEnoughRP()
    {
        var rs = CreateTestSystem();
        var state = CreateTestState();

        rs.LibraryCount = 0;
        rs.UniversityCount = 0;
        rs.ResearchLabCount = 10; // 10 * 10 = 100 RP/month, exactly enough for Tech 0 (cost 100)

        rs.EnqueueResearch(0, state);
        rs.MonthlyTick(state, 0);

        Assert.True(state.IsTechUnlocked(0));
        Assert.Equal(-1, rs.ResearchQueue[0]); // Queue cleared
    }

    [Fact]
    public void MonthlyTick_PartialProgress_DoesNotComplete()
    {
        var rs = CreateTestSystem();
        var state = CreateTestState();

        rs.LibraryCount = 10; // 10 RP/month, tech costs 100

        rs.EnqueueResearch(0, state);
        rs.MonthlyTick(state, 0);

        Assert.False(state.IsTechUnlocked(0));
        Assert.Equal(0, rs.ResearchQueue[0]); // Still in queue
    }

    [Fact]
    public void MonthlyTick_OverflowRP_GoesToNextInQueue()
    {
        var rs = CreateTestSystem();
        rs.LoadDirect(new[]
        {
            new ResearchSystem.TechDefinition { Id = 0, Name = "A", Cost = 50 },
            new ResearchSystem.TechDefinition { Id = 1, Name = "B", Cost = 50 },
        });
        var state = CreateTestState();

        rs.ResearchLabCount = 10; // 100 RP/month, two 50-cost techs
        rs.EnqueueResearch(0, state);
        rs.EnqueueResearch(1, state);

        rs.MonthlyTick(state, 0);

        Assert.True(state.IsTechUnlocked(0));
        Assert.True(state.IsTechUnlocked(1));
    }

    [Fact]
    public void MonthlyTick_AdvancesEra()
    {
        var rs = CreateTestSystem();
        var state = CreateTestState();
        state.Era = 0;
        state.Population = 600;
        // Unlock 5 techs to meet Medieval threshold
        for (int i = 0; i < 5; i++) state.UnlockTech(i);

        rs.MonthlyTick(state, 0);

        Assert.Equal(1, state.Era);
    }

    [Fact]
    public void MonthlyTick_UpdatesWorldStateResearchRate()
    {
        var rs = CreateTestSystem();
        var state = CreateTestState();
        rs.ResearchLabCount = 5; // 50 RP base
        rs.FundingMultiplier = 1.5f; // effective = 75

        rs.MonthlyTick(state, 0);

        Assert.Equal(75f, state.ResearchRate, precision: 1);
    }

    // =========================================================================
    // JSON loading
    // =========================================================================

    [Fact]
    public void LoadFromJson_ParsesTechsCorrectly()
    {
        var rs = new ResearchSystem();
        string json = File.ReadAllText(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "..", "base", "data", "tech", "technologies.json"));
        rs.LoadFromJson(json);

        Assert.True(rs.TechCount >= 30);
        Assert.NotNull(rs.Technologies[0]);
        Assert.Equal("Cobblestone Paving", rs.Technologies[0]!.Name);
        Assert.Equal(3, rs.BranchingDecisions.Length);
    }

    [Fact]
    public void GetAvailableTechs_ReturnsOnlyResearchable()
    {
        var rs = CreateTestSystem();
        var state = CreateTestState();

        var available = rs.GetAvailableTechs(state);
        // Only techs 0 and 1 have no prereqs
        Assert.Contains(0, available);
        Assert.Contains(1, available);
        Assert.DoesNotContain(2, available); // Needs 0 and 1
    }

    [Fact]
    public void GetTechsByCategory_FiltersCorrectly()
    {
        var rs = CreateTestSystem();
        var infra = rs.GetTechsByCategory("Infrastructure");
        Assert.Contains(0, infra);
        Assert.Contains(2, infra);
        Assert.DoesNotContain(1, infra); // Education
    }
}
