namespace Forge.Engine.Data;

/// <summary>
/// Compressed Sparse Row (CSR) road network graph. Each node is an intersection,
/// edges are road segments with travel cost. Supports efficient neighbor iteration
/// for pathfinding algorithms (A*, Dijkstra, contraction hierarchies).
/// </summary>
public sealed class RoadGraph
{
    private int _nodeCount;
    private int _edgeCount;

    // CSR structure
    private int[] _rowOffsets;    // Per node: start index into _colIndices (size: nodeCount + 1)
    private int[] _colIndices;    // Target node for each edge
    private float[] _edgeCosts;   // Travel cost for each edge
    private byte[] _edgeLevels;   // Road level: 0=dirt, 1=paved, 2=highway

    // Node data
    private int[] _nodeGridX;     // Grid position of each intersection
    private int[] _nodeGridY;

    // Lookup: grid position -> node ID (-1 if no node)
    private readonly Dictionary<long, int> _gridToNode = new();

    public int NodeCount => _nodeCount;
    public int EdgeCount => _edgeCount;

    public RoadGraph(int maxNodes)
    {
        _rowOffsets = new int[maxNodes + 1];
        _colIndices = new int[maxNodes * 4]; // Assume avg 4 edges per node
        _edgeCosts = new float[maxNodes * 4];
        _edgeLevels = new byte[maxNodes * 4];
        _nodeGridX = new int[maxNodes];
        _nodeGridY = new int[maxNodes];
    }

    /// <summary>Add a node at the given grid position. Returns node ID.</summary>
    public int AddNode(int gridX, int gridY)
    {
        long key = PackKey(gridX, gridY);
        if (_gridToNode.TryGetValue(key, out int existing))
            return existing;

        int id = _nodeCount++;
        _nodeGridX[id] = gridX;
        _nodeGridY[id] = gridY;
        _gridToNode[key] = id;
        return id;
    }

    /// <summary>Get the node ID at a grid position, or -1 if none.</summary>
    public int GetNodeAt(int gridX, int gridY)
    {
        long key = PackKey(gridX, gridY);
        return _gridToNode.GetValueOrDefault(key, -1);
    }

    /// <summary>
    /// Rebuild the CSR structure from an edge list. Call after adding/removing roads.
    /// </summary>
    public void BuildFromEdgeList(List<(int from, int to, float cost, byte level)> edges)
    {
        _edgeCount = edges.Count;

        // Sort edges by source node
        edges.Sort((a, b) => a.from.CompareTo(b.from));

        // Ensure capacity
        if (_colIndices.Length < _edgeCount)
        {
            _colIndices = new int[_edgeCount];
            _edgeCosts = new float[_edgeCount];
            _edgeLevels = new byte[_edgeCount];
        }

        // Build CSR offsets
        Array.Clear(_rowOffsets, 0, _nodeCount + 1);

        for (int i = 0; i < _edgeCount; i++)
        {
            _rowOffsets[edges[i].from + 1]++;
        }

        // Prefix sum
        for (int i = 1; i <= _nodeCount; i++)
        {
            _rowOffsets[i] += _rowOffsets[i - 1];
        }

        // Fill edge data
        for (int i = 0; i < _edgeCount; i++)
        {
            _colIndices[i] = edges[i].to;
            _edgeCosts[i] = edges[i].cost;
            _edgeLevels[i] = edges[i].level;
        }
    }

    /// <summary>
    /// Iterate neighbors of a node. Returns (targetNode, cost, level) tuples.
    /// </summary>
    public NeighborEnumerator GetNeighbors(int nodeId) => new(this, nodeId);

    public ref struct NeighborEnumerator
    {
        private readonly RoadGraph _graph;
        private readonly int _start;
        private readonly int _end;
        private int _current;

        public NeighborEnumerator(RoadGraph graph, int nodeId)
        {
            _graph = graph;
            _start = graph._rowOffsets[nodeId];
            _end = graph._rowOffsets[nodeId + 1];
            _current = _start - 1;
        }

        public bool MoveNext() => ++_current < _end;

        public (int targetNode, float cost, byte level) Current =>
            (_graph._colIndices[_current], _graph._edgeCosts[_current], _graph._edgeLevels[_current]);

        public NeighborEnumerator GetEnumerator() => this;
    }

    public (int gridX, int gridY) GetNodePosition(int nodeId) =>
        (_nodeGridX[nodeId], _nodeGridY[nodeId]);

    private static long PackKey(int x, int y) => ((long)x << 32) | (uint)y;
}
