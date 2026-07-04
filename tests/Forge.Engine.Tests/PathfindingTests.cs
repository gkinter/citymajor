using Forge.Engine.Data;
using Forge.Engine.Math;
using Xunit;

namespace Forge.Engine.Tests;

public class PathfindingTests
{
    // =========================================================================
    // Helper: build a road graph from a simple edge list
    // =========================================================================

    private static RoadGraph BuildGraph(int nodeCount, List<(int from, int to, float cost)> edges)
    {
        var graph = new RoadGraph(nodeCount);

        // Add nodes at grid positions (simple 1D layout for testing).
        for (int i = 0; i < nodeCount; i++)
            graph.AddNode(i, 0);

        // Build bidirectional edge list.
        var edgeList = new List<(int from, int to, float cost, byte level)>();
        foreach (var (from, to, cost) in edges)
        {
            edgeList.Add((from, to, cost, 1));
            edgeList.Add((to, from, cost, 1)); // bidirectional
        }

        graph.BuildFromEdgeList(edgeList);
        return graph;
    }

    private static RoadGraph BuildGridGraph(int width, int height)
    {
        int nodeCount = width * height;
        var graph = new RoadGraph(nodeCount);

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                graph.AddNode(x, y);

        var edges = new List<(int from, int to, float cost, byte level)>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int id = y * width + x;
                if (x + 1 < width)
                {
                    int right = y * width + (x + 1);
                    edges.Add((id, right, 1f, 1));
                    edges.Add((right, id, 1f, 1));
                }
                if (y + 1 < height)
                {
                    int down = (y + 1) * width + x;
                    edges.Add((id, down, 1f, 1));
                    edges.Add((down, id, 1f, 1));
                }
            }
        }

        graph.BuildFromEdgeList(edges);
        return graph;
    }

    // =========================================================================
    // A* Tests (existing functionality)
    // =========================================================================

    [Fact]
    public void AStar_SameNode_ReturnsSingleNode()
    {
        var graph = BuildGraph(3, [(0, 1, 1f), (1, 2, 1f)]);
        var path = Pathfinding.FindPathAStar(graph, 1, 1);

        Assert.Single(path);
        Assert.Equal(1, path[0]);
    }

    [Fact]
    public void AStar_SimpleChain_FindsPath()
    {
        // 0 --1-- 1 --1-- 2 --1-- 3
        var graph = BuildGraph(4, [(0, 1, 1f), (1, 2, 1f), (2, 3, 1f)]);
        var path = Pathfinding.FindPathAStar(graph, 0, 3);

        Assert.Equal(4, path.Count);
        Assert.Equal(0, path[0]);
        Assert.Equal(3, path[3]);
    }

    [Fact]
    public void AStar_NoPath_ReturnsEmpty()
    {
        // 0 -- 1    2 (disconnected)
        var graph = new RoadGraph(3);
        graph.AddNode(0, 0);
        graph.AddNode(1, 0);
        graph.AddNode(2, 0);
        graph.BuildFromEdgeList([(0, 1, 1f, 1), (1, 0, 1f, 1)]);

        var path = Pathfinding.FindPathAStar(graph, 0, 2);
        Assert.Empty(path);
    }

    [Fact]
    public void AStar_ChoosesShorterPath()
    {
        // 0 --10-- 2
        // |         |
        // 1-- 1 -- 1
        // via 0->1->2 (cost 2) is shorter than 0->2 (cost 10)
        var graph = BuildGraph(3, [(0, 1, 1f), (1, 2, 1f), (0, 2, 10f)]);
        var path = Pathfinding.FindPathAStar(graph, 0, 2);

        Assert.Equal(3, path.Count);
        Assert.Equal(new List<int> { 0, 1, 2 }, path);
    }

    // =========================================================================
    // Contraction Hierarchies Tests
    // =========================================================================

    [Fact]
    public void CH_Build_EmptyGraph_Succeeds()
    {
        var graph = new RoadGraph(0);
        graph.BuildFromEdgeList([]);

        var ch = new ContractionHierarchies();
        ch.Build(graph);

        Assert.True(ch.IsBuilt);
    }

    [Fact]
    public void CH_SameNode_ReturnsTrivialPath()
    {
        var graph = BuildGridGraph(10, 10);
        var ch = new ContractionHierarchies();
        ch.Build(graph);

        var result = ch.FindPath(5, 5);
        Assert.True(result.Found);
        Assert.Single(result.NodePath);
        Assert.Equal(0f, result.TotalCost);
    }

    [Fact]
    public void CH_SimpleChain_MatchesAStar()
    {
        var graph = BuildGraph(5, [
            (0, 1, 2f),
            (1, 2, 3f),
            (2, 3, 1f),
            (3, 4, 4f),
        ]);

        // A* reference result.
        var astarPath = Pathfinding.FindPathAStar(graph, 0, 4);

        // CH result.
        var ch = new ContractionHierarchies();
        ch.Build(graph);
        var chResult = ch.FindPath(0, 4);

        Assert.True(chResult.Found);
        // Total cost should match.
        float astarCost = 2f + 3f + 1f + 4f;
        Assert.Equal(astarCost, chResult.TotalCost, 0.001f);
    }

    [Fact]
    public void CH_GridGraph_ShortestPathMatchesAStar()
    {
        // 10x10 grid graph (100 nodes, meets CH threshold).
        var graph = BuildGridGraph(10, 10);

        var ch = new ContractionHierarchies();
        ch.Build(graph);

        // Path from top-left (0) to bottom-right (99).
        // Manhattan distance on a unit grid = 9+9 = 18.
        var chResult = ch.FindPath(0, 99);
        Assert.True(chResult.Found);
        Assert.Equal(18f, chResult.TotalCost, 0.001f);

        // Verify A* gives the same cost.
        var astarPath = Pathfinding.FindPathAStar(graph, 0, 99);
        Assert.NotEmpty(astarPath);

        // Compute A* cost by walking the path.
        float astarCost = astarPath.Count - 1; // Each edge has cost 1.
        Assert.Equal(astarCost, chResult.TotalCost, 0.001f);
    }

    [Fact]
    public void CH_GetDistance_MatchesFindPath()
    {
        var graph = BuildGridGraph(10, 10);
        var ch = new ContractionHierarchies();
        ch.Build(graph);

        float pathDist = ch.FindPath(0, 99).TotalCost;
        float distOnly = ch.GetDistance(0, 99);

        Assert.Equal(pathDist, distOnly, 0.001f);
    }

    [Fact]
    public void CH_GetDistance_SameNode_ReturnsZero()
    {
        var graph = BuildGridGraph(10, 10);
        var ch = new ContractionHierarchies();
        ch.Build(graph);

        Assert.Equal(0f, ch.GetDistance(42, 42));
    }

    [Fact]
    public void CH_NoPath_ReturnsNotFound()
    {
        // Create a graph with two disconnected components.
        var graph = new RoadGraph(200);
        for (int i = 0; i < 200; i++)
            graph.AddNode(i, 0);

        var edges = new List<(int from, int to, float cost, byte level)>();
        // Component A: nodes 0-99 in a chain.
        for (int i = 0; i < 99; i++)
        {
            edges.Add((i, i + 1, 1f, 1));
            edges.Add((i + 1, i, 1f, 1));
        }
        // Component B: nodes 100-199 in a chain.
        for (int i = 100; i < 199; i++)
        {
            edges.Add((i, i + 1, 1f, 1));
            edges.Add((i + 1, i, 1f, 1));
        }
        graph.BuildFromEdgeList(edges);

        var ch = new ContractionHierarchies();
        ch.Build(graph);

        var result = ch.FindPath(0, 150);
        Assert.False(result.Found);
        Assert.Equal(float.PositiveInfinity, ch.GetDistance(0, 150));
    }

    [Fact]
    public void CH_BatchQuery_ReturnsCorrectResults()
    {
        var graph = BuildGridGraph(10, 10);
        var ch = new ContractionHierarchies();
        ch.Build(graph);

        var queries = new (int start, int end)[] { (0, 99), (0, 0), (10, 50) };
        var results = new PathResult[3];

        ch.FindPathsBatch(queries, results);

        Assert.True(results[0].Found);
        Assert.Equal(18f, results[0].TotalCost, 0.001f);

        Assert.True(results[1].Found);
        Assert.Equal(0f, results[1].TotalCost);

        Assert.True(results[2].Found);
    }

    [Fact]
    public void CH_WeightedEdges_FindsOptimalPath()
    {
        // Diamond graph:
        //     0
        //    / \
        //   1   2
        //    \ /
        //     3
        // 0->1: 1, 1->3: 1 (total 2)
        // 0->2: 5, 2->3: 5 (total 10)
        // CH should find the cheaper path through node 1.
        // Need >= 100 nodes for CH threshold, so pad with extra disconnected nodes.
        var graph = new RoadGraph(120);
        for (int i = 0; i < 120; i++)
            graph.AddNode(i, 0);

        var edges = new List<(int from, int to, float cost, byte level)>();
        edges.Add((0, 1, 1f, 1)); edges.Add((1, 0, 1f, 1));
        edges.Add((1, 3, 1f, 1)); edges.Add((3, 1, 1f, 1));
        edges.Add((0, 2, 5f, 1)); edges.Add((2, 0, 5f, 1));
        edges.Add((2, 3, 5f, 1)); edges.Add((3, 2, 5f, 1));

        // Add a backbone chain so we have >= 100 connected nodes.
        for (int i = 3; i < 119; i++)
        {
            edges.Add((i, i + 1, 1f, 1));
            edges.Add((i + 1, i, 1f, 1));
        }

        graph.BuildFromEdgeList(edges);

        var ch = new ContractionHierarchies();
        ch.Build(graph);

        var result = ch.FindPath(0, 3);
        Assert.True(result.Found);
        Assert.Equal(2f, result.TotalCost, 0.001f);
    }

    [Fact]
    public void CH_IncrementalUpdate_StillFindsCorrectPaths()
    {
        var graph = BuildGridGraph(10, 10);

        var ch = new ContractionHierarchies();
        ch.Build(graph);

        // Verify initial path works.
        Assert.True(ch.FindPath(0, 99).Found);

        // Trigger incremental update (will do full rebuild since region is valid).
        ch.UpdateRegion(graph, 5, 5, 3);

        // Paths should still work after update.
        var result = ch.FindPath(0, 99);
        Assert.True(result.Found);
        Assert.Equal(18f, result.TotalCost, 0.001f);
    }

    [Fact]
    public void CH_OutOfBoundsNodes_ReturnsNotFound()
    {
        var graph = BuildGridGraph(10, 10);
        var ch = new ContractionHierarchies();
        ch.Build(graph);

        var result = ch.FindPath(0, 500);
        Assert.False(result.Found);

        Assert.Equal(float.PositiveInfinity, ch.GetDistance(-1, 50));
    }

    // =========================================================================
    // FindPath with CH integration
    // =========================================================================

    [Fact]
    public void FindPath_WithCH_UsesHierarchy()
    {
        var graph = BuildGridGraph(10, 10);
        var ch = new ContractionHierarchies();
        ch.Build(graph);

        var path = Pathfinding.FindPath(graph, 0, 99, ch);
        Assert.NotEmpty(path);
        Assert.Equal(0, path[0]);
        Assert.Equal(99, path[^1]);
    }

    [Fact]
    public void FindPath_WithoutCH_FallsBackToAStar()
    {
        var graph = BuildGraph(4, [(0, 1, 1f), (1, 2, 1f), (2, 3, 1f)]);
        var path = Pathfinding.FindPath(graph, 0, 3);

        Assert.Equal(4, path.Count);
        Assert.Equal(0, path[0]);
        Assert.Equal(3, path[3]);
    }

    // =========================================================================
    // Grid pathfinding tests
    // =========================================================================

    [Fact]
    public void GridPath_SameCell_ReturnsSingleCell()
    {
        var tiles = new TileData(10);
        var path = Pathfinding.FindGridPath(tiles, 3, 3, 3, 3);

        Assert.Single(path);
        Assert.Equal((3, 3), path[0]);
    }

    [Fact]
    public void GridPath_AdjacentCell_FindsPath()
    {
        var tiles = new TileData(10);
        var path = Pathfinding.FindGridPath(tiles, 0, 0, 1, 0);

        Assert.Equal(2, path.Count);
        Assert.Equal((0, 0), path[0]);
        Assert.Equal((1, 0), path[1]);
    }

    [Fact]
    public void GridPath_AvoidsWater()
    {
        var tiles = new TileData(10);

        // Place a wall of water across the middle.
        for (int x = 0; x < 10; x++)
            tiles.TerrainType[tiles.Index(x, 5)] = 3; // Water

        // Leave a gap at x=9.
        tiles.TerrainType[tiles.Index(9, 5)] = 0; // Grass

        var path = Pathfinding.FindGridPath(tiles, 0, 0, 0, 9);

        // Path should exist (going around through x=9).
        Assert.NotEmpty(path);
        Assert.Equal((0, 0), path[0]);
        Assert.Equal((0, 9), path[^1]);

        // Path should not contain any water tiles.
        foreach (var (x, y) in path)
        {
            int idx = tiles.Index(x, y);
            Assert.NotEqual(3, tiles.TerrainType[idx]);
        }
    }

    [Fact]
    public void GridPath_BlockedCompletely_ReturnsEmpty()
    {
        var tiles = new TileData(10);

        // Surround the start with water.
        tiles.TerrainType[tiles.Index(1, 0)] = 3;
        tiles.TerrainType[tiles.Index(0, 1)] = 3;

        var path = Pathfinding.FindGridPath(tiles, 0, 0, 9, 9);
        Assert.Empty(path);
    }

    // =========================================================================
    // Performance sanity: CH on a larger graph should not take forever
    // =========================================================================

    [Fact]
    public void CH_LargeGrid_BuildsAndQueries()
    {
        // 50x50 = 2500 nodes. Should build in well under 1 second.
        var graph = BuildGridGraph(50, 50);

        var ch = new ContractionHierarchies();
        ch.Build(graph);

        Assert.True(ch.IsBuilt);
        Assert.Equal(2500, ch.NodeCount);

        // Query corner to corner.
        var result = ch.FindPath(0, 2499);
        Assert.True(result.Found);
        Assert.Equal(98f, result.TotalCost, 0.001f); // Manhattan: 49+49
    }
}
