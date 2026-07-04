using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Xunit;

namespace Forge.Engine.Tests;

public class ProductionChainTests
{
    private static WorldState CreateTestWorld(int size = 32)
    {
        return new WorldState(size);
    }

    // =========================================================================
    // Registry loading
    // =========================================================================

    [Fact]
    public void LoadChains_RegistersAllDefinitions()
    {
        var registry = new ProductionChainRegistry();
        registry.LoadChains();

        // Should have registered all built-in chains (40+ buildings)
        Assert.True(registry.Definitions.Count >= 40,
            $"Expected at least 40 production defs, got {registry.Definitions.Count}");
    }

    [Fact]
    public void GetDef_WheatFarm_ReturnsCorrectDefinition()
    {
        var registry = new ProductionChainRegistry();
        registry.LoadChains();

        var def = registry.GetDef(100);
        Assert.NotNull(def);
        Assert.Equal("Wheat Farm", def.Value.Name);
        Assert.Empty(def.Value.InputGoods);
        Assert.Single(def.Value.OutputGoods);
        Assert.Equal(Good.Wheat, def.Value.OutputGoods[0]);
        Assert.Equal(120f, def.Value.OutputQuantities[0]);
        Assert.Equal(8, def.Value.WorkersNeeded);
    }

    [Fact]
    public void GetDef_SteelMill_HasCorrectRatios()
    {
        var registry = new ProductionChainRegistry();
        registry.LoadChains();

        var def = registry.GetDef(122);
        Assert.NotNull(def);
        Assert.Equal("Steel Mill", def.Value.Name);

        // Steel mill takes 1.37t iron ore + 0.78t coal per ton steel
        Assert.Equal(2, def.Value.InputGoods.Length);
        Assert.Contains(Good.IronOre, def.Value.InputGoods);
        Assert.Contains(Good.Coal, def.Value.InputGoods);

        // Iron ore input should be 137 (for 100 steel output)
        int ironIdx = global::System.Array.IndexOf(def.Value.InputGoods, Good.IronOre);
        Assert.Equal(137f, def.Value.InputQuantities[ironIdx]);

        // Coal input should be 78
        int coalIdx = global::System.Array.IndexOf(def.Value.InputGoods, Good.Coal);
        Assert.Equal(78f, def.Value.InputQuantities[coalIdx]);

        // Output is 100 tons of steel
        Assert.Single(def.Value.OutputGoods);
        Assert.Equal(Good.Steel, def.Value.OutputGoods[0]);
        Assert.Equal(100f, def.Value.OutputQuantities[0]);
    }

    [Fact]
    public void GetDef_FlourMill_Has137to1WheatToFlourRatio()
    {
        var registry = new ProductionChainRegistry();
        registry.LoadChains();

        var def = registry.GetDef(101);
        Assert.NotNull(def);
        Assert.Equal("Flour Mill", def.Value.Name);

        // 164.4 wheat -> 120 flour (ratio: 164.4/120 = 1.37)
        Assert.Equal(Good.Wheat, def.Value.InputGoods[0]);
        Assert.Equal(164.4f, def.Value.InputQuantities[0], 1);

        Assert.Equal(Good.Flour, def.Value.OutputGoods[0]);
        Assert.Equal(120f, def.Value.OutputQuantities[0]);

        float ratio = def.Value.InputQuantities[0] / def.Value.OutputQuantities[0];
        Assert.True(global::System.Math.Abs(ratio - 1.37f) < 0.01f,
            $"Wheat-to-flour ratio should be ~1.37, got {ratio}");
    }

    [Fact]
    public void GetDef_AutoFactory_Uses977kgSteelPerVehicle()
    {
        var registry = new ProductionChainRegistry();
        registry.LoadChains();

        var def = registry.GetDef(171);
        Assert.NotNull(def);
        Assert.Equal("Automobile Factory", def.Value.Name);

        // 97.7 tons steel per 10 vehicles = 9.77 tons per vehicle = 977 kg
        int steelIdx = global::System.Array.IndexOf(def.Value.InputGoods, Good.Steel);
        Assert.True(steelIdx >= 0, "Auto factory should require steel");
        float steelPerVehicle = def.Value.InputQuantities[steelIdx] / def.Value.OutputQuantities[0];
        float kgPerVehicle = steelPerVehicle * 1000f;

        Assert.True(global::System.Math.Abs(kgPerVehicle - 9770f) < 100f,
            $"Expected ~9770 kg steel per 10 vehicles, got {kgPerVehicle}");
    }

    [Fact]
    public void GetDef_OilRefinery_ProducesMultipleOutputs()
    {
        var registry = new ProductionChainRegistry();
        registry.LoadChains();

        var def = registry.GetDef(141);
        Assert.NotNull(def);
        Assert.Equal("Oil Refinery", def.Value.Name);

        // Oil refinery produces fuel, plastics, and chemicals
        Assert.Equal(3, def.Value.OutputGoods.Length);
        Assert.Contains(Good.Fuel, def.Value.OutputGoods);
        Assert.Contains(Good.Plastics, def.Value.OutputGoods);
        Assert.Contains(Good.Chemicals, def.Value.OutputGoods);
    }

    [Fact]
    public void GetDef_UnknownType_ReturnsNull()
    {
        var registry = new ProductionChainRegistry();
        registry.LoadChains();

        var def = registry.GetDef(9999);
        Assert.Null(def);
    }

    // =========================================================================
    // Production chain completeness
    // =========================================================================

    [Fact]
    public void AllChains_InputOutputArraysMatch()
    {
        var registry = new ProductionChainRegistry();
        registry.LoadChains();

        foreach (var (id, def) in registry.Definitions)
        {
            Assert.Equal(def.InputGoods.Length, def.InputQuantities.Length);
            Assert.Equal(def.OutputGoods.Length, def.OutputQuantities.Length);
            Assert.True(def.OutputGoods.Length > 0,
                $"Building {def.Name} (type {id}) must produce at least one output");
        }
    }

    [Fact]
    public void AllChains_WorkersNeededPositive()
    {
        var registry = new ProductionChainRegistry();
        registry.LoadChains();

        foreach (var (id, def) in registry.Definitions)
        {
            Assert.True(def.WorkersNeeded > 0,
                $"Building {def.Name} (type {id}) must need at least one worker");
        }
    }

    [Fact]
    public void AllChains_OutputQuantitiesPositive()
    {
        var registry = new ProductionChainRegistry();
        registry.LoadChains();

        foreach (var (id, def) in registry.Definitions)
        {
            for (int i = 0; i < def.OutputQuantities.Length; i++)
            {
                Assert.True(def.OutputQuantities[i] > 0f,
                    $"Building {def.Name}: output {def.OutputGoods[i]} must be positive");
            }
        }
    }

    // =========================================================================
    // Food chain completeness (wheat -> flour -> food)
    // =========================================================================

    [Fact]
    public void FoodChain_IsComplete()
    {
        var registry = new ProductionChainRegistry();
        registry.LoadChains();

        // Verify the full food chain: wheat farm -> flour mill -> bakery
        var wheatFarm = registry.GetDef(100);
        Assert.NotNull(wheatFarm);
        Assert.Contains(Good.Wheat, wheatFarm.Value.OutputGoods);

        var flourMill = registry.GetDef(101);
        Assert.NotNull(flourMill);
        Assert.Contains(Good.Wheat, flourMill.Value.InputGoods);
        Assert.Contains(Good.Flour, flourMill.Value.OutputGoods);

        var bakery = registry.GetDef(102);
        Assert.NotNull(bakery);
        Assert.Contains(Good.Flour, bakery.Value.InputGoods);
        Assert.Contains(Good.Food, bakery.Value.OutputGoods);
    }

    // =========================================================================
    // Productivity calculation
    // =========================================================================

    [Fact]
    public void CalculateProductivity_FullyStaffedWithPower_Returns1()
    {
        var registry = new ProductionChainRegistry();
        registry.LoadChains();
        var economy = new EconomySystem(registry);
        var state = CreateTestWorld();

        // Place a wheat farm (no inputs needed)
        int buildingId = state.Buildings.Allocate();
        state.Buildings.TypeId[buildingId] = 100;
        state.Buildings.State[buildingId] = 1; // operational
        state.Buildings.Condition[buildingId] = 255; // perfect condition
        state.Buildings.Occupants[buildingId] = 8; // full staff
        state.Buildings.GridX[buildingId] = 5;
        state.Buildings.GridY[buildingId] = 5;
        state.Tiles.PowerGrid[state.Tiles.Index(5, 5)] = 1;

        float productivity = registry.CalculateProductivity(state, buildingId, economy);
        Assert.Equal(1.0f, productivity, 3);
    }

    [Fact]
    public void CalculateProductivity_HalfStaffed_ReturnsHalf()
    {
        var registry = new ProductionChainRegistry();
        registry.LoadChains();
        var economy = new EconomySystem(registry);
        var state = CreateTestWorld();

        int buildingId = state.Buildings.Allocate();
        state.Buildings.TypeId[buildingId] = 100; // Wheat Farm (8 workers)
        state.Buildings.State[buildingId] = 1;
        state.Buildings.Condition[buildingId] = 255;
        state.Buildings.Occupants[buildingId] = 4; // half staff
        state.Buildings.GridX[buildingId] = 5;
        state.Buildings.GridY[buildingId] = 5;
        state.Tiles.PowerGrid[state.Tiles.Index(5, 5)] = 1;

        float productivity = registry.CalculateProductivity(state, buildingId, economy);
        Assert.Equal(0.5f, productivity, 3);
    }

    [Fact]
    public void CalculateProductivity_NoPower_ReturnsZero()
    {
        var registry = new ProductionChainRegistry();
        registry.LoadChains();
        var economy = new EconomySystem(registry);
        var state = CreateTestWorld();

        // Steel mill needs power
        int buildingId = state.Buildings.Allocate();
        state.Buildings.TypeId[buildingId] = 122; // Steel Mill
        state.Buildings.State[buildingId] = 1;
        state.Buildings.Condition[buildingId] = 255;
        state.Buildings.Occupants[buildingId] = 30;
        state.Buildings.GridX[buildingId] = 5;
        state.Buildings.GridY[buildingId] = 5;
        // No power!

        float productivity = registry.CalculateProductivity(state, buildingId, economy);
        Assert.Equal(0.0f, productivity);
    }

    [Fact]
    public void CalculateProductivity_NotOperational_ReturnsZero()
    {
        var registry = new ProductionChainRegistry();
        registry.LoadChains();
        var economy = new EconomySystem(registry);
        var state = CreateTestWorld();

        int buildingId = state.Buildings.Allocate();
        state.Buildings.TypeId[buildingId] = 100;
        state.Buildings.State[buildingId] = 0; // constructing, not operational
        state.Buildings.Condition[buildingId] = 255;
        state.Buildings.Occupants[buildingId] = 8;
        state.Buildings.GridX[buildingId] = 5;
        state.Buildings.GridY[buildingId] = 5;
        state.Tiles.PowerGrid[state.Tiles.Index(5, 5)] = 1;

        float productivity = registry.CalculateProductivity(state, buildingId, economy);
        Assert.Equal(0.0f, productivity);
    }

    [Fact]
    public void CalculateProductivity_DamagedBuilding_ReducesOutput()
    {
        var registry = new ProductionChainRegistry();
        registry.LoadChains();
        var economy = new EconomySystem(registry);
        var state = CreateTestWorld();

        int buildingId = state.Buildings.Allocate();
        state.Buildings.TypeId[buildingId] = 100;
        state.Buildings.State[buildingId] = 1;
        state.Buildings.Condition[buildingId] = 128; // 50% condition
        state.Buildings.Occupants[buildingId] = 8;
        state.Buildings.GridX[buildingId] = 5;
        state.Buildings.GridY[buildingId] = 5;
        state.Tiles.PowerGrid[state.Tiles.Index(5, 5)] = 1;

        float productivity = registry.CalculateProductivity(state, buildingId, economy);
        // Condition = 128/255 ≈ 0.502
        Assert.True(productivity > 0.49f && productivity < 0.51f,
            $"50% condition should give ~50% productivity, got {productivity}");
    }

    // =========================================================================
    // JSON loading
    // =========================================================================

    [Fact]
    public void LoadChainsFromJson_ValidJson_RegistersCorrectly()
    {
        var registry = new ProductionChainRegistry();
        string json = @"[
            {
                ""buildingTypeId"": 999,
                ""name"": ""Test Factory"",
                ""inputs"": [""Steel""],
                ""inputQuantities"": [50.0],
                ""outputs"": [""Vehicles""],
                ""outputQuantities"": [5.0],
                ""workersNeeded"": 10,
                ""minEducationLevel"": 1,
                ""powerConsumption"": 5.0,
                ""waterConsumption"": 10.0,
                ""pollutionOutput"": 0.1,
                ""era"": 4
            }
        ]";

        registry.LoadChainsFromJson(json);

        var def = registry.GetDef(999);
        Assert.NotNull(def);
        Assert.Equal("Test Factory", def.Value.Name);
        Assert.Equal(Good.Steel, def.Value.InputGoods[0]);
        Assert.Equal(50f, def.Value.InputQuantities[0]);
        Assert.Equal(Good.Vehicles, def.Value.OutputGoods[0]);
        Assert.Equal(5f, def.Value.OutputQuantities[0]);
    }
}
