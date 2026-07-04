using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Engine.Data;
using Forge.Engine.Simulation;

namespace Forge.Game.Simulation;

/// <summary>
/// Registry of all production building definitions. Each definition describes
/// what inputs a building consumes, what outputs it produces, and at what ratios.
///
/// Production chains model real-world industrial ratios:
/// - 1.37t wheat per 1t flour (milling yield ~73%)
/// - 1.37t iron ore + 0.78t coal per 1t steel (blast furnace)
/// - 977kg steel per vehicle (modern auto manufacturing)
///
/// Productivity is Leontief-limited: output = min(input_i / required_i) * base_output,
/// further modified by workforce availability and building condition.
/// </summary>
public sealed class ProductionChainRegistry
{
    /// <summary>
    /// Defines one production building type: its inputs, outputs, workforce needs, and constraints.
    /// </summary>
    public struct ProductionDef
    {
        public ushort BuildingTypeId;
        public string Name;
        public Good[] InputGoods;
        public float[] InputQuantities;
        public Good[] OutputGoods;
        public float[] OutputQuantities;
        public int WorkersNeeded;
        public byte MinEducationLevel;
        public float PowerConsumption;
        public float WaterConsumption;
        public float PollutionOutput;
        public byte Era;
    }

    private readonly Dictionary<ushort, ProductionDef> _definitions = new();

    /// <summary>All registered production definitions, keyed by building type ID.</summary>
    public IReadOnlyDictionary<ushort, ProductionDef> Definitions => _definitions;

    /// <summary>
    /// Load production chain definitions from embedded JSON data.
    /// Call once at game initialization.
    /// </summary>
    public void LoadChains()
    {
        _definitions.Clear();
        var defs = GetBuiltInChains();
        foreach (var def in defs)
        {
            _definitions[def.BuildingTypeId] = def;
        }
    }

    /// <summary>
    /// Load production chain definitions from a JSON string.
    /// Used for modding support and testing.
    /// </summary>
    public void LoadChainsFromJson(string json)
    {
        _definitions.Clear();
        var rawDefs = JsonSerializer.Deserialize<List<RawProductionDef>>(json, JsonOpts);
        if (rawDefs == null) return;

        foreach (var raw in rawDefs)
        {
            var def = ConvertRawDef(raw);
            _definitions[def.BuildingTypeId] = def;
        }
    }

    /// <summary>
    /// Get the production definition for a building type. Returns null if not a production building.
    /// </summary>
    public ProductionDef? GetDef(ushort buildingTypeId)
    {
        return _definitions.TryGetValue(buildingTypeId, out var def) ? def : null;
    }

    /// <summary>
    /// Calculate the actual productivity multiplier for a building, considering:
    /// 1. Input availability (Leontief minimum across all inputs)
    /// 2. Workforce (actual workers / needed workers)
    /// 3. Building condition (condition / 255)
    /// 4. Power availability (binary: has power or not)
    ///
    /// Returns a value between 0.0 (no production) and 1.0 (full production).
    /// </summary>
    public float CalculateProductivity(WorldState state, int buildingId, EconomySystem economy)
    {
        if (!state.Buildings.IsActive(buildingId)) return 0f;

        ushort typeId = state.Buildings.TypeId[buildingId];
        var defOpt = GetDef(typeId);
        if (defOpt == null) return 0f;

        var def = defOpt.Value;

        // Building must be operational (state == 1)
        if (state.Buildings.State[buildingId] != 1) return 0f;

        // Factor 1: Input availability (Leontief -- bottleneck determines output)
        float inputFactor = 1.0f;
        if (def.InputGoods.Length > 0)
        {
            int tileX = state.Buildings.GridX[buildingId];
            int tileY = state.Buildings.GridY[buildingId];
            int zoneId = economy.GetMarketZoneForTile(tileX, tileY, state.Tiles.Size);

            for (int i = 0; i < def.InputGoods.Length; i++)
            {
                float required = def.InputQuantities[i];
                if (required <= 0f) continue;

                float available = economy.GetZoneSupply(zoneId, def.InputGoods[i]);
                float ratio = available / required;
                if (ratio < inputFactor) inputFactor = ratio;
            }
            inputFactor = MathF.Min(inputFactor, 1.0f);
        }

        // Factor 2: Workforce
        float workerFactor = 1.0f;
        if (def.WorkersNeeded > 0)
        {
            int actualWorkers = state.Buildings.Occupants[buildingId];
            workerFactor = MathF.Min((float)actualWorkers / def.WorkersNeeded, 1.0f);
        }

        // Factor 3: Building condition (0-255 -> 0.0-1.0)
        float conditionFactor = state.Buildings.Condition[buildingId] / 255f;

        // Factor 4: Power availability (binary)
        float powerFactor = 1.0f;
        if (def.PowerConsumption > 0f)
        {
            int tileIdx = state.Tiles.Index(
                state.Buildings.GridX[buildingId],
                state.Buildings.GridY[buildingId]);
            powerFactor = state.Tiles.PowerGrid[tileIdx] != 0 ? 1.0f : 0.0f;
        }

        return inputFactor * workerFactor * conditionFactor * powerFactor;
    }

    // =========================================================================
    // Built-in production chain data (hardcoded for reliability)
    // =========================================================================

    private static List<ProductionDef> GetBuiltInChains()
    {
        return new List<ProductionDef>
        {
            // === FOOD CHAIN ===
            // Wheat Farm: no inputs, produces wheat
            MakeDef(100, "Wheat Farm", Array.Empty<Good>(), Array.Empty<float>(),
                new[] { Good.Wheat }, new[] { 120f }, 8, 0, 0.1f, 50f, 0.02f, 0),
            // Flour Mill: 1.37:1 wheat-to-flour (164.4 wheat -> 120 flour)
            MakeDef(101, "Flour Mill", new[] { Good.Wheat }, new[] { 164.4f },
                new[] { Good.Flour }, new[] { 120f }, 4, 0, 0.5f, 5f, 0.05f, 0),
            // Bakery: flour -> food
            MakeDef(102, "Bakery", new[] { Good.Flour }, new[] { 60f },
                new[] { Good.Food }, new[] { 80f }, 6, 0, 0.3f, 3f, 0.01f, 0),
            // Cattle Ranch: produces livestock
            MakeDef(103, "Cattle Ranch", Array.Empty<Good>(), Array.Empty<float>(),
                new[] { Good.Livestock }, new[] { 60f }, 6, 0, 0.1f, 80f, 0.04f, 0),
            // Meat Packing: livestock -> meat
            MakeDef(104, "Meat Packing Plant", new[] { Good.Livestock }, new[] { 60f },
                new[] { Good.Meat }, new[] { 45f }, 12, 0, 1.0f, 20f, 0.08f, 2),

            // === CONSTRUCTION CHAIN ===
            // Logging Camp: produces timber
            MakeDef(110, "Logging Camp", Array.Empty<Good>(), Array.Empty<float>(),
                new[] { Good.Timber }, new[] { 80f }, 10, 0, 0.2f, 1f, 0.03f, 0),
            // Sawmill: timber -> lumber
            MakeDef(111, "Sawmill", new[] { Good.Timber }, new[] { 80f },
                new[] { Good.Lumber }, new[] { 60f }, 8, 0, 1.5f, 5f, 0.06f, 1),
            // Clay Pit: produces clay
            MakeDef(112, "Clay Pit", Array.Empty<Good>(), Array.Empty<float>(),
                new[] { Good.Clay }, new[] { 100f }, 6, 0, 0.3f, 10f, 0.04f, 0),
            // Brick Factory: clay -> bricks
            MakeDef(113, "Brick Factory", new[] { Good.Clay }, new[] { 100f },
                new[] { Good.Bricks }, new[] { 75f }, 10, 0, 2.0f, 8f, 0.10f, 1),
            // Stone Quarry: produces stone
            MakeDef(114, "Stone Quarry", Array.Empty<Good>(), Array.Empty<float>(),
                new[] { Good.Stone }, new[] { 90f }, 12, 0, 1.0f, 5f, 0.08f, 0),
            // Cement Plant: stone -> cement
            MakeDef(115, "Cement Plant", new[] { Good.Stone }, new[] { 90f },
                new[] { Good.Cement }, new[] { 60f }, 15, 1, 5.0f, 20f, 0.15f, 3),

            // === METALS CHAIN ===
            // Iron Mine: produces iron ore
            MakeDef(120, "Iron Mine", Array.Empty<Good>(), Array.Empty<float>(),
                new[] { Good.IronOre }, new[] { 100f }, 20, 0, 3.0f, 15f, 0.12f, 1),
            // Coal Mine: produces coal
            MakeDef(121, "Coal Mine", Array.Empty<Good>(), Array.Empty<float>(),
                new[] { Good.Coal }, new[] { 80f }, 18, 0, 2.0f, 10f, 0.15f, 2),
            // Steel Mill: 1.37t ore + 0.78t coal per 1t steel
            MakeDef(122, "Steel Mill", new[] { Good.IronOre, Good.Coal }, new[] { 137f, 78f },
                new[] { Good.Steel }, new[] { 100f }, 30, 1, 10.0f, 40f, 0.25f, 3),

            // === TEXTILE CHAIN ===
            // Cotton Farm
            MakeDef(130, "Cotton Farm", Array.Empty<Good>(), Array.Empty<float>(),
                new[] { Good.Cotton }, new[] { 80f }, 10, 0, 0.1f, 60f, 0.02f, 1),
            // Textile Mill: cotton -> textiles
            MakeDef(131, "Textile Mill", new[] { Good.Cotton }, new[] { 80f },
                new[] { Good.Textiles }, new[] { 60f }, 20, 1, 3.0f, 30f, 0.10f, 2),
            // Garment Factory: textiles -> clothing
            MakeDef(132, "Garment Factory", new[] { Good.Textiles }, new[] { 60f },
                new[] { Good.Clothing }, new[] { 50f }, 25, 1, 2.0f, 10f, 0.05f, 3),

            // === PETROCHEMICAL CHAIN ===
            // Oil Well
            MakeDef(140, "Oil Well", Array.Empty<Good>(), Array.Empty<float>(),
                new[] { Good.CrudeOil }, new[] { 100f }, 15, 1, 5.0f, 20f, 0.12f, 3),
            // Oil Refinery: crude -> fuel + plastics + chemicals
            MakeDef(141, "Oil Refinery", new[] { Good.CrudeOil }, new[] { 100f },
                new[] { Good.Fuel, Good.Plastics, Good.Chemicals }, new[] { 50f, 25f, 15f },
                25, 2, 8.0f, 50f, 0.20f, 3),
            // Gas Extraction
            MakeDef(142, "Gas Extraction Plant", Array.Empty<Good>(), Array.Empty<float>(),
                new[] { Good.NaturalGas }, new[] { 80f }, 12, 1, 3.0f, 10f, 0.08f, 3),

            // === ENERGY CHAIN ===
            // Coal Power Plant
            MakeDef(150, "Coal Power Plant", new[] { Good.Coal }, new[] { 60f },
                new[] { Good.Electricity }, new[] { 200f }, 20, 1, 0f, 80f, 0.30f, 3),
            // Gas Power Plant
            MakeDef(151, "Gas Power Plant", new[] { Good.NaturalGas }, new[] { 50f },
                new[] { Good.Electricity }, new[] { 180f }, 15, 2, 0f, 40f, 0.15f, 4),
            // Oil Power Plant
            MakeDef(152, "Oil Power Plant", new[] { Good.Fuel }, new[] { 40f },
                new[] { Good.Electricity }, new[] { 160f }, 18, 1, 0f, 60f, 0.22f, 3),

            // === ELECTRONICS CHAIN ===
            // Sand Pit
            MakeDef(160, "Sand Pit", Array.Empty<Good>(), Array.Empty<float>(),
                new[] { Good.Sand }, new[] { 100f }, 6, 0, 0.5f, 5f, 0.03f, 0),
            // Glass Factory: sand -> glass
            MakeDef(161, "Glass Factory", new[] { Good.Sand }, new[] { 80f },
                new[] { Good.Glass }, new[] { 50f }, 12, 1, 4.0f, 15f, 0.10f, 2),
            // Silicon Foundry: sand -> electronics (semiconductor fab)
            MakeDef(162, "Silicon Foundry", new[] { Good.Sand }, new[] { 60f },
                new[] { Good.Electronics }, new[] { 20f }, 30, 3, 15.0f, 80f, 0.08f, 4),

            // === AUTOMOTIVE CHAIN ===
            // Rubber Plantation
            MakeDef(170, "Rubber Plantation", Array.Empty<Good>(), Array.Empty<float>(),
                new[] { Good.Rubber }, new[] { 50f }, 8, 0, 0.1f, 40f, 0.01f, 2),
            // Automobile Factory: 977kg steel + rubber + glass + electronics per vehicle
            MakeDef(171, "Automobile Factory",
                new[] { Good.Steel, Good.Rubber, Good.Glass, Good.Electronics },
                new[] { 97.7f, 15f, 10f, 5f },
                new[] { Good.Vehicles }, new[] { 10f }, 50, 2, 12.0f, 30f, 0.15f, 4),

            // === CONSUMER GOODS CHAIN ===
            // Pharmaceutical Plant: chemicals -> medicine
            MakeDef(175, "Pharmaceutical Plant", new[] { Good.Chemicals }, new[] { 30f },
                new[] { Good.Medicine }, new[] { 20f }, 25, 3, 5.0f, 40f, 0.06f, 4),
            // Consumer Goods: plastics + textiles -> consumer goods
            MakeDef(176, "Consumer Goods Factory",
                new[] { Good.Plastics, Good.Textiles }, new[] { 30f, 20f },
                new[] { Good.ConsumerGoods }, new[] { 40f }, 20, 1, 3.0f, 10f, 0.08f, 4),
            // Furniture Workshop: lumber -> furniture
            MakeDef(177, "Furniture Workshop", new[] { Good.Lumber }, new[] { 40f },
                new[] { Good.Furniture }, new[] { 25f }, 10, 1, 1.5f, 3f, 0.03f, 2),
            // Paper Mill: timber -> paper
            MakeDef(178, "Paper Mill", new[] { Good.Timber }, new[] { 60f },
                new[] { Good.Paper }, new[] { 45f }, 12, 1, 3.0f, 60f, 0.12f, 2),

            // === ADVANCED CHAIN ===
            // Lithium Mine
            MakeDef(180, "Lithium Mine", Array.Empty<Good>(), Array.Empty<float>(),
                new[] { Good.Lithium }, new[] { 30f }, 15, 2, 5.0f, 100f, 0.10f, 4),
            // Battery Factory: lithium + chemicals -> batteries
            MakeDef(181, "Battery Factory",
                new[] { Good.Lithium, Good.Chemicals }, new[] { 20f, 10f },
                new[] { Good.Batteries }, new[] { 15f }, 25, 3, 8.0f, 25f, 0.08f, 5),
            // Rare Earth Mine
            MakeDef(182, "Rare Earth Mine", Array.Empty<Good>(), Array.Empty<float>(),
                new[] { Good.RareEarth }, new[] { 20f }, 20, 2, 8.0f, 60f, 0.15f, 4),

            // === MINING ===
            // Copper Mine
            MakeDef(185, "Copper Mine", Array.Empty<Good>(), Array.Empty<float>(),
                new[] { Good.Copper }, new[] { 70f }, 15, 0, 3.0f, 20f, 0.10f, 1),
            // Gold Mine
            MakeDef(186, "Gold Mine", Array.Empty<Good>(), Array.Empty<float>(),
                new[] { Good.Gold }, new[] { 5f }, 20, 1, 5.0f, 30f, 0.12f, 1),

            // === UTILITIES ===
            // Water Treatment
            MakeDef(190, "Water Treatment Plant", new[] { Good.Chemicals }, new[] { 5f },
                new[] { Good.Water }, new[] { 500f }, 10, 2, 4.0f, 0f, 0.02f, 3),
            // Fishing Dock
            MakeDef(191, "Fishing Dock", Array.Empty<Good>(), Array.Empty<float>(),
                new[] { Good.Fish }, new[] { 40f }, 8, 0, 0.2f, 0f, 0.01f, 0),

            // === NUCLEAR ===
            // Uranium Mine
            MakeDef(195, "Uranium Mine", Array.Empty<Good>(), Array.Empty<float>(),
                new[] { Good.Uranium }, new[] { 10f }, 25, 3, 8.0f, 40f, 0.05f, 4),
            // Nuclear Power Plant
            MakeDef(196, "Nuclear Power Plant", new[] { Good.Uranium }, new[] { 5f },
                new[] { Good.Electricity }, new[] { 500f }, 40, 3, 0f, 200f, 0.02f, 4),
        };
    }

    private static ProductionDef MakeDef(
        ushort id, string name,
        Good[] inputs, float[] inputQty,
        Good[] outputs, float[] outputQty,
        int workers, byte edu,
        float power, float water, float pollution, byte era)
    {
        return new ProductionDef
        {
            BuildingTypeId = id,
            Name = name,
            InputGoods = inputs,
            InputQuantities = inputQty,
            OutputGoods = outputs,
            OutputQuantities = outputQty,
            WorkersNeeded = workers,
            MinEducationLevel = edu,
            PowerConsumption = power,
            WaterConsumption = water,
            PollutionOutput = pollution,
            Era = era,
        };
    }

    // =========================================================================
    // JSON deserialization support
    // =========================================================================

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private sealed class RawProductionDef
    {
        [JsonPropertyName("buildingTypeId")]
        public ushort BuildingTypeId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("inputs")]
        public List<string> Inputs { get; set; } = new();

        [JsonPropertyName("inputQuantities")]
        public List<float> InputQuantities { get; set; } = new();

        [JsonPropertyName("outputs")]
        public List<string> Outputs { get; set; } = new();

        [JsonPropertyName("outputQuantities")]
        public List<float> OutputQuantities { get; set; } = new();

        [JsonPropertyName("workersNeeded")]
        public int WorkersNeeded { get; set; }

        [JsonPropertyName("minEducationLevel")]
        public byte MinEducationLevel { get; set; }

        [JsonPropertyName("powerConsumption")]
        public float PowerConsumption { get; set; }

        [JsonPropertyName("waterConsumption")]
        public float WaterConsumption { get; set; }

        [JsonPropertyName("pollutionOutput")]
        public float PollutionOutput { get; set; }

        [JsonPropertyName("era")]
        public byte Era { get; set; }
    }

    private static ProductionDef ConvertRawDef(RawProductionDef raw)
    {
        var inputGoods = new Good[raw.Inputs.Count];
        for (int i = 0; i < raw.Inputs.Count; i++)
        {
            if (!Enum.TryParse<Good>(raw.Inputs[i], ignoreCase: true, out var g))
                throw new InvalidOperationException($"Unknown good '{raw.Inputs[i]}' in production def '{raw.Name}'");
            inputGoods[i] = g;
        }

        var outputGoods = new Good[raw.Outputs.Count];
        for (int i = 0; i < raw.Outputs.Count; i++)
        {
            if (!Enum.TryParse<Good>(raw.Outputs[i], ignoreCase: true, out var g))
                throw new InvalidOperationException($"Unknown good '{raw.Outputs[i]}' in production def '{raw.Name}'");
            outputGoods[i] = g;
        }

        return new ProductionDef
        {
            BuildingTypeId = raw.BuildingTypeId,
            Name = raw.Name,
            InputGoods = inputGoods,
            InputQuantities = raw.InputQuantities.ToArray(),
            OutputGoods = outputGoods,
            OutputQuantities = raw.OutputQuantities.ToArray(),
            WorkersNeeded = raw.WorkersNeeded,
            MinEducationLevel = raw.MinEducationLevel,
            PowerConsumption = raw.PowerConsumption,
            WaterConsumption = raw.WaterConsumption,
            PollutionOutput = raw.PollutionOutput,
            Era = raw.Era,
        };
    }
}
