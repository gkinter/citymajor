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
/// plus Event*Mult / Law*Mult / ActiveLawIds / ActiveOrdinances / NextElectionYear /
/// BlackoutFraction / WaterShortageFraction / CulturalDna / delivery delay /
/// abandoned / fire / EMS / L2 sample.
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

    [Fact]
    public void SnapshotRoundTrip_PreservesLawMults()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        var state = host.State!;
        // Pin non-default Mults (as if ordinances were active) without relying on law catalog JSON.
        state.LawTrafficCapacityMult = 0.55f;
        state.LawConstructionSpeedMult = 1.40f;
        state.LawSpawnDemandMult = 1.025f;
        state.LawResidentialSpawnMult = 0.765f;
        state.LawIndustrialSpawnMult = 0.835f;
        state.LawCommercialSpawnMult = 0.91f;

        string json = host.GetSnapshotJson();
        var saved = JsonSerializer.Deserialize(json, SnapshotJsonContext.Default.SimSnapshotDto);
        Assert.NotNull(saved);

        using (var doc = JsonDocument.Parse(json))
        {
            Assert.True(doc.RootElement.TryGetProperty("lawTrafficCapacityMult", out _));
            Assert.True(doc.RootElement.TryGetProperty("lawConstructionSpeedMult", out _));
            Assert.True(doc.RootElement.TryGetProperty("lawSpawnDemandMult", out _));
            Assert.True(doc.RootElement.TryGetProperty("lawResidentialSpawnMult", out _));
            Assert.True(doc.RootElement.TryGetProperty("lawIndustrialSpawnMult", out _));
            Assert.True(doc.RootElement.TryGetProperty("lawCommercialSpawnMult", out _));
        }

        Assert.Equal(0.55f, saved!.LawTrafficCapacityMult, precision: 3);
        Assert.Equal(1.40f, saved.LawConstructionSpeedMult, precision: 3);
        Assert.Equal(1.025f, saved.LawSpawnDemandMult, precision: 3);
        Assert.Equal(0.765f, saved.LawResidentialSpawnMult, precision: 3);
        Assert.Equal(0.835f, saved.LawIndustrialSpawnMult, precision: 3);
        Assert.Equal(0.91f, saved.LawCommercialSpawnMult, precision: 3);

        Assert.True(host.LoadSnapshotFromJson(json));

        Assert.Equal(saved.LawTrafficCapacityMult, host.State!.LawTrafficCapacityMult, precision: 3);
        Assert.Equal(saved.LawConstructionSpeedMult, host.State.LawConstructionSpeedMult, precision: 3);
        Assert.Equal(saved.LawSpawnDemandMult, host.State.LawSpawnDemandMult, precision: 3);
        Assert.Equal(saved.LawResidentialSpawnMult, host.State.LawResidentialSpawnMult, precision: 3);
        Assert.Equal(saved.LawIndustrialSpawnMult, host.State.LawIndustrialSpawnMult, precision: 3);
        Assert.Equal(saved.LawCommercialSpawnMult, host.State.LawCommercialSpawnMult, precision: 3);

        var roundTrip = JsonSerializer.Deserialize(
            host.GetSnapshotJson(),
            SnapshotJsonContext.Default.SimSnapshotDto);
        Assert.NotNull(roundTrip);
        Assert.Equal(saved.LawTrafficCapacityMult, roundTrip!.LawTrafficCapacityMult, precision: 3);
        Assert.Equal(saved.LawSpawnDemandMult, roundTrip.LawSpawnDemandMult, precision: 3);
        Assert.Equal(saved.LawCommercialSpawnMult, roundTrip.LawCommercialSpawnMult, precision: 3);
    }

    [Fact]
    public void SnapshotRoundTrip_PreservesActiveLawIds()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions
        {
            SkipStarterCity = true,
            DataPaths = SimDataPaths.FromContentRoot(FindRepoRoot()),
        });
        // Fallback if pack path resolution failed in CI sandboxes.
        if (host.Laws.DefinitionCount == 0
            || host.Laws.GetIndexById("speed_limit") < 0
            || host.Laws.GetIndexById("parking_regulations") < 0
            || host.Laws.GetIndexById("congestion_charge") < 0)
        {
            host.Laws.LoadFromJson("""
                [
                  {
                    "id": "speed_limit",
                    "name": "Speed Limits",
                    "category": "traffic",
                    "era_min": "industrial",
                    "parameters": [],
                    "effects": { "road_capacity": -0.1 },
                    "faction_reactions": {},
                    "cost_monthly": 0,
                    "compliance_base": 0.7,
                    "description": "Speed limits"
                  },
                  {
                    "id": "parking_regulations",
                    "name": "Parking Regulations",
                    "category": "traffic",
                    "era_min": "industrial",
                    "parameters": [],
                    "effects": { "commercial_accessibility": -0.1 },
                    "faction_reactions": {},
                    "cost_monthly": 0,
                    "compliance_base": 0.75,
                    "description": "Parking"
                  },
                  {
                    "id": "congestion_charge",
                    "name": "Congestion Charge",
                    "category": "traffic",
                    "era_min": "industrial",
                    "parameters": [],
                    "effects": { "traffic_congestion": 0.15 },
                    "faction_reactions": {},
                    "cost_monthly": 0,
                    "compliance_base": 0.8,
                    "description": "Congestion charge"
                  }
                ]
                """);
        }

        Assert.True(host.Laws.DefinitionCount >= 3, "ordinance catalog must load");
        Assert.True(host.SetLawActive("speed_limit", true));
        Assert.True(host.SetLawActive("parking_regulations", true));

        // Pin Mults independently so restore proves both id toggles and scalar Mults survive.
        host.State!.LawTrafficCapacityMult = 0.62f;
        host.State.LawResidentialSpawnMult = 0.88f;

        string json = host.GetSnapshotJson();
        var saved = JsonSerializer.Deserialize(json, SnapshotJsonContext.Default.SimSnapshotDto);
        Assert.NotNull(saved);

        using (var doc = JsonDocument.Parse(json))
        {
            Assert.True(doc.RootElement.TryGetProperty("activeLawIds", out var idsEl));
            Assert.Equal(JsonValueKind.Array, idsEl.ValueKind);
            Assert.Equal(2, idsEl.GetArrayLength());
        }

        Assert.Equal(2, saved!.ActiveLawIds.Length);
        Assert.Contains("speed_limit", saved.ActiveLawIds);
        Assert.Contains("parking_regulations", saved.ActiveLawIds);
        Assert.Equal(0.62f, saved.LawTrafficCapacityMult, precision: 3);

        // Flip laws away from saved set so restore cannot pass by coincidence.
        Assert.True(host.SetLawActive("speed_limit", false));
        Assert.True(host.SetLawActive("congestion_charge", true));
        Assert.False(host.Laws.IsActive(host.Laws.GetIndexById("speed_limit")));

        Assert.True(host.LoadSnapshotFromJson(json));

        Assert.True(host.Laws.IsActive(host.Laws.GetIndexById("speed_limit")));
        Assert.True(host.Laws.IsActive(host.Laws.GetIndexById("parking_regulations")));
        Assert.False(host.Laws.IsActive(host.Laws.GetIndexById("congestion_charge")));
        Assert.Equal(2, host.Laws.ActiveLawCount);
        Assert.Equal(2, host.State!.ActiveLawCount);
        Assert.Equal(saved.LawTrafficCapacityMult, host.State.LawTrafficCapacityMult, precision: 3);
        Assert.Equal(saved.LawResidentialSpawnMult, host.State.LawResidentialSpawnMult, precision: 3);

        var roundTrip = JsonSerializer.Deserialize(
            host.GetSnapshotJson(),
            SnapshotJsonContext.Default.SimSnapshotDto);
        Assert.NotNull(roundTrip);
        Assert.Equal(saved.ActiveLawIds, roundTrip!.ActiveLawIds);
        Assert.Equal(saved.LawTrafficCapacityMult, roundTrip.LawTrafficCapacityMult, precision: 3);
    }

    [Fact]
    public void SnapshotRoundTrip_PreservesActiveOrdinancesAndNextElectionYear()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        var state = host.State!;
        // Pin non-default politics bitfield + election year (orthogonal to ActiveLawIds).
        const ulong ordinances = (1UL << 3) | (1UL << 7) | (1UL << 12);
        state.ActiveOrdinances = ordinances;
        state.NextElectionYear = 2036;

        string json = host.GetSnapshotJson();
        var saved = JsonSerializer.Deserialize(json, SnapshotJsonContext.Default.SimSnapshotDto);
        Assert.NotNull(saved);

        using (var doc = JsonDocument.Parse(json))
        {
            Assert.True(doc.RootElement.TryGetProperty("activeOrdinances", out var ordEl));
            Assert.Equal(JsonValueKind.Number, ordEl.ValueKind);
            Assert.True(doc.RootElement.TryGetProperty("nextElectionYear", out var yearEl));
            Assert.Equal(2036, yearEl.GetInt32());
        }

        Assert.Equal(ordinances, saved!.ActiveOrdinances);
        Assert.Equal(2036, saved.NextElectionYear);

        // Mutate so restore cannot pass by Init coincidence.
        state.ActiveOrdinances = 0UL;
        state.NextElectionYear = 2020;

        Assert.True(host.LoadSnapshotFromJson(json));

        Assert.Equal(ordinances, host.State!.ActiveOrdinances);
        Assert.Equal(2036, host.State.NextElectionYear);

        var roundTrip = JsonSerializer.Deserialize(
            host.GetSnapshotJson(),
            SnapshotJsonContext.Default.SimSnapshotDto);
        Assert.NotNull(roundTrip);
        Assert.Equal(ordinances, roundTrip!.ActiveOrdinances);
        Assert.Equal(2036, roundTrip.NextElectionYear);
    }

    [Fact]
    public void SnapshotRoundTrip_PreservesBlackoutAndWaterShortageFractions()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        var state = host.State!;
        // Pin rolling L0 utility shortages (orthogonal to Power/WaterCoverageFraction).
        state.BlackoutFraction = 0.37f;
        state.WaterShortageFraction = 0.28f;
        host.Services.UtilityBalance.RestoreRollingFractions(0.37f, 0.28f);

        string json = host.GetSnapshotJson();
        var saved = JsonSerializer.Deserialize(json, SnapshotJsonContext.Default.SimSnapshotDto);
        Assert.NotNull(saved);

        using (var doc = JsonDocument.Parse(json))
        {
            Assert.True(doc.RootElement.TryGetProperty("blackoutFraction", out var blackoutEl));
            Assert.Equal(JsonValueKind.Number, blackoutEl.ValueKind);
            Assert.Equal(0.37f, blackoutEl.GetSingle(), precision: 3);
            Assert.True(doc.RootElement.TryGetProperty("waterShortageFraction", out var shortageEl));
            Assert.Equal(0.28f, shortageEl.GetSingle(), precision: 3);
        }

        Assert.Equal(0.37f, saved!.BlackoutFraction, precision: 3);
        Assert.Equal(0.28f, saved.WaterShortageFraction, precision: 3);

        // Mutate so restore cannot pass by Init coincidence.
        state.BlackoutFraction = 0f;
        state.WaterShortageFraction = 0f;
        host.Services.UtilityBalance.RestoreRollingFractions(0f, 0f);

        Assert.True(host.LoadSnapshotFromJson(json));

        Assert.Equal(0.37f, host.State!.BlackoutFraction, precision: 3);
        Assert.Equal(0.28f, host.State.WaterShortageFraction, precision: 3);
        Assert.Equal(0.37f, host.Services.UtilityBalance.BlackoutFraction, precision: 3);
        Assert.Equal(0.28f, host.Services.UtilityBalance.WaterShortageFraction, precision: 3);

        var roundTrip = JsonSerializer.Deserialize(
            host.GetSnapshotJson(),
            SnapshotJsonContext.Default.SimSnapshotDto);
        Assert.NotNull(roundTrip);
        Assert.Equal(0.37f, roundTrip!.BlackoutFraction, precision: 3);
        Assert.Equal(0.28f, roundTrip.WaterShortageFraction, precision: 3);
    }

    [Fact]
    public void SnapshotRoundTrip_PreservesCulturalDna()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        var state = host.State!;
        // Pin non-default politics flavor vector (−1…+1, length 8).
        float[] dna =
        [
            0.55f, -0.40f, 0.25f, -0.80f,
            0.10f, 0.70f, -0.15f, 0.90f,
        ];
        for (int i = 0; i < dna.Length; i++)
            state.CulturalDna[i] = dna[i];

        string json = host.GetSnapshotJson();
        var saved = JsonSerializer.Deserialize(json, SnapshotJsonContext.Default.SimSnapshotDto);
        Assert.NotNull(saved);

        using (var doc = JsonDocument.Parse(json))
        {
            Assert.True(doc.RootElement.TryGetProperty("culturalDna", out var el));
            Assert.Equal(JsonValueKind.Array, el.ValueKind);
            Assert.Equal(8, el.GetArrayLength());
            Assert.Equal(0.55f, el[0].GetSingle(), precision: 3);
            Assert.Equal(0.90f, el[7].GetSingle(), precision: 3);
        }

        Assert.Equal(8, saved!.CulturalDna.Length);
        for (int i = 0; i < dna.Length; i++)
            Assert.Equal(dna[i], saved.CulturalDna[i], precision: 3);

        // Mutate so restore cannot pass by Init coincidence.
        for (int i = 0; i < state.CulturalDna.Length; i++)
            state.CulturalDna[i] = 0f;

        Assert.True(host.LoadSnapshotFromJson(json));

        for (int i = 0; i < dna.Length; i++)
            Assert.Equal(dna[i], host.State!.CulturalDna[i], precision: 3);

        var roundTrip = JsonSerializer.Deserialize(
            host.GetSnapshotJson(),
            SnapshotJsonContext.Default.SimSnapshotDto);
        Assert.NotNull(roundTrip);
        Assert.Equal(8, roundTrip!.CulturalDna.Length);
        for (int i = 0; i < dna.Length; i++)
            Assert.Equal(dna[i], roundTrip.CulturalDna[i], precision: 3);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "base", "data", "laws", "laws.json")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repo root (base/data/laws/laws.json).");
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
