using Forge.SimCore;
using Xunit;

namespace Forge.SimCore.Tests;

/// <summary>Cathedral P4.2 — graph commute time feeds household satisfaction (SB-4232).</summary>
public sealed class CathedralCommuteTests
{
    [Fact]
    public void LongerRoadGraphPath_HasHigherTravelCostThanShortPath()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        const int homeX = 10;
        const int homeY = 10;
        const int workNearX = 18;
        const int workNearY = 10;
        const int workFarX = 28;
        const int workFarY = 10;

        for (int x = 8; x <= 30; x++)
            host.PlaceRoad(x, 9);

        int homeId = PlaceTestBuilding(host, homeX, homeY, zone: 1, typeId: 101);
        int workNearId = PlaceTestBuilding(host, workNearX, workNearY, zone: 3, typeId: 301);
        int workFarId = PlaceTestBuilding(host, workFarX, workFarY, zone: 3, typeId: 301);

        for (int i = 0; i < 10; i++)
            host.Tick(1.0);

        float nearCost = CommuteTravelTime.CalculateTravelCost(
            host.State, homeId, workNearId, host.State.RoadEdgeTravelTimes);
        float farCost = CommuteTravelTime.CalculateTravelCost(
            host.State, homeId, workFarId, host.State.RoadEdgeTravelTimes);

        Assert.True(nearCost < farCost,
            $"expected near work travel cost ({nearCost:F2}) < far work ({farCost:F2})");
        Assert.True(nearCost < Euclidean(homeX, homeY, workFarX, workFarY),
            "graph path to near work should be shorter than crow-flies to far work");
    }

    [Fact]
    public void LongerCommute_LowersHouseholdSatisfaction()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        for (int x = 8; x <= 30; x++)
            host.PlaceRoad(x, 9);

        int homeId = PlaceTestBuilding(host, 10, 10, zone: 1, typeId: 101);
        int workNearId = PlaceTestBuilding(host, 16, 10, zone: 3, typeId: 301);
        int workFarId = PlaceTestBuilding(host, 28, 10, zone: 3, typeId: 301);

        int slot = AllocateWorkingCommuter(host, homeId, workNearId);
        for (int i = 0; i < 10; i++)
            host.Tick(1.0);

        float satNear = host.Population.CalculateSatisfaction(host.State, slot);

        host.State.Households.WorkBuildingId[slot] = (ushort)workFarId;
        float satFar = host.Population.CalculateSatisfaction(host.State, slot);

        Assert.True(satNear > satFar,
            $"longer commute should lower satisfaction (near={satNear:F1}, far={satFar:F1})");
    }

    [Fact]
    public void CollectCommuteHudMetrics_ReportsOdCoverageAndCommuteSatisfaction()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        for (int x = 8; x <= 20; x++)
            host.PlaceRoad(x, 9);

        // Warm the road graph without risking building churn on hand-allocated IDs.
        for (int i = 0; i < 4; i++)
            host.Tick(1.0);

        int homeId = PlaceTestBuilding(host, 10, 10, zone: 1, typeId: 101);
        int workId = PlaceTestBuilding(host, 16, 10, zone: 3, typeId: 301);
        Assert.True(homeId > 0 && workId > 0 && homeId != workId);
        AllocateWorkingCommuter(host, homeId, workId);

        var (meanMin, coverage, meanSat) = host.CollectCommuteHudMetrics();
        var audit = host.Population.AuditCommuters(host.State);

        Assert.Equal(1, audit.WorkingCommuters);
        Assert.Equal(1, audit.AssignedCommuters);
        Assert.Equal(audit.Coverage, coverage, precision: 4);
        Assert.True(coverage >= 0.99f, $"expected full O-D coverage, got {coverage}");
        Assert.True(meanMin > 0f, $"expected positive mean commute minutes, got {meanMin}");
        Assert.InRange(meanSat, 0f, 1f);
        Assert.True(meanSat > 0.2f, $"expected usable commute satisfaction, got {meanSat}");
    }

    [Fact]
    public void ConnectedRoadPath_BeatsEuclideanFallbackWhenDetourIsShorter()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        const int homeX = 10;
        const int homeY = 10;
        const int workX = 12;
        const int workY = 12;

        // Direct diagonal is ~2.8 tiles but no road — fallback would be short.
        for (int y = 9; y <= 32; y++)
            host.PlaceRoad(9, y);
        for (int x = 9; x <= 13; x++)
            host.PlaceRoad(x, 32);
        for (int y = 12; y <= 32; y++)
            host.PlaceRoad(13, y);

        int homeId = PlaceTestBuilding(host, homeX, homeY, zone: 1, typeId: 101);
        int workId = PlaceTestBuilding(host, workX, workY, zone: 3, typeId: 301);

        for (int i = 0; i < 8; i++)
            host.Tick(1.0);

        float graphCost = CommuteTravelTime.CalculateTravelCost(
            host.State, homeId, workId, host.State.RoadEdgeTravelTimes);
        float euclidean = Euclidean(homeX, homeY, workX, workY);

        Assert.True(graphCost > euclidean * 2f,
            $"detour road path ({graphCost:F2}) should exceed crow-flies ({euclidean:F2})");
    }

    private static int AllocateWorkingCommuter(SimHost host, int homeId, int workId)
    {
        int slot = host.State.Households.Allocate();
        host.State.Households.MemberCount[slot] = 2;
        host.State.Households.AgeGroup[slot] = 1;
        host.State.Households.Education[slot] = 1;
        host.State.Households.Income[slot] = 2000;
        host.State.Households.HomeBuildingId[slot] = (ushort)homeId;
        host.State.Households.WorkBuildingId[slot] = (ushort)workId;
        host.State.Households.Flags[slot] = 1;
        host.State.Buildings.Occupants[homeId]++;
        host.State.Buildings.Occupants[workId]++;
        return slot;
    }

    private static int PlaceTestBuilding(SimHost host, int x, int y, byte zone, ushort typeId)
    {
        int id = host.State.Buildings.Allocate();
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

    private static float Euclidean(int ax, int ay, int bx, int by)
    {
        float dx = ax - bx;
        float dy = ay - by;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
