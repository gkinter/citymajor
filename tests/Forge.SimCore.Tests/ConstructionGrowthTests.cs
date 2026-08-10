using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Forge.SimCore;
using Forge.SimWasm;
using Xunit;

namespace Forge.SimCore.Tests;

[Collection("SimHost")]
public sealed class ConstructionGrowthTests
{
    private const string FastConstructionLawJson = """
        [
          {
            "id": "fast_construction",
            "name": "Fast Build Permits",
            "category": "planning",
            "era_min": "frontier",
            "parameters": [],
            "effects": {
              "construction_speed": 1.0
            },
            "faction_reactions": {},
            "cost_monthly": 0,
            "compliance_base": 0.9,
            "tech_prerequisite": null,
            "description": "Accelerated permitting"
          }
        ]
        """;

    [Fact]
    public void ZonedTiles_AfterDailyTicks_ShowConstructionOrGrowth()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        host.State.Population = 800;
        host.State.CityFunds = 100_000;

        int center = 32;
        for (int y = center - 6; y <= center + 6; y++)
        {
            host.PlaceRoad(center, y);
            for (int x = center - 6; x <= center + 6; x++)
                host.PaintZone(x, y, zoneType: 1);
        }

        int buildingsBefore = host.State.Buildings.Count;

        for (int day = 0; day < 45; day++)
            host.Tick(WasmConfig.GameDayInterval);

        var snap = host.GetSnapshot();
        bool grew = host.State.Buildings.Count > buildingsBefore;
        bool hadConstructing = snap.ConstructingBuildingCount > 0;
        bool hasOperational = grew && CountOperational(host.State) > 0;

        Assert.True(grew || hadConstructing || hasOperational,
            "expected zone growth to spawn constructing and/or operational buildings");
    }

    [Fact]
    public void ProcessConstruction_AdvancesConditionEachDay()
    {
        var growth = new ZoneGrowthSystem(16, seed: 1);
        var state = new WorldState(16, maxBuildings: 16);
        var economy = new EconomySystem();

        int slot = state.Buildings.Allocate();
        state.Buildings.GridX[slot] = 5;
        state.Buildings.GridY[slot] = 5;
        state.Buildings.Width[slot] = 1;
        state.Buildings.Height[slot] = 1;
        state.Buildings.TypeId[slot] = 100;
        state.Buildings.Level[slot] = 1;
        state.Buildings.State[slot] = 0;
        state.Buildings.Condition[slot] = 0;
        state.Buildings.MaxOccupants[slot] = 4;
        state.Tiles.ZoneType[state.Tiles.Index(5, 5)] = 1;
        state.Tiles.BuildingId[state.Tiles.Index(5, 5)] = (ushort)slot;

        growth.Tick(state, economy);

        Assert.True(state.Buildings.Condition[slot] > 0);
        Assert.Equal(0, state.Buildings.State[slot]);
        Assert.Equal(1, state.ConstructingBuildingCount);
    }

    [Fact]
    public void LawConstructionSpeed_ReducesConstructionDuration()
    {
        int slowDays = ZoneGrowthSystem.GetConstructionDays(zoneType: 1, level: 1, lawSpeedMult: 1f);
        int fastDays = ZoneGrowthSystem.GetConstructionDays(zoneType: 1, level: 1, lawSpeedMult: 4f);

        Assert.True(fastDays < slowDays,
            $"construction_speed law should shorten build time (slow={slowDays}, fast={fastDays})");

        int operationalAfterSlow = DaysUntilOperationalForPlacedBuilding(lawSpeedMult: 1f);
        int operationalAfterFast = DaysUntilOperationalForPlacedBuilding(lawSpeedMult: 4f);
        Assert.True(operationalAfterFast < operationalAfterSlow);
    }

    [Fact]
    public void SetLawActive_RecomputesConstructionSpeedMultiplier()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });
        host.Laws.LoadFromJson(FastConstructionLawJson);

        Assert.Equal(1f, host.State.LawConstructionSpeedMult);

        Assert.True(host.SetLawActive("fast_construction", true));
        Assert.Equal(2f, host.State.LawConstructionSpeedMult, 3);

        var snap = host.GetSnapshot();
        Assert.Equal(2f, snap.LawConstructionSpeedMult, 3);
    }

    [Fact]
    public void GetConstructionDays_IndustrialLongerThanResidentialLow()
    {
        int resLow = ZoneGrowthSystem.GetConstructionDays(zoneType: 1, level: 1, lawSpeedMult: 1f);
        int industrial = ZoneGrowthSystem.GetConstructionDays(zoneType: 4, level: 1, lawSpeedMult: 1f);

        Assert.True(industrial > resLow);
    }

    private static int DaysUntilOperationalForPlacedBuilding(float lawSpeedMult)
    {
        var growth = new ZoneGrowthSystem(16, seed: 1);
        var state = new WorldState(16, maxBuildings: 16);
        var economy = new EconomySystem();
        state.LawConstructionSpeedMult = lawSpeedMult;

        int slot = state.Buildings.Allocate();
        state.Buildings.GridX[slot] = 4;
        state.Buildings.GridY[slot] = 4;
        state.Buildings.Width[slot] = 1;
        state.Buildings.Height[slot] = 1;
        state.Buildings.TypeId[slot] = 100;
        state.Buildings.Level[slot] = 1;
        state.Buildings.State[slot] = 0;
        state.Buildings.Condition[slot] = 0;
        state.Buildings.MaxOccupants[slot] = 4;
        state.Tiles.ZoneType[state.Tiles.Index(4, 4)] = 1;
        state.Tiles.BuildingId[state.Tiles.Index(4, 4)] = (ushort)slot;

        int days = 0;
        while (state.Buildings.State[slot] == 0 && days < 30)
        {
            growth.Tick(state, economy);
            days++;
        }

        Assert.Equal(1, state.Buildings.State[slot]);
        return days;
    }

    private static int CountOperational(WorldState state)
    {
        int count = 0;
        for (int i = 0; i < state.Buildings.Capacity; i++)
        {
            if (!state.Buildings.IsActive(i)) continue;
            if (state.Buildings.State[i] == 1) count++;
        }

        return count;
    }
}
