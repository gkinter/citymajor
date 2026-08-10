using Forge.Engine.Simulation;

namespace Forge.Game.Simulation;

/// <summary>
/// Full political simulation: mayor approval, factions, elections, laws, protests, corruption.
///
/// Tick cadence:
/// - DailyTick: decay decision impacts, escalate/de-escalate protests
/// - MonthlyTick: recalculate approval, faction clout, corruption index, check consequences
/// - ElectionTick: called by MonthlyTick when Year == NextElectionYear and Month == 11
/// </summary>
public sealed class PoliticsSystem
{
    // =========================================================================
    // Enums
    // =========================================================================

    public enum FactionId : byte
    {
        BusinessOwners = 0,
        Workers = 1,
        PropertyOwners = 2,
        Intelligentsia = 3,
        Religious = 4,
        Newcomers = 5,
    }

    public enum ProtestPhase : byte
    {
        None = 0,
        Complaint = 1,
        Petition = 2,
        Rally = 3,
        Protest = 4,
        Riot = 5,
    }

    public enum LawStatus : byte
    {
        Proposed = 0,
        Enacted = 1,
        Rejected = 2,
        Repealed = 3,
    }

    // =========================================================================
    // Constants
    // =========================================================================

    public const int FactionCount = 6;
    public const int CouncilSeatCount = 9;
    public const int CouncilMajority = 5;
    public const int ElectionIntervalYears = 4;
    public const int MaxLaws = 128;
    public const float DecisionDecayPerDay = 1f / 180f; // 6 game months = ~180 days
    public const int MaxDecisionImpacts = 64;
    public const int MaxProtestEscalationDays = 30;

    // Approval weights
    private const float W_Happiness = 0.30f;
    private const float W_Economy = 0.20f;
    private const float W_Services = 0.15f;
    private const float W_Safety = 0.10f;
    private const float W_Decisions = 0.15f;
    private const float W_Scandal = 0.10f;

    // =========================================================================
    // State
    // =========================================================================

    public Faction[] Factions { get; } = new Faction[FactionCount];
    public byte[] CouncilSeats { get; } = new byte[CouncilSeatCount]; // faction id per seat
    public Law[] Laws { get; } = new Law[MaxLaws];
    public int LawCount { get; private set; }

    public DecisionImpact[] DecisionImpacts { get; } = new DecisionImpact[MaxDecisionImpacts];
    public int DecisionImpactCount { get; private set; }

    public ProtestPhase CurrentProtestPhase { get; private set; } = ProtestPhase.None;
    public int ProtestDaysAtCurrentPhase { get; private set; }

    public float CorruptionIndex { get; private set; }
    public float LobbyAcceptance { get; set; }
    public float TransparencyLevel { get; set; } = 0.5f;
    public float MediaFreedom { get; set; } = 0.5f;

    /// <summary>Running score from player decisions, decays over time.</summary>
    public float DecisionScore { get; private set; }

    /// <summary>Scandal magnitude (0-1). External events or corruption can raise this.</summary>
    public float ScandalLevel { get; set; }

    // Inputs from other systems (set externally before MonthlyTick)
    public float EconomyScore { get; set; } = 0.5f;
    public float ServiceScore { get; set; } = 0.5f;
    public float SafetyScore { get; set; } = 0.5f;

    // =========================================================================
    // Data types
    // =========================================================================

    public struct Faction
    {
        public FactionId Id;
        public string Name;
        public int Members;
        public float Wealth;        // average wealth per member (0-10)
        public int RelevantLaws;     // number of enacted laws this faction cares about
        public float BaseSupport;    // 0-1, base electoral support
        public float Satisfaction;   // 0-1, how happy with current government
        public float[] CulturalAlignment; // 8 floats matching CulturalDna dimensions (-1 to +1)
        public float CampaignEffect; // election modifier from campaign spending

        /// <summary>Clout = members * wealth * (1 + relevantLaws * 0.1)</summary>
        public readonly float Clout => Members * Wealth * (1f + RelevantLaws * 0.1f);
    }

    public struct Law
    {
        public int Id;
        public string Name;
        public string Description;
        public LawStatus Status;
        public int ProposedByFaction; // -1 = mayor
        public int VotesFor;
        public int VotesAgainst;
        public int ProposedYear;
        public int ProposedMonth;

        /// <summary>Which simulation parameters this law modifies, as key-value pairs.</summary>
        public LawEffect[] Effects;
    }

    public struct LawEffect
    {
        public string ParameterName;
        public float Modifier; // additive modifier to the parameter
    }

    public struct DecisionImpact
    {
        public string Description;
        public float Magnitude; // positive = good decision, negative = bad
        public float RemainingStrength; // starts at 1.0, decays to 0
    }

    // =========================================================================
    // Initialization
    // =========================================================================

    public PoliticsSystem()
    {
        InitializeFactions();
        InitializeCouncil();
    }

    private void InitializeFactions()
    {
        Factions[0] = new Faction
        {
            Id = FactionId.BusinessOwners, Name = "Business Owners",
            Members = 200, Wealth = 7.0f, RelevantLaws = 0, BaseSupport = 0.18f,
            Satisfaction = 0.6f,
            CulturalAlignment = new float[] { 0.3f, 0.7f, -0.3f, 0.4f, 0.5f, -0.2f, 0.2f, 0.0f },
            CampaignEffect = 1.0f,
        };
        Factions[1] = new Faction
        {
            Id = FactionId.Workers, Name = "Workers",
            Members = 500, Wealth = 3.0f, RelevantLaws = 0, BaseSupport = 0.25f,
            Satisfaction = 0.5f,
            CulturalAlignment = new float[] { -0.2f, -0.5f, 0.0f, 0.1f, -0.3f, 0.1f, 0.0f, 0.3f },
            CampaignEffect = 1.0f,
        };
        Factions[2] = new Faction
        {
            Id = FactionId.PropertyOwners, Name = "Property Owners",
            Members = 300, Wealth = 6.0f, RelevantLaws = 0, BaseSupport = 0.15f,
            Satisfaction = 0.55f,
            CulturalAlignment = new float[] { -0.1f, 0.3f, -0.1f, -0.2f, 0.3f, -0.3f, 0.1f, 0.0f },
            CampaignEffect = 1.0f,
        };
        Factions[3] = new Faction
        {
            Id = FactionId.Intelligentsia, Name = "Intelligentsia",
            Members = 150, Wealth = 5.0f, RelevantLaws = 0, BaseSupport = 0.12f,
            Satisfaction = 0.6f,
            CulturalAlignment = new float[] { 0.8f, 0.2f, 0.5f, 0.6f, 0.2f, 0.3f, 0.7f, 0.5f },
            CampaignEffect = 1.0f,
        };
        Factions[4] = new Faction
        {
            Id = FactionId.Religious, Name = "Religious",
            Members = 250, Wealth = 4.0f, RelevantLaws = 0, BaseSupport = 0.15f,
            Satisfaction = 0.5f,
            CulturalAlignment = new float[] { -0.7f, -0.3f, 0.0f, -0.5f, -0.1f, -0.4f, -0.9f, 0.2f },
            CampaignEffect = 1.0f,
        };
        Factions[5] = new Faction
        {
            Id = FactionId.Newcomers, Name = "Newcomers",
            Members = 100, Wealth = 2.0f, RelevantLaws = 0, BaseSupport = 0.10f,
            Satisfaction = 0.45f,
            CulturalAlignment = new float[] { 0.1f, 0.0f, 0.1f, 0.8f, 0.0f, 0.2f, 0.0f, 0.4f },
            CampaignEffect = 1.0f,
        };
    }

    private void InitializeCouncil()
    {
        // Default council: distribute roughly by base support
        CouncilSeats[0] = (byte)FactionId.BusinessOwners;
        CouncilSeats[1] = (byte)FactionId.BusinessOwners;
        CouncilSeats[2] = (byte)FactionId.Workers;
        CouncilSeats[3] = (byte)FactionId.Workers;
        CouncilSeats[4] = (byte)FactionId.Workers;
        CouncilSeats[5] = (byte)FactionId.PropertyOwners;
        CouncilSeats[6] = (byte)FactionId.Intelligentsia;
        CouncilSeats[7] = (byte)FactionId.Religious;
        CouncilSeats[8] = (byte)FactionId.Newcomers;
    }

    // =========================================================================
    // Approval calculation
    // =========================================================================

    /// <summary>
    /// Calculate mayor approval rating (0-100 scale).
    /// Formula: happiness*0.30 + economy*0.20 + services*0.15 + safety*0.10
    ///        + decisions*0.15 - scandal*0.10
    /// All input scores are expected on 0-1 scale; output is 0-100.
    /// </summary>
    public float CalculateApproval(float happiness, float economy, float services,
                                    float safety, float decisions, float scandal)
    {
        float raw = happiness * W_Happiness
                  + economy * W_Economy
                  + services * W_Services
                  + safety * W_Safety
                  + decisions * W_Decisions
                  - scandal * W_Scandal;

        return Math.Clamp(raw * 100f, 0f, 100f);
    }

    // =========================================================================
    // Faction clout
    // =========================================================================

    /// <summary>
    /// Compute clout for a faction. Clout = members * wealth * (1 + relevantLaws * 0.1).
    /// </summary>
    public static float ComputeFactionClout(int members, float wealth, int relevantLaws)
    {
        return members * wealth * (1f + relevantLaws * 0.1f);
    }

    // =========================================================================
    // Election
    // =========================================================================

    /// <summary>
    /// Calculate a faction's vote share for an election.
    /// faction_vote = base_support * cultural_alignment_score * satisfaction * campaign_effect
    /// Cultural alignment score = dot product of faction alignment with city DNA, normalized to 0-1.
    /// </summary>
    public static float CalculateFactionVote(in Faction faction, float[] cityDna)
    {
        float alignment = ComputeCulturalAlignmentScore(faction.CulturalAlignment, cityDna);
        return faction.BaseSupport * alignment * faction.Satisfaction * faction.CampaignEffect;
    }

    /// <summary>
    /// Dot product of faction cultural alignment with city cultural DNA,
    /// normalized from [-8,+8] range to [0,1].
    /// </summary>
    public static float ComputeCulturalAlignmentScore(float[] factionAlignment, float[] cityDna)
    {
        if (factionAlignment == null || cityDna == null) return 0.5f;
        int len = Math.Min(factionAlignment.Length, cityDna.Length);
        float dot = 0f;
        for (int i = 0; i < len; i++)
        {
            dot += factionAlignment[i] * cityDna[i];
        }
        // Normalize from [-8,8] to [0,1]
        return Math.Clamp((dot + 8f) / 16f, 0f, 1f);
    }

    /// <summary>
    /// Resolve an election: compute vote shares for all factions, distribute 9 council seats
    /// proportionally (largest remainder method), and set next election year.
    /// Returns the vote shares for each faction (6 elements).
    /// </summary>
    public float[] ResolveElection(WorldState state)
    {
        float[] voteShares = new float[FactionCount];
        float totalVotes = 0f;

        for (int i = 0; i < FactionCount; i++)
        {
            voteShares[i] = CalculateFactionVote(in Factions[i], state.CulturalDna);
            totalVotes += voteShares[i];
        }

        // Normalize to proportions
        if (totalVotes > 0.0001f)
        {
            for (int i = 0; i < FactionCount; i++)
                voteShares[i] /= totalVotes;
        }
        else
        {
            // Fallback: equal distribution
            for (int i = 0; i < FactionCount; i++)
                voteShares[i] = 1f / FactionCount;
        }

        // Distribute seats using largest remainder method
        DistributeSeats(voteShares);

        // Set next election
        state.NextElectionYear = state.Year + ElectionIntervalYears;

        // Copy council seats to WorldState
        Array.Copy(CouncilSeats, state.CouncilSeats, CouncilSeatCount);

        return voteShares;
    }

    /// <summary>
    /// Largest remainder method (Hamilton's method) for distributing seats.
    /// </summary>
    internal void DistributeSeats(float[] voteShares)
    {
        float[] quotas = new float[FactionCount];
        int[] seats = new int[FactionCount];
        int allocated = 0;

        for (int i = 0; i < FactionCount; i++)
        {
            quotas[i] = voteShares[i] * CouncilSeatCount;
            seats[i] = (int)quotas[i]; // integer part
            allocated += seats[i];
        }

        // Distribute remaining seats by largest fractional remainder
        int remaining = CouncilSeatCount - allocated;
        while (remaining > 0)
        {
            float bestRemainder = -1f;
            int bestIdx = 0;
            for (int i = 0; i < FactionCount; i++)
            {
                float frac = quotas[i] - seats[i];
                if (frac > bestRemainder)
                {
                    bestRemainder = frac;
                    bestIdx = i;
                }
            }
            seats[bestIdx]++;
            quotas[bestIdx] = seats[bestIdx]; // zero out remainder for this faction
            remaining--;
        }

        // Fill council seats array
        int seatIdx = 0;
        for (int faction = 0; faction < FactionCount; faction++)
        {
            for (int s = 0; s < seats[faction]; s++)
            {
                if (seatIdx < CouncilSeatCount)
                    CouncilSeats[seatIdx++] = (byte)faction;
            }
        }
    }

    // =========================================================================
    // Law system
    // =========================================================================

    /// <summary>
    /// Propose a new law. Returns the law index, or -1 if at capacity.
    /// </summary>
    public int ProposeLaw(string name, string description, int proposedByFaction,
                           int year, int month, LawEffect[] effects)
    {
        if (LawCount >= MaxLaws) return -1;

        int idx = LawCount;
        Laws[idx] = new Law
        {
            Id = idx,
            Name = name,
            Description = description,
            Status = LawStatus.Proposed,
            ProposedByFaction = proposedByFaction,
            VotesFor = 0,
            VotesAgainst = 0,
            ProposedYear = year,
            ProposedMonth = month,
            Effects = effects,
        };
        LawCount++;
        return idx;
    }

    /// <summary>
    /// Council votes on a proposed law. Each seat casts a vote based on faction preference.
    /// Returns true if the law passes (5/9 majority).
    /// The factionVotePreference array (6 elements) has each faction's preference:
    /// positive = for, negative = against, 0 = abstain.
    /// </summary>
    public bool VoteOnLaw(int lawIndex, float[] factionVotePreference)
    {
        if (lawIndex < 0 || lawIndex >= LawCount) return false;
        if (Laws[lawIndex].Status != LawStatus.Proposed) return false;

        int votesFor = 0;
        int votesAgainst = 0;

        for (int seat = 0; seat < CouncilSeatCount; seat++)
        {
            byte factionId = CouncilSeats[seat];
            if (factionId >= FactionCount) continue;

            float pref = factionVotePreference[factionId];
            if (pref > 0f)
                votesFor++;
            else if (pref < 0f)
                votesAgainst++;
            // pref == 0 => abstain
        }

        Laws[lawIndex].VotesFor = votesFor;
        Laws[lawIndex].VotesAgainst = votesAgainst;

        if (votesFor >= CouncilMajority)
        {
            Laws[lawIndex].Status = LawStatus.Enacted;
            // Count relevant laws per faction
            int proposer = Laws[lawIndex].ProposedByFaction;
            if (proposer >= 0 && proposer < FactionCount)
            {
                Factions[proposer].RelevantLaws++;
            }
            return true;
        }
        else
        {
            Laws[lawIndex].Status = LawStatus.Rejected;
            return false;
        }
    }

    /// <summary>
    /// Repeal an enacted law by index.
    /// </summary>
    public bool RepealLaw(int lawIndex)
    {
        if (lawIndex < 0 || lawIndex >= LawCount) return false;
        if (Laws[lawIndex].Status != LawStatus.Enacted) return false;

        Laws[lawIndex].Status = LawStatus.Repealed;

        int proposer = Laws[lawIndex].ProposedByFaction;
        if (proposer >= 0 && proposer < FactionCount)
        {
            Factions[proposer].RelevantLaws = Math.Max(0, Factions[proposer].RelevantLaws - 1);
        }
        return true;
    }

    /// <summary>
    /// Get all active law effects. Iterates enacted laws and aggregates effects.
    /// </summary>
    public Dictionary<string, float> GetActiveLawEffects()
    {
        var effects = new Dictionary<string, float>();
        for (int i = 0; i < LawCount; i++)
        {
            if (Laws[i].Status != LawStatus.Enacted) continue;
            if (Laws[i].Effects == null) continue;

            foreach (var effect in Laws[i].Effects)
            {
                if (!effects.ContainsKey(effect.ParameterName))
                    effects[effect.ParameterName] = 0f;
                effects[effect.ParameterName] += effect.Modifier;
            }
        }
        return effects;
    }

    // =========================================================================
    // Decision impact tracking
    // =========================================================================

    /// <summary>
    /// Record a player decision's impact on approval. Decays over 6 game months.
    /// </summary>
    public void RecordDecision(string description, float magnitude)
    {
        if (DecisionImpactCount >= MaxDecisionImpacts)
        {
            // Evict the weakest impact
            int weakestIdx = 0;
            float weakestVal = float.MaxValue;
            for (int i = 0; i < DecisionImpactCount; i++)
            {
                float absVal = Math.Abs(DecisionImpacts[i].Magnitude * DecisionImpacts[i].RemainingStrength);
                if (absVal < weakestVal)
                {
                    weakestVal = absVal;
                    weakestIdx = i;
                }
            }
            DecisionImpacts[weakestIdx] = new DecisionImpact
            {
                Description = description,
                Magnitude = magnitude,
                RemainingStrength = 1.0f,
            };
        }
        else
        {
            DecisionImpacts[DecisionImpactCount] = new DecisionImpact
            {
                Description = description,
                Magnitude = magnitude,
                RemainingStrength = 1.0f,
            };
            DecisionImpactCount++;
        }
    }

    /// <summary>
    /// Compute the aggregate decision score (0-1 scale, 0.5 = neutral).
    /// </summary>
    public float ComputeDecisionScore()
    {
        if (DecisionImpactCount == 0) return 0.5f;

        float total = 0f;
        float weight = 0f;
        for (int i = 0; i < DecisionImpactCount; i++)
        {
            total += DecisionImpacts[i].Magnitude * DecisionImpacts[i].RemainingStrength;
            weight += Math.Abs(DecisionImpacts[i].RemainingStrength);
        }

        if (weight < 0.0001f) return 0.5f;
        // Normalize to 0-1 range: total/weight gives [-1,1], map to [0,1]
        float normalized = (total / weight + 1f) * 0.5f;
        return Math.Clamp(normalized, 0f, 1f);
    }

    // =========================================================================
    // Protest escalation
    // =========================================================================

    /// <summary>
    /// Escalate or de-escalate protests based on approval rating.
    /// Called daily. Protest phases advance after MaxProtestEscalationDays days.
    /// Approval > 60 de-escalates, approval 40-60 is the protest range, below 40 escalates.
    /// </summary>
    public void UpdateProtests(float approvalRating)
    {
        if (approvalRating >= 60f)
        {
            // De-escalate
            ProtestDaysAtCurrentPhase = 0;
            if (CurrentProtestPhase > ProtestPhase.None)
            {
                CurrentProtestPhase = (ProtestPhase)((int)CurrentProtestPhase - 1);
            }
        }
        else if (approvalRating < 40f)
        {
            // Escalate
            ProtestDaysAtCurrentPhase++;
            if (ProtestDaysAtCurrentPhase >= MaxProtestEscalationDays &&
                CurrentProtestPhase < ProtestPhase.Riot)
            {
                CurrentProtestPhase = (ProtestPhase)((int)CurrentProtestPhase + 1);
                ProtestDaysAtCurrentPhase = 0;
            }
        }
        else if (approvalRating < 60f && CurrentProtestPhase == ProtestPhase.None)
        {
            // In the 40-60 range with no protests: start complaints
            ProtestDaysAtCurrentPhase++;
            if (ProtestDaysAtCurrentPhase >= MaxProtestEscalationDays)
            {
                CurrentProtestPhase = ProtestPhase.Complaint;
                ProtestDaysAtCurrentPhase = 0;
            }
        }
        // 40-60 with existing protests: maintain, no escalation or de-escalation
    }

    // =========================================================================
    // Corruption
    // =========================================================================

    /// <summary>
    /// Calculate corruption index (0-100).
    /// Formula: base(10) + lobbyAcceptance * 5 + (1 - transparency) * 10 - mediaFreedom * 15
    /// </summary>
    public float CalculateCorruption(float lobbyAcceptance, float transparency, float mediaFreedom)
    {
        float corruption = 10f
            + lobbyAcceptance * 5f
            + (1f - transparency) * 10f
            - mediaFreedom * 15f;
        return Math.Clamp(corruption, 0f, 100f);
    }

    // =========================================================================
    // Tick methods
    // =========================================================================

    /// <summary>
    /// Daily tick: decay decision impacts, update protests.
    /// </summary>
    public void DailyTick(WorldState state, double dt)
    {
        // Decay decision impacts
        for (int i = DecisionImpactCount - 1; i >= 0; i--)
        {
            DecisionImpacts[i].RemainingStrength -= DecisionDecayPerDay;
            if (DecisionImpacts[i].RemainingStrength <= 0f)
            {
                // Remove by swapping with last
                DecisionImpactCount--;
                if (i < DecisionImpactCount)
                    DecisionImpacts[i] = DecisionImpacts[DecisionImpactCount];
                DecisionImpacts[DecisionImpactCount] = default;
            }
        }

        // Update protests
        float approval = CalculateApproval(
            state.Happiness, EconomyScore, ServiceScore, SafetyScore,
            ComputeDecisionScore(), ScandalLevel);
        UpdateProtests(approval);
    }

    /// <summary>
    /// Monthly tick: recalculate approval, corruption, check election, check consequences.
    /// </summary>
    public void MonthlyTick(WorldState state, double dt)
    {
        // Compute scores
        DecisionScore = ComputeDecisionScore();
        CorruptionIndex = CalculateCorruption(LobbyAcceptance, TransparencyLevel, MediaFreedom);

        // Update approval rating on WorldState (0-1 scale for WorldState, we store 0-100 internally)
        float approval = CalculateApproval(
            state.Happiness, EconomyScore, ServiceScore, SafetyScore,
            DecisionScore, ScandalLevel);
        state.ApprovalRating = approval / 100f;

        // Scandal naturally decays
        ScandalLevel = Math.Max(0f, ScandalLevel - 0.02f);

        // Check election
        if (state.Year >= state.NextElectionYear && state.Month == 11)
        {
            ResolveElection(state);
        }

        // Approval consequences
        ApplyApprovalConsequences(state, approval);

        // Keep WorldState council seats in sync for WASM / snapshot export (P5.6 stub).
        Array.Copy(CouncilSeats, state.CouncilSeats, CouncilSeatCount);
    }

    /// <summary>Copy initialized council seats onto <paramref name="state"/> (boot / tests).</summary>
    public void SyncCouncilToWorld(WorldState state)
    {
        Array.Copy(CouncilSeats, state.CouncilSeats, CouncilSeatCount);
    }

    /// <summary>Restore council seat faction ids from a snapshot (save/load).</summary>
    public void RestoreCouncilSeats(ReadOnlySpan<int> seats, WorldState state)
    {
        int n = Math.Min(seats.Length, CouncilSeatCount);
        for (int i = 0; i < n; i++)
        {
            int faction = seats[i];
            CouncilSeats[i] = (byte)Math.Clamp(faction, 0, FactionCount - 1);
        }

        Array.Copy(CouncilSeats, state.CouncilSeats, CouncilSeatCount);
    }

    /// <summary>
    /// Apply consequences based on approval level.
    /// >80: bonus funding, 40-60: protests possible, less than 20: forced election.
    /// </summary>
    public void ApplyApprovalConsequences(WorldState state, float approval)
    {
        if (approval > 80f)
        {
            // Bonus funding: 5% of current funds
            state.CityFunds += state.CityFunds / 20;
        }

        if (approval < 20f)
        {
            // Forced election next month
            if (state.NextElectionYear > state.Year ||
                (state.NextElectionYear == state.Year && state.Month < 11))
            {
                state.NextElectionYear = state.Year;
                // If we're past November, set it to next year
                if (state.Month >= 11)
                    state.NextElectionYear = state.Year + 1;
            }
        }
    }

    /// <summary>
    /// Get the count of seats held by a specific faction.
    /// </summary>
    public int GetFactionSeatCount(int factionId)
    {
        int count = 0;
        for (int i = 0; i < CouncilSeatCount; i++)
        {
            if (CouncilSeats[i] == factionId)
                count++;
        }
        return count;
    }
}
