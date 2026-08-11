using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Forge.SimCore;
using Xunit;

namespace Forge.SimCore.Tests;

/// <summary>Characterization tests for Cathedral P2 housing / rent burden / abandonment.</summary>
public sealed class CathedralHousingTests
{
    [Fact]
    public void MeanRentBurden_MatchesManualRollupFromSeededHouseholds()
    {
        var host = new SimHost();
        host.Init(64);

        for (int i = 0; i < 90; i++)
            host.Tick(1.0);

        var snap = host.GetSnapshot();
        float manual = host.Population.AuditMeanRentBurden(host.State);
        float expectedVacancy = HousingHeraldSystem.CalculateCityVacancy(host.State);

        Assert.Equal(manual, snap.MeanRentBurden, precision: 4);
        Assert.Equal(manual, host.State.MeanRentBurden, precision: 4);
        Assert.Equal(expectedVacancy, snap.ResidentialVacancy, precision: 4);
        Assert.Equal(expectedVacancy, host.State.ResidentialVacancy, precision: 4);
    }

    [Fact]
    public void RentBurden_Increases_WhenResidentialDemandHighAndSupplyLow()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        host.State.Population = 1200;
        host.RestoreHouseholds(1200, 300);

        int center = 32;
        for (int y = center - 4; y <= center + 4; y++)
        {
            host.PlaceRoad(center, y);
            for (int x = center - 4; x <= center + 4; x++)
                host.PaintZone(x, y, zoneType: 3); // commercial only — no residential supply
        }

        for (int i = 0; i < 90; i++)
            host.Tick(1.0);

        var snap = host.GetSnapshot();
        Assert.True(snap.MeanRentBurden > 0.35f,
            "Expected elevated rent burden when housing supply lags population demand.");
    }

    [Fact]
    public void MigrationOut_WhenRentBurdenAboveThreshold()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        host.State.Population = 800;
        host.RestoreHouseholds(800, 200);

        int popBefore = host.State.Population;

        for (int i = 0; i < 180; i++)
            host.Tick(1.0);

        Assert.True(host.State.Population < popBefore,
            "Expected net emigration when rent burden exceeds threshold for multiple months.");
    }

    [Fact]
    public void ComputeDeclineChance_RisesWithPollutionCrimeAndNegativeDemand()
    {
        float healthy = ZoneGrowthSystem.ComputeDeclineChance(demand: 0.5f, pollution: 0f, crime: 0f);
        float polluted = ZoneGrowthSystem.ComputeDeclineChance(demand: 0f, pollution: 1f, crime: 0f);
        float crime = ZoneGrowthSystem.ComputeDeclineChance(demand: 0f, pollution: 0f, crime: 1f);
        float collapsing = ZoneGrowthSystem.ComputeDeclineChance(demand: -1f, pollution: 1f, crime: 1f);

        Assert.Equal(0f, healthy);
        Assert.True(polluted > 0f);
        Assert.True(crime > 0f);
        Assert.True(collapsing >= polluted);
        Assert.True(collapsing <= 0.1f);
    }

    [Fact]
    public void Tick_HighPollutionCrime_AbandonsBuildingAndExportsCount()
    {
        var state = new WorldState(32);
        Array.Fill(state.Tiles.TerrainType, (byte)0);
        state.Buildings.Allocate(); // burn slot 0

        int slot = state.Buildings.Allocate();
        Assert.True(slot > 0);
        state.Buildings.GridX[slot] = 10;
        state.Buildings.GridY[slot] = 10;
        state.Buildings.Width[slot] = 1;
        state.Buildings.Height[slot] = 1;
        state.Buildings.Level[slot] = 1;
        state.Buildings.State[slot] = 1; // operational
        state.Buildings.MaxOccupants[slot] = 20;
        state.Buildings.Occupants[slot] = 10;
        state.Buildings.Condition[slot] = 255;
        state.Buildings.TypeId[slot] = 1;

        int idx = state.Tiles.Index(10, 10);
        state.Tiles.BuildingId[idx] = (ushort)slot;
        state.Tiles.ZoneType[idx] = 1; // residential
        state.Tiles.Pollution[idx] = 1f;
        state.Tiles.Crime[idx] = 1f;
        state.Tiles.RoadFlags[state.Tiles.Index(10, 9)] = 1;

        // Oversupply housing vs jobs so residential demand is negative.
        for (int i = 0; i < 8; i++)
        {
            int extra = state.Buildings.Allocate();
            if (extra < 0) break;
            int x = 12 + i;
            state.Buildings.GridX[extra] = x;
            state.Buildings.GridY[extra] = 10;
            state.Buildings.Width[extra] = 1;
            state.Buildings.Height[extra] = 1;
            state.Buildings.Level[extra] = 1;
            state.Buildings.State[extra] = 1;
            state.Buildings.MaxOccupants[extra] = 40;
            state.Buildings.Occupants[extra] = 2;
            state.Buildings.Condition[extra] = 255;
            state.Buildings.TypeId[extra] = 1;
            int eIdx = state.Tiles.Index(x, 10);
            state.Tiles.BuildingId[eIdx] = (ushort)extra;
            state.Tiles.ZoneType[eIdx] = 1;
            state.Tiles.Pollution[eIdx] = 1f;
            state.Tiles.Crime[eIdx] = 1f;
        }

        var growth = new ZoneGrowthSystem(32, seed: 42);
        var economy = new EconomySystem();

        bool sawAbandoned = false;
        for (int day = 0; day < 80; day++)
        {
            growth.Tick(state, economy);
            if (state.AbandonedBuildingCount > 0)
            {
                sawAbandoned = true;
                break;
            }
        }

        Assert.True(sawAbandoned, "Expected pollution/crime + negative demand to abandon at least one building.");
        Assert.Equal(ZoneGrowthSystem.CountAbandonedBuildings(state), state.AbandonedBuildingCount);
    }

    [Fact]
    public void AbandonedBuildingCount_ExportedOnSimHostSnapshot()
    {
        var host = new SimHost();
        host.Init(32, new SimHostInitOptions { SkipStarterCity = true });

        int slot = host.State.Buildings.Allocate();
        Assert.True(slot > 0);
        host.State.Buildings.GridX[slot] = 8;
        host.State.Buildings.GridY[slot] = 8;
        host.State.Buildings.Width[slot] = 1;
        host.State.Buildings.Height[slot] = 1;
        host.State.Buildings.Level[slot] = 1;
        host.State.Buildings.State[slot] = 2; // abandoned
        host.State.Buildings.MaxOccupants[slot] = 10;
        host.State.Buildings.Occupants[slot] = 0;
        host.State.Buildings.Condition[slot] = 200;
        host.State.Buildings.TypeId[slot] = 1;
        int idx = host.State.Tiles.Index(8, 8);
        host.State.Tiles.BuildingId[idx] = (ushort)slot;
        host.State.Tiles.ZoneType[idx] = 1;
        host.State.AbandonedBuildingCount = ZoneGrowthSystem.CountAbandonedBuildings(host.State);

        var snap = host.GetSnapshot();
        Assert.True(snap.AbandonedBuildingCount >= 1);
        Assert.Equal(host.State.AbandonedBuildingCount, snap.AbandonedBuildingCount);
        Assert.Contains(snap.Buildings, b => b.State == 2);
    }
}
