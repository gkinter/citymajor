using System.Text.Json;
using Forge.SimCore;
using Forge.SimWasm;
using Xunit;

namespace Forge.SimCore.Tests;

/// <summary>Characterization tests for Cathedral P4 population O-D truth.</summary>
public sealed class CathedralPopulationTests
{
    [Fact]
    public void Bootstrap_MinimalWorld_AssignsHomeAndWork()
    {
        var state = new Forge.Engine.Simulation.WorldState(32, maxHouseholds: 16, maxBuildings: 16);
        var pop = new Forge.Game.Simulation.PopulationSystem();

        int resId = state.Buildings.Allocate();
        state.Buildings.GridX[resId] = 10;
        state.Buildings.GridY[resId] = 10;
        state.Buildings.TypeId[resId] = 101;
        state.Buildings.State[resId] = 1;
        state.Buildings.MaxOccupants[resId] = 20;
        state.Tiles.ZoneType[state.Tiles.Index(10, 10)] = 1;

        int comId = state.Buildings.Allocate();
        state.Buildings.GridX[comId] = 20;
        state.Buildings.GridY[comId] = 20;
        state.Buildings.TypeId[comId] = 301;
        state.Buildings.State[comId] = 1;
        state.Buildings.MaxOccupants[comId] = 20;
        state.Tiles.ZoneType[state.Tiles.Index(20, 20)] = 3;

        int slot = state.Households.Allocate();
        state.Households.MemberCount[slot] = 2;
        state.Households.AgeGroup[slot] = 1;
        state.Households.Education[slot] = 1;

        pop.BootstrapCommuterAssignments(state);

        Assert.NotEqual(0, state.Households.HomeBuildingId[slot]);
        Assert.NotEqual(0, state.Households.WorkBuildingId[slot]);
        Assert.Equal(1, pop.AuditCommuters(state).WorkingCommuters);
    }

    [Fact]
    public void SeededHousehold_CommuteOdSample_MatchesHomeAndWorkTiles()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        int homeX = 12;
        int homeY = 13;
        int workX = 40;
        int workY = 41;

        for (int y = homeY; y <= workY; y += 14)
            host.PlaceRoad(homeX - 1, y);
        for (int x = homeX - 1; x <= workX; x += 14)
            host.PlaceRoad(x, homeY - 1);

        host.PaintZone(homeX, homeY, zoneType: 1);
        host.PaintZone(workX, workY, zoneType: 3);

        int homeId = PlaceTestBuilding(host, homeX, homeY, zone: 1, typeId: 101);
        int workId = PlaceTestBuilding(host, workX, workY, zone: 3, typeId: 301);
        Assert.True(homeId >= 0, $"home allocate failed ({homeId})");
        Assert.True(workId >= 0, $"work allocate failed ({workId})");
        Assert.NotEqual(homeId, workId);
        Assert.NotEqual(0, homeId);
        Assert.NotEqual(0, workId);

        int slot = host.State.Households.Allocate();
        host.State.Households.MemberCount[slot] = 3;
        host.State.Households.AgeGroup[slot] = 1;
        host.State.Households.Education[slot] = 1;
        host.State.Households.Income[slot] = 1800;
        host.State.Households.HomeBuildingId[slot] = (ushort)homeId;
        host.State.Households.WorkBuildingId[slot] = (ushort)workId;
        host.State.Households.Flags[slot] = 1;
        host.State.Buildings.Occupants[homeId] = 1;
        host.State.Buildings.Occupants[workId] = 1;
        host.State.Population = 3;

        Assert.True(host.State.Households.IsActive(slot));
        Assert.Equal(1, host.State.Households.AgeGroup[slot]);
        Assert.NotEqual(0, host.State.Households.HomeBuildingId[slot]);
        Assert.NotEqual(0, host.State.Households.WorkBuildingId[slot]);

        var audit = host.Population.AuditCommuters(host.State);
        Assert.Equal(1, audit.WorkingCommuters);
        Assert.Equal(1, audit.AssignedCommuters);

        var json = host.GetSnapshotJson();
        var dto = JsonSerializer.Deserialize(json, SnapshotJsonContext.Default.SimSnapshotDto);
        Assert.NotNull(dto);
        Assert.True(dto.CommuterCoverage >= 0.99f, $"expected full coverage, got {dto.CommuterCoverage}");

        var pair = Assert.Single(dto.CommuteOdSample);
        Assert.Equal(homeX, pair.HomeTileX);
        Assert.Equal(homeY, pair.HomeTileZ);
        Assert.Equal(workX, pair.WorkTileX);
        Assert.Equal(workY, pair.WorkTileZ);
        Assert.Equal(1, pair.TripCount);
    }

    [Fact]
    public void StarterCity_AfterBootstrap_CommuterCoverageAtLeastNinetyPercent()
    {
        var host = new SimHost();
        host.Init(64);

        var audit = host.Population.AuditCommuters(host.State);
        int housed = 0;
        for (int i = 0; i < host.State.Households.Capacity; i++)
        {
            if (!host.State.Households.IsActive(i)) continue;
            if (host.State.Households.HomeBuildingId[i] != 0) housed++;
        }

        Assert.True(audit.WorkingCommuters > 0,
            $"starter city should have working commuters (housed={housed}, buildings={host.State.Buildings.Count})");
        Assert.True(audit.Coverage >= 0.9f,
            $"expected >=90% commuter coverage, got {audit.Coverage:P1} " +
            $"({audit.AssignedCommuters}/{audit.WorkingCommuters})");
    }

    [Fact]
    public void CommuterCoverage_MatchesSnapshotExport()
    {
        var host = new SimHost();
        host.Init(64);

        for (int i = 0; i < 30; i++)
            host.Tick(1.0);

        var audit = host.Population.AuditCommuters(host.State);
        var json = host.GetSnapshotJson();
        var dto = JsonSerializer.Deserialize(json, SnapshotJsonContext.Default.SimSnapshotDto);
        Assert.NotNull(dto);

        Assert.Equal(audit.Coverage, dto.CommuterCoverage, precision: 4);
        Assert.True(dto.CommuteOdSample.Length > 0,
            "snapshot should include sample O-D pairs when commuters are assigned");
    }

    [Fact]
    public void CollectHouseholdSample_ScalesToOneHundredRows()
    {
        var state = new Forge.Engine.Simulation.WorldState(32, maxHouseholds: 160, maxBuildings: 32);
        var pop = new Forge.Game.Simulation.PopulationSystem();

        int homeId = state.Buildings.Allocate();
        state.Buildings.GridX[homeId] = 8;
        state.Buildings.GridY[homeId] = 8;
        state.Buildings.TypeId[homeId] = 101;
        state.Buildings.State[homeId] = 1;
        state.Buildings.MaxOccupants[homeId] = 200;

        int workId = state.Buildings.Allocate();
        state.Buildings.GridX[workId] = 20;
        state.Buildings.GridY[workId] = 20;
        state.Buildings.TypeId[workId] = 301;
        state.Buildings.State[workId] = 1;
        state.Buildings.MaxOccupants[workId] = 200;

        for (int i = 0; i < 120; i++)
        {
            int slot = state.Households.Allocate();
            Assert.True(slot >= 0);
            state.Households.MemberCount[slot] = 2;
            state.Households.AgeGroup[slot] = 1;
            state.Households.Education[slot] = 1;
            state.Households.Income[slot] = 2000;
            state.Households.Happiness[slot] = (byte)(40 + (i % 200));
            state.Households.HomeBuildingId[slot] = (ushort)homeId;
            state.Households.WorkBuildingId[slot] = (ushort)(i % 3 == 0 ? 0 : workId);
            state.Households.Flags[slot] = 1;
        }

        state.Population = 240;
        pop.RefreshHousingSnapshotMetrics(state);

        var sample = pop.CollectHouseholdSample(state);
        Assert.Equal(Forge.Game.Simulation.PopulationSystem.DefaultL2SampleLimit, sample.Length);
        Assert.Equal(100, sample.Length);

        // Sorted by happiness desc.
        for (int i = 1; i < sample.Length; i++)
            Assert.True(sample[i - 1].Happiness >= sample[i].Happiness);

        Assert.Contains(sample, row => row.WorkBuildingId == workId);
        Assert.Contains(sample, row => row.WorkBuildingId == 0);
        Assert.All(sample, row =>
        {
            Assert.Equal(homeId, row.HomeBuildingId);
            Assert.True(row.RentBurden >= 0f);
            Assert.Equal(8, row.TileX);
            Assert.Equal(8, row.TileZ);
        });
    }

    [Fact]
    public void PopulationL2_ExportIncludesJobCommuteAndRentBurden()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        int homeX = 10;
        int homeY = 11;
        int workX = 30;
        int workY = 31;

        host.PaintZone(homeX, homeY, zoneType: 1);
        host.PaintZone(workX, workY, zoneType: 3);
        int homeId = PlaceTestBuilding(host, homeX, homeY, zone: 1, typeId: 101);
        int workId = PlaceTestBuilding(host, workX, workY, zone: 3, typeId: 301);
        Assert.True(homeId > 0);
        Assert.True(workId > 0);

        int slot = host.State.Households.Allocate();
        host.State.Households.MemberCount[slot] = 3;
        host.State.Households.AgeGroup[slot] = 1;
        host.State.Households.Education[slot] = 1;
        host.State.Households.Income[slot] = 1800;
        host.State.Households.Happiness[slot] = 200;
        host.State.Households.HomeBuildingId[slot] = (ushort)homeId;
        host.State.Households.WorkBuildingId[slot] = (ushort)workId;
        host.State.Households.Flags[slot] = 1;
        host.State.Buildings.Occupants[homeId] = 1;
        host.State.Buildings.Occupants[workId] = 1;
        host.State.Population = 3;
        host.Population.RefreshHousingSnapshotMetrics(host.State);

        var l2 = host.GetPopulationL2();
        var row = Assert.Single(l2.Households);
        Assert.Equal($"HH-{slot:D5}", row.Id);
        Assert.Equal(homeId, row.HomeBuildingId);
        Assert.Equal(workId, row.WorkBuildingId);
        Assert.Equal(homeX, row.TileX);
        Assert.Equal(homeY, row.TileZ);
        Assert.True(row.CommuteMin > 0f, $"expected positive commute, got {row.CommuteMin}");
        Assert.True(row.RentBurden > 0f, $"expected positive rent burden, got {row.RentBurden}");

        var json = host.GetSnapshotJson();
        var dto = JsonSerializer.Deserialize(json, SnapshotJsonContext.Default.SimSnapshotDto);
        Assert.NotNull(dto);
        var exported = Assert.Single(dto.PopulationL2.Households);
        Assert.Equal(workId, exported.WorkBuildingId);
        Assert.Equal(row.RentBurden, exported.RentBurden, precision: 4);
        Assert.True(dto.PopulationL2.Households.Length <=
            Forge.Game.Simulation.PopulationSystem.DefaultL2SampleLimit);
    }

    private static int PlaceTestBuilding(SimHost host, int x, int y, byte zone, ushort typeId)
    {
        int id = host.State.Buildings.Allocate();
        if (id < 0) return -1;

        int idx = host.State.Tiles.Index(x, y);
        host.State.Buildings.GridX[id] = x;
        host.State.Buildings.GridY[id] = y;
        host.State.Buildings.TypeId[id] = typeId;
        host.State.Buildings.State[id] = 1;
        host.State.Buildings.MaxOccupants[id] = 48;
        host.State.Tiles.ZoneType[idx] = zone;
        host.State.Tiles.BuildingId[idx] = (ushort)id;
        return id;
    }
}
