using System.Text.Json;
using Forge.Engine.Data;
using Forge.SimCore;
using Forge.SimWasm;
using Xunit;

namespace Forge.SimCore.Tests;

[Collection("SimHost")]
public sealed class RoadGraphSnapshotTests
{
    [Fact]
    public void SnapshotExport_THighwayLocal_ContainsRampAndHighwayOffTypes()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        Assert.True(host.PlaceRoad(5, 4, RoadTier.HighwayLevel));
        Assert.True(host.PlaceRoad(4, 5, 1));
        Assert.True(host.PlaceRoad(5, 5, 1));
        Assert.True(host.PlaceRoad(6, 5, 1));
        host.Tick(0.001);

        var json = host.GetSnapshotJson();
        var dto = JsonSerializer.Deserialize(json, SnapshotJsonContext.Default.SimSnapshotDto);
        Assert.NotNull(dto);
        Assert.NotNull(dto.RoadGraph);

        var graph = dto.RoadGraph;
        Assert.True(graph.NodeCount >= 2);
        Assert.Equal(graph.NodeCount, graph.NodeTypes.Length);
        Assert.Equal(graph.NodeCount, graph.NodeTileX.Length);
        Assert.Equal(graph.NodeCount, graph.NodeTileZ.Length);

        Assert.Equal(
            (byte)RoadNodeType.Ramp,
            NodeTypeAt(graph, 5, 5));
        Assert.Equal(
            (byte)RoadNodeType.HighwayOff,
            NodeTypeAt(graph, 5, 4));
        Assert.Equal(
            (byte)RoadNodeType.DeadEnd,
            NodeTypeAt(graph, 4, 5));
    }

    [Fact]
    public void RoadGraphSnapshotDto_From_THighwayLocal_MatchesGraphTypes()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        Assert.True(host.PlaceRoad(5, 4, RoadTier.HighwayLevel));
        Assert.True(host.PlaceRoad(4, 5, 1));
        Assert.True(host.PlaceRoad(5, 5, 1));
        Assert.True(host.PlaceRoad(6, 5, 1));
        host.Tick(0.001);

        var exported = RoadGraphSnapshotDto.From(host.State.Roads);
        Assert.True(exported.NodeCount >= 2);
        Assert.Equal(
            (byte)RoadNodeType.Ramp,
            NodeTypeAt(exported, 5, 5));
        Assert.Equal(
            (byte)RoadNodeType.HighwayOff,
            NodeTypeAt(exported, 5, 4));
    }

    [Fact]
    public void RoadGraphSnapshotDto_From_ExportsEdgeEndpointsParallelToVolumes()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        Assert.True(host.PlaceRoad(2, 2, 1));
        Assert.True(host.PlaceRoad(3, 2, 1));
        Assert.True(host.PlaceRoad(4, 2, 1));
        host.Tick(0.001);

        float[] volumes = { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f };
        float[] times = { 1.1f, 1.2f, 1.3f, 1.4f, 1.5f, 1.6f, 1.7f, 1.8f };
        var exported = RoadGraphSnapshotDto.From(
            host.State.Roads,
            volumes,
            times);

        Assert.True(exported.EdgeCount > 0);
        Assert.Equal(exported.EdgeCount, exported.EdgeFrom.Length);
        Assert.Equal(exported.EdgeCount, exported.EdgeTo.Length);
        Assert.Equal(exported.EdgeCount, exported.EdgeVolumes.Length);
        Assert.Equal(exported.EdgeCount, exported.TravelTimes.Length);

        for (int e = 0; e < exported.EdgeCount; e++)
        {
            Assert.InRange(exported.EdgeFrom[e], 0, exported.NodeCount - 1);
            Assert.InRange(exported.EdgeTo[e], 0, exported.NodeCount - 1);
            Assert.NotEqual(exported.EdgeFrom[e], exported.EdgeTo[e]);
        }
    }

    private static byte NodeTypeAt(RoadGraphSnapshotDto graph, int tileX, int tileZ)
    {
        for (int i = 0; i < graph.NodeCount; i++)
        {
            if (graph.NodeTileX[i] == tileX && graph.NodeTileZ[i] == tileZ)
                return graph.NodeTypes[i];
        }

        throw new Xunit.Sdk.XunitException(
            $"No road graph node at ({tileX}, {tileZ}); nodes={graph.NodeCount}");
    }
}
