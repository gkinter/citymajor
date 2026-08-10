using Forge.SimCore;
using Xunit;

namespace Forge.SimCore.Tests;

public sealed class RoadTierGraphTests
{
    [Fact]
    public void PlaceRoad_DifferentTiers_ProduceDifferentEdgeLevels()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        host.PlaceRoad(10, 10, 0);
        host.PlaceRoad(11, 10, 0);
        host.Tick(0.001);

        byte dirtLevel = GetFirstEdgeLevel(host);
        Assert.Equal(0, dirtLevel);

        host.Bulldoze(10, 10);
        host.Bulldoze(11, 10);

        host.PlaceRoad(10, 10, 2);
        host.PlaceRoad(11, 10, 2);
        host.Tick(0.001);

        byte highwayLevel = GetFirstEdgeLevel(host);
        Assert.Equal(2, highwayLevel);
        Assert.NotEqual(dirtLevel, highwayLevel);
    }

    [Fact]
    public void PlaceRoad_StoresTierInRoadFlags()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        host.PlaceRoad(5, 5, 2);
        byte flags = host.State.Tiles.RoadFlags[host.State.Tiles.Index(5, 5)];

        Assert.Equal(2, RoadTier.ExtractLevel(flags));
    }

    [Fact]
    public void RoadCapacityForLevel_MatchesArchitectureTable()
    {
        Assert.Equal(200f, RoadTier.RoadCapacityForLevel(0));
        Assert.Equal(800f, RoadTier.RoadCapacityForLevel(1));
        Assert.Equal(2200f, RoadTier.RoadCapacityForLevel(2));
        Assert.True(RoadTier.RoadCapacityForLevel(2) > RoadTier.RoadCapacityForLevel(0));
    }

    [Fact]
    public void RebuildGraph_AssignsTravelCostFromTier()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        host.PlaceRoad(20, 20, 0);
        host.PlaceRoad(21, 20, 0);
        host.Tick(0.001);

        float dirtCost = GetFirstEdgeCost(host);
        Assert.Equal(1.5f, dirtCost, 3);

        host.Bulldoze(20, 20);
        host.Bulldoze(21, 20);

        host.PlaceRoad(20, 20, 2);
        host.PlaceRoad(21, 20, 2);
        host.Tick(0.001);

        float highwayCost = GetFirstEdgeCost(host);
        Assert.Equal(0.7f, highwayCost, 3);
        Assert.True(highwayCost < dirtCost);
    }

    private static byte GetFirstEdgeLevel(SimHost host)
    {
        var roads = host.State.Roads;
        for (int n = 0; n < roads.NodeCount; n++)
        {
            foreach (var (_, _, level) in roads.GetNeighbors(n))
                return level;
        }

        Assert.Fail("No edges in road graph");
        return 0;
    }

    private static float GetFirstEdgeCost(SimHost host)
    {
        var roads = host.State.Roads;
        for (int n = 0; n < roads.NodeCount; n++)
        {
            foreach (var (_, cost, _) in roads.GetNeighbors(n))
                return cost;
        }

        Assert.Fail("No edges in road graph");
        return 0f;
    }
}
