using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Xunit;

namespace Forge.SimCore.Tests;

/// <summary>Cathedral P2.4 — housing Herald trigger characterization tests.</summary>
public sealed class HousingHeraldTests
{
    private const string HousingEventsJson = """
    [
      {
        "id": "housing_crisis",
        "name": "Housing Crisis",
        "category": "social",
        "era_min": "frontier",
        "severity": 3,
        "base_probability": 0,
        "duration_months": 6,
        "effects": { "happiness": -15 },
        "description": "Skyrocketing rents push families to the margins."
      },
      {
        "id": "housing_shortage",
        "name": "Housing Shortage",
        "category": "social",
        "era_min": "frontier",
        "severity": 2,
        "base_probability": 0,
        "duration_months": 6,
        "effects": { "happiness": -10 },
        "description": "Vacancy collapsed while demand stays hot."
      }
    ]
    """;

    private static (WorldState state, EventSystem events, HousingHeraldSystem herald) CreateHarness()
    {
        var state = new WorldState(32);
        var events = new EventSystem(seed: 7);
        events.LoadDefinitionsFromJson(HousingEventsJson);
        return (state, events, new HousingHeraldSystem());
    }

    [Fact]
    public void HousingCrisis_Fires_AfterTwoMonthsHighMeanBurden()
    {
        var (state, events, herald) = CreateHarness();

        herald.MonthlyTick(state, events, meanRentBurden: 0.55f, residentialDemand: 0f);
        Assert.False(events.IsEventTypeActive("housing_crisis"));

        herald.MonthlyTick(state, events, meanRentBurden: 0.56f, residentialDemand: 0f);
        Assert.True(events.IsEventTypeActive("housing_crisis"));
    }

    [Fact]
    public void HousingCrisis_ResetsStreak_WhenBurdenFalls()
    {
        var (state, events, herald) = CreateHarness();

        herald.MonthlyTick(state, events, meanRentBurden: 0.55f, residentialDemand: 0f);
        herald.MonthlyTick(state, events, meanRentBurden: 0.40f, residentialDemand: 0f);
        herald.MonthlyTick(state, events, meanRentBurden: 0.55f, residentialDemand: 0f);

        Assert.False(events.IsEventTypeActive("housing_crisis"));
    }

    [Fact]
    public void HousingShortage_Fires_WhenVacancyLowAndDemandHigh()
    {
        var (state, events, herald) = CreateHarness();

        PlaceResidentialBuilding(state, x: 10, y: 10, maxOccupants: 100, occupants: 98);
        Assert.True(HousingHeraldSystem.CalculateCityVacancy(state) < 0.05f);

        herald.MonthlyTick(state, events, meanRentBurden: 0.2f, residentialDemand: 0.5f);
        Assert.False(events.IsEventTypeActive("housing_shortage"));

        herald.MonthlyTick(state, events, meanRentBurden: 0.2f, residentialDemand: 0.5f);
        Assert.True(events.IsEventTypeActive("housing_shortage"));
    }

    [Fact]
    public void HousingShortage_DoesNotFire_WhenVacancyHealthy()
    {
        var (state, events, herald) = CreateHarness();

        PlaceResidentialBuilding(state, x: 10, y: 10, maxOccupants: 100, occupants: 50);

        herald.MonthlyTick(state, events, meanRentBurden: 0.2f, residentialDemand: 0.8f);
        herald.MonthlyTick(state, events, meanRentBurden: 0.2f, residentialDemand: 0.8f);

        Assert.False(events.IsEventTypeActive("housing_shortage"));
    }

    [Fact]
    public void HousingCrisis_Integration_FiresFromSimHostRentPressure()
    {
        var host = CreateSimHostWithData();
        host.State.Population = 1200;
        host.RestoreHouseholds(1200, 300);

        int center = 32;
        for (int y = center - 4; y <= center + 4; y++)
        {
            host.PlaceRoad(center, y);
            for (int x = center - 4; x <= center + 4; x++)
                host.PaintZone(x, y, zoneType: 3);
        }

        AdvanceMonths(host, months: 3);

        Assert.True(host.GetSnapshot().MeanRentBurden > HousingHeraldSystem.HousingCrisisBurdenThreshold);
        Assert.True(host.Events.IsEventTypeActive("housing_crisis"),
            "Expected housing_crisis Herald after sustained high mean rent burden.");
    }

    [Fact]
    public void HousingShortage_Integration_FiresFromPackedResidentialSupply()
    {
        var host = CreateSimHostWithData();
        host.State.Population = 2000;
        host.RestoreHouseholds(2000, 400);

        for (int i = 0; i < 6; i++)
        {
            int x = 20 + i;
            host.PaintZone(x, 20, zoneType: 1);
            host.PaintZone(x, 21, zoneType: 1);
        }

        SeedPackedResidentialBuildings(host.State, startX: 20, startY: 20, count: 6);

        AdvanceMonths(host, months: 3);

        Assert.True(host.Economy.ResidentialDemand > HousingHeraldSystem.HousingShortageDemandThreshold);
        Assert.True(
            HousingHeraldSystem.CalculateCityVacancy(host.State)
                < HousingHeraldSystem.HousingShortageVacancyThreshold);
        Assert.True(host.Events.IsEventTypeActive("housing_shortage"),
            "Expected housing_shortage Herald when vacancy is tight and residential demand is high.");
    }

    private static SimHost CreateSimHostWithData()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions
        {
            SkipStarterCity = true,
            DataPaths = SimDataPaths.FromContentRoot(FindRepoRoot()),
        });
        return host;
    }

    private static void AdvanceMonths(SimHost host, int months)
    {
        const int ticksPerMonth = 30;
        for (int month = 0; month < months; month++)
        {
            for (int day = 0; day < ticksPerMonth; day++)
                host.Tick(1.0);
        }
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

    private static void PlaceResidentialBuilding(
        WorldState state,
        int x,
        int y,
        ushort maxOccupants,
        ushort occupants)
    {
        int slot = state.Buildings.Allocate();
        Assert.True(slot >= 0);

        state.Buildings.GridX[slot] = x;
        state.Buildings.GridY[slot] = y;
        state.Buildings.Width[slot] = 1;
        state.Buildings.Height[slot] = 1;
        state.Buildings.Level[slot] = 1;
        state.Buildings.State[slot] = 1;
        state.Buildings.MaxOccupants[slot] = maxOccupants;
        state.Buildings.Occupants[slot] = occupants;
        state.Buildings.Condition[slot] = 255;

        int idx = state.Tiles.Index(x, y);
        state.Tiles.ZoneType[idx] = 1;
        state.Tiles.BuildingId[idx] = (ushort)slot;
    }

    private static void SeedPackedResidentialBuildings(
        WorldState state,
        int startX,
        int startY,
        int count)
    {
        for (int i = 0; i < count; i++)
        {
            int x = startX + i;
            int y = startY;
            int slot = state.Buildings.Allocate();
            if (slot < 0) break;

            state.Buildings.GridX[slot] = x;
            state.Buildings.GridY[slot] = y;
            state.Buildings.Width[slot] = 1;
            state.Buildings.Height[slot] = 1;
            state.Buildings.Level[slot] = 1;
            state.Buildings.State[slot] = 1;
            state.Buildings.MaxOccupants[slot] = 80;
            state.Buildings.Occupants[slot] = 78;
            state.Buildings.Condition[slot] = 255;

            int idx = state.Tiles.Index(x, y);
            state.Tiles.ZoneType[idx] = 1;
            state.Tiles.BuildingId[idx] = (ushort)slot;

            int slot2 = state.Buildings.Allocate();
            if (slot2 < 0) continue;

            state.Buildings.GridX[slot2] = x;
            state.Buildings.GridY[slot2] = y + 1;
            state.Buildings.Width[slot2] = 1;
            state.Buildings.Height[slot2] = 1;
            state.Buildings.Level[slot2] = 1;
            state.Buildings.State[slot2] = 1;
            state.Buildings.MaxOccupants[slot2] = 80;
            state.Buildings.Occupants[slot2] = 78;
            state.Buildings.Condition[slot2] = 255;

            int idx2 = state.Tiles.Index(x, y + 1);
            state.Tiles.ZoneType[idx2] = 1;
            state.Tiles.BuildingId[idx2] = (ushort)slot2;
        }
    }
}
