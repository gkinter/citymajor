using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Forge.SimCore;
using Xunit;

namespace Forge.SimCore.Tests;

public sealed class UtilityBalanceTests
{
    private const uint ServicePowerPlant = 1u << 7;
    private const uint ServiceWaterPump = 1u << 8;

    [Fact]
    public void PartitionBalance_PowerPlantCoversLocalPartition()
    {
        const int size = 64;
        var state = new WorldState(size, maxBuildings: 16);
        var services = new ServiceSystem(size, utilityPartitionSize: 32);

        PaintZoneBlock(state, 10, 10, 20, 20, zoneType: 1);
        int plant = PlaceBuilding(state, 12, 12, ServicePowerPlant, level: 2);

        services.L0Tick(state, 1f);

        Assert.True(state.PowerCoverageFraction > 0f);
        Assert.True(services.UtilityBalance.GetPowerBalance(0, 0) > 0f);
        Assert.InRange(state.UtilityStressIndex, 0f, 1f);
        _ = plant;
    }

    [Fact]
    public void PartitionBalance_NoSupply_ReducesCoverage()
    {
        const int size = 64;
        var state = new WorldState(size, maxBuildings: 8);
        var services = new ServiceSystem(size, utilityPartitionSize: 32);

        PaintZoneBlock(state, 0, 0, 31, 31, zoneType: 4);

        services.L0Tick(state, 1f);

        Assert.True(state.PowerCoverageFraction < 1f);
        Assert.True(state.WaterCoverageFraction < 1f);
        Assert.True(state.UtilityStressIndex > 0f);
        Assert.True(state.BlackoutFraction > 0f || state.WaterShortageFraction > 0f);
    }

    [Fact]
    public void AfterSimHostTicks_SnapshotMirrorsUtilityState()
    {
        var host = new SimHost();
        host.Init(128);

        for (int i = 0; i < 20; i++)
            host.Tick(1.0);

        var snap = host.GetSnapshot();

        Assert.False(float.IsNaN(snap.PowerCoverageFraction));
        Assert.False(float.IsNaN(snap.WaterCoverageFraction));
        Assert.False(float.IsNaN(snap.UtilityStressIndex));
        Assert.InRange(snap.PowerCoverageFraction, 0f, 1f);
        Assert.InRange(snap.WaterCoverageFraction, 0f, 1f);
        Assert.InRange(snap.UtilityStressIndex, 0f, 1f);
        Assert.Equal(host.State.PowerCoverageFraction, snap.PowerCoverageFraction, 4);
        Assert.Equal(host.State.WaterCoverageFraction, snap.WaterCoverageFraction, 4);
        Assert.Equal(host.State.UtilityStressIndex, snap.UtilityStressIndex, 4);
    }

    [Fact]
    public void WaterPump_ImprovesLocalWaterBalance()
    {
        const int size = 64;
        var state = new WorldState(size, maxBuildings: 8);
        var services = new ServiceSystem(size, utilityPartitionSize: 32);

        PaintZoneBlock(state, 34, 34, 36, 36, zoneType: 1);
        PlaceBuilding(state, 35, 35, ServiceWaterPump, level: 3);

        services.L0Tick(state, 1f);

        Assert.True(services.UtilityBalance.GetWaterBalance(1, 1) > 0f);
        Assert.True(state.WaterCoverageFraction > 0f);
    }

    [Fact]
    public void RollingFractions_SmoothAcrossTicks()
    {
        const int size = 32;
        var state = new WorldState(size, maxBuildings: 4);
        var services = new ServiceSystem(size, utilityPartitionSize: 32);

        PaintZoneBlock(state, 0, 0, 20, 20, zoneType: 3);

        services.L0Tick(state, 1f);
        float firstBlackout = state.BlackoutFraction;

        services.L0Tick(state, 1f);
        float secondBlackout = state.BlackoutFraction;

        Assert.True(secondBlackout >= firstBlackout);
        Assert.True(secondBlackout <= 1f);
    }

    private static void PaintZoneBlock(WorldState state, int minX, int minY, int maxX, int maxY, byte zoneType)
    {
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            if (!state.Tiles.InBounds(x, y)) continue;
            state.Tiles.ZoneType[state.Tiles.Index(x, y)] = zoneType;
        }
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
