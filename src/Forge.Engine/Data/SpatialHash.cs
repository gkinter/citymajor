using System.Runtime.CompilerServices;

namespace Forge.Engine.Data;

/// <summary>
/// Uniform-grid spatial hash for fast range queries over 2D entities.
/// Each cell stores a set of entity IDs. Insert/remove are O(1),
/// range queries are O(cells_in_range + results).
///
/// Designed for city-builder tile grids: "find all buildings within 15 tiles
/// of this fire station" is a radius query that only touches relevant cells.
/// </summary>
public sealed class SpatialHash<T> where T : struct
{
    private readonly float _cellSize;
    private readonly float _inverseCellSize;
    private readonly Dictionary<long, List<Entry>> _cells;
    private int _entityCount;

    private struct Entry
    {
        public int EntityId;
        public float X;
        public float Y;
    }

    /// <summary>Number of entities currently in the spatial hash.</summary>
    public int EntityCount => _entityCount;

    /// <summary>Number of non-empty cells.</summary>
    public int CellCount => _cells.Count;

    /// <summary>Average entities per non-empty cell.</summary>
    public float AverageEntitiesPerCell =>
        _cells.Count == 0 ? 0f : (float)_entityCount / _cells.Count;

    /// <summary>
    /// Create a spatial hash with the given cell size.
    /// Smaller cells = faster queries but more memory. 16 is a good default for tile grids.
    /// </summary>
    public SpatialHash(float cellSize = 16f)
    {
        if (cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be positive.");

        _cellSize = cellSize;
        _inverseCellSize = 1f / cellSize;
        _cells = new Dictionary<long, List<Entry>>();
        _entityCount = 0;
    }

    /// <summary>
    /// Insert an entity at the given world position. O(1).
    /// </summary>
    public void Insert(int entityId, float x, float y)
    {
        long key = CellKey(x, y);
        if (!_cells.TryGetValue(key, out var list))
        {
            list = new List<Entry>(4);
            _cells[key] = list;
        }

        list.Add(new Entry { EntityId = entityId, X = x, Y = y });
        _entityCount++;
    }

    /// <summary>
    /// Remove an entity from the given world position. O(entities_in_cell).
    /// Returns true if the entity was found and removed.
    /// </summary>
    public bool Remove(int entityId, float x, float y)
    {
        long key = CellKey(x, y);
        if (!_cells.TryGetValue(key, out var list))
            return false;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].EntityId == entityId)
            {
                // Swap-remove for O(1) within the cell
                int last = list.Count - 1;
                if (i != last)
                    list[i] = list[last];
                list.RemoveAt(last);

                if (list.Count == 0)
                    _cells.Remove(key);

                _entityCount--;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Move an entity from one position to another. If the cell hasn't changed,
    /// this is a fast in-place update. Otherwise it's a remove + insert.
    /// </summary>
    public void Move(int entityId, float oldX, float oldY, float newX, float newY)
    {
        long oldKey = CellKey(oldX, oldY);
        long newKey = CellKey(newX, newY);

        if (oldKey == newKey)
        {
            // Same cell — update position in place
            if (_cells.TryGetValue(oldKey, out var list))
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].EntityId == entityId)
                    {
                        list[i] = new Entry { EntityId = entityId, X = newX, Y = newY };
                        return;
                    }
                }
            }
            // Entity not found in expected cell — fall through to insert
            Insert(entityId, newX, newY);
            return;
        }

        // Different cell — remove from old, insert into new
        Remove(entityId, oldX, oldY);
        Insert(entityId, newX, newY);
    }

    /// <summary>
    /// Find all entity IDs within a circle centered at (cx, cy) with the given radius.
    /// Results are appended to the provided list (not cleared first).
    /// </summary>
    public void QueryRadius(float cx, float cy, float radius, List<int> results)
    {
        if (radius < 0f) throw new ArgumentOutOfRangeException(nameof(radius));

        float radiusSq = radius * radius;

        int minCellX = (int)MathF.Floor((cx - radius) * _inverseCellSize);
        int maxCellX = (int)MathF.Floor((cx + radius) * _inverseCellSize);
        int minCellY = (int)MathF.Floor((cy - radius) * _inverseCellSize);
        int maxCellY = (int)MathF.Floor((cy + radius) * _inverseCellSize);

        for (int cellY = minCellY; cellY <= maxCellY; cellY++)
        {
            for (int cellX = minCellX; cellX <= maxCellX; cellX++)
            {
                long key = PackKey(cellX, cellY);
                if (!_cells.TryGetValue(key, out var list))
                    continue;

                for (int i = 0; i < list.Count; i++)
                {
                    float dx = list[i].X - cx;
                    float dy = list[i].Y - cy;
                    if (dx * dx + dy * dy <= radiusSq)
                    {
                        results.Add(list[i].EntityId);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Find all entity IDs within an axis-aligned rectangle.
    /// Results are appended to the provided list (not cleared first).
    /// </summary>
    public void QueryRect(float minX, float minY, float maxX, float maxY, List<int> results)
    {
        int minCellX = (int)MathF.Floor(minX * _inverseCellSize);
        int maxCellX = (int)MathF.Floor(maxX * _inverseCellSize);
        int minCellY = (int)MathF.Floor(minY * _inverseCellSize);
        int maxCellY = (int)MathF.Floor(maxY * _inverseCellSize);

        for (int cellY = minCellY; cellY <= maxCellY; cellY++)
        {
            for (int cellX = minCellX; cellX <= maxCellX; cellX++)
            {
                long key = PackKey(cellX, cellY);
                if (!_cells.TryGetValue(key, out var list))
                    continue;

                for (int i = 0; i < list.Count; i++)
                {
                    ref Entry e = ref System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list)[i];
                    if (e.X >= minX && e.X <= maxX && e.Y >= minY && e.Y <= maxY)
                    {
                        results.Add(e.EntityId);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Remove all entities and cells.
    /// </summary>
    public void Clear()
    {
        _cells.Clear();
        _entityCount = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long CellKey(float x, float y)
    {
        int cx = (int)MathF.Floor(x * _inverseCellSize);
        int cy = (int)MathF.Floor(y * _inverseCellSize);
        return PackKey(cx, cy);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long PackKey(int cx, int cy)
    {
        // Pack two 32-bit ints into a 64-bit long for dictionary key
        return ((long)cx << 32) | (uint)cy;
    }
}
