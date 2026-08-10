using System.Text.Json;
using Forge.Engine.Data;
using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Forge.SimCore;
using Forge.SimWasm;
using Xunit;

namespace Forge.SimCore.Tests;

/// <summary>
/// Characterization tests for Cathedral P5.1 utilities foundation (SB-4987)
/// and P5.2 emergency response time (SB-4242).
/// </summary>
public sealed class CathedralUtilitiesTests
{
    private const uint ServicePowerPlant = 1u << 7;
    private const uint ServiceWaterPump = 1u << 8;
    private const uint ServiceFire = 1u << 3;

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

        // ServiceSystem facade must honor the same BPR edge times.
        host.State.RoadEdgeTravelTimes = congested;
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

    private static void PlaceBuilding(
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
    }
}
