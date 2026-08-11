using System.Text.Json;
using Forge.Engine.Data;
using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Forge.SimCore;
using Forge.SimWasm;
using Xunit;

namespace Forge.SimCore.Tests;

/// <summary>
/// Characterization tests for Cathedral P5.1 utilities foundation (SB-4987),
/// P5.2 emergency response time (SB-4242), P5.3 fire response v1,
/// P5.4 EMS survival curve (Phase 5b), Tier-2 hospital capacity,
/// Tier-2 wildfire / arson rings, aerial / lookout / fire rating,
/// Tier-2 education depth, Tier-2 park amenity parity, Tier-2 hospital→HH
/// health progression, P4 health→satisfaction/migration, Tier-2 tourism
/// attractions stub, Tier-2 police/crime depth, Tier-2 waste/pollution depth,
/// and Cathedral P3.5 bilateral trade freight metrics.
/// </summary>
public sealed class CathedralUtilitiesTests
{
    private const uint ServicePowerPlant = 1u << 7;
    private const uint ServiceWaterPump = 1u << 8;
    private const uint ServiceFire = 1u << 3;
    private const uint ServiceHealth = 1u << 4;

    [Fact]
    public void GetSnapshotJson_IncludesPowerAndWaterCoverageFractions()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        using var doc = JsonDocument.Parse(host.GetSnapshotJson());
        Assert.True(doc.RootElement.TryGetProperty("powerCoverageFraction", out var power));
        Assert.True(doc.RootElement.TryGetProperty("waterCoverageFraction", out var water));
        Assert.Equal(JsonValueKind.Number, power.ValueKind);
        Assert.Equal(JsonValueKind.Number, water.ValueKind);
        Assert.InRange(power.GetSingle(), 0f, 1f);
        Assert.InRange(water.GetSingle(), 0f, 1f);
    }

    [Fact]
    public void SnapshotDto_MirrorsWorldStateUtilityCoverage()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        host.State!.PowerCoverageFraction = 0.42f;
        host.State.WaterCoverageFraction = 0.77f;
        host.State.UtilityStressIndex = 0.31f;

        var dto = SimSnapshotDto.From(host.GetSnapshot(), host.State);

        Assert.Equal(0.42f, dto.PowerCoverageFraction, precision: 4);
        Assert.Equal(0.77f, dto.WaterCoverageFraction, precision: 4);
        Assert.Equal(0.31f, dto.UtilityStressIndex, precision: 4);
    }

    [Fact]
    public void L0Tick_NoSupply_ReducesCoverageBelowFull()
    {
        const int size = 64;
        var state = new WorldState(size, maxBuildings: 8);
        var services = new ServiceSystem(size, utilityPartitionSize: 32);

        // Zoned demand without plants → coverage < 1 (HUD shows <100%).
        for (int y = 0; y <= 31; y++)
        for (int x = 0; x <= 31; x++)
            state.Tiles.ZoneType[state.Tiles.Index(x, y)] = 1;

        services.L0Tick(state, 1f);

        Assert.True(state.PowerCoverageFraction < 1f);
        Assert.True(state.WaterCoverageFraction < 1f);
        Assert.True(state.UtilityStressIndex > 0f);
    }

    [Fact]
    public void L0Tick_PowerAndWaterPlants_RestoreLocalCoverage()
    {
        const int size = 64;
        var state = new WorldState(size, maxBuildings: 16);
        var services = new ServiceSystem(size, utilityPartitionSize: 32);

        for (int y = 10; y <= 14; y++)
        for (int x = 10; x <= 14; x++)
            state.Tiles.ZoneType[state.Tiles.Index(x, y)] = 1;

        PlaceBuilding(state, 12, 12, ServicePowerPlant, level: 3);
        PlaceBuilding(state, 13, 12, ServiceWaterPump, level: 3);

        services.L0Tick(state, 1f);

        Assert.True(state.PowerCoverageFraction > 0f);
        Assert.True(state.WaterCoverageFraction > 0f);
        Assert.InRange(state.UtilityStressIndex, 0f, 1f);
    }

    [Fact]
    public void EmergencyResponseTime_UsesDistancePlusTraffic()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        // Straight paved corridor — station west, incidents mid/east.
        for (int x = 8; x <= 32; x++)
            host.PlaceRoad(x, 10);

        PlaceBuilding(host.State!, 10, 11, ServiceFire);
        const int nearX = 18;
        const int farX = 30;
        const int incidentY = 11;

        // PlaceRoad defers graph rebuild until Tick.
        host.Tick(1.0);

        Assert.True(host.State.Roads.EdgeCount > 0, "road graph should have edges after PlaceRoad + Tick");

        float[] freeFlow = CaptureFreeFlowEdgeCosts(host.State.Roads);
        float[] congested = new float[freeFlow.Length];
        for (int e = 0; e < freeFlow.Length; e++)
            congested[e] = TrafficBpr.CalculateTravelTime(freeFlow[e], volume: 200f, capacity: 100f);

        float nearFree = EmergencyResponseTime.CalculateMinutes(
            host.State, nearX, incidentY, ServiceFire, freeFlow);
        float farFree = EmergencyResponseTime.CalculateMinutes(
            host.State, farX, incidentY, ServiceFire, freeFlow);
        float farCongested = EmergencyResponseTime.CalculateMinutes(
            host.State, farX, incidentY, ServiceFire, congested);

        Assert.True(nearFree < farFree,
            $"near incident ({nearFree:F2} min) should be faster than far ({farFree:F2} min)");
        Assert.True(farCongested > farFree,
            $"congestion should raise response time (free={farFree:F2}, congested={farCongested:F2})");

        // ServiceSystem facade must honor the same BPR edge times (hydrant on-site → no 2×).
        host.State.RoadEdgeTravelTimes = congested;
        PlaceBuilding(host.State!, farX, incidentY + 1, FireResponse.ServiceHydrant);
        var services = new ServiceSystem(64);
        float viaServices = services.CalculateFireResponseTime(host.State, farX, incidentY);
        Assert.Equal(farCongested, viaServices, precision: 3);
    }

    [Fact]
    public void SnapshotJson_ExportsMeanEmergencyResponseMinutes()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        // Corridor + fire station so mean response is below the no-station default.
        for (int x = 8; x <= 24; x++)
            host.PlaceRoad(x, 10);
        PlaceBuilding(host.State!, 10, 11, ServiceFire);
        for (int x = 12; x <= 20; x++)
            host.State!.Tiles.ZoneType[host.State.Tiles.Index(x, 11)] = 1;

        host.Tick(1.0);
        host.Services!.UpdateMeanEmergencyResponse(host.State!);

        float mean = host.State.MeanEmergencyResponseMinutes;
        Assert.True(mean > 0f && mean < EmergencyResponseTime.NoStationResponseMinutes,
            $"expected mean response below no-station default, got {mean:F2}");

        using var doc = JsonDocument.Parse(host.GetSnapshotJson());
        Assert.True(doc.RootElement.TryGetProperty(
            "meanEmergencyResponseMinutes", out var minutes));
        Assert.Equal(JsonValueKind.Number, minutes.ValueKind);
        Assert.Equal(mean, minutes.GetSingle(), precision: 3);

        var dto = SimSnapshotDto.From(host.GetSnapshot(), host.State);
        Assert.Equal(mean, dto.MeanEmergencyResponseMinutes, precision: 3);

        var snap = host.GetSnapshot();
        Assert.Equal(mean, snap.MeanEmergencyResponseMinutes, precision: 3);
    }

    [Fact]
    public void HydrantCoverage_ReducesResponseMinutesAndSpreadChance()
    {
        var state = new WorldState(32, maxBuildings: 16);
        PlaceBuilding(state, 10, 10, ServiceFire);
        PlaceBuilding(state, 16, 10, serviceFlags: 0); // residential target
        state.Tiles.ZoneType[state.Tiles.Index(16, 10)] = 1;

        float without = EmergencyResponseTime.CalculateMinutes(state, 16, 10, ServiceFire);
        float withMult = FireResponse.ApplyHydrantResponseMultiplier(without, hasHydrant: false);
        Assert.Equal(without * FireResponse.NoHydrantResponseMultiplier, withMult, precision: 4);

        PlaceBuilding(state, 15, 10, FireResponse.ServiceHydrant);
        Assert.True(FireResponse.HasHydrantCoverage(state, 16, 10));

        float covered = FireResponse.ApplyHydrantResponseMultiplier(without, hasHydrant: true);
        Assert.Equal(without, covered, precision: 4);

        float spreadNoHydrant = FireResponse.CalculateSpreadChance(
            FireResponse.MaterialWood, windFactor: 1f, adjacentBurningCount: 1, targetHasHydrant: false);
        float spreadHydrant = FireResponse.CalculateSpreadChance(
            FireResponse.MaterialWood, windFactor: 1f, adjacentBurningCount: 1, targetHasHydrant: true);
        Assert.True(spreadHydrant < spreadNoHydrant,
            $"hydrant should cut spread ({spreadHydrant:F3} vs {spreadNoHydrant:F3})");
        Assert.Equal(spreadNoHydrant * FireResponse.HydrantSpreadInverse, spreadHydrant, precision: 4);
    }

    [Fact]
    public void FireSpread_IgnitesAdjacentBuilding_WithoutHydrant()
    {
        var state = new WorldState(32, maxBuildings: 16);
        state.WindSpeed = 15f; // max wind factor → higher spread

        int source = PlaceBuilding(state, 10, 10, serviceFlags: 0, level: 1);
        int neighbor = PlaceBuilding(state, 11, 10, serviceFlags: 0, level: 1);
        Assert.True(FireResponse.TryIgnite(state, source));
        Assert.Equal(1, FireResponse.CountActiveFires(state));

        // Force ignition: high chance + seeded RNG that always draws low.
        int ignited = FireResponse.TickSpread(state, hours: 10f, rng: new AlwaysLowRandom());
        Assert.True(ignited >= 1, "adjacent wood building should ignite under high wind");
        Assert.True(FireResponse.IsBurning(state.Buildings, neighbor));
        Assert.True(state.ActiveFireCount >= 0); // updated by ServiceSystem, not TickSpread alone

        var services = new ServiceSystem(32);
        services.UpdateFireResponse(state, hours: 0f, rng: new AlwaysLowRandom());
        Assert.True(state.ActiveFireCount >= 2);
        Assert.InRange(state.HydrantCoverageFraction, 0f, 1f);
    }

    [Fact]
    public void SnapshotJson_ExportsHydrantCoverageAndActiveFires()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        int house = PlaceBuilding(host.State!, 12, 12, serviceFlags: 0);
        host.State!.Tiles.ZoneType[host.State.Tiles.Index(12, 12)] = 1;
        PlaceBuilding(host.State, 12, 13, FireResponse.ServiceHydrant);
        Assert.True(FireResponse.TryIgnite(host.State, house));

        host.Services!.UpdateFireResponse(host.State, hours: 0f);

        Assert.Equal(1, host.State.ActiveFireCount);
        Assert.True(host.State.HydrantCoverageFraction > 0f);

        using var doc = JsonDocument.Parse(host.GetSnapshotJson());
        Assert.True(doc.RootElement.TryGetProperty("hydrantCoverageFraction", out var hydrant));
        Assert.True(doc.RootElement.TryGetProperty("activeFireCount", out var fires));
        Assert.Equal(host.State.HydrantCoverageFraction, hydrant.GetSingle(), precision: 3);
        Assert.Equal(host.State.ActiveFireCount, fires.GetInt32());

        var dto = SimSnapshotDto.From(host.GetSnapshot(), host.State);
        Assert.Equal(host.State.HydrantCoverageFraction, dto.HydrantCoverageFraction, precision: 3);
        Assert.Equal(host.State.ActiveFireCount, dto.ActiveFireCount);

        var snap = host.GetSnapshot();
        Assert.Equal(host.State.HydrantCoverageFraction, snap.HydrantCoverageFraction, precision: 3);
        Assert.Equal(host.State.ActiveFireCount, snap.ActiveFireCount);
    }

    [Fact]
    public void EmsSurvival_BucketsMatchMissingSystemsCurve()
    {
        Assert.Equal(EmsSurvival.SurvivalUnder5Min, EmsSurvival.CalculateRate(3f));
        Assert.Equal(EmsSurvival.Survival5To10Min, EmsSurvival.CalculateRate(7f));
        Assert.Equal(EmsSurvival.Survival10To15Min, EmsSurvival.CalculateRate(12f));
        Assert.Equal(EmsSurvival.SurvivalOver15Min, EmsSurvival.CalculateRate(20f));
        Assert.Equal(EmsSurvival.SurvivalOver15Min, EmsSurvival.DefaultMeanRate);
        // ServiceSystem facade must stay in lockstep with SimCore helper.
        Assert.Equal(EmsSurvival.CalculateRate(4f), ServiceSystem.CalculateEmsSurvivalRate(4f));
    }

    [Fact]
    public void EmsSurvival_FasterResponseRaisesMeanSurvivalRate()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        for (int x = 8; x <= 40; x++)
            host.PlaceRoad(x, 10);
        for (int x = 12; x <= 36; x++)
            host.State!.Tiles.ZoneType[host.State.Tiles.Index(x, 11)] = 1;

        // Far station → slower response → lower survival.
        PlaceBuilding(host.State!, 8, 11, ServiceFire);
        host.Services!.UpdateMeanEmergencyResponse(host.State!);
        float farMinutes = host.State.MeanEmergencyResponseMinutes;
        float farSurvival = host.State.MeanEmsSurvivalRate;

        // Near station → faster response → higher survival.
        PlaceBuilding(host.State, 20, 11, ServiceFire);
        host.Services.UpdateMeanEmergencyResponse(host.State);
        float nearMinutes = host.State.MeanEmergencyResponseMinutes;
        float nearSurvival = host.State.MeanEmsSurvivalRate;

        Assert.True(nearMinutes < farMinutes,
            $"near station should cut mean minutes ({nearMinutes:F2} vs {farMinutes:F2})");
        Assert.True(nearSurvival > farSurvival,
            $"faster response should raise mean survival ({nearSurvival:F3} vs {farSurvival:F3})");
        Assert.InRange(nearSurvival, EmsSurvival.SurvivalOver15Min, EmsSurvival.SurvivalUnder5Min);
        Assert.InRange(farSurvival, EmsSurvival.SurvivalOver15Min, EmsSurvival.SurvivalUnder5Min);
    }

    [Fact]
    public void SnapshotJson_ExportsMeanEmsSurvivalRate()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        for (int x = 8; x <= 24; x++)
            host.PlaceRoad(x, 10);
        PlaceBuilding(host.State!, 10, 11, ServiceFire);
        for (int x = 12; x <= 20; x++)
            host.State!.Tiles.ZoneType[host.State.Tiles.Index(x, 11)] = 1;

        host.Services!.UpdateMeanEmergencyResponse(host.State!);

        float rate = host.State.MeanEmsSurvivalRate;
        Assert.InRange(rate, 0.40f, 0.90f);

        using var doc = JsonDocument.Parse(host.GetSnapshotJson());
        Assert.True(doc.RootElement.TryGetProperty("meanEmsSurvivalRate", out var survival));
        Assert.Equal(JsonValueKind.Number, survival.ValueKind);
        Assert.Equal(rate, survival.GetSingle(), precision: 3);

        var dto = SimSnapshotDto.From(host.GetSnapshot(), host.State);
        Assert.Equal(rate, dto.MeanEmsSurvivalRate, precision: 3);

        var snap = host.GetSnapshot();
        Assert.Equal(rate, snap.MeanEmsSurvivalRate, precision: 3);
    }

    [Fact]
    public void HospitalCapacity_NearestWithBeds_SkipsFullHospital()
    {
        var state = new WorldState(64, maxBuildings: 16);

        // Near hospital is full; far hospital has free beds → EMS diverts.
        int near = PlaceBuilding(state, 10, 10, ServiceHealth, level: 1, maxOccupants: 10, occupants: 10);
        int far = PlaceBuilding(state, 30, 10, ServiceHealth, level: 1, maxOccupants: 20, occupants: 5);

        Assert.Equal(0, HospitalCapacity.GetAvailableBeds(state.Buildings, near));
        Assert.Equal(15, HospitalCapacity.GetAvailableBeds(state.Buildings, far));

        Assert.True(HospitalCapacity.TryFindNearestWithCapacity(
            state, 12, 10,
            out int id, out int hx, out int hy, out _));
        Assert.Equal(far, id);
        Assert.Equal(30, hx);
        Assert.Equal(10, hy);
    }

    [Fact]
    public void HospitalCapacity_FullCity_AppliesNoCapacityTransportPenalty()
    {
        var state = new WorldState(64, maxBuildings: 8);
        PlaceBuilding(state, 10, 10, ServiceHealth, level: 1, maxOccupants: 5, occupants: 5);

        Assert.Equal(0, HospitalCapacity.CountAvailableBeds(state));
        Assert.Equal(1f, HospitalCapacity.CalculateOccupancyFraction(state), precision: 3);
        Assert.Equal(
            HospitalCapacity.NoCapacityTransportMinutes,
            HospitalCapacity.CalculateTransportMinutes(state, 12, 10));
    }

    [Fact]
    public void HospitalCapacity_NoHospitals_ZeroTransportKeepsP54Survival()
    {
        var state = new WorldState(64, maxBuildings: 4);
        Assert.Equal(0f, HospitalCapacity.CalculateTransportMinutes(state, 8, 8));
        Assert.Equal(0f, HospitalCapacity.CalculateOccupancyFraction(state));
        Assert.Equal(0, HospitalCapacity.CountAvailableBeds(state));

        float chain = HospitalCapacity.CalculateEmsChainMinutes(4f, 0f);
        Assert.Equal(4f, chain);
        Assert.Equal(EmsSurvival.SurvivalUnder5Min, EmsSurvival.CalculateRate(chain));
    }

    [Fact]
    public void HospitalCapacity_NearHospitalRaisesSurvivalVsFullDivert()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        for (int x = 8; x <= 40; x++)
            host.PlaceRoad(x, 10);
        for (int x = 12; x <= 36; x++)
            host.State!.Tiles.ZoneType[host.State.Tiles.Index(x, 11)] = 1;

        PlaceBuilding(host.State!, 20, 11, ServiceFire);

        // Only full hospital → transport penalty → lower survival.
        PlaceBuilding(host.State, 10, 11, ServiceHealth, level: 1, maxOccupants: 4, occupants: 4);
        host.Services!.UpdateMeanEmergencyResponse(host.State);
        host.Services.UpdateHospitalCapacity(host.State);
        float fullSurvival = host.State.MeanEmsSurvivalRate;
        Assert.Equal(0, host.State.AvailableHospitalBeds);
        Assert.Equal(1f, host.State.HospitalBedOccupancyFraction, precision: 3);

        // Add a hospital with free beds near the corridor → shorter chain → higher survival.
        PlaceBuilding(host.State, 22, 11, ServiceHealth, level: 1, maxOccupants: 40, occupants: 5);
        host.Services.UpdateMeanEmergencyResponse(host.State);
        host.Services.UpdateHospitalCapacity(host.State);
        float openSurvival = host.State.MeanEmsSurvivalRate;

        Assert.True(host.State.AvailableHospitalBeds > 0);
        Assert.True(openSurvival > fullSurvival,
            $"open beds should raise mean survival ({openSurvival:F3} vs {fullSurvival:F3})");
        Assert.InRange(host.State.HospitalBedOccupancyFraction, 0f, 1f);
    }

    [Fact]
    public void SnapshotJson_ExportsHospitalCapacityFields()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        PlaceBuilding(host.State!, 10, 10, ServiceHealth, level: 1, maxOccupants: 20, occupants: 8);
        host.Services!.UpdateHospitalCapacity(host.State!);

        Assert.Equal(12, host.State.AvailableHospitalBeds);
        Assert.Equal(0.4f, host.State.HospitalBedOccupancyFraction, precision: 3);

        using var doc = JsonDocument.Parse(host.GetSnapshotJson());
        Assert.True(doc.RootElement.TryGetProperty("hospitalBedOccupancyFraction", out var occ));
        Assert.True(doc.RootElement.TryGetProperty("availableHospitalBeds", out var beds));
        Assert.Equal(0.4f, occ.GetSingle(), precision: 3);
        Assert.Equal(12, beds.GetInt32());

        var dto = SimSnapshotDto.From(host.GetSnapshot(), host.State);
        Assert.Equal(0.4f, dto.HospitalBedOccupancyFraction, precision: 3);
        Assert.Equal(12, dto.AvailableHospitalBeds);

        var snap = host.GetSnapshot();
        Assert.Equal(0.4f, snap.HospitalBedOccupancyFraction, precision: 3);
        Assert.Equal(12, snap.AvailableHospitalBeds);
    }

    [Fact]
    public void Wildfire_DroughtRisk_HeatwaveAndDrySummer()
    {
        var state = new WorldState(32, maxBuildings: 4);
        state.WeatherCondition = 0;
        state.Month = 1; // winter/spring-ish depending on map — force mild via precip
        state.Precipitation = 0.5f;
        Assert.Equal(WildfireArson.BaseDroughtRisk, WildfireArson.CalculateDroughtRisk(state), precision: 3);

        state.Month = 7; // summer (Season == 1)
        state.Precipitation = 0.05f;
        Assert.Equal(WildfireArson.DrySummerDroughtRisk, WildfireArson.CalculateDroughtRisk(state), precision: 3);

        state.WeatherCondition = 6; // heatwave wins
        Assert.Equal(WildfireArson.HeatwaveDroughtRisk, WildfireArson.CalculateDroughtRisk(state), precision: 3);
    }

    [Fact]
    public void Wildfire_SpreadsAcrossFuel_StopsAtRoadFirebreak()
    {
        var state = new WorldState(32, maxBuildings: 8);
        // Forest strip: (5,10)-(7,10), road firebreak at x=8, more forest at (9,10)
        for (int x = 5; x <= 9; x++)
        {
            if (x == 8) continue;
            state.Tiles.TerrainType[state.Tiles.Index(x, 10)] = WildfireArson.TerrainForest;
        }

        state.Tiles.RoadFlags[state.Tiles.Index(8, 10)] = 1; // firebreak

        Assert.True(WildfireArson.TryIgniteWildfireTile(state, 5, 10));
        // One hop per tick (burners snapshotted) — two ticks reach the firebreak edge.
        WildfireArson.TickWildfire(state, hours: 1f, rng: new AlwaysLowRandom());
        Assert.True(WildfireArson.IsWildfireBurning(state, 6, 10), "should spread along fuel");
        WildfireArson.TickWildfire(state, hours: 1f, rng: new AlwaysLowRandom());
        Assert.True(WildfireArson.IsWildfireBurning(state, 7, 10), "should reach firebreak edge");
        Assert.False(WildfireArson.IsWildfireBurning(state, 9, 10), "road firebreak must stop spread");
        // Third tick still cannot cross the road.
        WildfireArson.TickWildfire(state, hours: 1f, rng: new AlwaysLowRandom());
        Assert.False(WildfireArson.IsWildfireBurning(state, 9, 10), "road firebreak must hold");
    }

    [Fact]
    public void Wildfire_EdgeIgnitesAdjacentBuilding()
    {
        var state = new WorldState(32, maxBuildings: 8);
        state.Tiles.TerrainType[state.Tiles.Index(10, 10)] = WildfireArson.TerrainForest;
        int house = PlaceBuilding(state, 11, 10, serviceFlags: 0);

        Assert.True(WildfireArson.TryIgniteWildfireTile(state, 10, 10));
        WildfireArson.TickWildfire(state, hours: 10f, rng: new AlwaysLowRandom());

        Assert.True(FireResponse.IsBurning(state.Buildings, house),
            "wildfire edge should ignite adjacent building");
    }

    [Fact]
    public void Arson_HighCrimeIgnites_AndDetectsRing()
    {
        var state = new WorldState(32, maxBuildings: 8);
        int a = PlaceBuilding(state, 10, 10, serviceFlags: 0);
        int b = PlaceBuilding(state, 12, 10, serviceFlags: 0);
        state.Tiles.Crime[state.Tiles.Index(10, 10)] = 0.9f;
        state.Tiles.Crime[state.Tiles.Index(12, 10)] = 0.9f;
        state.Tiles.ZoneType[state.Tiles.Index(10, 10)] = 1;
        state.Tiles.ZoneType[state.Tiles.Index(12, 10)] = 1;

        Assert.True(WildfireArson.ArsonRiskFromCrime(0.9f) > 0f);
        Assert.Equal(0f, WildfireArson.ArsonRiskFromCrime(0.2f), precision: 3);

        int ignited = WildfireArson.TickArson(state, hours: 10f, rng: new AlwaysLowRandom());
        Assert.Equal(2, ignited);
        Assert.True(FireResponse.IsBurning(state.Buildings, a));
        Assert.True(FireResponse.IsBurning(state.Buildings, b));
        Assert.True(WildfireArson.DetectArsonRing(state));
    }

    [Fact]
    public void SnapshotJson_ExportsWildfireArsonFields()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        host.State!.WeatherCondition = 6;
        host.State.Tiles.TerrainType[host.State.Tiles.Index(20, 20)] = WildfireArson.TerrainForest;
        Assert.True(WildfireArson.TryIgniteWildfireTile(host.State, 20, 20));

        int house = PlaceBuilding(host.State, 22, 22, serviceFlags: 0);
        host.State.Tiles.Crime[host.State.Tiles.Index(22, 22)] = 0.95f;
        host.State.Tiles.ZoneType[host.State.Tiles.Index(22, 22)] = 1;
        Assert.True(FireResponse.TryIgnite(host.State, house));
        // Second high-crime fire for ring detection
        int house2 = PlaceBuilding(host.State, 24, 22, serviceFlags: 0);
        host.State.Tiles.Crime[host.State.Tiles.Index(24, 22)] = 0.95f;
        host.State.Tiles.ZoneType[host.State.Tiles.Index(24, 22)] = 1;
        Assert.True(FireResponse.TryIgnite(host.State, house2));

        host.Services!.UpdateWildfireArson(host.State, hours: 0f);

        Assert.Equal(WildfireArson.HeatwaveDroughtRisk, host.State.WildfireRiskIndex, precision: 3);
        Assert.True(host.State.ActiveWildfireTileCount >= 1);
        Assert.True(host.State.ArsonRingActive);
        Assert.True(host.State.ArsonRiskIndex > 0f);

        using var doc = JsonDocument.Parse(host.GetSnapshotJson());
        Assert.True(doc.RootElement.TryGetProperty("wildfireRiskIndex", out var risk));
        Assert.True(doc.RootElement.TryGetProperty("activeWildfireTileCount", out var tiles));
        Assert.True(doc.RootElement.TryGetProperty("arsonRiskIndex", out var arson));
        Assert.True(doc.RootElement.TryGetProperty("arsonRingActive", out var ring));
        Assert.Equal(host.State.WildfireRiskIndex, risk.GetSingle(), precision: 3);
        Assert.Equal(host.State.ActiveWildfireTileCount, tiles.GetInt32());
        Assert.Equal(host.State.ArsonRiskIndex, arson.GetSingle(), precision: 3);
        Assert.True(ring.GetBoolean());

        var dto = SimSnapshotDto.From(host.GetSnapshot(), host.State);
        Assert.Equal(host.State.WildfireRiskIndex, dto.WildfireRiskIndex, precision: 3);
        Assert.Equal(host.State.ActiveWildfireTileCount, dto.ActiveWildfireTileCount);
        Assert.Equal(host.State.ArsonRiskIndex, dto.ArsonRiskIndex, precision: 3);
        Assert.True(dto.ArsonRingActive);

        var snap = host.GetSnapshot();
        Assert.Equal(host.State.WildfireRiskIndex, snap.WildfireRiskIndex, precision: 3);
        Assert.Equal(host.State.ActiveWildfireTileCount, snap.ActiveWildfireTileCount);
        Assert.True(snap.ArsonRingActive);
    }

    [Fact]
    public void Lookout_CoverageCutsSpark_WhenChanceBorderline()
    {
        var state = new WorldState(32, maxBuildings: 4);
        state.WeatherCondition = 6; // drought = 1.0 → sparkChance 0.35
        state.Tiles.TerrainType[state.Tiles.Index(16, 16)] = WildfireArson.TerrainForest;

        // RNG returns 0.20: succeeds at 0.35, fails at 0.35 * 0.25 = 0.0875.
        // Next(size) pinned to 16 so spark probes hit the single fuel tile.
        var rng = new FixedDoubleRandom(0.20);

        Assert.Equal(1, WildfireArson.TickWildfire(state, hours: 1f, rng: rng));
        Assert.True(WildfireArson.IsWildfireBurning(state, 16, 16));

        state.WildfireIntensity![state.Tiles.Index(16, 16)] = 0;
        PlaceBuilding(state, 16, 15, serviceFlags: WildfireArson.ServiceLookout);
        Assert.True(WildfireArson.HasLookoutCoverage(state, 16, 16));
        Assert.Equal(1, WildfireArson.CountLookoutTowers(state));

        Assert.Equal(0, WildfireArson.TickWildfire(state, hours: 1f, rng: new FixedDoubleRandom(0.20)));
        Assert.False(WildfireArson.IsWildfireBurning(state, 16, 16));
    }

    [Fact]
    public void Aerial_SuppressesBurningFuelTiles()
    {
        var state = new WorldState(32, maxBuildings: 4);
        for (int x = 5; x <= 10; x++)
            state.Tiles.TerrainType[state.Tiles.Index(x, 10)] = WildfireArson.TerrainForest;

        Assert.True(WildfireArson.TryIgniteWildfireTile(state, 5, 10));
        Assert.True(WildfireArson.TryIgniteWildfireTile(state, 6, 10));
        Assert.True(WildfireArson.TryIgniteWildfireTile(state, 7, 10));
        Assert.True(WildfireArson.TryIgniteWildfireTile(state, 8, 10));
        Assert.Equal(4, WildfireArson.CountActiveWildfireTiles(state));

        Assert.Equal(0, WildfireArson.TickAerialSuppression(state, hours: 1f));

        PlaceBuilding(state, 20, 20, serviceFlags: WildfireArson.ServiceAerialFire);
        Assert.True(WildfireArson.HasAerialFirefighting(state));

        int suppressed = WildfireArson.TickAerialSuppression(state, hours: 1f);
        Assert.Equal(WildfireArson.AerialSuppressPerHour, suppressed);
        Assert.Equal(4 - WildfireArson.AerialSuppressPerHour, WildfireArson.CountActiveWildfireTiles(state));
    }

    [Fact]
    public void FireRating_LookoutAndAerialRaise_FiresLower()
    {
        var state = new WorldState(32, maxBuildings: 16);
        state.HydrantCoverageFraction = 1f;
        state.WildfireRiskIndex = WildfireArson.BaseDroughtRisk;
        state.ArsonRiskIndex = 0f;
        state.ArsonRingActive = false;
        state.ActiveFireCount = 0;
        state.ActiveWildfireTileCount = 0;

        byte baseline = WildfireArson.CalculateFireSafetyRating(state);

        PlaceBuilding(state, 8, 8, serviceFlags: WildfireArson.ServiceLookout);
        PlaceBuilding(state, 10, 8, serviceFlags: WildfireArson.ServiceAerialFire);
        PlaceBuilding(state, 12, 8, serviceFlags: EmergencyResponseTime.ServiceFire);
        PlaceBuilding(state, 14, 8, serviceFlags: FireResponse.ServiceHydrant);

        byte equipped = WildfireArson.CalculateFireSafetyRating(state);
        Assert.True(equipped > baseline, $"equipped {equipped} should beat baseline {baseline}");

        state.ActiveFireCount = 5;
        state.ActiveWildfireTileCount = 6;
        state.ArsonRingActive = true;
        byte penalized = WildfireArson.CalculateFireSafetyRating(state);
        Assert.True(penalized < equipped, $"penalized {penalized} should be below equipped {equipped}");

        Assert.Equal(
            WildfireArson.MaxInsurancePremiumMult,
            WildfireArson.InsurancePremiumMultFromRating(1),
            precision: 3);
        Assert.Equal(
            WildfireArson.MinInsurancePremiumMult,
            WildfireArson.InsurancePremiumMultFromRating(10),
            precision: 3);
        Assert.True(
            WildfireArson.InsurancePremiumMultFromRating(3) >
            WildfireArson.InsurancePremiumMultFromRating(8));
    }

    [Fact]
    public void SnapshotJson_ExportsFireRatingLookoutAerialFields()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        PlaceBuilding(host.State!, 10, 10, serviceFlags: WildfireArson.ServiceLookout);
        PlaceBuilding(host.State!, 12, 10, serviceFlags: WildfireArson.ServiceAerialFire);
        host.State!.HydrantCoverageFraction = 0.9f;

        host.Services!.UpdateWildfireArson(host.State, hours: 0f);

        Assert.Equal(1, host.State.LookoutTowerCount);
        Assert.True(host.State.AerialFirefightingAvailable);
        Assert.InRange(host.State.FireSafetyRating, WildfireArson.MinFireSafetyRating, WildfireArson.MaxFireSafetyRating);
        Assert.Equal(
            WildfireArson.InsurancePremiumMultFromRating(host.State.FireSafetyRating),
            host.State.FireInsurancePremiumMult,
            precision: 3);

        using var doc = JsonDocument.Parse(host.GetSnapshotJson());
        Assert.True(doc.RootElement.TryGetProperty("lookoutTowerCount", out var lookouts));
        Assert.True(doc.RootElement.TryGetProperty("aerialFirefightingAvailable", out var aerial));
        Assert.True(doc.RootElement.TryGetProperty("fireSafetyRating", out var rating));
        Assert.True(doc.RootElement.TryGetProperty("fireInsurancePremiumMult", out var premium));
        Assert.Equal(1, lookouts.GetInt32());
        Assert.True(aerial.GetBoolean());
        Assert.Equal(host.State.FireSafetyRating, rating.GetByte());
        Assert.Equal(host.State.FireInsurancePremiumMult, premium.GetSingle(), precision: 3);

        var dto = SimSnapshotDto.From(host.GetSnapshot(), host.State);
        Assert.Equal(1, dto.LookoutTowerCount);
        Assert.True(dto.AerialFirefightingAvailable);
        Assert.Equal(host.State.FireSafetyRating, dto.FireSafetyRating);
        Assert.Equal(host.State.FireInsurancePremiumMult, dto.FireInsurancePremiumMult, precision: 3);

        var snap = host.GetSnapshot();
        Assert.Equal(1, snap.LookoutTowerCount);
        Assert.True(snap.AerialFirefightingAvailable);
        Assert.Equal(host.State.FireSafetyRating, snap.FireSafetyRating);
    }

    [Fact]
    public void EducationUpgradeChance_HigherWithCoverageAndQuality()
    {
        float none = EducationProgression.UpgradeChance(0f, 1f, level: 0);
        float thin = EducationProgression.UpgradeChance(0.1f, 1f, level: 0);
        float covered = EducationProgression.UpgradeChance(1f, 1f, level: 0);
        float advanced = EducationProgression.UpgradeChance(1f, 1f, level: 2);

        Assert.Equal(0f, none);
        Assert.Equal(0f, thin);
        Assert.True(covered > 0f);
        Assert.True(advanced > 0f && advanced < covered,
            $"higher education levels should be harder (L0={covered:F3}, L2={advanced:F3})");
    }

    [Fact]
    public void EducationTick_SchoolCoverage_RaisesHouseholdLevels()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        int home = PlaceBuilding(host.State!, 16, 16, serviceFlags: 0);
        PlaceBuilding(host.State!, 16, 17, serviceFlags: EducationProgression.ServiceEducation, level: 3);

        int slot = host.State!.Households.Allocate();
        Assert.True(slot >= 0);
        host.State.Households.HomeBuildingId[slot] = (ushort)home;
        host.State.Households.Education[slot] = 0;
        host.State.Households.MemberCount[slot] = 2;

        host.Services!.RebuildFromWorld(host.State);
        float before = host.State.MeanEducationLevel;

        int delta = 0;
        for (int i = 0; i < 40; i++)
        {
            delta += EducationProgression.Tick(
                host.State,
                host.Services.EducationCoverage,
                schoolQuality: 1f,
                days: 1f,
                rng: new AlwaysLowRandom());
        }

        Assert.True(delta > 0, "forced-success RNG should upgrade households under a school");
        Assert.True(host.State.Households.Education[slot] > 0);
        Assert.True(host.State.MeanEducationLevel > before);
        Assert.True(host.State.EducationCoverageFraction > 0.5f);
    }

    [Fact]
    public void EducationTick_NoCoverage_DecaysLevel()
    {
        var state = new WorldState(32, maxBuildings: 8);
        int home = PlaceBuilding(state, 10, 10, serviceFlags: 0);
        int slot = state.Households.Allocate();
        state.Households.HomeBuildingId[slot] = (ushort)home;
        state.Households.Education[slot] = 2;

        var emptyCoverage = new InfluenceMap(32, 32);
        emptyCoverage.Recalculate();

        for (int i = 0; i < 30; i++)
        {
            EducationProgression.Tick(
                state,
                emptyCoverage,
                schoolQuality: 1f,
                days: 1f,
                rng: new AlwaysLowRandom());
        }

        Assert.True(state.Households.Education[slot] < 2,
            "uncovered households should slowly lose education");
        Assert.Equal(0f, state.EducationCoverageFraction);
    }

    [Fact]
    public void ResearchEducationMultiplier_MonotonicWithMean()
    {
        float low = EducationProgression.ResearchEducationMultiplier(0f);
        float mid = EducationProgression.ResearchEducationMultiplier(1.5f);
        float high = EducationProgression.ResearchEducationMultiplier(3f);
        Assert.Equal(EducationProgression.MinResearchEducationMult, low, precision: 3);
        Assert.Equal(EducationProgression.MaxResearchEducationMult, high, precision: 3);
        Assert.True(mid > low && mid < high);
    }

    [Fact]
    public void SnapshotJson_ExportsEducationProgressionFields()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        int home = PlaceBuilding(host.State!, 12, 12, serviceFlags: 0);
        PlaceBuilding(host.State!, 12, 13, serviceFlags: EducationProgression.ServiceEducation, level: 2);
        int slot = host.State!.Households.Allocate();
        host.State.Households.HomeBuildingId[slot] = (ushort)home;
        host.State.Households.Education[slot] = 1;
        host.State.Households.MemberCount[slot] = 3;

        host.Services!.RebuildFromWorld(host.State);
        host.Services.UpdateEducationProgression(host.State, days: 0f);

        Assert.True(host.State.EducationCoverageFraction > 0f);
        Assert.Equal(1f, host.State.MeanEducationLevel, precision: 3);

        using var doc = JsonDocument.Parse(host.GetSnapshotJson());
        Assert.True(doc.RootElement.TryGetProperty("meanEducationLevel", out var mean));
        Assert.True(doc.RootElement.TryGetProperty("educationCoverageFraction", out var cov));
        Assert.Equal(host.State.MeanEducationLevel, mean.GetSingle(), precision: 3);
        Assert.Equal(host.State.EducationCoverageFraction, cov.GetSingle(), precision: 3);

        var dto = SimSnapshotDto.From(host.GetSnapshot(), host.State);
        Assert.Equal(host.State.MeanEducationLevel, dto.MeanEducationLevel, precision: 3);
        Assert.Equal(host.State.EducationCoverageFraction, dto.EducationCoverageFraction, precision: 3);

        var snap = host.GetSnapshot();
        Assert.Equal(host.State.MeanEducationLevel, snap.MeanEducationLevel, precision: 3);
        Assert.Equal(host.State.EducationCoverageFraction, snap.EducationCoverageFraction, precision: 3);
    }

    [Fact]
    public void ParkAmenity_PaintedZone_RaisesLocalParkAccess()
    {
        var state = new WorldState(32, maxBuildings: 8);
        float none = ParkAmenity.LocalParkAccess(state, 16, 16);
        Assert.Equal(0f, none);

        state.Tiles.ZoneType[state.Tiles.Index(18, 16)] = ParkAmenity.ZonePark;
        float withZone = ParkAmenity.LocalParkAccess(state, 16, 16);
        Assert.True(withZone > 0f, "painted park zone must contribute exercise access");
        Assert.True(ParkAmenity.HasNearbyPark(state, 16, 16));
        Assert.True(ParkAmenity.ExerciseContribution(withZone) > 0f);
    }

    [Fact]
    public void ParkAmenity_ParkBuilding_RaisesLocalParkAccess()
    {
        var state = new WorldState(32, maxBuildings: 8);
        PlaceBuilding(state, 20, 16, serviceFlags: ParkAmenity.ServicePark);

        float access = ParkAmenity.LocalParkAccess(state, 16, 16);
        Assert.True(access > 0f);
        Assert.True(ParkAmenity.HasNearbyPark(state, 16, 16));
    }

    [Fact]
    public void CalculateHealthScore_PaintedParkZone_HigherThanBare()
    {
        const int size = 32;
        var state = new WorldState(size, maxBuildings: 8);
        var services = new ServiceSystem(size);

        float bare = services.CalculateHealthScore(state, 16, 16);

        state.Tiles.ZoneType[state.Tiles.Index(17, 16)] = ParkAmenity.ZonePark;
        float withPark = services.CalculateHealthScore(state, 16, 16);

        Assert.True(withPark > bare,
            $"painted park should raise health/exercise (bare={bare:F3}, park={withPark:F3})");
    }

    [Fact]
    public void HealthRecoveryTarget_HigherWithCoverageAndQuality()
    {
        float thin = HealthProgression.RecoveryTarget(0.1f, 1f, pollution: 0f, exerciseContribution: 0f);
        float covered = HealthProgression.RecoveryTarget(1f, 1f, pollution: 0f, exerciseContribution: 0f);
        float polluted = HealthProgression.RecoveryTarget(1f, 1f, pollution: 1f, exerciseContribution: 0f);
        float withExercise = HealthProgression.RecoveryTarget(1f, 1f, pollution: 0f, exerciseContribution: 0.1f);

        Assert.True(covered > thin);
        Assert.True(covered > polluted);
        Assert.True(withExercise > covered);
        Assert.InRange(covered, 0f, 1f);
    }

    [Fact]
    public void HealthTick_HospitalCoverage_RaisesHealthSatisfaction()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        int home = PlaceBuilding(host.State!, 16, 16, serviceFlags: 0);
        PlaceBuilding(host.State!, 16, 17, serviceFlags: HealthProgression.ServiceHealth, level: 3);

        int slot = host.State!.Households.Allocate();
        Assert.True(slot >= 0);
        host.State.Households.HomeBuildingId[slot] = (ushort)home;
        host.State.Households.HealthSatisfaction[slot] = 40;
        host.State.Households.MemberCount[slot] = 2;

        host.Services!.RebuildFromWorld(host.State);
        byte before = host.State.Households.HealthSatisfaction[slot];

        int improved = 0;
        for (int i = 0; i < 20; i++)
        {
            improved += HealthProgression.Tick(
                host.State,
                host.Services.HealthCoverage,
                hospitalQuality: 1f,
                days: 1f);
        }

        Assert.True(improved > 0, "hospital coverage should raise HealthSatisfaction over time");
        Assert.True(host.State.Households.HealthSatisfaction[slot] > before);
        Assert.True(host.State.HealthCoverageFraction > 0.5f);
        Assert.True(host.State.MeanHealthSatisfaction > before / 255f);
    }

    [Fact]
    public void HealthTick_NoCoverage_DecaysHealthSatisfaction()
    {
        var state = new WorldState(32, maxBuildings: 8);
        int home = PlaceBuilding(state, 10, 10, serviceFlags: 0);
        int slot = state.Households.Allocate();
        state.Households.HomeBuildingId[slot] = (ushort)home;
        state.Households.HealthSatisfaction[slot] = 220;

        var emptyCoverage = new InfluenceMap(32, 32);
        emptyCoverage.Recalculate();

        for (int i = 0; i < 40; i++)
        {
            HealthProgression.Tick(
                state,
                emptyCoverage,
                hospitalQuality: 1f,
                days: 1f);
        }

        Assert.True(state.Households.HealthSatisfaction[slot] < 220,
            "uncovered households should slowly lose HealthSatisfaction");
        Assert.Equal(0f, state.HealthCoverageFraction);
    }

    [Fact]
    public void SnapshotJson_ExportsHealthProgressionFields()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        int home = PlaceBuilding(host.State!, 12, 12, serviceFlags: 0);
        PlaceBuilding(host.State!, 12, 13, serviceFlags: HealthProgression.ServiceHealth, level: 2);
        int slot = host.State!.Households.Allocate();
        host.State.Households.HomeBuildingId[slot] = (ushort)home;
        host.State.Households.HealthSatisfaction[slot] = 160;
        host.State.Households.MemberCount[slot] = 2;

        host.Services!.RebuildFromWorld(host.State);
        host.Services.UpdateHealthProgression(host.State, days: 0f);

        Assert.True(host.State.HealthCoverageFraction > 0f);
        Assert.Equal(160f / 255f, host.State.MeanHealthSatisfaction, precision: 2);

        using var doc = JsonDocument.Parse(host.GetSnapshotJson());
        Assert.True(doc.RootElement.TryGetProperty("healthCoverageFraction", out var cov));
        Assert.True(doc.RootElement.TryGetProperty("meanHealthSatisfaction", out var health));
        Assert.Equal(host.State.HealthCoverageFraction, cov.GetSingle(), precision: 3);
        Assert.Equal(host.State.MeanHealthSatisfaction, health.GetSingle(), precision: 3);

        var dto = SimSnapshotDto.From(host.GetSnapshot(), host.State);
        Assert.Equal(host.State.HealthCoverageFraction, dto.HealthCoverageFraction, precision: 3);
        Assert.Equal(host.State.MeanHealthSatisfaction, dto.MeanHealthSatisfaction, precision: 3);

        var snap = host.GetSnapshot();
        Assert.Equal(host.State.HealthCoverageFraction, snap.HealthCoverageFraction, precision: 3);
        Assert.Equal(host.State.MeanHealthSatisfaction, snap.MeanHealthSatisfaction, precision: 3);
    }

    [Fact]
    public void HealthTick_NearPark_RaisesHealthSatisfactionViaExercise()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        int home = PlaceBuilding(host.State!, 16, 16, serviceFlags: 0);
        host.State!.Tiles.ZoneType[host.State.Tiles.Index(17, 16)] = ParkAmenity.ZonePark;

        int slot = host.State.Households.Allocate();
        Assert.True(slot >= 0);
        host.State.Households.HomeBuildingId[slot] = (ushort)home;
        host.State.Households.HealthSatisfaction[slot] = 40;
        host.State.Households.LeisureSatisfaction[slot] = 100;
        host.State.Households.MemberCount[slot] = 2;

        host.Services!.RebuildFromWorld(host.State);
        byte beforeHealth = host.State.Households.HealthSatisfaction[slot];
        byte beforeLeisure = host.State.Households.LeisureSatisfaction[slot];

        int improvedHealth = 0;
        int improvedLeisure = 0;
        for (int i = 0; i < 20; i++)
        {
            improvedHealth += HealthProgression.Tick(
                host.State,
                host.Services.HealthCoverage,
                hospitalQuality: 1f,
                days: 1f);
            improvedLeisure += ParkAmenity.Tick(host.State, days: 1f);
        }

        Assert.True(improvedHealth > 0, "park exercise should raise HealthSatisfaction even without a hospital");
        Assert.True(host.State.Households.HealthSatisfaction[slot] > beforeHealth);
        Assert.True(improvedLeisure > 0);
        Assert.True(host.State.Households.LeisureSatisfaction[slot] > beforeLeisure);
        Assert.True(host.State.MeanParkAccess > 0f);
        Assert.True(host.State.MeanHealthSatisfaction > beforeHealth / 255f);
    }

    [Fact]
    public void SnapshotJson_ExportsParkAmenityFields()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        int home = PlaceBuilding(host.State!, 12, 12, serviceFlags: 0);
        host.State!.Tiles.ZoneType[host.State.Tiles.Index(12, 13)] = ParkAmenity.ZonePark;
        int slot = host.State.Households.Allocate();
        host.State.Households.HomeBuildingId[slot] = (ushort)home;
        host.State.Households.HealthSatisfaction[slot] = 160;
        host.State.Households.MemberCount[slot] = 2;

        host.Services!.UpdateParkAmenity(host.State, days: 0f);

        Assert.True(host.State.MeanParkAccess > 0f);
        Assert.True(host.State.ParkAccessFraction > 0f);

        using var doc = JsonDocument.Parse(host.GetSnapshotJson());
        Assert.True(doc.RootElement.TryGetProperty("meanParkAccess", out var mean));
        Assert.True(doc.RootElement.TryGetProperty("parkAccessFraction", out var cov));
        Assert.True(doc.RootElement.TryGetProperty("meanHealthSatisfaction", out var health));
        Assert.Equal(host.State.MeanParkAccess, mean.GetSingle(), precision: 3);
        Assert.Equal(host.State.ParkAccessFraction, cov.GetSingle(), precision: 3);
        Assert.Equal(host.State.MeanHealthSatisfaction, health.GetSingle(), precision: 3);

        var dto = SimSnapshotDto.From(host.GetSnapshot(), host.State);
        Assert.Equal(host.State.MeanParkAccess, dto.MeanParkAccess, precision: 3);
        Assert.Equal(host.State.ParkAccessFraction, dto.ParkAccessFraction, precision: 3);
        Assert.Equal(host.State.MeanHealthSatisfaction, dto.MeanHealthSatisfaction, precision: 3);

        var snap = host.GetSnapshot();
        Assert.Equal(host.State.MeanParkAccess, snap.MeanParkAccess, precision: 3);
        Assert.Equal(host.State.ParkAccessFraction, snap.ParkAccessFraction, precision: 3);
        Assert.Equal(host.State.MeanHealthSatisfaction, snap.MeanHealthSatisfaction, precision: 3);
    }

    [Fact]
    public void HealthSatisfaction_FeedsP4SatisfactionAfterHealthProgression()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        int home = PlaceBuilding(host.State!, 10, 10, serviceFlags: 0);
        int work = PlaceBuilding(host.State!, 16, 10, serviceFlags: 0);
        PlaceBuilding(host.State!, 11, 10, serviceFlags: HealthProgression.ServiceHealth, level: 3);

        int slot = host.State!.Households.Allocate();
        Assert.True(slot >= 0);
        var hh = host.State.Households;
        hh.HomeBuildingId[slot] = (ushort)home;
        hh.WorkBuildingId[slot] = (ushort)work;
        hh.MemberCount[slot] = 2;
        hh.AgeGroup[slot] = 1;
        hh.Education[slot] = 1;
        hh.WealthLevel[slot] = 2;
        hh.Income[slot] = 1800;
        hh.Flags[slot] = 1; // active, employed
        hh.HealthSatisfaction[slot] = 40;
        hh.LeisureSatisfaction[slot] = 128;

        host.Services!.RebuildFromWorld(host.State);
        float satLow = host.Population.CalculateSatisfaction(host.State, slot);

        for (int i = 0; i < 14; i++)
            HealthProgression.Tick(host.State, host.Services.HealthCoverage, hospitalQuality: 1f, days: 1f);

        Assert.True(host.State.Households.HealthSatisfaction[slot] > 40);
        float satHigh = host.Population.CalculateSatisfaction(host.State, slot);
        Assert.True(satHigh > satLow,
            $"hospital→health should raise P4 satisfaction (low={satLow:F1}, high={satHigh:F1})");
        Assert.True(host.State.MeanHealthSatisfaction > 40f / 255f);
        Assert.True(host.State.HealthCoverageFraction > 0f);
    }

    [Fact]
    public void TourismAttractions_CountParksLandmarks_AndRaiseBudgetIncome()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });
        var state = host.State!;
        state.Population = 500;
        state.Happiness = 0.4f;

        PlaceBuilding(state, 10, 10, TourismAttractions.ServicePark);
        PlaceBuilding(state, 20, 20, TourismAttractions.ServiceLandmark);
        for (int i = 0; i < TourismAttractions.ParkTilesPerAttraction; i++)
            state.Tiles.ZoneType[state.Tiles.Index(i % 8, 30 + i / 8)] = TourismAttractions.ZonePark;

        Assert.Equal(2, TourismAttractions.CountParkAttractions(state)); // building + zone site
        Assert.Equal(1, TourismAttractions.CountLandmarkAttractions(state));
        Assert.Equal(3, TourismAttractions.CountTourismAttractions(state));

        var budget = new BudgetSystem();
        var economy = new EconomySystem();
        budget.CalculateMonthlyBudget(state, economy);

        Assert.Equal(2, budget.ParkAttractionCount);
        Assert.Equal(1, budget.LandmarkAttractionCount);
        Assert.True(budget.TourismIncome >
            state.Population * state.Happiness * 0.5f * 0.01f * TourismAttractions.SpendPerTourist);

        TourismAttractions.PublishCounts(state);
        Assert.Equal(3, state.TourismAttractionCount);

        var snap = host.GetSnapshot();
        Assert.Equal(state.ParkAttractionCount, snap.ParkAttractionCount);
        Assert.Equal(state.LandmarkAttractionCount, snap.LandmarkAttractionCount);
        Assert.Equal(state.TourismAttractionCount, snap.TourismAttractionCount);
        Assert.Equal(state.TourismIncome, snap.TourismIncome, precision: 2);

        var dto = SimSnapshotDto.From(snap, state);
        Assert.Equal(state.TourismAttractionCount, dto.TourismAttractionCount);
        Assert.Equal(state.TourismIncome, dto.TourismIncome, precision: 2);

        using var doc = JsonDocument.Parse(host.GetSnapshotJson());
        Assert.True(doc.RootElement.TryGetProperty("tourismAttractionCount", out var attr));
        Assert.True(doc.RootElement.TryGetProperty("tourismIncome", out var income));
        Assert.Equal(state.TourismAttractionCount, attr.GetInt32());
        Assert.Equal(state.TourismIncome, income.GetSingle(), precision: 2);
    }

    [Fact]
    public void BilateralTradeRoutes_ExportFreightMetricsOnSnapshot()
    {
        var host = new SimHost();
        host.Init(32, new SimHostInitOptions { SkipStarterCity = true });
        var state = host.State!;

        Assert.True(host.Trade.CreateTradeRoute(1, Good.Steel, 50f, 10f, 12, deliveryDelay: 1f));
        host.Trade.ProcessTrade(state, host.Economy);

        Assert.Equal(1, state.BilateralRouteCount);
        Assert.Equal(500f, state.BilateralTradeValue, precision: 1);
        Assert.Equal(4f, state.MeanFreightMonths, precision: 2);

        var snap = host.GetSnapshot();
        Assert.Equal(1, snap.BilateralRouteCount);
        Assert.Equal(500f, snap.BilateralTradeValue, precision: 1);
        Assert.Equal(4f, snap.MeanFreightMonths, precision: 2);

        var dto = SimSnapshotDto.From(snap, state);
        Assert.Equal(1, dto.BilateralRouteCount);
        Assert.Equal(4f, dto.MeanFreightMonths, precision: 2);

        using var doc = JsonDocument.Parse(host.GetSnapshotJson());
        Assert.True(doc.RootElement.TryGetProperty("bilateralRouteCount", out var routes));
        Assert.True(doc.RootElement.TryGetProperty("bilateralTradeValue", out var value));
        Assert.True(doc.RootElement.TryGetProperty("meanFreightMonths", out var freight));
        Assert.Equal(1, routes.GetInt32());
        Assert.Equal(500f, value.GetSingle(), precision: 1);
        Assert.Equal(4f, freight.GetSingle(), precision: 2);
    }

    [Fact]
    public void PoliceCrime_HigherQuality_LowersCrimeAtSameCoverage()
    {
        float lowQ = PoliceCrime.CalculateCrimeRate(
            policeCoverage: 0.8f, policeQuality: 0.3f,
            unemployment: 0f, poverty: 0f, education: 0f, lighting: 1f, abandoned: 0f);
        float highQ = PoliceCrime.CalculateCrimeRate(
            policeCoverage: 0.8f, policeQuality: 1f,
            unemployment: 0f, poverty: 0f, education: 0f, lighting: 1f, abandoned: 0f);
        float uncovered = PoliceCrime.CalculateCrimeRate(
            policeCoverage: 0f, policeQuality: 1f,
            unemployment: 0f, poverty: 0f, education: 0f, lighting: 1f, abandoned: 0f);

        Assert.True(highQ < lowQ,
            $"higher station quality should suppress more crime (lowQ={lowQ:F3}, highQ={highQ:F3})");
        Assert.True(uncovered > lowQ);
        Assert.True(PoliceCrime.EffectivePoliceFactor(0.8f, 1f) >
                    PoliceCrime.EffectivePoliceFactor(0.8f, 0.3f));
    }

    [Fact]
    public void PoliceTick_StationCoverage_RaisesSafetyAndLowersMeanCrime()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        int home = PlaceBuilding(host.State!, 16, 16, serviceFlags: 0);
        PlaceBuilding(host.State!, 16, 17, serviceFlags: PoliceCrime.ServicePolice, level: 3);

        int slot = host.State!.Households.Allocate();
        Assert.True(slot >= 0);
        host.State.Households.HomeBuildingId[slot] = (ushort)home;
        host.State.Households.SafetySatisfaction[slot] = 40;
        host.State.Households.MemberCount[slot] = 2;

        host.Services!.RebuildFromWorld(host.State);
        host.Services.DailyTick(host.State, 1.0);
        byte before = host.State.Households.SafetySatisfaction[slot];
        float crimeBefore = host.State.Tiles.Crime[host.State.Tiles.Index(16, 16)];

        int improved = 0;
        for (int i = 0; i < 20; i++)
        {
            host.Services.DailyTick(host.State, 1.0);
            improved += PoliceCrime.Tick(host.State, host.Services.PoliceCoverage, days: 1f);
        }

        Assert.True(host.State.PoliceCoverageFraction > 0.5f);
        Assert.True(host.State.Households.SafetySatisfaction[slot] > before,
            "police coverage should raise SafetySatisfaction over time");
        Assert.True(improved > 0);
        Assert.True(host.State.MeanCrimeRate < 0.35f,
            $"covered crime should be suppressed (mean={host.State.MeanCrimeRate:F3}, tile={crimeBefore:F3})");
        Assert.True(host.State.MeanSafetySatisfaction > before / 255f);
    }

    [Fact]
    public void SnapshotJson_ExportsPoliceCrimeFields()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        int home = PlaceBuilding(host.State!, 12, 12, serviceFlags: 0);
        PlaceBuilding(host.State!, 12, 13, serviceFlags: PoliceCrime.ServicePolice, level: 2);
        int slot = host.State!.Households.Allocate();
        host.State.Households.HomeBuildingId[slot] = (ushort)home;
        host.State.Households.SafetySatisfaction[slot] = 180;
        host.State.Households.MemberCount[slot] = 2;

        host.Services!.RebuildFromWorld(host.State);
        // Write crime without blending safety yet.
        host.Services.DailyTick(host.State, 1.0);
        host.State.Households.SafetySatisfaction[slot] = 180;
        host.Services.UpdatePoliceCrime(host.State, days: 0f);

        Assert.True(host.State.PoliceCoverageFraction > 0f);
        Assert.InRange(host.State.MeanCrimeRate, 0f, 1f);
        Assert.Equal(180f / 255f, host.State.MeanSafetySatisfaction, precision: 2);

        using var doc = JsonDocument.Parse(host.GetSnapshotJson());
        Assert.True(doc.RootElement.TryGetProperty("policeCoverageFraction", out var cov));
        Assert.True(doc.RootElement.TryGetProperty("meanCrimeRate", out var crime));
        Assert.True(doc.RootElement.TryGetProperty("meanSafetySatisfaction", out var safety));
        Assert.Equal(host.State.PoliceCoverageFraction, cov.GetSingle(), precision: 3);
        Assert.Equal(host.State.MeanCrimeRate, crime.GetSingle(), precision: 3);
        Assert.Equal(host.State.MeanSafetySatisfaction, safety.GetSingle(), precision: 3);

        var dto = SimSnapshotDto.From(host.GetSnapshot(), host.State);
        Assert.Equal(host.State.PoliceCoverageFraction, dto.PoliceCoverageFraction, precision: 3);
        Assert.Equal(host.State.MeanCrimeRate, dto.MeanCrimeRate, precision: 3);
        Assert.Equal(host.State.MeanSafetySatisfaction, dto.MeanSafetySatisfaction, precision: 3);

        var snap = host.GetSnapshot();
        Assert.Equal(host.State.PoliceCoverageFraction, snap.PoliceCoverageFraction, precision: 3);
        Assert.Equal(host.State.MeanCrimeRate, snap.MeanCrimeRate, precision: 3);
        Assert.Equal(host.State.MeanSafetySatisfaction, snap.MeanSafetySatisfaction, precision: 3);
    }

    [Fact]
    public void SafetySatisfaction_FeedsP4SatisfactionAfterPoliceCrime()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        int home = PlaceBuilding(host.State!, 11, 10, serviceFlags: 0);
        PlaceBuilding(host.State!, 11, 11, serviceFlags: PoliceCrime.ServicePolice, level: 3);
        int work = PlaceBuilding(host.State!, 20, 20, serviceFlags: 0);

        int slot = host.State!.Households.Allocate();
        host.State.Households.HomeBuildingId[slot] = (ushort)home;
        host.State.Households.WorkBuildingId[slot] = (ushort)work;
        host.State.Households.SafetySatisfaction[slot] = 30;
        host.State.Households.HealthSatisfaction[slot] = 128;
        host.State.Households.LeisureSatisfaction[slot] = 128;
        host.State.Households.MemberCount[slot] = 2;
        host.State.Households.Income[slot] = 1500;

        host.Services!.RebuildFromWorld(host.State);
        var pop = new PopulationSystem(seed: 42);
        float satLow = pop.CalculateSatisfaction(host.State, slot);

        for (int i = 0; i < 25; i++)
            host.Services.DailyTick(host.State, 1.0);

        float satHigh = pop.CalculateSatisfaction(host.State, slot);
        Assert.True(host.State.Households.SafetySatisfaction[slot] > 30);
        Assert.True(satHigh > satLow,
            $"police→safety should raise P4 satisfaction (low={satLow:F1}, high={satHigh:F1})");
        Assert.True(host.State.MeanSafetySatisfaction > 30f / 255f);
        Assert.True(host.State.PoliceCoverageFraction > 0f);
    }

    [Fact]
    public void WasteCollection_HigherQuality_AbatesMoreAtSameCoverage()
    {
        float lowQ = WasteCollection.PollutionDelta(
            coverage: 0.8f, depotQuality: 0.3f, zoneType: 1, days: 1f);
        float highQ = WasteCollection.PollutionDelta(
            coverage: 0.8f, depotQuality: 1f, zoneType: 1, days: 1f);
        float uncovered = WasteCollection.PollutionDelta(
            coverage: 0f, depotQuality: 1f, zoneType: 1, days: 1f);

        Assert.True(highQ < lowQ,
            $"higher depot quality should abate more (lowQ={lowQ:F4}, highQ={highQ:F4})");
        Assert.True(uncovered > 0f, "uncovered residential should accumulate waste pollution");
        Assert.True(highQ < 0f, "full-quality coverage should net-clean");
        Assert.True(WasteCollection.EffectiveCollectionFactor(0.8f, 1f) >
                    WasteCollection.EffectiveCollectionFactor(0.8f, 0.3f));
    }

    [Fact]
    public void WasteTick_DepotCoverage_LowersPollutionAndRaisesEnvironment()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        int home = PlaceBuilding(host.State!, 16, 16, serviceFlags: 0);
        host.State!.Tiles.ZoneType[host.State.Tiles.Index(16, 16)] = 1; // Residential
        host.State.Tiles.Pollution[host.State.Tiles.Index(16, 16)] = 0.55f;
        PlaceBuilding(host.State!, 16, 17, serviceFlags: WasteCollection.ServiceGarbage, level: 3);

        int slot = host.State!.Households.Allocate();
        Assert.True(slot >= 0);
        host.State.Households.HomeBuildingId[slot] = (ushort)home;
        host.State.Households.MemberCount[slot] = 2;

        host.Services!.RebuildFromWorld(host.State);
        float pollutionBefore = host.State.Tiles.Pollution[host.State.Tiles.Index(16, 16)];

        int cleaned = 0;
        for (int i = 0; i < 20; i++)
        {
            host.Services.DailyTick(host.State, 1.0);
            cleaned += WasteCollection.Tick(host.State, host.Services.WasteCoverage, days: 1f);
        }

        float pollutionAfter = host.State.Tiles.Pollution[host.State.Tiles.Index(16, 16)];
        Assert.True(host.State.WasteCoverageFraction > 0.5f);
        Assert.True(pollutionAfter < pollutionBefore,
            $"waste coverage should lower pollution (before={pollutionBefore:F3}, after={pollutionAfter:F3})");
        Assert.True(cleaned > 0);
        Assert.True(host.State.MeanPollution < 0.55f);
        Assert.True(host.State.MeanEnvironmentScore > 0.45f);
    }

    [Fact]
    public void SnapshotJson_ExportsWasteCollectionFields()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        int home = PlaceBuilding(host.State!, 12, 12, serviceFlags: 0);
        host.State!.Tiles.ZoneType[host.State.Tiles.Index(12, 12)] = 1;
        host.State.Tiles.Pollution[host.State.Tiles.Index(12, 12)] = 0.25f;
        PlaceBuilding(host.State!, 12, 13, serviceFlags: WasteCollection.ServiceGarbage, level: 2);
        int slot = host.State!.Households.Allocate();
        host.State.Households.HomeBuildingId[slot] = (ushort)home;
        host.State.Households.MemberCount[slot] = 2;

        host.Services!.RebuildFromWorld(host.State);
        host.Services.UpdateWasteCollection(host.State, days: 0f);

        Assert.True(host.State.WasteCoverageFraction > 0f);
        Assert.InRange(host.State.MeanPollution, 0f, 1f);
        Assert.Equal(1f - host.State.MeanPollution, host.State.MeanEnvironmentScore, precision: 3);

        using var doc = JsonDocument.Parse(host.GetSnapshotJson());
        Assert.True(doc.RootElement.TryGetProperty("wasteCoverageFraction", out var cov));
        Assert.True(doc.RootElement.TryGetProperty("meanPollution", out var pollution));
        Assert.True(doc.RootElement.TryGetProperty("meanEnvironmentScore", out var env));
        Assert.Equal(host.State.WasteCoverageFraction, cov.GetSingle(), precision: 3);
        Assert.Equal(host.State.MeanPollution, pollution.GetSingle(), precision: 3);
        Assert.Equal(host.State.MeanEnvironmentScore, env.GetSingle(), precision: 3);

        var dto = SimSnapshotDto.From(host.GetSnapshot(), host.State);
        Assert.Equal(host.State.WasteCoverageFraction, dto.WasteCoverageFraction, precision: 3);
        Assert.Equal(host.State.MeanPollution, dto.MeanPollution, precision: 3);
        Assert.Equal(host.State.MeanEnvironmentScore, dto.MeanEnvironmentScore, precision: 3);

        var snap = host.GetSnapshot();
        Assert.Equal(host.State.WasteCoverageFraction, snap.WasteCoverageFraction, precision: 3);
        Assert.Equal(host.State.MeanPollution, snap.MeanPollution, precision: 3);
        Assert.Equal(host.State.MeanEnvironmentScore, snap.MeanEnvironmentScore, precision: 3);
    }

    [Fact]
    public void WasteCoverage_LowersPollution_RaisesP4EnvironmentSatisfaction()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        int home = PlaceBuilding(host.State!, 11, 10, serviceFlags: 0);
        host.State!.Tiles.ZoneType[host.State.Tiles.Index(11, 10)] = 1;
        host.State.Tiles.Pollution[host.State.Tiles.Index(11, 10)] = 0.7f;
        host.State.Tiles.Noise[host.State.Tiles.Index(11, 10)] = 0.1f;
        host.State.Tiles.Desirability[host.State.Tiles.Index(11, 10)] = 0f;
        PlaceBuilding(host.State!, 11, 11, serviceFlags: WasteCollection.ServiceGarbage, level: 3);
        int work = PlaceBuilding(host.State!, 20, 20, serviceFlags: 0);

        int slot = host.State!.Households.Allocate();
        host.State.Households.HomeBuildingId[slot] = (ushort)home;
        host.State.Households.WorkBuildingId[slot] = (ushort)work;
        host.State.Households.HealthSatisfaction[slot] = 128;
        host.State.Households.SafetySatisfaction[slot] = 128;
        host.State.Households.LeisureSatisfaction[slot] = 128;
        host.State.Households.MemberCount[slot] = 2;
        host.State.Households.Income[slot] = 1500;

        host.Services!.RebuildFromWorld(host.State);
        var pop = new PopulationSystem(seed: 42);
        float satDirty = pop.CalculateSatisfaction(host.State, slot);

        for (int i = 0; i < 25; i++)
            host.Services.DailyTick(host.State, 1.0);

        float satClean = pop.CalculateSatisfaction(host.State, slot);
        Assert.True(host.State.Tiles.Pollution[host.State.Tiles.Index(11, 10)] < 0.7f);
        Assert.True(satClean > satDirty,
            $"waste→lower pollution should raise P4 satisfaction (dirty={satDirty:F1}, clean={satClean:F1})");
        Assert.True(host.State.WasteCoverageFraction > 0f);
        Assert.True(host.State.MeanEnvironmentScore > 0.3f);
    }

    /// <summary>RNG that always returns 0 so probabilistic spread always succeeds.</summary>
    private sealed class AlwaysLowRandom : Random
    {
        public override double NextDouble() => 0.0;
    }

    /// <summary>RNG with a fixed NextDouble (and Next(size) pinned to mid-map for spark probes).</summary>
    private sealed class FixedDoubleRandom : Random
    {
        private readonly double _value;
        public FixedDoubleRandom(double value) => _value = value;
        public override double NextDouble() => _value;
        public override int Next(int maxValue) => maxValue > 16 ? 16 : 0;
    }

    private static float[] CaptureFreeFlowEdgeCosts(RoadGraph graph)
    {
        var costs = new float[graph.EdgeCount];
        int e = 0;
        for (int n = 0; n < graph.NodeCount; n++)
        {
            foreach (var (_, cost, _) in graph.GetNeighbors(n))
                costs[e++] = cost;
        }

        Assert.Equal(graph.EdgeCount, e);
        return costs;
    }

    private static int PlaceBuilding(
        WorldState state,
        int x,
        int y,
        uint serviceFlags,
        byte level = 1,
        ushort maxOccupants = 20,
        ushort occupants = 10)
    {
        int slot = state.Buildings.Allocate();
        state.Buildings.GridX[slot] = x;
        state.Buildings.GridY[slot] = y;
        state.Buildings.Width[slot] = 1;
        state.Buildings.Height[slot] = 1;
        state.Buildings.ServiceFlags[slot] = serviceFlags;
        state.Buildings.Level[slot] = level;
        state.Buildings.State[slot] = 1;
        state.Buildings.MaxOccupants[slot] = maxOccupants;
        state.Buildings.Occupants[slot] = occupants;
        state.Tiles.BuildingId[state.Tiles.Index(x, y)] = (ushort)slot;
        return slot;
    }
}
