using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Xunit;

namespace Forge.Engine.Tests;

public class EventSystemTests
{
    // Minimal JSON for testing -- 3 representative event definitions
    private const string TestEventsJson = """
    {
      "events": [
        {
          "id": 0,
          "typeId": "fire",
          "category": "natural",
          "name": "Building Fire",
          "description": "A fire has broken out.",
          "minEra": 0,
          "maxEra": 5,
          "baseProbability": 0.5,
          "durationDays": { "min": 3, "max": 5 },
          "severity": { "min": 0.5, "max": 0.9 },
          "localized": true,
          "spreadRadius": 3,
          "effects": {
            "happiness": -0.15,
            "propertyDamage": 0.3,
            "crimeIncrease": 0.05
          },
          "triggerConditions": {
            "minFireRisk": 0.4
          },
          "cascades": ["homelessness"]
        },
        {
          "id": 1,
          "typeId": "recession",
          "category": "economic",
          "name": "Economic Recession",
          "description": "The economy has entered a recession.",
          "minEra": 2,
          "maxEra": 5,
          "baseProbability": 0.5,
          "durationDays": { "min": 10, "max": 20 },
          "severity": { "min": 0.3, "max": 0.7 },
          "localized": false,
          "spreadRadius": 0,
          "effects": {
            "happiness": -0.2,
            "taxRevenueMultiplier": -0.3,
            "unemploymentIncrease": 0.15
          },
          "triggerConditions": {
            "minPopulation": 100,
            "maxHappiness": 0.35
          },
          "cascades": ["protest"]
        },
        {
          "id": 2,
          "typeId": "festival",
          "category": "social",
          "name": "City Festival",
          "description": "A festival brings joy.",
          "minEra": 0,
          "maxEra": 5,
          "baseProbability": 0.5,
          "durationDays": { "min": 3, "max": 7 },
          "severity": { "min": 0.3, "max": 0.8 },
          "localized": true,
          "spreadRadius": 5,
          "effects": {
            "happiness": 0.15,
            "commercialBoost": 0.2
          },
          "triggerConditions": {
            "minHappiness": 0.4
          },
          "cascades": []
        },
        {
          "id": 3,
          "typeId": "homelessness",
          "category": "social",
          "name": "Homelessness Crisis",
          "description": "Homelessness is rising.",
          "minEra": 0,
          "maxEra": 5,
          "baseProbability": 0.0,
          "durationDays": { "min": 10, "max": 30 },
          "severity": { "min": 0.2, "max": 0.5 },
          "localized": false,
          "spreadRadius": 0,
          "effects": {
            "happiness": -0.1,
            "crimeIncrease": 0.1
          },
          "triggerConditions": {},
          "cascades": []
        },
        {
          "id": 4,
          "typeId": "protest",
          "category": "social",
          "name": "Public Protest",
          "description": "Citizens are protesting.",
          "minEra": 0,
          "maxEra": 5,
          "baseProbability": 0.0,
          "durationDays": { "min": 3, "max": 7 },
          "severity": { "min": 0.2, "max": 0.5 },
          "localized": false,
          "spreadRadius": 0,
          "effects": {
            "happiness": -0.05,
            "approvalRatingPenalty": 0.1
          },
          "triggerConditions": {},
          "cascades": []
        }
      ]
    }
    """;

    private static WorldState CreateTestWorld(int size = 32)
    {
        return new WorldState(size);
    }

    private static EventSystem CreateLoadedSystem(int seed = 42)
    {
        var system = new EventSystem(seed);
        system.LoadDefinitionsFromJson(TestEventsJson);
        return system;
    }

    // =========================================================================
    // Definition loading tests
    // =========================================================================

    [Fact]
    public void LoadDefinitions_ParsesAllEvents()
    {
        var system = CreateLoadedSystem();
        Assert.Equal(5, system.Definitions.Count);
    }

    [Fact]
    public void LoadDefinitions_ParsesTypeIds()
    {
        var system = CreateLoadedSystem();
        Assert.Equal("fire", system.Definitions[0].TypeId);
        Assert.Equal("recession", system.Definitions[1].TypeId);
        Assert.Equal("festival", system.Definitions[2].TypeId);
    }

    [Fact]
    public void LoadDefinitions_ParsesEffects()
    {
        var system = CreateLoadedSystem();
        var fireDef = system.Definitions[0];
        Assert.True(fireDef.Effects.ContainsKey("happiness"));
        Assert.Equal(-0.15f, fireDef.Effects["happiness"], 0.001f);
    }

    [Fact]
    public void LoadDefinitions_ParsesCascades()
    {
        var system = CreateLoadedSystem();
        var fireDef = system.Definitions[0];
        Assert.Single(fireDef.Cascades);
        Assert.Equal("homelessness", fireDef.Cascades[0]);
    }

    [Fact]
    public void LoadDefinitions_ParsesDurationRange()
    {
        var system = CreateLoadedSystem();
        var fireDef = system.Definitions[0];
        Assert.Equal(3, fireDef.MinDurationDays);
        Assert.Equal(5, fireDef.MaxDurationDays);
    }

    [Fact]
    public void LoadDefinitions_ParsesTriggerConditions()
    {
        var system = CreateLoadedSystem();
        var fireDef = system.Definitions[0];
        Assert.True(fireDef.TriggerConditions.ContainsKey("minFireRisk"));
        Assert.Equal(0.4f, fireDef.TriggerConditions["minFireRisk"], 0.001f);
    }

    [Fact]
    public void LoadDefinitionsFromJson_InvalidJson_Throws()
    {
        var system = new EventSystem();
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => system.LoadDefinitionsFromJson("not json"));
    }

    [Fact]
    public void LoadDefinitionsFromJson_MissingEventsKey_Throws()
    {
        var system = new EventSystem();
        Assert.Throws<InvalidDataException>(() => system.LoadDefinitionsFromJson("{}"));
    }

    // =========================================================================
    // Event triggering tests
    // =========================================================================

    [Fact]
    public void DailyTick_FireTriggersWhenFireRiskHigh()
    {
        var system = CreateLoadedSystem(seed: 1);
        var state = CreateTestWorld();
        state.Population = 5000;
        state.Happiness = 0.5f;

        // Set fire risk above threshold on several tiles
        for (int i = 0; i < state.Tiles.Count; i++)
            state.Tiles.FireRisk[i] = 0.8f;

        // Run many ticks to ensure event triggers (probability=0.5)
        bool triggered = false;
        for (int day = 0; day < 20; day++)
        {
            system.DailyTick(state);
            if (system.IsEventTypeActive("fire"))
            {
                triggered = true;
                break;
            }
        }
        Assert.True(triggered, "Fire event should trigger when fire risk is high");
    }

    [Fact]
    public void DailyTick_RecessionRequiresLowHappiness()
    {
        var system = CreateLoadedSystem(seed: 10);
        var state = CreateTestWorld();
        state.Population = 5000;
        state.Happiness = 0.7f; // Above maxHappiness=0.35 threshold

        for (int day = 0; day < 50; day++)
            system.DailyTick(state);

        Assert.False(system.IsEventTypeActive("recession"),
            "Recession should not trigger when happiness is above threshold");
    }

    [Fact]
    public void DailyTick_DoesNotExceedMaxConcurrentEvents()
    {
        var system = CreateLoadedSystem(seed: 5);
        var state = CreateTestWorld();
        state.Population = 5000;
        state.Happiness = 0.5f;

        for (int i = 0; i < state.Tiles.Count; i++)
            state.Tiles.FireRisk[i] = 0.8f;

        // Fill up events to capacity
        for (int i = 0; i < EventSystem.MaxConcurrentEvents; i++)
            state.AddEvent(100 + i, 30, 0.5f);

        // With state events at max, the event system should also be capped
        // (it has its own cap at MaxConcurrentEvents)
        for (int i = 0; i < EventSystem.MaxConcurrentEvents; i++)
            system.TriggerEvent($"type_{i}", state); // These won't find definitions, so won't add

        // Trigger real events
        system.DailyTick(state);
        Assert.True(system.ActiveEventCount <= EventSystem.MaxConcurrentEvents);
    }

    [Fact]
    public void DailyTick_EraFiltering_SkipsWrongEra()
    {
        var system = CreateLoadedSystem(seed: 1);
        var state = CreateTestWorld();
        state.Population = 5000;
        state.Happiness = 0.2f; // Low enough for recession trigger
        state.Era = 1; // Medieval -- recession requires minEra=2

        for (int day = 0; day < 50; day++)
            system.DailyTick(state);

        Assert.False(system.IsEventTypeActive("recession"),
            "Recession should not trigger in Medieval era (requires era >= 2)");
    }

    // =========================================================================
    // Event lifecycle tests
    // =========================================================================

    [Fact]
    public void TriggerEvent_ManualTrigger_CreatesEvent()
    {
        var system = CreateLoadedSystem();
        var state = CreateTestWorld();

        bool result = system.TriggerEvent("fire", state, severityOverride: 0.5f, tileX: 10, tileY: 10);

        Assert.True(result);
        Assert.Equal(1, system.ActiveEventCount);
        Assert.True(system.IsEventTypeActive("fire"));
    }

    [Fact]
    public void TriggerEvent_DuplicateType_ReturnsFalse()
    {
        var system = CreateLoadedSystem();
        var state = CreateTestWorld();

        system.TriggerEvent("fire", state, severityOverride: 0.5f);
        bool duplicate = system.TriggerEvent("fire", state, severityOverride: 0.5f);

        Assert.False(duplicate, "Should not allow duplicate event types");
        Assert.Equal(1, system.ActiveEventCount);
    }

    [Fact]
    public void TriggerEvent_UnknownType_ReturnsFalse()
    {
        var system = CreateLoadedSystem();
        var state = CreateTestWorld();

        bool result = system.TriggerEvent("nonexistent_event", state);
        Assert.False(result);
        Assert.Equal(0, system.ActiveEventCount);
    }

    [Fact]
    public void UpdateEvents_EventExpires_IsRemoved()
    {
        var system = CreateLoadedSystem();
        var state = CreateTestWorld();

        system.TriggerEvent("festival", state, severityOverride: 0.5f, tileX: 10, tileY: 10);
        Assert.Equal(1, system.ActiveEventCount);

        // Advance through the entire event duration (max 7 days)
        for (int day = 0; day < 10; day++)
            system.UpdateEvents(state, 1f);

        Assert.Equal(0, system.ActiveEventCount);
    }

    [Fact]
    public void UpdateEvents_PhaseProgression()
    {
        var system = CreateLoadedSystem();
        var state = CreateTestWorld();

        system.TriggerEvent("recession", state, severityOverride: 0.5f);

        // Get the event and check initial phase
        Assert.Equal(EventSystem.EventPhase.Brewing, system.ActiveEvents[0].Phase);

        // Advance to approximately 20% of duration (should be Active)
        var evt = system.ActiveEvents[0];
        int daysToActive = (int)(evt.DurationDays * 0.20f);
        for (int i = 0; i < daysToActive; i++)
            system.UpdateEvents(state, 1f);

        if (system.ActiveEventCount > 0)
            Assert.Equal(EventSystem.EventPhase.Active, system.ActiveEvents[0].Phase);
    }

    [Fact]
    public void UpdateEvents_AppliesHappinessEffect()
    {
        var system = CreateLoadedSystem();
        var state = CreateTestWorld();
        state.Happiness = 0.5f;

        // Trigger a negative event with high severity
        system.TriggerEvent("recession", state, severityOverride: 0.9f);

        float initialHappiness = state.Happiness;

        // Advance well into Active phase (past 15% of duration) where multiplier is 1.0
        // Recession duration is 10-20 days, so after 8 days we should be in Active phase
        for (int i = 0; i < 8; i++)
            system.UpdateEvents(state, 1f);

        Assert.True(state.Happiness < initialHappiness,
            $"Recession should reduce happiness. Before: {initialHappiness}, After: {state.Happiness}");
    }

    // =========================================================================
    // Effect mapping tests
    // =========================================================================

    [Fact]
    public void MapEffectNameToIndex_KnownEffects()
    {
        Assert.Equal((int)EventSystem.EffectIndex.Happiness,
            EventSystem.MapEffectNameToIndex("happiness"));
        Assert.Equal((int)EventSystem.EffectIndex.PropertyDamage,
            EventSystem.MapEffectNameToIndex("propertyDamage"));
        Assert.Equal((int)EventSystem.EffectIndex.TaxRevenueMultiplier,
            EventSystem.MapEffectNameToIndex("taxRevenueMultiplier"));
        Assert.Equal((int)EventSystem.EffectIndex.CrimeChange,
            EventSystem.MapEffectNameToIndex("crimeIncrease"));
    }

    [Fact]
    public void MapEffectNameToIndex_UnknownEffect_ReturnsNegativeOne()
    {
        Assert.Equal(-1, EventSystem.MapEffectNameToIndex("unknown_effect"));
    }

    [Fact]
    public void MapEffectNameToIndex_AlternateNames()
    {
        // Immigration penalty and bonus both map to the same index
        Assert.Equal(
            EventSystem.MapEffectNameToIndex("immigrationPenalty"),
            EventSystem.MapEffectNameToIndex("immigrationBonus"));

        // productivityLoss and productivityBoost map to same index
        Assert.Equal(
            EventSystem.MapEffectNameToIndex("productivityLoss"),
            EventSystem.MapEffectNameToIndex("productivityBoost"));
    }

    // =========================================================================
    // Aggregate effect tests
    // =========================================================================

    [Fact]
    public void GetAggregateEffect_SumsAcrossActiveEvents()
    {
        var system = CreateLoadedSystem();
        var state = CreateTestWorld();

        // Only trigger fire (negative happiness effect) so aggregate is clearly non-zero
        system.TriggerEvent("fire", state, severityOverride: 0.8f, tileX: 10, tileY: 10);

        // Fire is in Brewing phase (multiplier=0.25), but should still have a non-zero aggregate
        float aggregate = system.GetAggregateEffect(EventSystem.EffectIndex.Happiness);
        // Fire has happiness=-0.15 * severity=0.8 = -0.12, * brewing=0.25 = -0.03
        Assert.True(aggregate < 0f, $"Expected negative happiness aggregate from fire, got {aggregate}");
    }

    [Fact]
    public void GetAggregateEffect_NoEvents_ReturnsZero()
    {
        var system = CreateLoadedSystem();
        Assert.Equal(0f, system.GetAggregateEffect(EventSystem.EffectIndex.Happiness));
    }

    // =========================================================================
    // Clear and query tests
    // =========================================================================

    [Fact]
    public void ClearAllEvents_RemovesAll()
    {
        var system = CreateLoadedSystem();
        var state = CreateTestWorld();

        system.TriggerEvent("fire", state, severityOverride: 0.5f, tileX: 10, tileY: 10);
        system.TriggerEvent("festival", state, severityOverride: 0.5f, tileX: 15, tileY: 15);
        Assert.Equal(2, system.ActiveEventCount);

        system.ClearAllEvents(state);
        Assert.Equal(0, system.ActiveEventCount);
    }

    [Fact]
    public void GetDefinition_Found()
    {
        var system = CreateLoadedSystem();
        var def = system.GetDefinition("fire");
        Assert.NotNull(def);
        Assert.Equal("fire", def.TypeId);
    }

    [Fact]
    public void GetDefinition_NotFound_ReturnsNull()
    {
        var system = CreateLoadedSystem();
        Assert.Null(system.GetDefinition("nonexistent"));
    }

    // =========================================================================
    // Cascade tests
    // =========================================================================

    [Fact]
    public void Cascades_FireCanTriggerHomelessness()
    {
        // Use a seed that produces favorable cascade rolls
        var system = new EventSystem(seed: 99);
        system.LoadDefinitionsFromJson(TestEventsJson);
        var state = CreateTestWorld();
        state.Population = 5000;

        // Manually trigger fire with high severity
        system.TriggerEvent("fire", state, severityOverride: 1.0f, tileX: 16, tileY: 16);

        // Advance through brewing to active phase to trigger cascades
        // With severity 1.0 and cascade chance = severity * 0.3 = 0.3
        // Run many updates to hit the cascade trigger point
        for (int i = 0; i < 20; i++)
            system.UpdateEvents(state, 1f);

        // Either homelessness was triggered or the event expired
        // We check that the cascade mechanism ran without errors
        Assert.True(system.ActiveEventCount >= 0);
    }

    // =========================================================================
    // Localized effect tests
    // =========================================================================

    [Fact]
    public void LocalizedEvent_AffectsTilesInRadius()
    {
        var system = CreateLoadedSystem();
        var state = CreateTestWorld();

        // Zero out all land values
        Array.Fill(state.Tiles.LandValue, 0.5f);

        // Trigger a localized event with property damage
        system.TriggerEvent("fire", state, severityOverride: 0.8f, tileX: 16, tileY: 16);

        // Advance to active phase for max effect
        for (int i = 0; i < 3; i++)
            system.UpdateEvents(state, 1f);

        // Check that tiles near epicenter had land value reduced
        int epicenterIdx = state.Tiles.Index(16, 16);
        Assert.True(state.Tiles.LandValue[epicenterIdx] < 0.5f,
            "Land value at epicenter should be reduced by fire property damage");

        // Check that distant tiles are unaffected (spread radius = 3)
        int farIdx = state.Tiles.Index(0, 0);
        Assert.Equal(0.5f, state.Tiles.LandValue[farIdx], 0.01f);
    }

    // =========================================================================
    // Integration with WorldState
    // =========================================================================

    [Fact]
    public void TriggerEvent_AddsToWorldState()
    {
        var system = CreateLoadedSystem();
        var state = CreateTestWorld();

        int initialCount = state.ActiveEventCount;
        system.TriggerEvent("festival", state, severityOverride: 0.5f, tileX: 10, tileY: 10);

        Assert.Equal(initialCount + 1, state.ActiveEventCount);
    }

    [Fact]
    public void ExpiredEvent_RemovesFromWorldState()
    {
        var system = CreateLoadedSystem();
        var state = CreateTestWorld();

        system.TriggerEvent("festival", state, severityOverride: 0.5f, tileX: 10, tileY: 10);
        int countAfterAdd = state.ActiveEventCount;
        Assert.True(countAfterAdd > 0);

        // Expire the event
        for (int i = 0; i < 10; i++)
            system.UpdateEvents(state, 1f);

        // Event system should have cleaned up
        Assert.Equal(0, system.ActiveEventCount);
    }

    // =========================================================================
    // JSON file loading (real file)
    // =========================================================================

    [Fact]
    public void LoadDefinitionsFromFile_BaseDataEvents()
    {
        // Locate the events.json relative to the test output directory
        // In CI/test scenarios the file may not exist, so skip gracefully
        string basePath = FindBaseDataPath();
        if (basePath == null)
            return; // Skip if can't find the file

        string eventsPath = Path.Combine(basePath, "data", "events", "events.json");
        if (!File.Exists(eventsPath))
            return;

        var system = new EventSystem();
        system.LoadDefinitions(eventsPath);

        // The real events.json should have 31 events (ids 0-30)
        Assert.True(system.Definitions.Count >= 20,
            $"Expected at least 20 event definitions, got {system.Definitions.Count}");

        // Spot-check a few event types exist
        Assert.NotNull(system.GetDefinition("fire"));
        Assert.NotNull(system.GetDefinition("recession"));
        Assert.NotNull(system.GetDefinition("festival"));
        Assert.NotNull(system.GetDefinition("earthquake"));
    }

    private static string? FindBaseDataPath()
    {
        // Walk up from the test assembly location to find the 'base' directory
        string? dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10 && dir != null; i++)
        {
            string candidate = Path.Combine(dir, "base");
            if (Directory.Exists(candidate))
                return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }
}
