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
/// and Tier-2 wildfire / arson rings.
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

    /// <summary>RNG that always returns 0 so probabilistic spread always succeeds.</summary>
    private sealed class AlwaysLowRandom : Random
    {
        public override double NextDouble() => 0.0;
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
