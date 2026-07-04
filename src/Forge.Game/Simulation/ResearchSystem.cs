using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Engine.Simulation;

namespace Forge.Game.Simulation;

/// <summary>
/// Technology research simulation: RP generation, research queue, tech prerequisites,
/// eureka bonuses, era transitions, and branching decisions.
///
/// Supports up to 256 technologies (matching WorldState.UnlockedTech bitfield).
/// Tech definitions loaded from JSON at runtime.
/// </summary>
public sealed class ResearchSystem
{
    // =========================================================================
    // Constants
    // =========================================================================

    public const int MaxTechnologies = 256;
    public const int MaxResearchQueue = 3;
    public const int MaxCategories = 17;
    public const int MaxBranchingDecisions = 16;
    public const float EurekaMinBonus = 0.25f;
    public const float EurekaMaxBonus = 0.50f;

    // RP generation constants
    public const float RpPerLibrary = 1f;
    public const float RpBasePerUniversity = 5f;
    public const float RpPerProfessor = 1f;
    public const float RpPerResearchLab = 10f;
    public const float RpPerTechCampus = 20f;
    public const float RpPerEducatedCitizen = 0.01f;
    public const float RpPerHeavyIndustry = 2f;

    // Era thresholds
    public static readonly EraRequirement[] EraRequirements = new EraRequirement[]
    {
        new() { Era = 0, Name = "Ancient",     MinPopulation = 0,      RequiredTechCount = 0 },
        new() { Era = 1, Name = "Medieval",    MinPopulation = 500,    RequiredTechCount = 5 },
        new() { Era = 2, Name = "Colonial",    MinPopulation = 2000,   RequiredTechCount = 15 },
        new() { Era = 3, Name = "Industrial",  MinPopulation = 10000,  RequiredTechCount = 30 },
        new() { Era = 4, Name = "Modern",      MinPopulation = 50000,  RequiredTechCount = 60 },
        new() { Era = 5, Name = "Future",      MinPopulation = 200000, RequiredTechCount = 100 },
    };

    // =========================================================================
    // State
    // =========================================================================

    /// <summary>All loaded technology definitions, indexed by tech ID.</summary>
    public TechDefinition[] Technologies { get; private set; } = Array.Empty<TechDefinition>();

    /// <summary>Number of loaded technologies.</summary>
    public int TechCount { get; private set; }

    /// <summary>Research queue: up to 3 tech IDs. -1 = empty slot.</summary>
    public int[] ResearchQueue { get; } = { -1, -1, -1 };

    /// <summary>Progress for each queued tech (0.0 to cost).</summary>
    public float[] QueueProgress { get; } = new float[MaxResearchQueue];

    /// <summary>Eureka bonuses applied to specific techs. Key = techId, Value = bonus multiplier (0.25-0.50).</summary>
    public Dictionary<int, float> EurekaBonuses { get; } = new();

    /// <summary>Branching decisions made. Key = decision ID, Value = chosen tech ID.</summary>
    public Dictionary<int, int> BranchingChoices { get; } = new();

    /// <summary>Branching decision definitions.</summary>
    public BranchingDecision[] BranchingDecisions { get; private set; } = Array.Empty<BranchingDecision>();

    // RP generation building counts (set externally before MonthlyTick)
    public int LibraryCount { get; set; }
    public int UniversityCount { get; set; }
    public int ProfessorCount { get; set; }
    public int ResearchLabCount { get; set; }
    public int TechCampusCount { get; set; }
    public int EducatedPopulation { get; set; }
    public int HeavyIndustryCount { get; set; }

    // RP multipliers
    public float FundingMultiplier { get; set; } = 1.0f;
    public float EducationLevelMultiplier { get; set; } = 1.0f;
    public float SpecializationMultiplier { get; set; } = 1.0f;
    public float CollaborationMultiplier { get; set; } = 1.0f;

    // =========================================================================
    // Data types
    // =========================================================================

    public class TechDefinition
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("category")]
        public string Category { get; set; } = "";

        [JsonPropertyName("era")]
        public int Era { get; set; }

        [JsonPropertyName("cost")]
        public float Cost { get; set; }

        [JsonPropertyName("prerequisites")]
        public int[] Prerequisites { get; set; } = Array.Empty<int>();

        [JsonPropertyName("effects")]
        public TechEffect[] Effects { get; set; } = Array.Empty<TechEffect>();

        [JsonPropertyName("unlocksBuildings")]
        public string[] UnlocksBuildings { get; set; } = Array.Empty<string>();

        [JsonPropertyName("unlocksPolicies")]
        public string[] UnlocksPolicies { get; set; } = Array.Empty<string>();

        [JsonPropertyName("eurekaCondition")]
        public string? EurekaCondition { get; set; }

        [JsonPropertyName("eurekaBonus")]
        public float EurekaBonus { get; set; }

        [JsonPropertyName("branchingDecisionId")]
        public int BranchingDecisionId { get; set; } = -1;
    }

    public class TechEffect
    {
        [JsonPropertyName("parameter")]
        public string Parameter { get; set; } = "";

        [JsonPropertyName("modifier")]
        public float Modifier { get; set; }
    }

    public struct EraRequirement
    {
        public int Era;
        public string Name;
        public int MinPopulation;
        public int RequiredTechCount;
    }

    public class BranchingDecision
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("optionA")]
        public int OptionA { get; set; }

        [JsonPropertyName("optionB")]
        public int OptionB { get; set; }
    }

    public class TechDataFile
    {
        [JsonPropertyName("technologies")]
        public TechDefinition[] Technologies { get; set; } = Array.Empty<TechDefinition>();

        [JsonPropertyName("branchingDecisions")]
        public BranchingDecision[] BranchingDecisions { get; set; } = Array.Empty<BranchingDecision>();
    }

    // =========================================================================
    // Loading
    // =========================================================================

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Load technology definitions from a JSON file.
    /// </summary>
    public void LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Tech data file not found: {filePath}");

        string json = File.ReadAllText(filePath);
        LoadFromJson(json);
    }

    /// <summary>
    /// Load technology definitions from a JSON string.
    /// Supports both the legacy object format {"technologies":[...], "branchingDecisions":[...]}
    /// and the new array format where each entry has string IDs like "T001".
    /// </summary>
    public void LoadFromJson(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        Technologies = new TechDefinition[MaxTechnologies];
        TechCount = 0;

        if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            // Legacy format: {"technologies": [...], "branchingDecisions": [...]}
            var data = JsonSerializer.Deserialize<TechDataFile>(json, JsonOptions)
                ?? throw new InvalidDataException("Failed to deserialize tech data.");

            foreach (var tech in data.Technologies)
            {
                if (tech.Id >= 0 && tech.Id < MaxTechnologies)
                {
                    Technologies[tech.Id] = tech;
                    TechCount = Math.Max(TechCount, tech.Id + 1);
                }
            }
            BranchingDecisions = data.BranchingDecisions ?? Array.Empty<BranchingDecision>();
        }
        else if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            // New format: array of objects with string IDs ("T001"), string eras, etc.
            // Build a lookup from string ID → integer index for prerequisite resolution
            var stringIdToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var rawTechs = new List<(JsonElement elem, int index)>();

            int index = 0;
            foreach (var elem in doc.RootElement.EnumerateArray())
            {
                string stringId = elem.GetProperty("id").GetString() ?? "";
                stringIdToIndex[stringId] = index;
                rawTechs.Add((elem.Clone(), index));
                index++;
            }

            // Build branching decisions from branch_group fields
            var branchGroups = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

            foreach (var (elem, idx) in rawTechs)
            {
                var tech = ParseNewFormatTech(elem, idx, stringIdToIndex);
                if (tech.Id >= 0 && tech.Id < MaxTechnologies)
                {
                    Technologies[tech.Id] = tech;
                    TechCount = Math.Max(TechCount, tech.Id + 1);
                }

                // Collect branch groups
                if (elem.TryGetProperty("branch_group", out var bg) && bg.ValueKind == JsonValueKind.String)
                {
                    string group = bg.GetString()!;
                    if (!branchGroups.ContainsKey(group))
                        branchGroups[group] = new List<int>();
                    branchGroups[group].Add(idx);
                }
            }

            // Build BranchingDecision objects from branch groups (pairs of exclusive techs)
            var decisions = new List<BranchingDecision>();
            int decisionId = 0;
            foreach (var (groupName, members) in branchGroups)
            {
                // Only create a decision if there are exactly 2 exclusive members
                var exclusiveMembers = members
                    .Where(m => Technologies[m] != null)
                    .ToList();
                if (exclusiveMembers.Count >= 2)
                {
                    var decision = new BranchingDecision
                    {
                        Id = decisionId,
                        Name = groupName,
                        Description = $"Branching decision: {groupName}",
                        OptionA = exclusiveMembers[0],
                        OptionB = exclusiveMembers[1],
                    };
                    decisions.Add(decision);

                    // Tag the techs with this decision
                    if (Technologies[exclusiveMembers[0]] != null)
                        Technologies[exclusiveMembers[0]]!.BranchingDecisionId = decisionId;
                    if (Technologies[exclusiveMembers[1]] != null)
                        Technologies[exclusiveMembers[1]]!.BranchingDecisionId = decisionId;

                    decisionId++;
                }
            }
            BranchingDecisions = decisions.ToArray();
        }
        else
        {
            throw new InvalidDataException("Tech data JSON must be an object or array.");
        }
    }

    /// <summary>
    /// Map a string era name (from new-format JSON) to an integer era index.
    /// </summary>
    private static int ParseEraString(string era)
    {
        return era.ToLowerInvariant() switch
        {
            "ancient" => 0,
            "frontier" => 0,
            "medieval" => 1,
            "colonial" => 2,
            "postwar" => 2,
            "industrial" => 3,
            "modern" => 4,
            "future" => 5,
            _ => 0,
        };
    }

    /// <summary>
    /// Parse a single technology from the new JSON array format.
    /// </summary>
    private static TechDefinition ParseNewFormatTech(
        JsonElement elem, int index, Dictionary<string, int> idLookup)
    {
        var tech = new TechDefinition
        {
            Id = index,
            Name = elem.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
            Description = elem.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
            Category = elem.TryGetProperty("category", out var c) ? c.GetString() ?? "" : "",
        };

        // Era: string → int
        if (elem.TryGetProperty("era", out var eraElem))
        {
            if (eraElem.ValueKind == JsonValueKind.Number)
                tech.Era = eraElem.GetInt32();
            else
                tech.Era = ParseEraString(eraElem.GetString() ?? "");
        }

        // Cost: "cost_rp" or "cost"
        if (elem.TryGetProperty("cost_rp", out var costRp))
            tech.Cost = costRp.GetSingle();
        else if (elem.TryGetProperty("cost", out var cost))
            tech.Cost = cost.GetSingle();

        // Prerequisites: string[] → int[] via lookup
        if (elem.TryGetProperty("prerequisites", out var prereqs) && prereqs.ValueKind == JsonValueKind.Array)
        {
            var prereqList = new List<int>();
            foreach (var p in prereqs.EnumerateArray())
            {
                if (p.ValueKind == JsonValueKind.Number)
                {
                    prereqList.Add(p.GetInt32());
                }
                else if (p.ValueKind == JsonValueKind.String)
                {
                    string pid = p.GetString()!;
                    if (idLookup.TryGetValue(pid, out int prereqIndex))
                        prereqList.Add(prereqIndex);
                }
            }
            tech.Prerequisites = prereqList.ToArray();
        }

        // Effects: dictionary format → TechEffect[]
        if (elem.TryGetProperty("effects", out var effects))
        {
            if (effects.ValueKind == JsonValueKind.Object)
            {
                var effectList = new List<TechEffect>();
                foreach (var prop in effects.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Number)
                    {
                        effectList.Add(new TechEffect
                        {
                            Parameter = prop.Name,
                            Modifier = prop.Value.GetSingle(),
                        });
                    }
                }
                tech.Effects = effectList.ToArray();
            }
            else if (effects.ValueKind == JsonValueKind.Array)
            {
                tech.Effects = JsonSerializer.Deserialize<TechEffect[]>(effects.GetRawText(), JsonOptions)
                    ?? Array.Empty<TechEffect>();
            }
        }

        // Unlocks → UnlocksBuildings
        if (elem.TryGetProperty("unlocks", out var unlocks) && unlocks.ValueKind == JsonValueKind.Array)
        {
            var unlockList = new List<string>();
            foreach (var u in unlocks.EnumerateArray())
            {
                if (u.ValueKind == JsonValueKind.String)
                    unlockList.Add(u.GetString()!);
            }
            tech.UnlocksBuildings = unlockList.ToArray();
        }
        else if (elem.TryGetProperty("unlocksBuildings", out var ub) && ub.ValueKind == JsonValueKind.Array)
        {
            tech.UnlocksBuildings = JsonSerializer.Deserialize<string[]>(ub.GetRawText(), JsonOptions)
                ?? Array.Empty<string>();
        }

        // Eureka
        if (elem.TryGetProperty("eureka_condition", out var ec))
            tech.EurekaCondition = ec.ValueKind == JsonValueKind.Null ? null : ec.GetString();
        else if (elem.TryGetProperty("eurekaCondition", out var ec2))
            tech.EurekaCondition = ec2.ValueKind == JsonValueKind.Null ? null : ec2.GetString();

        if (elem.TryGetProperty("eureka_bonus", out var eb))
            tech.EurekaBonus = eb.GetSingle();
        else if (elem.TryGetProperty("eurekaBonus", out var eb2))
            tech.EurekaBonus = eb2.GetSingle();

        return tech;
    }

    /// <summary>
    /// Directly load from pre-built arrays (for testing or procedural generation).
    /// </summary>
    public void LoadDirect(TechDefinition[] techs, BranchingDecision[]? decisions = null)
    {
        Technologies = new TechDefinition[MaxTechnologies];
        TechCount = 0;

        foreach (var tech in techs)
        {
            if (tech.Id >= 0 && tech.Id < MaxTechnologies)
            {
                Technologies[tech.Id] = tech;
                TechCount = Math.Max(TechCount, tech.Id + 1);
            }
        }

        BranchingDecisions = decisions ?? Array.Empty<BranchingDecision>();
    }

    // =========================================================================
    // RP generation
    // =========================================================================

    /// <summary>
    /// Calculate base RP per month from city buildings and population.
    /// Base = library(1) + university(5 + 1/professor) + research_lab(10)
    ///      + tech_campus(20) + educated_pop(0.01/citizen) + private_RD(2/heavy_industry)
    /// </summary>
    public float CalculateBaseRP()
    {
        float rp = LibraryCount * RpPerLibrary
                 + UniversityCount * RpBasePerUniversity
                 + ProfessorCount * RpPerProfessor
                 + ResearchLabCount * RpPerResearchLab
                 + TechCampusCount * RpPerTechCampus
                 + EducatedPopulation * RpPerEducatedCitizen
                 + HeavyIndustryCount * RpPerHeavyIndustry;
        return rp;
    }

    /// <summary>
    /// Calculate effective RP per month.
    /// Effective = base * funding * education_level * specialization * collaboration
    /// </summary>
    public float CalculateEffectiveRP()
    {
        float baseRp = CalculateBaseRP();
        return baseRp * FundingMultiplier * EducationLevelMultiplier
                      * SpecializationMultiplier * CollaborationMultiplier;
    }

    // =========================================================================
    // Research queue
    // =========================================================================

    /// <summary>
    /// Enqueue a technology for research. Returns true if successfully added.
    /// Validates prerequisites, branching exclusions, and that the tech isn't already unlocked.
    /// </summary>
    public bool EnqueueResearch(int techId, WorldState state)
    {
        if (techId < 0 || techId >= TechCount) return false;
        if (Technologies[techId] == null) return false;
        if (state.IsTechUnlocked(techId)) return false;

        // Check already in queue
        for (int i = 0; i < MaxResearchQueue; i++)
        {
            if (ResearchQueue[i] == techId) return false;
        }

        // Check prerequisites
        if (!ArePrerequisitesMet(techId, state)) return false;

        // Check branching exclusion
        if (IsBranchExcluded(techId)) return false;

        // Find empty slot
        for (int i = 0; i < MaxResearchQueue; i++)
        {
            if (ResearchQueue[i] == -1)
            {
                ResearchQueue[i] = techId;
                QueueProgress[i] = 0f;
                return true;
            }
        }

        return false; // Queue full
    }

    /// <summary>
    /// Remove a technology from the research queue.
    /// </summary>
    public bool DequeueResearch(int techId)
    {
        for (int i = 0; i < MaxResearchQueue; i++)
        {
            if (ResearchQueue[i] == techId)
            {
                ResearchQueue[i] = -1;
                QueueProgress[i] = 0f;
                // Compact queue
                CompactQueue();
                return true;
            }
        }
        return false;
    }

    private void CompactQueue()
    {
        int write = 0;
        for (int read = 0; read < MaxResearchQueue; read++)
        {
            if (ResearchQueue[read] != -1)
            {
                if (write != read)
                {
                    ResearchQueue[write] = ResearchQueue[read];
                    QueueProgress[write] = QueueProgress[read];
                    ResearchQueue[read] = -1;
                    QueueProgress[read] = 0f;
                }
                write++;
            }
        }
    }

    // =========================================================================
    // Prerequisites
    // =========================================================================

    /// <summary>
    /// Check if all prerequisites for a tech are unlocked.
    /// </summary>
    public bool ArePrerequisitesMet(int techId, WorldState state)
    {
        if (techId < 0 || techId >= MaxTechnologies) return false;
        var tech = Technologies[techId];
        if (tech == null) return false;
        if (tech.Prerequisites == null || tech.Prerequisites.Length == 0) return true;

        foreach (int prereq in tech.Prerequisites)
        {
            if (!state.IsTechUnlocked(prereq))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Check if a tech is excluded by a branching decision the player already made.
    /// </summary>
    public bool IsBranchExcluded(int techId)
    {
        if (techId < 0 || techId >= MaxTechnologies) return false;
        var tech = Technologies[techId];
        if (tech == null) return false;
        if (tech.BranchingDecisionId < 0) return false;

        // If a branching decision was made for this decision group, the other option is excluded
        if (BranchingChoices.TryGetValue(tech.BranchingDecisionId, out int chosenTechId))
        {
            return chosenTechId != techId;
        }

        return false; // No decision made yet, not excluded
    }

    /// <summary>
    /// Make a branching decision, choosing one tech and excluding the other.
    /// </summary>
    public bool MakeBranchingChoice(int decisionId, int chosenTechId)
    {
        if (BranchingChoices.ContainsKey(decisionId)) return false; // Already decided

        // Validate the decision exists
        BranchingDecision? decision = null;
        foreach (var d in BranchingDecisions)
        {
            if (d.Id == decisionId)
            {
                decision = d;
                break;
            }
        }
        if (decision == null) return false;

        // Validate the chosen tech is one of the options
        if (chosenTechId != decision.OptionA && chosenTechId != decision.OptionB) return false;

        BranchingChoices[decisionId] = chosenTechId;
        return true;
    }

    // =========================================================================
    // Eureka
    // =========================================================================

    /// <summary>
    /// Grant a eureka bonus for a specific tech. Bonus is clamped to [0.25, 0.50].
    /// </summary>
    public void GrantEureka(int techId, float bonus)
    {
        if (techId < 0 || techId >= MaxTechnologies) return;
        float clamped = Math.Clamp(bonus, EurekaMinBonus, EurekaMaxBonus);
        EurekaBonuses[techId] = clamped;
    }

    /// <summary>
    /// Get the effective cost of a tech after eureka bonus.
    /// </summary>
    public float GetEffectiveCost(int techId)
    {
        if (techId < 0 || techId >= MaxTechnologies) return float.MaxValue;
        var tech = Technologies[techId];
        if (tech == null) return float.MaxValue;

        float cost = tech.Cost;
        if (EurekaBonuses.TryGetValue(techId, out float bonus))
        {
            cost *= (1f - bonus);
        }
        return cost;
    }

    // =========================================================================
    // Era transitions
    // =========================================================================

    /// <summary>
    /// Check if the city qualifies for the next era. Returns the era number if transition
    /// should occur, or -1 if no transition is available.
    /// </summary>
    public int CheckEraTransition(WorldState state)
    {
        int currentEra = state.Era;
        int nextEra = currentEra + 1;

        if (nextEra >= EraRequirements.Length) return -1;

        var req = EraRequirements[nextEra];

        // Check population
        if (state.Population < req.MinPopulation) return -1;

        // Check tech count
        int unlockedCount = CountUnlockedTechs(state);
        if (unlockedCount < req.RequiredTechCount) return -1;

        return nextEra;
    }

    /// <summary>
    /// Count the total number of unlocked technologies.
    /// </summary>
    public static int CountUnlockedTechs(WorldState state)
    {
        int count = 0;
        for (int i = 0; i < state.UnlockedTech.Length; i++)
        {
            count += BitCount(state.UnlockedTech[i]);
        }
        return count;
    }

    private static int BitCount(ulong value)
    {
        // Hamming weight / popcount
        value -= (value >> 1) & 0x5555555555555555UL;
        value = (value & 0x3333333333333333UL) + ((value >> 2) & 0x3333333333333333UL);
        value = (value + (value >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
        return (int)((value * 0x0101010101010101UL) >> 56);
    }

    // =========================================================================
    // Monthly tick
    // =========================================================================

    /// <summary>
    /// Monthly research tick: generate RP, advance research queue, complete techs, check era.
    /// </summary>
    public void MonthlyTick(WorldState state, double dt)
    {
        float effectiveRp = CalculateEffectiveRP();
        state.ResearchRate = effectiveRp;

        // Distribute RP to queued research (first item gets 100%, overflow to next)
        float remainingRp = effectiveRp;

        for (int i = 0; i < MaxResearchQueue; i++)
        {
            if (ResearchQueue[i] == -1) break;
            if (remainingRp <= 0f) break;

            int techId = ResearchQueue[i];
            float cost = GetEffectiveCost(techId);
            float needed = cost - QueueProgress[i];

            if (remainingRp >= needed)
            {
                // Complete this tech
                remainingRp -= needed;
                QueueProgress[i] = cost;
                CompleteTech(techId, state);
                ResearchQueue[i] = -1;
                QueueProgress[i] = 0f;
            }
            else
            {
                QueueProgress[i] += remainingRp;
                remainingRp = 0f;
            }
        }

        // Compact queue after completions
        CompactQueue();

        // Store accumulated RP
        state.ResearchPoints += effectiveRp;

        // Update current research tracking on WorldState
        if (ResearchQueue[0] != -1)
        {
            state.CurrentResearchId = ResearchQueue[0];
            float cost = GetEffectiveCost(ResearchQueue[0]);
            state.CurrentResearchProgress = cost > 0 ? QueueProgress[0] / cost : 1f;
        }
        else
        {
            state.CurrentResearchId = -1;
            state.CurrentResearchProgress = 0f;
        }

        // Check era transition
        int newEra = CheckEraTransition(state);
        if (newEra >= 0)
        {
            state.Era = newEra;
        }
    }

    /// <summary>
    /// Mark a technology as complete: unlock it and apply its effects.
    /// </summary>
    internal void CompleteTech(int techId, WorldState state)
    {
        state.UnlockTech(techId);

        // Remove eureka bonus (already consumed)
        EurekaBonuses.Remove(techId);
    }

    // =========================================================================
    // Query helpers
    // =========================================================================

    /// <summary>
    /// Get all techs available for research (prerequisites met, not unlocked, not excluded).
    /// </summary>
    public List<int> GetAvailableTechs(WorldState state)
    {
        var available = new List<int>();
        for (int i = 0; i < TechCount; i++)
        {
            if (Technologies[i] == null) continue;
            if (state.IsTechUnlocked(i)) continue;
            if (!ArePrerequisitesMet(i, state)) continue;
            if (IsBranchExcluded(i)) continue;
            available.Add(i);
        }
        return available;
    }

    /// <summary>
    /// Get all techs in a specific category.
    /// </summary>
    public List<int> GetTechsByCategory(string category)
    {
        var results = new List<int>();
        for (int i = 0; i < TechCount; i++)
        {
            if (Technologies[i] != null && Technologies[i].Category == category)
                results.Add(i);
        }
        return results;
    }

    /// <summary>
    /// Get the research progress as a fraction (0.0-1.0) for the first queued tech.
    /// Returns 0 if nothing is being researched.
    /// </summary>
    public float GetCurrentResearchFraction()
    {
        if (ResearchQueue[0] == -1) return 0f;
        float cost = GetEffectiveCost(ResearchQueue[0]);
        if (cost <= 0f) return 1f;
        return Math.Clamp(QueueProgress[0] / cost, 0f, 1f);
    }
}
