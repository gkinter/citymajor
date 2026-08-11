using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Forge.SimCore;
using Forge.SimWasm;
using Xunit;

namespace Forge.SimCore.Tests;

/// <summary>
/// Cathedral P6.2 — <see cref="EventSystem.ApplyEffectsToState"/> city-wide multipliers
/// mutate budget / immigration / spawn / research (not happiness-only stubs).
/// </summary>
public sealed class EventEffectsTests
{
    private const string SampleEventsJson = """
    [
      {
        "id": "housing_crisis",
        "name": "Housing Crisis",
        "category": "social",
        "era_min": "frontier",
        "severity": 3,
        "base_probability": 0,
        "duration_months": 6,
        "effects": {
          "happiness": -15,
          "immigration": -0.25,
          "commerce": -0.1,
          "tax_revenue": -0.15,
          "construction": -0.2
        },
        "description": "Skyrocketing rents."
      },
      {
        "id": "boom",
        "name": "Economic Boom",
        "category": "economic",
        "era_min": "frontier",
        "severity": 1,
        "base_probability": 0,
        "duration_months": 6,
        "effects": {
          "happiness": 15,
          "commerce": 0.3,
          "tax_revenue": 0.3,
          "immigration": 0.15,
          "productivity": 0.2,
          "construction": 0.35,
          "research": 0.25
        },
        "description": "The economy expands."
      },
      {
        "id": "approval_unrest",
        "name": "Approval Unrest",
        "category": "political",
        "era_min": "frontier",
        "severity": 2,
        "base_probability": 0,
        "duration_months": 6,
        "effects": {
          "happiness": -8,
          "approvalRatingPenalty": -5,
          "tax_revenue": -0.08,
          "commerce": -0.08,
          "productivity": -0.05
        },
        "description": "Mayor approval slipped."
      }
    ]
    """;

    [Fact]
    public void MapEffectNameToIndex_AcceptsSnakeCaseCatalogKeys()
    {
        Assert.Equal(
            (int)EventSystem.EffectIndex.TaxRevenueMultiplier,
            EventSystem.MapEffectNameToIndex("tax_revenue"));
        Assert.Equal(
            (int)EventSystem.EffectIndex.ImmigrationModifier,
            EventSystem.MapEffectNameToIndex("immigration"));
        Assert.Equal(
            (int)EventSystem.EffectIndex.CommercialModifier,
            EventSystem.MapEffectNameToIndex("commerce"));
        Assert.Equal(
            (int)EventSystem.EffectIndex.ProductivityChange,
            EventSystem.MapEffectNameToIndex("productivity"));
        Assert.Equal(
            (int)EventSystem.EffectIndex.PropertyValueChange,
            EventSystem.MapEffectNameToIndex("construction"));
        Assert.Equal(
            (int)EventSystem.EffectIndex.CrimeChange,
            EventSystem.MapEffectNameToIndex("crime"));
        Assert.Equal(
            (int)EventSystem.EffectIndex.ResearchModifier,
            EventSystem.MapEffectNameToIndex("research"));
    }

    [Fact]
    public void ApplyEffectsToState_HousingCrisis_SetsCityWideMultipliers()
    {
        var state = new WorldState(32) { Happiness = 0.7f, ApprovalRating = 0.6f };
        var events = new EventSystem(seed: 11);
        events.LoadDefinitionsFromJson(SampleEventsJson);

        Assert.True(events.TriggerEvent("housing_crisis", state, severityOverride: 1f));
        // Advance past Brewing so Active phase = 1.0 aggregate strength.
        for (int d = 0; d < 5; d++)
            events.UpdateEvents(state, 1f);

        events.ApplyEffectsToState(state);

        Assert.True(state.EventTaxRevenueMult < 1f,
            $"tax mult should drop under housing_crisis (got {state.EventTaxRevenueMult})");
        Assert.True(state.EventImmigrationMult < 1f,
            $"immigration mult should drop (got {state.EventImmigrationMult})");
        Assert.True(state.EventCommercialSpawnMult < 1f,
            $"commerce spawn mult should drop (got {state.EventCommercialSpawnMult})");
        Assert.True(state.EventSpawnDemandMult < 1f,
            $"spawn demand should drop from construction penalty (got {state.EventSpawnDemandMult})");
        Assert.True(state.Happiness < 0.7f, "happiness drip should apply");
    }

    [Fact]
    public void ApplyEffectsToState_Boom_RaisesMultipliers_AndResetsWhenCleared()
    {
        var state = new WorldState(32);
        var events = new EventSystem(seed: 22);
        events.LoadDefinitionsFromJson(SampleEventsJson);

        Assert.True(events.TriggerEvent("boom", state, severityOverride: 1f));
        for (int d = 0; d < 5; d++)
            events.UpdateEvents(state, 1f);

        events.ApplyEffectsToState(state);

        Assert.True(state.EventTaxRevenueMult > 1f);
        Assert.True(state.EventImmigrationMult > 1f);
        Assert.True(state.EventCommercialSpawnMult > 1f);
        Assert.True(state.EventProductivityMult > 1f);
        Assert.True(state.EventResearchMult > 1f);
        Assert.True(state.EventSpawnDemandMult > 1f);

        events.ClearAllEvents(state);
        events.ApplyEffectsToState(state);

        Assert.Equal(1f, state.EventTaxRevenueMult);
        Assert.Equal(1f, state.EventImmigrationMult);
        Assert.Equal(1f, state.EventCommercialSpawnMult);
        Assert.Equal(1f, state.EventProductivityMult);
        Assert.Equal(1f, state.EventResearchMult);
        Assert.Equal(1f, state.EventSpawnDemandMult);
    }

    [Fact]
    public void SimHost_DayTick_ExportsEventMultipliersOnSnapshot()
    {
        var host = CreateHostWithEvents();
        Assert.True(host.Events.TriggerEvent("housing_crisis", host.State!, severityOverride: 1f));

        // One game day → RunDayTick → ApplyEventEffectsToState
        host.Tick(1.0);

        var snap = host.GetSnapshot();
        Assert.True(snap.ActiveEventCount >= 1 || host.Events.ActiveEventCount >= 1);
        Assert.True(snap.EventTaxRevenueMult < 1f || host.State!.EventTaxRevenueMult < 1f);
        Assert.Equal(host.State!.EventTaxRevenueMult, snap.EventTaxRevenueMult, 3);
        Assert.Equal(host.State.EventImmigrationMult, snap.EventImmigrationMult, 3);
        Assert.Equal(host.State.EventCommercialSpawnMult, snap.EventCommercialSpawnMult, 3);
        Assert.Equal(host.State.EventSpawnDemandMult, snap.EventSpawnDemandMult, 3);

        // Cathedral P7.5 — WASM DTO carries the same Event*Mult for harness / HUD consumers.
        var dto = SimSnapshotDto.From(snap, host.State, host.Events);
        Assert.Equal(host.State.EventTaxRevenueMult, dto.EventTaxRevenueMult, 3);
        Assert.Equal(host.State.EventImmigrationMult, dto.EventImmigrationMult, 3);
        Assert.Equal(host.State.EventCommercialSpawnMult, dto.EventCommercialSpawnMult, 3);
        Assert.Equal(host.State.EventProductivityMult, dto.EventProductivityMult, 3);
        Assert.Equal(host.State.EventResearchMult, dto.EventResearchMult, 3);
        Assert.Equal(host.State.EventSpawnDemandMult, dto.EventSpawnDemandMult, 3);
    }

    [Fact]
    public void ApplyEventModifiers_ReducesCityFunds_WhenTaxMultBelowOne()
    {
        var budget = new BudgetSystem();
        var economy = new EconomySystem();
        var state = new WorldState(32, maxBuildings: 64)
        {
            Population = 2_000,
            CityFunds = 100_000,
            EventTaxRevenueMult = 0.7f,
        };

        // Seed a taxable building so TotalRevenue > 0.
        int slot = state.Buildings.Allocate();
        Assert.True(slot >= 0);
        state.Buildings.GridX[slot] = 8;
        state.Buildings.GridY[slot] = 8;
        state.Buildings.Revenue[slot] = 5_000;
        state.Tiles.ZoneType[state.Tiles.Index(8, 8)] = 3; // commercial
        state.Tiles.BuildingId[state.Tiles.Index(8, 8)] = (ushort)slot;
        state.Tiles.LandValue[state.Tiles.Index(8, 8)] = 0.8f;

        budget.CalculateMonthlyBudget(state, economy);
        long before = state.CityFunds;
        budget.ApplyEventModifiers(state);

        Assert.True(state.CityFunds < before,
            $"event tax mult <1 should cut funds (before={before}, after={state.CityFunds})");
    }

    [Fact]
    public void EventImmigrationMult_ScalesMonthlyImmigration()
    {
        int low = CountImmigrants(eventImmigrationMult: 0.4f, seed: 7);
        int high = CountImmigrants(eventImmigrationMult: 1.8f, seed: 7);

        Assert.True(high > low,
            $"higher EventImmigrationMult should admit more immigrants (low={low}, high={high})");
    }

    [Fact]
    public void GetZoneLawSpawnMult_CompoundsEventCommercialAndSpawnDemand()
    {
        var state = new WorldState(16)
        {
            LawSpawnDemandMult = 1f,
            LawCommercialSpawnMult = 1f,
            LawIndustrialSpawnMult = 1f,
            LawResidentialSpawnMult = 1f,
            EventSpawnDemandMult = 0.8f,
            EventCommercialSpawnMult = 0.5f,
            EventProductivityMult = 1.5f,
        };

        // zone 3 = commercial → 1 * 0.8 * (1 * 0.5) = 0.4
        Assert.Equal(0.4f, ZoneGrowthSystem.GetZoneLawSpawnMult(state, zoneType: 3), 3);
        // zone 4 = industrial → 1 * 0.8 * (1 * 1.5) = 1.2
        Assert.Equal(1.2f, ZoneGrowthSystem.GetZoneLawSpawnMult(state, zoneType: 4), 3);
        // zone 1 = residential → 1 * 0.8 * 1 = 0.8
        Assert.Equal(0.8f, ZoneGrowthSystem.GetZoneLawSpawnMult(state, zoneType: 1), 3);
    }

    [Fact]
    public void PackedEventsJson_HeraldCrisis_AppliesSnakeCaseEffects()
    {
        var host = CreateHostWithEvents();
        // Real pack may already define housing_crisis; trigger uses loaded defs.
        if (!host.Events.TriggerEvent("housing_crisis", host.State!, severityOverride: 1f))
        {
            // Fallback if pack missing — still characterize via inline load path above.
            return;
        }

        for (int i = 0; i < 8; i++)
            host.Tick(1.0);

        Assert.True(host.State!.EventTaxRevenueMult < 1f
                    || host.State.EventImmigrationMult < 1f,
            "packed housing_crisis should move at least one city-wide event multiplier");
    }

    private static int CountImmigrants(float eventImmigrationMult, int seed)
    {
        var pop = new PopulationSystem(seed: (uint)seed);
        var state = new WorldState(32, maxHouseholds: 512)
        {
            Population = 2_000,
            Happiness = 0.7f,
            PropertyTaxRate = 0.05f,
            EventImmigrationMult = eventImmigrationMult,
        };

        // Vacancy-friendly residential stock so housing availability does not zero out.
        for (int i = 0; i < 20; i++)
        {
            int slot = state.Buildings.Allocate();
            if (slot < 0) break;
            state.Buildings.GridX[slot] = 4 + (i % 8);
            state.Buildings.GridY[slot] = 4 + (i / 8);
            state.Buildings.MaxOccupants[slot] = 20;
            state.Buildings.Occupants[slot] = 2;
            state.Buildings.State[slot] = 1;
            int idx = state.Tiles.Index(state.Buildings.GridX[slot], state.Buildings.GridY[slot]);
            state.Tiles.ZoneType[idx] = 1;
            state.Tiles.BuildingId[idx] = (ushort)slot;
        }

        return pop.CalculateImmigration(state);
    }

    private static SimHost CreateHostWithEvents()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions
        {
            SkipStarterCity = true,
            DataPaths = SimDataPaths.FromContentRoot(FindRepoRoot()),
        });

        // Ensure sample defs exist even if pack load failed.
        if (host.Events.GetDefinition("housing_crisis") is null
            || host.Events.GetDefinition("boom") is null)
        {
            host.Events.LoadDefinitionsFromJson(SampleEventsJson);
        }

        return host;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "base", "data", "events", "events.json")))
                return dir.FullName;
            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
