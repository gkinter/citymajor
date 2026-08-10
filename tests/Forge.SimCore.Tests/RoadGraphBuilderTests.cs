using Forge.Engine.Data;
using Forge.SimCore;
using Xunit;

namespace Forge.SimCore.Tests;

public sealed class RoadGraphBuilderTests
{
    [Fact]
    public void StraightFiveTileRoad_TwoEndpointNodes_OneEdgeLengthFour()
    {
        var tiles = new TileData(16);
        PaintStraightRoad(tiles, 2, 2, 2, 6, tier: 1);

        var graph = new RoadGraph(16);
        RoadGraphBuilder.Build(tiles, graph);

        Assert.Equal(2, graph.NodeCount);
        Assert.Equal(2, graph.EdgeCount); // bidirectional

        int nodeA = graph.GetNodeAt(2, 2);
        int nodeB = graph.GetNodeAt(2, 6);
        Assert.True(nodeA >= 0);
        Assert.True(nodeB >= 0);

        foreach (var (_, cost, level) in graph.GetNeighbors(nodeA))
        {
            Assert.Equal(1, level);
            Assert.Equal(4f, cost, 3);
        }
        foreach (var (_, cost, level) in graph.GetNeighbors(nodeB))
        {
            Assert.Equal(1, level);
            Assert.Equal(4f, cost, 3);
        }
    }

    [Fact]
    public void TJunction_FourNodes_OneIntersectionThreeDeadEnds()
    {
        var tiles = new TileData(16);
        // T: north arm + east/west at center (5,5)
        SetRoad(tiles, 5, 4, tier: 1);
        SetRoad(tiles, 4, 5, tier: 1);
        SetRoad(tiles, 5, 5, tier: 1);
        SetRoad(tiles, 6, 5, tier: 1);

        var graph = new RoadGraph(16);
        RoadGraphBuilder.Build(tiles, graph);

        Assert.Equal(4, graph.NodeCount);
        Assert.Equal(6, graph.EdgeCount); // 3 arms × 2 directions

        int centerNode = graph.GetNodeAt(5, 5);
        Assert.True(centerNode >= 0);

        int centerDegree = 0;
        foreach (var _ in graph.GetNeighbors(centerNode))
            centerDegree++;
        Assert.Equal(3, centerDegree);
    }

    [Fact]
    public void MixedTierSegment_EdgeLevelIsMinTierAlongSegment()
    {
        var tiles = new TileData(16);
        SetRoad(tiles, 3, 3, tier: 2);
        SetRoad(tiles, 4, 3, tier: 0);
        SetRoad(tiles, 5, 3, tier: 2);

        var graph = new RoadGraph(16);
        RoadGraphBuilder.Build(tiles, graph);

        Assert.Equal(2, graph.NodeCount);

        int nodeA = graph.GetNodeAt(3, 3);
        int nodeB = graph.GetNodeAt(5, 3);
        Assert.True(nodeA >= 0);
        Assert.True(nodeB >= 0);

        foreach (var (_, _, level) in graph.GetNeighbors(nodeA))
            Assert.Equal(0, level);
        foreach (var (_, _, level) in graph.GetNeighbors(nodeB))
            Assert.Equal(0, level);
    }

    [Fact]
    public void IsNodeTile_DegreeTwoStraight_IsNotNode()
    {
        var tiles = new TileData(8);
        SetRoad(tiles, 1, 1, tier: 1);
        SetRoad(tiles, 2, 1, tier: 1);
        SetRoad(tiles, 3, 1, tier: 1);

        Assert.False(RoadGraphBuilder.IsNodeTile(tiles, 2, 1));
        Assert.True(RoadGraphBuilder.IsNodeTile(tiles, 1, 1));
        Assert.True(RoadGraphBuilder.IsNodeTile(tiles, 3, 1));
    }

    private static void PaintStraightRoad(TileData tiles, int x0, int y0, int x1, int y1, byte tier)
    {
        int dx = Math.Sign(x1 - x0);
        int dy = Math.Sign(y1 - y0);
        int x = x0;
        int y = y0;
        while (true)
        {
            SetRoad(tiles, x, y, tier);
            if (x == x1 && y == y1) break;
            x += dx;
            y += dy;
        }
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
        byte tier = RoadTier.ExtractLevel(tiles.RoadFlags[idx]);
        byte connections = 0;
        if (HasRoad(tiles, x, y - 1)) connections |= 0x01;
        if (HasRoad(tiles, x + 1, y)) connections |= 0x02;
        if (HasRoad(tiles, x, y + 1)) connections |= 0x04;
        if (HasRoad(tiles, x - 1, y)) connections |= 0x08;
        if (connections == 0) connections = 0x01;
        tiles.RoadFlags[idx] = (byte)(connections | ((tier & 0x03) << 4));
    }

    private static bool HasRoad(TileData tiles, int x, int y)
    {
        if (!tiles.InBounds(x, y)) return false;
        return tiles.RoadFlags[tiles.Index(x, y)] != 0;
    }
}
