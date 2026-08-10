using System.Text.Json;
using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Forge.SimCore;
using Forge.SimWasm;
using Xunit;

namespace Forge.SimCore.Tests;

/// <summary>
/// Characterization tests for Cathedral P5.1 utilities foundation (SB-4987).
/// Pins power/water coverage export on WorldState → snapshot/status JSON before
/// emergency-response (P5.2) and fire-spread (P5.3) deepen.
/// </summary>
public sealed class CathedralUtilitiesTests
{
    private const uint ServicePowerPlant = 1u << 7;
    private const uint ServiceWaterPump = 1u << 8;

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

    [Fact(Skip = "P5.2 emergency response time (distance + traffic) not wired yet")]
    public void EmergencyResponseTime_UsesDistancePlusTraffic()
    {
        // Placeholder: assert response minutes = graph distance / speed × congestion.
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
