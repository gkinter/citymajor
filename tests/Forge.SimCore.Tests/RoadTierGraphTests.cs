using Forge.Engine.Data;
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

    [Fact]
    public void PlaceRoad_StoresBridgeAndTunnelInRoadFlags()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        host.PlaceRoad(7, 7, 1, bridge: true);
        byte bridgeFlags = host.State.Tiles.RoadFlags[host.State.Tiles.Index(7, 7)];
        Assert.True(RoadFlags.IsBridge(bridgeFlags));
        Assert.False(RoadFlags.IsTunnel(bridgeFlags));
        Assert.False(RoadFlags.IsRamp(bridgeFlags));

        host.PlaceRoad(8, 7, 1, tunnel: true);
        byte tunnelFlags = host.State.Tiles.RoadFlags[host.State.Tiles.Index(8, 7)];
        Assert.False(RoadFlags.IsBridge(tunnelFlags));
        Assert.True(RoadFlags.IsTunnel(tunnelFlags));
        Assert.False(RoadFlags.IsRamp(tunnelFlags));
    }

    [Fact]
    public void PlaceRoad_BridgeIncreasesGraphEdgeCost()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        host.PlaceRoad(30, 30, 1);
        host.PlaceRoad(31, 30, 1);
        host.PlaceRoad(32, 30, 1, bridge: true);
        host.PlaceRoad(33, 30, 1);
        host.Tick(0.001);

        float plainCost = 0f;
        var hostPlain = new SimHost();
        hostPlain.Init(64, new SimHostInitOptions { SkipStarterCity = true });
        for (int x = 30; x <= 33; x++)
            hostPlain.PlaceRoad(x, 30, 1);
        hostPlain.Tick(0.001);
        plainCost = GetSegmentCost(hostPlain, 30, 30);

        float bridgeCost = GetSegmentCost(host, 30, 30);
        Assert.Equal(plainCost + 0.15f, bridgeCost, 3);
    }

    private static float GetSegmentCost(SimHost host, int x, int y)
    {
        var roads = host.State.Roads;
        int node = roads.GetNodeAt(x, y);
        Assert.True(node >= 0);
        foreach (var (_, cost, _) in roads.GetNeighbors(node))
            return cost;
        Assert.Fail("No edges from node");
        return 0f;
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
