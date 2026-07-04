using Forge.Engine.Data;
using Xunit;

namespace Forge.Engine.Tests;

public class BuildingDataTests
{
    [Fact]
    public void Allocate_ReturnsValidSlot()
    {
        var buildings = new BuildingData(16);

        int slot = buildings.Allocate();

        Assert.True(slot >= 0 && slot < 16);
        Assert.Equal(1, buildings.Count);
        Assert.True(buildings.IsActive(slot));
    }

    [Fact]
    public void Allocate_SetsDefaultLevelAndCondition()
    {
        var buildings = new BuildingData(16);

        int slot = buildings.Allocate();

        Assert.Equal(1, buildings.Level[slot]);
        Assert.Equal(255, buildings.Condition[slot]);
    }

    [Fact]
    public void Free_ReturnsSlotToPool()
    {
        var buildings = new BuildingData(16);

        int slot = buildings.Allocate();
        buildings.Free(slot);

        Assert.Equal(0, buildings.Count);
        Assert.False(buildings.IsActive(slot));
    }

    [Fact]
    public void Allocate_FullPool_ReturnsNegativeOne()
    {
        var buildings = new BuildingData(2);

        buildings.Allocate();
        buildings.Allocate();
        int result = buildings.Allocate();

        Assert.Equal(-1, result);
    }

    [Fact]
    public void CapacityGrowth_FreeAndReallocate()
    {
        var buildings = new BuildingData(2);

        int a = buildings.Allocate();
        int b = buildings.Allocate();
        Assert.Equal(-1, buildings.Allocate()); // Full

        buildings.Free(a);
        int c = buildings.Allocate(); // Reuses freed slot

        Assert.True(c >= 0);
        Assert.Equal(2, buildings.Count);
    }

    [Fact]
    public void Free_OutOfRange_DoesNotThrow()
    {
        var buildings = new BuildingData(8);

        buildings.Free(-1);
        buildings.Free(100);
        // No exception = pass
    }

    [Fact]
    public void Allocate_AllSlots_CountMatchesCapacity()
    {
        var buildings = new BuildingData(8);

        for (int i = 0; i < 8; i++)
        {
            Assert.True(buildings.Allocate() >= 0);
        }

        Assert.Equal(8, buildings.Count);
        Assert.Equal(8, buildings.Capacity);
    }

    [Fact]
    public void Fields_WritableOnAllocatedSlot()
    {
        var buildings = new BuildingData(4);
        int slot = buildings.Allocate();

        buildings.GridX[slot] = 100;
        buildings.GridY[slot] = 200;
        buildings.TypeId[slot] = 42;
        buildings.Occupants[slot] = 10;
        buildings.Revenue[slot] = -500;

        Assert.Equal(100, buildings.GridX[slot]);
        Assert.Equal(200, buildings.GridY[slot]);
        Assert.Equal(42, buildings.TypeId[slot]);
        Assert.Equal(10, buildings.Occupants[slot]);
        Assert.Equal(-500, buildings.Revenue[slot]);
    }
}

public class HouseholdDataTests
{
    [Fact]
    public void Allocate_ReturnsValidSlot()
    {
        var households = new HouseholdData(32);

        int slot = households.Allocate();

        Assert.True(slot >= 0);
        Assert.Equal(1, households.Count);
        Assert.True(households.IsActive(slot));
    }

    [Fact]
    public void Free_ClearsHomAndWorkBuilding()
    {
        var households = new HouseholdData(32);
        int slot = households.Allocate();

        households.HomeBuildingId[slot] = 5;
        households.WorkBuildingId[slot] = 10;
        households.Free(slot);

        Assert.Equal(0, households.HomeBuildingId[slot]);
        Assert.Equal(0, households.WorkBuildingId[slot]);
        Assert.Equal(0, households.Count);
    }

    [Fact]
    public void FullPool_ReturnsNegativeOne()
    {
        var households = new HouseholdData(2);
        households.Allocate();
        households.Allocate();

        Assert.Equal(-1, households.Allocate());
    }

    [Fact]
    public void WealthClassDistribution_StoredCorrectly()
    {
        var households = new HouseholdData(8);

        int a = households.Allocate();
        int b = households.Allocate();
        int c = households.Allocate();

        households.WealthLevel[a] = 0; // poor
        households.WealthLevel[b] = 2; // middle
        households.WealthLevel[c] = 4; // wealthy

        Assert.Equal(0, households.WealthLevel[a]);
        Assert.Equal(2, households.WealthLevel[b]);
        Assert.Equal(4, households.WealthLevel[c]);
    }

    [Fact]
    public void Free_OutOfRange_DoesNotThrow()
    {
        var households = new HouseholdData(4);
        households.Free(-1);
        households.Free(100);
    }

    [Fact]
    public void AllocateAndFree_CycleWorks()
    {
        var households = new HouseholdData(4);

        for (int round = 0; round < 3; round++)
        {
            var slots = new List<int>();
            for (int i = 0; i < 4; i++)
                slots.Add(households.Allocate());

            Assert.Equal(4, households.Count);

            foreach (int s in slots)
                households.Free(s);

            Assert.Equal(0, households.Count);
        }
    }
}

public class VehicleDataTests
{
    [Fact]
    public void Allocate_ReturnsValidSlot()
    {
        var vehicles = new VehicleData(16);
        int slot = vehicles.Allocate();

        Assert.True(slot >= 0);
        Assert.Equal(1, vehicles.Count);
        Assert.True(vehicles.IsActive(slot));
    }

    [Fact]
    public void Free_ReturnsSlot()
    {
        var vehicles = new VehicleData(16);
        int slot = vehicles.Allocate();
        vehicles.Free(slot);

        Assert.Equal(0, vehicles.Count);
        Assert.False(vehicles.IsActive(slot));
    }

    [Fact]
    public void RouteAssignment_StoredCorrectly()
    {
        var vehicles = new VehicleData(8);
        int slot = vehicles.Allocate();

        vehicles.CurrentRoadNode[slot] = 5;
        vehicles.TargetRoadNode[slot] = 42;
        vehicles.RouteProgress[slot] = 0.75f;
        vehicles.Speed[slot] = 2.5f;
        vehicles.Heading[slot] = 1.57f;

        Assert.Equal(5, vehicles.CurrentRoadNode[slot]);
        Assert.Equal(42, vehicles.TargetRoadNode[slot]);
        Assert.Equal(0.75f, vehicles.RouteProgress[slot]);
        Assert.Equal(2.5f, vehicles.Speed[slot]);
        Assert.Equal(1.57f, vehicles.Heading[slot]);
    }

    [Fact]
    public void FullPool_ReturnsNegativeOne()
    {
        var vehicles = new VehicleData(2);
        vehicles.Allocate();
        vehicles.Allocate();

        Assert.Equal(-1, vehicles.Allocate());
    }

    [Fact]
    public void Free_OutOfRange_DoesNotThrow()
    {
        var vehicles = new VehicleData(4);
        vehicles.Free(-1);
        vehicles.Free(100);
    }
}

public class RoadGraphTests
{
    [Fact]
    public void AddNode_ReturnsIncrementingIds()
    {
        var graph = new RoadGraph(32);

        int a = graph.AddNode(0, 0);
        int b = graph.AddNode(1, 0);
        int c = graph.AddNode(0, 1);

        Assert.Equal(0, a);
        Assert.Equal(1, b);
        Assert.Equal(2, c);
        Assert.Equal(3, graph.NodeCount);
    }

    [Fact]
    public void AddNode_DuplicatePosition_ReturnsSameId()
    {
        var graph = new RoadGraph(32);

        int a = graph.AddNode(5, 10);
        int b = graph.AddNode(5, 10);

        Assert.Equal(a, b);
        Assert.Equal(1, graph.NodeCount);
    }

    [Fact]
    public void GetNodeAt_ExistingNode_ReturnsId()
    {
        var graph = new RoadGraph(32);
        int id = graph.AddNode(7, 3);

        Assert.Equal(id, graph.GetNodeAt(7, 3));
    }

    [Fact]
    public void GetNodeAt_NoNode_ReturnsNegativeOne()
    {
        var graph = new RoadGraph(32);

        Assert.Equal(-1, graph.GetNodeAt(99, 99));
    }

    [Fact]
    public void GetNodePosition_ReturnsStoredCoordinates()
    {
        var graph = new RoadGraph(32);
        int id = graph.AddNode(15, 20);

        var (x, y) = graph.GetNodePosition(id);
        Assert.Equal(15, x);
        Assert.Equal(20, y);
    }

    [Fact]
    public void BuildFromEdgeList_SetsEdgeCount()
    {
        var graph = new RoadGraph(32);
        graph.AddNode(0, 0);
        graph.AddNode(1, 0);
        graph.AddNode(2, 0);

        var edges = new List<(int from, int to, float cost, byte level)>
        {
            (0, 1, 1.0f, 1),
            (1, 2, 1.5f, 2),
        };
        graph.BuildFromEdgeList(edges);

        Assert.Equal(2, graph.EdgeCount);
    }

    [Fact]
    public void CSR_NeighborIteration()
    {
        var graph = new RoadGraph(32);
        int a = graph.AddNode(0, 0);
        int b = graph.AddNode(1, 0);
        int c = graph.AddNode(0, 1);

        var edges = new List<(int from, int to, float cost, byte level)>
        {
            (a, b, 1.0f, 1),
            (a, c, 2.0f, 0),
            (b, c, 1.5f, 2),
        };
        graph.BuildFromEdgeList(edges);

        // Node a should have 2 neighbors
        var aNeighbors = new List<(int target, float cost, byte level)>();
        foreach (var n in graph.GetNeighbors(a))
        {
            aNeighbors.Add(n);
        }
        Assert.Equal(2, aNeighbors.Count);
        Assert.Contains(aNeighbors, n => n.target == b && global::System.Math.Abs(n.cost - 1.0f) < 0.001f);
        Assert.Contains(aNeighbors, n => n.target == c && global::System.Math.Abs(n.cost - 2.0f) < 0.001f);

        // Node b should have 1 neighbor
        var bNeighbors = new List<(int target, float cost, byte level)>();
        foreach (var n in graph.GetNeighbors(b))
        {
            bNeighbors.Add(n);
        }
        Assert.Single(bNeighbors);
        Assert.Equal(c, bNeighbors[0].target);
    }

    [Fact]
    public void CSR_NodeWithNoEdges_EmptyIteration()
    {
        var graph = new RoadGraph(32);
        int a = graph.AddNode(0, 0);
        int b = graph.AddNode(1, 0);

        var edges = new List<(int from, int to, float cost, byte level)>
        {
            (a, b, 1.0f, 1),
        };
        graph.BuildFromEdgeList(edges);

        // Node b has no outgoing edges
        var bNeighbors = new List<(int target, float cost, byte level)>();
        foreach (var n in graph.GetNeighbors(b))
        {
            bNeighbors.Add(n);
        }
        Assert.Empty(bNeighbors);
    }

    [Fact]
    public void BuildFromEdgeList_EmptyEdges()
    {
        var graph = new RoadGraph(32);
        graph.AddNode(0, 0);

        graph.BuildFromEdgeList(new List<(int from, int to, float cost, byte level)>());

        Assert.Equal(0, graph.EdgeCount);
    }

    [Fact]
    public void CSR_EdgeLevelsPreserved()
    {
        var graph = new RoadGraph(32);
        int a = graph.AddNode(0, 0);
        int b = graph.AddNode(1, 0);

        var edges = new List<(int from, int to, float cost, byte level)>
        {
            (a, b, 1.0f, 2), // highway
        };
        graph.BuildFromEdgeList(edges);

        foreach (var n in graph.GetNeighbors(a))
        {
            Assert.Equal(2, n.level);
        }
    }
}
