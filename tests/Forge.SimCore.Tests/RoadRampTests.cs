using Forge.Engine.Data;
using Forge.SimCore;
using Xunit;

namespace Forge.SimCore.Tests;

public sealed class RoadRampTests
{
    [Fact]
    public void THighwayLocalJunction_ClassifiesRampNodes()
    {
        var tiles = new TileData(16);
        // T: north highway arm + east/west local at center (5,5)
        SetRoad(tiles, 5, 4, tier: RoadTier.HighwayLevel);
        SetRoad(tiles, 4, 5, tier: 1);
        SetRoad(tiles, 5, 5, tier: 1);
        SetRoad(tiles, 6, 5, tier: 1);

        var graph = new RoadGraph(16);
        RoadGraphBuilder.Build(tiles, graph);

        int centerNode = graph.GetNodeAt(5, 5);
        Assert.True(centerNode >= 0);
        Assert.Equal(RoadNodeType.Ramp, graph.GetNodeType(centerNode));

        int northNode = graph.GetNodeAt(5, 4);
        Assert.True(northNode >= 0);
        Assert.Equal(RoadNodeType.HighwayOff, graph.GetNodeType(northNode));
    }

    [Fact]
    public void HighwayOnRamp_LocalDeadEndToHighway_ClassifiesHighwayOn()
    {
        var tiles = new TileData(16);
        SetRoad(tiles, 4, 5, tier: 1);
        SetRoad(tiles, 5, 5, tier: RoadTier.HighwayLevel);

        var graph = new RoadGraph(16);
        RoadGraphBuilder.Build(tiles, graph);

        int localNode = graph.GetNodeAt(4, 5);
        int highwayNode = graph.GetNodeAt(5, 5);
        Assert.True(localNode >= 0);
        Assert.True(highwayNode >= 0);
        Assert.Equal(RoadNodeType.HighwayOn, graph.GetNodeType(localNode));
        Assert.Equal(RoadNodeType.HighwayOff, graph.GetNodeType(highwayNode));
    }

    [Fact]
    public void PlaceRoad_LocalBetweenTwoHighways_Rejected()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        host.PlaceRoad(10, 10, RoadTier.HighwayLevel);
        host.PlaceRoad(12, 10, RoadTier.HighwayLevel);

        bool placed = host.PlaceRoad(11, 10, 1);
        Assert.False(placed);
        Assert.Equal(0, host.State.Tiles.RoadFlags[host.State.Tiles.Index(11, 10)]);
    }

    [Fact]
    public void PlaceRoad_SecondHighwayArmOnRampConnector_Rejected()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        host.PlaceRoad(20, 20, RoadTier.HighwayLevel);
        host.PlaceRoad(21, 20, 1);

        bool placed = host.PlaceRoad(21, 19, RoadTier.HighwayLevel);
        Assert.False(placed);
    }

    [Fact]
    public void PlaceRoad_ValidRampConnector_Allowed()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        Assert.True(host.PlaceRoad(30, 30, RoadTier.HighwayLevel));
        Assert.True(host.PlaceRoad(31, 30, 1));
        host.Tick(0.001);

        int rampNode = host.State.Roads.GetNodeAt(31, 30);
        Assert.True(rampNode >= 0);
        Assert.Equal(RoadNodeType.HighwayOn, host.State.Roads.GetNodeType(rampNode));
    }

    [Fact]
    public void PlaceRoad_RampFlag_SetsStructureBitsAndRequiresHighwayNeighbor()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        Assert.True(host.PlaceRoad(40, 40, RoadTier.HighwayLevel));

        Assert.False(host.PlaceRoad(42, 40, 1, ramp: true)); // not adjacent
        Assert.True(host.PlaceRoad(41, 40, 1, ramp: true));

        byte flags = host.State.Tiles.RoadFlags[host.State.Tiles.Index(41, 40)];
        Assert.True(RoadFlags.IsRamp(flags));
        Assert.False(RoadFlags.IsBridge(flags));
        Assert.False(RoadFlags.IsTunnel(flags));
        Assert.Equal(1, RoadTier.ExtractLevel(flags));
    }

    [Fact]
    public void PlaceRoad_RampTool_ClampsHighwayTierToCollector()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        Assert.True(host.PlaceRoad(50, 50, RoadTier.HighwayLevel));
        Assert.True(host.PlaceRoad(51, 50, RoadTier.HighwayLevel, ramp: true));

        byte flags = host.State.Tiles.RoadFlags[host.State.Tiles.Index(51, 50)];
        Assert.True(RoadFlags.IsRamp(flags));
        Assert.Equal(1, RoadTier.ExtractLevel(flags));
    }

    [Fact]
    public void PlaceRoad_RampExtension_FromExistingRamp_Allowed()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        Assert.True(host.PlaceRoad(60, 60, RoadTier.HighwayLevel));
        Assert.True(host.PlaceRoad(61, 60, 1, ramp: true));
        Assert.True(host.PlaceRoad(62, 60, 1, ramp: true));

        Assert.True(RoadFlags.IsRamp(host.State.Tiles.RoadFlags[host.State.Tiles.Index(62, 60)]));
    }

    [Fact]
    public void ClassifyNodeType_TJunction_AllLocal_IsIntersection()
    {
        var tiles = new TileData(16);
        SetRoad(tiles, 5, 4, tier: 1);
        SetRoad(tiles, 4, 5, tier: 1);
        SetRoad(tiles, 5, 5, tier: 1);
        SetRoad(tiles, 6, 5, tier: 1);

        Assert.Equal(RoadNodeType.Intersection, RoadGraphBuilder.ClassifyNodeType(tiles, 5, 5));
    }

    private static void SetRoad(TileData tiles, int x, int y, byte tier)
    {
        byte connections = 0;
        if (HasRoad(tiles, x, y - 1)) connections |= 0x01;
        if (HasRoad(tiles, x + 1, y)) connections |= 0x02;
        if (HasRoad(tiles, x, y + 1)) connections |= 0x04;
        if (HasRoad(tiles, x - 1, y)) connections |= 0x08;
        if (connections == 0) connections = 0x01;
        tiles.RoadFlags[tiles.Index(x, y)] = (byte)(connections | ((tier & 0x03) << 4));

        RefreshRoad(tiles, x - 1, y);
        RefreshRoad(tiles, x + 1, y);
        RefreshRoad(tiles, x, y - 1);
        RefreshRoad(tiles, x, y + 1);
    }

    private static void RefreshRoad(TileData tiles, int x, int y)
    {
        if (!tiles.InBounds(x, y)) return;
        int idx = tiles.Index(x, y);
        if (tiles.RoadFlags[idx] == 0) return;
        byte existing = tiles.RoadFlags[idx];
        byte tier = RoadTier.ExtractLevel(existing);
        byte connections = 0;
        if (HasRoad(tiles, x, y - 1)) connections |= 0x01;
        if (HasRoad(tiles, x + 1, y)) connections |= 0x02;
        if (HasRoad(tiles, x, y + 1)) connections |= 0x04;
        if (HasRoad(tiles, x - 1, y)) connections |= 0x08;
        if (connections == 0) connections = 0x01;
        byte flags = (byte)(connections | ((tier & 0x03) << 4));
        if (RoadFlags.IsRamp(existing)) flags |= RoadFlags.Ramp;
        else if (RoadFlags.IsBridge(existing)) flags |= RoadFlags.Bridge;
        else if (RoadFlags.IsTunnel(existing)) flags |= RoadFlags.Tunnel;
        tiles.RoadFlags[idx] = flags;
    }

    private static bool HasRoad(TileData tiles, int x, int y)
    {
        if (!tiles.InBounds(x, y)) return false;
        return tiles.RoadFlags[tiles.Index(x, y)] != 0;
    }
}
