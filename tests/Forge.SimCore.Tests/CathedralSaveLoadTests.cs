using System.Text.Json;
using Forge.Engine.Data;
using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Forge.SimCore;
using Forge.SimWasm;
using Xunit;

namespace Forge.SimCore.Tests;

/// <summary>
/// Characterization: Cathedral HUD metrics that are exported on the save API
/// (<see cref="SimHost.GetSnapshotJson"/> / <see cref="SimHost.LoadSnapshotFromJson"/>)
/// must survive snapshot restore — MeanRentBurden, councilSeats, mode shares,
/// plus Event*Mult / delivery delay / abandoned / fire / EMS / L2 sample.
/// </summary>
[Collection("SimHost")]
public sealed class CathedralSaveLoadTests
{
    private const uint ServiceFire = 1u << 3;

    [Fact]
    public void SnapshotRoundTrip_PreservesMeanRentBurdenCouncilSeatsAndModeShares()
    {
        var host = new SimHost();
        host.Init(64);

        for (int i = 0; i < 8; i++)
            host.Tick(WasmConfig.TrafficLiteInterval);
        for (int i = 0; i < 30; i++)
            host.Tick(1.0);

        // Mutate seats away from boot defaults so restore cannot pass by Init coincidence.
        Assert.Equal(PoliticsSystem.CouncilSeatCount, host.Politics.CouncilSeats.Length);
        host.Politics.CouncilSeats[0] = (byte)PoliticsSystem.FactionId.Intelligentsia;
        host.Politics.CouncilSeats[1] = (byte)PoliticsSystem.FactionId.Religious;
        host.Politics.CouncilSeats[2] = (byte)PoliticsSystem.FactionId.Newcomers;
        host.Politics.SyncCouncilToWorld(host.State!);

        string json = host.GetSnapshotJson();
        var saved = JsonSerializer.Deserialize(json, SnapshotJsonContext.Default.SimSnapshotDto);
        Assert.NotNull(saved);
        Assert.True(saved!.MeanRentBurden > 0f, "expected non-zero MeanRentBurden in save payload");
        Assert.Equal(PoliticsSystem.CouncilSeatCount, saved.CouncilSeats.Length);
        Assert.Equal((int)PoliticsSystem.FactionId.Intelligentsia, saved.CouncilSeats[0]);
        Assert.Equal((int)PoliticsSystem.FactionId.Religious, saved.CouncilSeats[1]);
        Assert.Equal((int)PoliticsSystem.FactionId.Newcomers, saved.CouncilSeats[2]);
        Assert.InRange(
            saved.CarModeShare + saved.TransitModeShare + saved.WalkModeShare,
            0.999f, 1.001f);
        Assert.True(saved.TransitModeShare > 0f || saved.WalkModeShare > 0f,
            "expected non-default mode split so restore is observable");

        using (var doc = JsonDocument.Parse(json))
        {
            Assert.True(doc.RootElement.TryGetProperty("meanRentBurden", out _));
            Assert.True(doc.RootElement.TryGetProperty("councilSeats", out _));
            Assert.True(doc.RootElement.TryGetProperty("carModeShare", out _));
            Assert.True(doc.RootElement.TryGetProperty("transitModeShare", out _));
            Assert.True(doc.RootElement.TryGetProperty("walkModeShare", out _));
        }

        Assert.True(host.LoadSnapshotFromJson(json));

        Assert.Equal(saved.MeanRentBurden, host.State!.MeanRentBurden, precision: 4);
        Assert.Equal(saved.MeanRentBurden, host.Population.MeanRentBurden, precision: 4);

        for (int i = 0; i < PoliticsSystem.CouncilSeatCount; i++)
        {
            Assert.Equal(saved.CouncilSeats[i], host.Politics.CouncilSeats[i]);
            Assert.Equal(saved.CouncilSeats[i], host.State.CouncilSeats[i]);
        }

        var (carAfter, transitAfter, walkAfter) = host.CollectModeShares();
        Assert.Equal(saved.CarModeShare, carAfter, precision: 4);
        Assert.Equal(saved.TransitModeShare, transitAfter, precision: 4);
        Assert.Equal(saved.WalkModeShare, walkAfter, precision: 4);

        var roundTrip = JsonSerializer.Deserialize(
            host.GetSnapshotJson(),
            SnapshotJsonContext.Default.SimSnapshotDto);
        Assert.NotNull(roundTrip);
        Assert.Equal(saved.MeanRentBurden, roundTrip!.MeanRentBurden, precision: 4);
        Assert.Equal(saved.CouncilSeats, roundTrip.CouncilSeats);
        Assert.Equal(saved.CarModeShare, roundTrip.CarModeShare, precision: 4);
        Assert.Equal(saved.TransitModeShare, roundTrip.TransitModeShare, precision: 4);
        Assert.Equal(saved.WalkModeShare, roundTrip.WalkModeShare, precision: 4);
    }

    [Fact]
    public void SnapshotRoundTrip_PreservesEventMultsDeliveryAbandonedFireAndL2Sample()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        var state = host.State!;
        for (int y = 8; y <= 14; y++)
        for (int x = 8; x <= 14; x++)
            state.Tiles.ZoneType[state.Tiles.Index(x, y)] = 1;

        int home = PlaceBuilding(state, 10, 10, serviceFlags: 0);
        int work = PlaceBuilding(state, 12, 12, serviceFlags: 0);
        int abandoned = PlaceBuilding(state, 11, 11, serviceFlags: 0);
        state.Buildings.State[abandoned] = 2; // abandoned
        PlaceBuilding(state, 10, 11, ServiceFire);
        PlaceBuilding(state, 10, 12, FireResponse.ServiceHydrant);
        Assert.True(FireResponse.TryIgnite(state, home));

        host.RestoreHouseholds(240, 80);
        // Pin a known L2 household onto home/work so restore can rematch by HH-xxxxx id.
        int hhSlot = -1;
        for (int i = 0; i < state.Households.Capacity; i++)
        {
            if (!state.Households.IsActive(i)) continue;
            hhSlot = i;
            break;
        }

        Assert.True(hhSlot >= 0);
        state.Households.HomeBuildingId[hhSlot] = (ushort)home;
        state.Households.WorkBuildingId[hhSlot] = (ushort)work;
        state.Households.Happiness[hhSlot] = 250;
        host.Population.RestoreMeanRentBurden(0.41f, state);
        host.Population.RestoreHouseholdSampleRows(state, new[]
        {
            new PopulationSystem.HouseholdSampleRow
            {
                Id = $"HH-{hhSlot:D5}",
                TileX = 10,
                TileZ = 10,
                Happiness = 250 / 255f,
                CommuteMin = 12f,
                HomeBuildingId = home,
                WorkBuildingId = work,
                RentBurden = 0.41f,
            },
        });

        state.EventTaxRevenueMult = 0.72f;
        state.EventImmigrationMult = 0.81f;
        state.EventCommercialSpawnMult = 1.25f;
        state.EventProductivityMult = 1.15f;
        state.EventResearchMult = 0.90f;
        state.EventSpawnDemandMult = 0.88f;
        host.Economy.RestoreMeanGoodsDeliveryDelay(0.55f, state);
        state.ResidentialVacancy = 0.33f;
        state.AbandonedBuildingCount = ZoneGrowthSystem.CountAbandonedBuildings(state);
        host.RebuildServicesAfterLoad();

        Assert.True(state.ActiveFireCount >= 1, "expected active fire before save");
        Assert.True(state.HydrantCoverageFraction < 1f || state.HydrantCoverageFraction > 0f);
        Assert.True(state.MeanEmsSurvivalRate > 0f);

        string json = host.GetSnapshotJson();
        var saved = JsonSerializer.Deserialize(json, SnapshotJsonContext.Default.SimSnapshotDto);
        Assert.NotNull(saved);

        using (var doc = JsonDocument.Parse(json))
        {
            Assert.True(doc.RootElement.TryGetProperty("eventTaxRevenueMult", out _));
            Assert.True(doc.RootElement.TryGetProperty("meanGoodsDeliveryDelay", out _));
            Assert.True(doc.RootElement.TryGetProperty("abandonedBuildingCount", out _));
            Assert.True(doc.RootElement.TryGetProperty("activeFireCount", out _));
            Assert.True(doc.RootElement.TryGetProperty("meanEmsSurvivalRate", out _));
            Assert.True(doc.RootElement.TryGetProperty("hydrantCoverageFraction", out _));
            Assert.True(doc.RootElement.TryGetProperty("populationL2", out _));
            Assert.True(doc.RootElement.TryGetProperty("buildings", out var buildingsEl));
            Assert.True(buildingsEl.GetArrayLength() > 0);
            var firstBuilding = buildingsEl[0];
            Assert.True(firstBuilding.TryGetProperty("fireRisk", out _));
            Assert.True(firstBuilding.TryGetProperty("serviceFlags", out _));
        }

        Assert.Equal(0.72f, saved!.EventTaxRevenueMult, precision: 3);
        Assert.Equal(0.55f, saved.MeanGoodsDeliveryDelay, precision: 3);
        Assert.True(saved.AbandonedBuildingCount >= 1);
        Assert.True(saved.ActiveFireCount >= 1);
        Assert.Contains(saved.PopulationL2.Households, h => h.Id == $"HH-{hhSlot:D5}");

        Assert.True(host.LoadSnapshotFromJson(json));

        Assert.Equal(saved.EventTaxRevenueMult, host.State!.EventTaxRevenueMult, precision: 3);
        Assert.Equal(saved.EventImmigrationMult, host.State.EventImmigrationMult, precision: 3);
        Assert.Equal(saved.EventCommercialSpawnMult, host.State.EventCommercialSpawnMult, precision: 3);
        Assert.Equal(saved.EventProductivityMult, host.State.EventProductivityMult, precision: 3);
        Assert.Equal(saved.EventResearchMult, host.State.EventResearchMult, precision: 3);
        Assert.Equal(saved.EventSpawnDemandMult, host.State.EventSpawnDemandMult, precision: 3);
        Assert.Equal(saved.MeanGoodsDeliveryDelay, host.State.MeanGoodsDeliveryDelay, precision: 3);
        Assert.Equal(saved.MeanGoodsDeliveryDelay, host.Economy.LastMeanGoodsDeliveryDelay, precision: 3);
        Assert.Equal(saved.AbandonedBuildingCount, host.State.AbandonedBuildingCount);
        Assert.Equal(saved.ResidentialVacancy, host.State.ResidentialVacancy, precision: 3);
        Assert.True(host.State.ActiveFireCount >= 1, "fire must survive via BuildingDto.FireRisk");
        Assert.Equal(saved.HydrantCoverageFraction, host.State.HydrantCoverageFraction, precision: 3);
        Assert.Equal(saved.MeanEmsSurvivalRate, host.State.MeanEmsSurvivalRate, precision: 3);

        var l2 = host.GetPopulationL2();
        var restored = Assert.Single(l2.Households, h => h.Id == $"HH-{hhSlot:D5}");
        Assert.Equal(home, restored.HomeBuildingId);
        Assert.Equal(work, restored.WorkBuildingId);
        Assert.InRange(restored.Happiness, 0.95f, 1f);
        Assert.Equal(0.41f, restored.RentBurden, precision: 3);
    }

    private static int PlaceBuilding(
        WorldState state,
        int x,
        int y,
        uint serviceFlags,
        byte level = 1)
    {
        int slot = state.Buildings.Allocate();
        state.Buildings.GridX[slot] = x;
        state.Buildings.GridY[slot] = y;
        state.Buildings.Width[slot] = 1;
        state.Buildings.Height[slot] = 1;
        state.Buildings.ServiceFlags[slot] = serviceFlags;
        state.Buildings.Level[slot] = level;
        state.Buildings.State[slot] = 1;
        state.Buildings.MaxOccupants[slot] = 20;
        state.Buildings.Occupants[slot] = 10;
        state.Tiles.BuildingId[state.Tiles.Index(x, y)] = (ushort)slot;
        return slot;
    }
}
