using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Xunit;

namespace Forge.Game.Tests;

public class PoliticsSystemTests
{
    private static WorldState CreateTestState() => new(64);

    // =========================================================================
    // Approval calculation
    // =========================================================================

    [Fact]
    public void CalculateApproval_AllMax_Returns90()
    {
        var ps = new PoliticsSystem();
        // happiness=1, economy=1, services=1, safety=1, decisions=1, scandal=0
        // Positive weights sum to 0.90 (0.30+0.20+0.15+0.10+0.15), scandal=0 subtracts nothing
        float approval = ps.CalculateApproval(1f, 1f, 1f, 1f, 1f, 0f);
        Assert.Equal(90f, approval, precision: 1);
    }

    [Fact]
    public void CalculateApproval_AllZero_ReturnsZero()
    {
        var ps = new PoliticsSystem();
        float approval = ps.CalculateApproval(0f, 0f, 0f, 0f, 0f, 0f);
        Assert.Equal(0f, approval, precision: 1);
    }

    [Fact]
    public void CalculateApproval_OnlyHappiness_Returns30()
    {
        var ps = new PoliticsSystem();
        // happiness=1, rest=0, scandal=0
        float approval = ps.CalculateApproval(1f, 0f, 0f, 0f, 0f, 0f);
        Assert.Equal(30f, approval, precision: 1);
    }

    [Fact]
    public void CalculateApproval_HighScandal_ReducesScore()
    {
        var ps = new PoliticsSystem();
        float withoutScandal = ps.CalculateApproval(0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0f);
        float withScandal = ps.CalculateApproval(0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 1f);
        Assert.True(withScandal < withoutScandal);
    }

    [Fact]
    public void CalculateApproval_WeightsSum()
    {
        var ps = new PoliticsSystem();
        // Positive weights: 0.30+0.20+0.15+0.10+0.15 = 0.90
        // With scandal=1: 0.90 - 0.10 = 0.80 → 80
        float withMaxScandal = ps.CalculateApproval(1f, 1f, 1f, 1f, 1f, 1f);
        Assert.Equal(80f, withMaxScandal, precision: 1);
    }

    [Fact]
    public void CalculateApproval_ClampsToZero()
    {
        var ps = new PoliticsSystem();
        // All zero except very high scandal
        float approval = ps.CalculateApproval(0f, 0f, 0f, 0f, 0f, 1f);
        // 0 - 1*0.10 = -0.10 → -10 clamped to 0
        Assert.Equal(0f, approval, precision: 1);
    }

    // =========================================================================
    // Faction clout
    // =========================================================================

    [Fact]
    public void FactionClout_BasicCalculation()
    {
        float clout = PoliticsSystem.ComputeFactionClout(100, 5.0f, 0);
        // 100 * 5.0 * (1 + 0) = 500
        Assert.Equal(500f, clout, precision: 1);
    }

    [Fact]
    public void FactionClout_WithRelevantLaws()
    {
        float clout = PoliticsSystem.ComputeFactionClout(200, 3.0f, 5);
        // 200 * 3.0 * (1 + 5*0.1) = 600 * 1.5 = 900
        Assert.Equal(900f, clout, precision: 1);
    }

    [Fact]
    public void FactionClout_ZeroMembers_ReturnsZero()
    {
        float clout = PoliticsSystem.ComputeFactionClout(0, 10f, 10);
        Assert.Equal(0f, clout);
    }

    [Fact]
    public void FactionClout_MatchesStructProperty()
    {
        var ps = new PoliticsSystem();
        var faction = ps.Factions[(int)PoliticsSystem.FactionId.BusinessOwners];
        float expected = PoliticsSystem.ComputeFactionClout(
            faction.Members, faction.Wealth, faction.RelevantLaws);
        Assert.Equal(expected, faction.Clout, precision: 1);
    }

    // =========================================================================
    // Election resolution
    // =========================================================================

    [Fact]
    public void ResolveElection_SetsNextElectionYear()
    {
        var ps = new PoliticsSystem();
        var state = CreateTestState();
        state.Year = 2028;
        state.Month = 11;

        ps.ResolveElection(state);

        Assert.Equal(2032, state.NextElectionYear);
    }

    [Fact]
    public void ResolveElection_DistributesAll9Seats()
    {
        var ps = new PoliticsSystem();
        var state = CreateTestState();

        float[] shares = ps.ResolveElection(state);

        // Verify all 9 seats are assigned
        int totalSeats = 0;
        for (int f = 0; f < PoliticsSystem.FactionCount; f++)
            totalSeats += ps.GetFactionSeatCount(f);
        Assert.Equal(PoliticsSystem.CouncilSeatCount, totalSeats);
    }

    [Fact]
    public void ResolveElection_VoteSharesNormalizeToOne()
    {
        var ps = new PoliticsSystem();
        var state = CreateTestState();

        float[] shares = ps.ResolveElection(state);

        float total = 0f;
        for (int i = 0; i < shares.Length; i++)
            total += shares[i];
        Assert.InRange(total, 0.99f, 1.01f);
    }

    [Fact]
    public void CalculateFactionVote_PositiveResult()
    {
        var ps = new PoliticsSystem();
        var faction = ps.Factions[(int)PoliticsSystem.FactionId.Workers];
        var cityDna = new float[8]; // all zeros = neutral
        float vote = PoliticsSystem.CalculateFactionVote(in faction, cityDna);
        Assert.True(vote > 0f);
    }

    [Fact]
    public void CulturalAlignmentScore_NeutralDna_ReturnsNearHalf()
    {
        // All zeros in city DNA => dot product = 0 => (0+8)/16 = 0.5
        var cityDna = new float[8];
        var factionAlign = new float[] { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f };
        float score = PoliticsSystem.ComputeCulturalAlignmentScore(factionAlign, cityDna);
        Assert.Equal(0.5f, score, precision: 2);
    }

    [Fact]
    public void CulturalAlignmentScore_PerfectAlignment()
    {
        // Both positive and matching => high dot product
        var cityDna = new float[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
        var factionAlign = new float[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
        float score = PoliticsSystem.ComputeCulturalAlignmentScore(factionAlign, cityDna);
        // dot = 8, (8+8)/16 = 1.0
        Assert.Equal(1f, score, precision: 2);
    }

    // =========================================================================
    // Law voting
    // =========================================================================

    [Fact]
    public void ProposeLaw_ReturnsValidIndex()
    {
        var ps = new PoliticsSystem();
        int idx = ps.ProposeLaw("Test Law", "A test", 0, 2024, 1,
            new[] { new PoliticsSystem.LawEffect { ParameterName = "tax_rate", Modifier = 0.05f } });
        Assert.Equal(0, idx);
        Assert.Equal(PoliticsSystem.LawStatus.Proposed, ps.Laws[idx].Status);
    }

    [Fact]
    public void VoteOnLaw_MajorityPasses()
    {
        var ps = new PoliticsSystem();
        // Set all 9 council seats to faction 0 (BusinessOwners)
        for (int i = 0; i < PoliticsSystem.CouncilSeatCount; i++)
            ps.CouncilSeats[i] = 0;

        int idx = ps.ProposeLaw("Pro-Business", "Helps business", 0, 2024, 1,
            new[] { new PoliticsSystem.LawEffect { ParameterName = "business_tax", Modifier = -0.05f } });

        // Faction 0 votes yes, rest don't matter (no seats)
        float[] prefs = { 1f, -1f, -1f, -1f, -1f, -1f };
        bool passed = ps.VoteOnLaw(idx, prefs);

        Assert.True(passed);
        Assert.Equal(PoliticsSystem.LawStatus.Enacted, ps.Laws[idx].Status);
        Assert.Equal(9, ps.Laws[idx].VotesFor);
    }

    [Fact]
    public void VoteOnLaw_MinorityFails()
    {
        var ps = new PoliticsSystem();
        // Distribute seats: 4 for faction 0, 5 for faction 1
        for (int i = 0; i < 4; i++) ps.CouncilSeats[i] = 0;
        for (int i = 4; i < 9; i++) ps.CouncilSeats[i] = 1;

        int idx = ps.ProposeLaw("Contested", "A test", 0, 2024, 1, Array.Empty<PoliticsSystem.LawEffect>());

        // Faction 0 votes for, faction 1 votes against
        float[] prefs = { 1f, -1f, 0f, 0f, 0f, 0f };
        bool passed = ps.VoteOnLaw(idx, prefs);

        Assert.False(passed);
        Assert.Equal(PoliticsSystem.LawStatus.Rejected, ps.Laws[idx].Status);
        Assert.Equal(4, ps.Laws[idx].VotesFor);
        Assert.Equal(5, ps.Laws[idx].VotesAgainst);
    }

    [Fact]
    public void VoteOnLaw_ExactMajority_Passes()
    {
        var ps = new PoliticsSystem();
        // 5 seats for faction 0, 4 for faction 1
        for (int i = 0; i < 5; i++) ps.CouncilSeats[i] = 0;
        for (int i = 5; i < 9; i++) ps.CouncilSeats[i] = 1;

        int idx = ps.ProposeLaw("Close Vote", "A test", 0, 2024, 1, Array.Empty<PoliticsSystem.LawEffect>());

        float[] prefs = { 1f, -1f, 0f, 0f, 0f, 0f };
        bool passed = ps.VoteOnLaw(idx, prefs);

        Assert.True(passed);
        Assert.Equal(5, ps.Laws[idx].VotesFor);
        Assert.Equal(4, ps.Laws[idx].VotesAgainst);
    }

    [Fact]
    public void VoteOnLaw_EnactedIncreasesRelevantLaws()
    {
        var ps = new PoliticsSystem();
        for (int i = 0; i < PoliticsSystem.CouncilSeatCount; i++)
            ps.CouncilSeats[i] = 0;

        int before = ps.Factions[0].RelevantLaws;
        int idx = ps.ProposeLaw("Business Law", "Test", 0, 2024, 1, Array.Empty<PoliticsSystem.LawEffect>());
        float[] prefs = { 1f, 0f, 0f, 0f, 0f, 0f };
        ps.VoteOnLaw(idx, prefs);

        Assert.Equal(before + 1, ps.Factions[0].RelevantLaws);
    }

    [Fact]
    public void RepealLaw_DecreasesRelevantLaws()
    {
        var ps = new PoliticsSystem();
        for (int i = 0; i < PoliticsSystem.CouncilSeatCount; i++)
            ps.CouncilSeats[i] = 0;

        int idx = ps.ProposeLaw("Temp Law", "Test", 0, 2024, 1, Array.Empty<PoliticsSystem.LawEffect>());
        float[] prefs = { 1f, 0f, 0f, 0f, 0f, 0f };
        ps.VoteOnLaw(idx, prefs);

        int before = ps.Factions[0].RelevantLaws;
        ps.RepealLaw(idx);
        Assert.Equal(before - 1, ps.Factions[0].RelevantLaws);
        Assert.Equal(PoliticsSystem.LawStatus.Repealed, ps.Laws[idx].Status);
    }

    [Fact]
    public void GetActiveLawEffects_AggregatesMultipleLaws()
    {
        var ps = new PoliticsSystem();
        for (int i = 0; i < PoliticsSystem.CouncilSeatCount; i++)
            ps.CouncilSeats[i] = 0;
        float[] prefs = { 1f, 0f, 0f, 0f, 0f, 0f };

        int law1 = ps.ProposeLaw("Law A", "Test", 0, 2024, 1,
            new[] { new PoliticsSystem.LawEffect { ParameterName = "tax_rate", Modifier = 0.05f } });
        ps.VoteOnLaw(law1, prefs);

        int law2 = ps.ProposeLaw("Law B", "Test", 0, 2024, 2,
            new[] { new PoliticsSystem.LawEffect { ParameterName = "tax_rate", Modifier = 0.03f } });
        ps.VoteOnLaw(law2, prefs);

        var effects = ps.GetActiveLawEffects();
        Assert.True(effects.ContainsKey("tax_rate"));
        Assert.Equal(0.08f, effects["tax_rate"], precision: 3);
    }

    // =========================================================================
    // Decision impact tracking
    // =========================================================================

    [Fact]
    public void RecordDecision_TracksImpact()
    {
        var ps = new PoliticsSystem();
        ps.RecordDecision("Built park", 0.5f);
        Assert.Equal(1, ps.DecisionImpactCount);
    }

    [Fact]
    public void ComputeDecisionScore_NoDecisions_ReturnsNeutral()
    {
        var ps = new PoliticsSystem();
        float score = ps.ComputeDecisionScore();
        Assert.Equal(0.5f, score, precision: 2);
    }

    [Fact]
    public void ComputeDecisionScore_PositiveDecision_AboveNeutral()
    {
        var ps = new PoliticsSystem();
        ps.RecordDecision("Good decision", 0.8f);
        float score = ps.ComputeDecisionScore();
        Assert.True(score > 0.5f);
    }

    [Fact]
    public void ComputeDecisionScore_NegativeDecision_BelowNeutral()
    {
        var ps = new PoliticsSystem();
        ps.RecordDecision("Bad decision", -0.8f);
        float score = ps.ComputeDecisionScore();
        Assert.True(score < 0.5f);
    }

    // =========================================================================
    // Protest escalation
    // =========================================================================

    [Fact]
    public void UpdateProtests_HighApproval_DeEscalates()
    {
        var ps = new PoliticsSystem();
        // Manually set to Rally phase
        typeof(PoliticsSystem).GetProperty(nameof(PoliticsSystem.CurrentProtestPhase))!
            .SetValue(ps, PoliticsSystem.ProtestPhase.Rally);

        ps.UpdateProtests(70f); // approval > 60 de-escalates
        Assert.Equal(PoliticsSystem.ProtestPhase.Petition, ps.CurrentProtestPhase);
    }

    [Fact]
    public void UpdateProtests_LowApproval_Escalates()
    {
        var ps = new PoliticsSystem();
        // Start at Complaint, simulate enough days at low approval
        typeof(PoliticsSystem).GetProperty(nameof(PoliticsSystem.CurrentProtestPhase))!
            .SetValue(ps, PoliticsSystem.ProtestPhase.Complaint);

        for (int d = 0; d <= PoliticsSystem.MaxProtestEscalationDays; d++)
            ps.UpdateProtests(30f); // approval < 40 escalates

        Assert.Equal(PoliticsSystem.ProtestPhase.Petition, ps.CurrentProtestPhase);
    }

    [Fact]
    public void UpdateProtests_NoProtests_StaysNone_WhenApprovalHigh()
    {
        var ps = new PoliticsSystem();
        ps.UpdateProtests(80f);
        Assert.Equal(PoliticsSystem.ProtestPhase.None, ps.CurrentProtestPhase);
    }

    // =========================================================================
    // Corruption
    // =========================================================================

    [Fact]
    public void CalculateCorruption_DefaultValues()
    {
        var ps = new PoliticsSystem();
        // base=10, lobby=0, transparency=0.5 => (1-0.5)*10=5, media=0.5 => 0.5*15=7.5
        float corruption = ps.CalculateCorruption(0f, 0.5f, 0.5f);
        Assert.Equal(7.5f, corruption, precision: 1);
    }

    [Fact]
    public void CalculateCorruption_HighLobby_Increases()
    {
        var ps = new PoliticsSystem();
        float low = ps.CalculateCorruption(0f, 0.5f, 0.5f);
        float high = ps.CalculateCorruption(1f, 0.5f, 0.5f);
        Assert.True(high > low);
    }

    [Fact]
    public void CalculateCorruption_HighMediaFreedom_Decreases()
    {
        var ps = new PoliticsSystem();
        float lowMedia = ps.CalculateCorruption(0f, 0.5f, 0f);
        float highMedia = ps.CalculateCorruption(0f, 0.5f, 1f);
        Assert.True(highMedia < lowMedia);
    }

    [Fact]
    public void CalculateCorruption_ClampsToZero()
    {
        var ps = new PoliticsSystem();
        // max media freedom => 10 + 0 + 5 - 15 = 0
        float corruption = ps.CalculateCorruption(0f, 0.5f, 1f);
        Assert.Equal(0f, corruption, precision: 1);
    }

    // =========================================================================
    // Monthly tick integration
    // =========================================================================

    [Fact]
    public void MonthlyTick_UpdatesApprovalOnWorldState()
    {
        var ps = new PoliticsSystem();
        var state = CreateTestState();
        state.Happiness = 0.7f;
        ps.EconomyScore = 0.6f;
        ps.ServiceScore = 0.5f;
        ps.SafetyScore = 0.8f;

        ps.MonthlyTick(state, 0);

        Assert.True(state.ApprovalRating > 0f);
        Assert.True(state.ApprovalRating <= 1f);
    }

    [Fact]
    public void MonthlyTick_TriggersElection_WhenDue()
    {
        var ps = new PoliticsSystem();
        var state = CreateTestState();
        state.Year = 2028;
        state.Month = 11;
        state.NextElectionYear = 2028;

        ps.MonthlyTick(state, 0);

        Assert.Equal(2032, state.NextElectionYear);
    }

    [Fact]
    public void ApprovalConsequences_HighApproval_BonusFunding()
    {
        var ps = new PoliticsSystem();
        var state = CreateTestState();
        state.CityFunds = 100_000;

        ps.ApplyApprovalConsequences(state, 85f);

        Assert.Equal(105_000, state.CityFunds);
    }

    [Fact]
    public void ApprovalConsequences_VeryLowApproval_ForcesElection()
    {
        var ps = new PoliticsSystem();
        var state = CreateTestState();
        state.Year = 2025;
        state.Month = 6;
        state.NextElectionYear = 2028;

        ps.ApplyApprovalConsequences(state, 15f);

        Assert.True(state.NextElectionYear <= state.Year);
    }

    // =========================================================================
    // DailyTick
    // =========================================================================

    [Fact]
    public void DailyTick_DecaysDecisionImpacts()
    {
        var ps = new PoliticsSystem();
        var state = CreateTestState();
        state.Happiness = 0.5f;

        ps.RecordDecision("Test", 0.5f);
        float strengthBefore = ps.DecisionImpacts[0].RemainingStrength;

        ps.DailyTick(state, 0);

        Assert.True(ps.DecisionImpacts[0].RemainingStrength < strengthBefore);
    }
}
