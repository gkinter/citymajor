using System.Text.Json;
using System.Text.Json.Serialization;

namespace Forge.Game.Simulation;

/// <summary>
/// Known effect keys from base/data/laws/laws.json (partial catalog).
/// Values are additive deltas summed across active ordinances via
/// <see cref="LawSystem.GetAggregateEffect"/>.
/// </summary>
public static class LawEffectKeys
{
    // Budget / treasury
    public const string TaxRevenue = "tax_revenue";
    public const string CityRevenue = "city_revenue";
    public const string TaxIncomeMult = "tax_income_mult";
    public const string BudgetExpenseMult = "budget_expense_mult";

    // Traffic capacity (road_capacity is the canonical catalog key; traffic_capacity is an alias)
    public const string RoadCapacity = "road_capacity";
    public const string TrafficCapacity = "traffic_capacity";
    public const string TrafficCongestion = "traffic_congestion";

    // Citizen sentiment (applied on day tick when wired)
    public const string Happiness = "happiness";
    public const string CitizenHappiness = "citizen_happiness";
    public const string Approval = "approval";

    // Zone growth / construction (from laws.json)
    public const string ConstructionCost = "construction_cost";
    public const string ConstructionSpeed = "construction_speed";
    public const string HousingSupply = "housing_supply";
    public const string IndustrialOutput = "industrial_output";
    public const string HousingDensity = "housing_density";
}

/// <summary>
/// City ordinance catalog loaded from base/data/laws/laws.json.
/// Tracks which ordinances are currently active and their slider values.
/// Aggregate effects are summed via <see cref="GetAggregateEffect"/> and applied by SimHost.
/// </summary>
public sealed class LawSystem
{
    public const int MaxLaws = 128;

    private readonly List<LawDefinition> _definitions = new();
    private readonly Dictionary<string, int> _indexById = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool[] _active = new bool[MaxLaws];
    private readonly float[][] _parameterValues = new float[MaxLaws][];

    /// <summary>Loaded ordinance definitions (read-only).</summary>
    public IReadOnlyList<LawDefinition> Definitions => _definitions;

    /// <summary>Number of definitions loaded from laws.json.</summary>
    public int DefinitionCount => _definitions.Count;

    /// <summary>Ordinances currently enabled by the player.</summary>
    public int ActiveLawCount { get; private set; }

    /// <summary>Load ordinance definitions from base/data/laws/laws.json.</summary>
    public void LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Law data file not found: {filePath}");

        LoadFromJson(File.ReadAllText(filePath));
    }

    /// <summary>Load ordinance definitions from a JSON string (top-level array).</summary>
    public void LoadFromJson(string json)
    {
        Clear();

        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("laws.json must be a top-level array.");

        foreach (var elem in doc.RootElement.EnumerateArray())
        {
            var definition = ParseLawDefinition(elem);
            if (string.IsNullOrWhiteSpace(definition.Id))
                continue;
            if (_definitions.Count >= MaxLaws)
                break;
            if (_indexById.ContainsKey(definition.Id))
                continue;

            int index = _definitions.Count;
            _definitions.Add(definition);
            _indexById[definition.Id] = index;
            _parameterValues[index] = CreateDefaultParameters(definition);
        }
    }

    private static LawDefinition ParseLawDefinition(JsonElement elem)
    {
        static string ReadString(JsonElement el, string name) =>
            el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
                ? prop.GetString() ?? ""
                : "";

        static float ReadFloat(JsonElement el, string name) =>
            el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number
                ? (float)prop.GetDouble()
                : 0f;

        static int ReadInt(JsonElement el, string name) =>
            el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number
                ? prop.GetInt32()
                : 0;

        var parameters = new List<LawParameterDefinition>();
        if (elem.TryGetProperty("parameters", out var paramsEl) && paramsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var param in paramsEl.EnumerateArray())
            {
                parameters.Add(new LawParameterDefinition
                {
                    Name = ReadString(param, "name"),
                    Min = ReadFloat(param, "min"),
                    Max = ReadFloat(param, "max"),
                    Default = ReadFloat(param, "default"),
                    Step = ReadFloat(param, "step"),
                });
            }
        }

        string? techPrerequisite = null;
        if (elem.TryGetProperty("tech_prerequisite", out var techEl)
            && techEl.ValueKind == JsonValueKind.String)
        {
            techPrerequisite = techEl.GetString();
        }

        return new LawDefinition
        {
            Id = ReadString(elem, "id"),
            Name = ReadString(elem, "name"),
            Category = ReadString(elem, "category"),
            EraMin = ReadString(elem, "era_min"),
            Parameters = parameters.ToArray(),
            Effects = ParseFloatMap(elem, "effects"),
            FactionReactions = ParseFloatMap(elem, "faction_reactions"),
            CostMonthly = ReadInt(elem, "cost_monthly"),
            ComplianceBase = ReadFloat(elem, "compliance_base"),
            TechPrerequisite = techPrerequisite,
            Description = ReadString(elem, "description"),
        };
    }

    private static Dictionary<string, float> ParseFloatMap(JsonElement elem, string name)
    {
        var map = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        if (!elem.TryGetProperty(name, out var obj) || obj.ValueKind != JsonValueKind.Object)
            return map;

        foreach (var prop in obj.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Number)
                map[prop.Name] = (float)prop.Value.GetDouble();
        }

        return map;
    }

    /// <summary>Resolve a definition index by slug id, or -1 when unknown.</summary>
    public int GetIndexById(string lawId)
    {
        if (string.IsNullOrWhiteSpace(lawId)) return -1;
        return _indexById.TryGetValue(lawId, out int index) ? index : -1;
    }

    /// <summary>Whether the ordinance at <paramref name="index"/> is currently active.</summary>
    public bool IsActive(int index) =>
        index >= 0 && index < _definitions.Count && _active[index];

    /// <summary>Enable or disable an ordinance by slug id.</summary>
    public bool SetActive(string lawId, bool active)
    {
        int index = GetIndexById(lawId);
        return index >= 0 && SetActive(index, active);
    }

    /// <summary>Enable or disable an ordinance by catalog index.</summary>
    public bool SetActive(int index, bool active)
    {
        if (index < 0 || index >= _definitions.Count) return false;
        if (_active[index] == active) return true;

        _active[index] = active;
        ActiveLawCount += active ? 1 : -1;
        return true;
    }

    /// <summary>
    /// Sum the <paramref name="effectKey"/> modifier across all active ordinances.
    /// Missing keys return 0 (safe default).
    /// </summary>
    public float GetAggregateEffect(string effectKey)
    {
        if (string.IsNullOrWhiteSpace(effectKey)) return 0f;

        float total = 0f;
        for (int i = 0; i < _definitions.Count; i++)
        {
            if (!_active[i]) continue;

            var effects = _definitions[i].Effects;
            if (effects is null || effects.Count == 0) continue;

            if (effects.TryGetValue(effectKey, out float value))
                total += value;
        }

        return total;
    }

    /// <summary>Total monthly operating cost (cost_monthly) for all active ordinances.</summary>
    public int GetActiveMonthlyOperatingCost()
    {
        int total = 0;
        for (int i = 0; i < _definitions.Count; i++)
        {
            if (!_active[i]) continue;
            total += _definitions[i].CostMonthly;
        }

        return total;
    }

    /// <summary>Current slider value for a parameter on an active ordinance.</summary>
    public float GetParameterValue(int lawIndex, int parameterIndex)
    {
        if (lawIndex < 0 || lawIndex >= _definitions.Count) return 0f;
        var values = _parameterValues[lawIndex];
        if (values is null || parameterIndex < 0 || parameterIndex >= values.Length) return 0f;
        return values[parameterIndex];
    }

    /// <summary>Set a slider value, clamped to the definition min/max.</summary>
    public bool SetParameterValue(int lawIndex, int parameterIndex, float value)
    {
        if (lawIndex < 0 || lawIndex >= _definitions.Count) return false;

        var definition = _definitions[lawIndex];
        if (definition.Parameters is null
            || parameterIndex < 0
            || parameterIndex >= definition.Parameters.Length)
            return false;

        var param = definition.Parameters[parameterIndex];
        _parameterValues[lawIndex][parameterIndex] = Math.Clamp(value, param.Min, param.Max);
        return true;
    }

    private void Clear()
    {
        _definitions.Clear();
        _indexById.Clear();
        Array.Clear(_active, 0, _active.Length);
        ActiveLawCount = 0;

        for (int i = 0; i < _parameterValues.Length; i++)
            _parameterValues[i] = Array.Empty<float>();
    }

    private static float[] CreateDefaultParameters(LawDefinition definition)
    {
        if (definition.Parameters is null || definition.Parameters.Length == 0)
            return Array.Empty<float>();

        var values = new float[definition.Parameters.Length];
        for (int i = 0; i < definition.Parameters.Length; i++)
            values[i] = definition.Parameters[i].Default;
        return values;
    }

    /// <summary>Static ordinance definition from laws.json.</summary>
    public sealed class LawDefinition
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("category")]
        public string Category { get; set; } = "";

        [JsonPropertyName("era_min")]
        public string EraMin { get; set; } = "";

        [JsonPropertyName("parameters")]
        public LawParameterDefinition[] Parameters { get; set; } = Array.Empty<LawParameterDefinition>();

        [JsonPropertyName("effects")]
        public Dictionary<string, float> Effects { get; set; } = new();

        [JsonPropertyName("faction_reactions")]
        public Dictionary<string, float> FactionReactions { get; set; } = new();

        [JsonPropertyName("cost_monthly")]
        public int CostMonthly { get; set; }

        [JsonPropertyName("compliance_base")]
        public float ComplianceBase { get; set; }

        [JsonPropertyName("tech_prerequisite")]
        public string? TechPrerequisite { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";
    }

    /// <summary>Slider parameter on an ordinance.</summary>
    public sealed class LawParameterDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("min")]
        public float Min { get; set; }

        [JsonPropertyName("max")]
        public float Max { get; set; }

        [JsonPropertyName("default")]
        public float Default { get; set; }

        [JsonPropertyName("step")]
        public float Step { get; set; }
    }
}
