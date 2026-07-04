namespace Forge.Engine.Data;

/// <summary>
/// Spatial chunking with dirty flags and camera-based loading/unloading.
/// The world is divided into 64x64 tile chunks. Each chunk tracks whether
/// its data has changed (dirty) and whether it's currently loaded for rendering.
/// </summary>
public sealed class ChunkManager
{
    private readonly int _worldSize;
    private readonly int _chunkSize;
    private readonly int _chunksPerAxis;
    private readonly int _totalChunks;

    // Per-chunk state
    private readonly bool[] _loaded;
    private readonly bool[] _dirty;
    private readonly bool[] _visible;
    private readonly int[] _lastAccessFrame;

    /// <summary>Number of chunks that are currently loaded.</summary>
    public int LoadedCount { get; private set; }

    /// <summary>Number of chunks marked dirty (need re-batching).</summary>
    public int DirtyCount { get; private set; }

    public int ChunksPerAxis => _chunksPerAxis;
    public int ChunkSize => _chunkSize;
    public int TotalChunks => _totalChunks;

    public ChunkManager(int worldSize, int chunkSize = 64)
    {
        _worldSize = worldSize;
        _chunkSize = chunkSize;
        _chunksPerAxis = worldSize / chunkSize;
        _totalChunks = _chunksPerAxis * _chunksPerAxis;

        _loaded = new bool[_totalChunks];
        _dirty = new bool[_totalChunks];
        _visible = new bool[_totalChunks];
        _lastAccessFrame = new int[_totalChunks];
    }

    /// <summary>Get chunk index from chunk coordinates.</summary>
    public int ChunkIndex(int cx, int cy) => cy * _chunksPerAxis + cx;

    /// <summary>Get chunk coordinates from a tile position.</summary>
    public (int cx, int cy) TileToChunk(int tileX, int tileY) =>
        (tileX / _chunkSize, tileY / _chunkSize);

    /// <summary>Mark a chunk as dirty (needs re-batching for rendering).</summary>
    public void MarkDirty(int cx, int cy)
    {
        int idx = ChunkIndex(cx, cy);
        if (idx >= 0 && idx < _totalChunks && !_dirty[idx])
        {
            _dirty[idx] = true;
            DirtyCount++;
        }
    }

    /// <summary>Mark a tile's chunk as dirty.</summary>
    public void MarkTileDirty(int tileX, int tileY)
    {
        var (cx, cy) = TileToChunk(tileX, tileY);
        MarkDirty(cx, cy);
    }

    /// <summary>Mark a rectangular tile area's chunks as dirty.</summary>
    public void MarkAreaDirty(int tileX, int tileY, int width, int height)
    {
        int cx0 = tileX / _chunkSize;
        int cy0 = tileY / _chunkSize;
        int cx1 = (tileX + width - 1) / _chunkSize;
        int cy1 = (tileY + height - 1) / _chunkSize;

        for (int cy = cy0; cy <= cy1; cy++)
        {
            for (int cx = cx0; cx <= cx1; cx++)
            {
                if (cx >= 0 && cx < _chunksPerAxis && cy >= 0 && cy < _chunksPerAxis)
                    MarkDirty(cx, cy);
            }
        }
    }

    /// <summary>Clear the dirty flag for a chunk (after re-batching).</summary>
    public void ClearDirty(int cx, int cy)
    {
        int idx = ChunkIndex(cx, cy);
        if (idx >= 0 && idx < _totalChunks && _dirty[idx])
        {
            _dirty[idx] = false;
            DirtyCount--;
        }
    }

    /// <summary>Check if a chunk is dirty.</summary>
    public bool IsDirty(int cx, int cy)
    {
        int idx = ChunkIndex(cx, cy);
        return idx >= 0 && idx < _totalChunks && _dirty[idx];
    }

    /// <summary>
    /// Update chunk visibility based on camera frustum. Loads chunks entering
    /// the view, marks distant chunks for unloading.
    /// </summary>
    /// <param name="minCx">Minimum visible chunk X (inclusive).</param>
    /// <param name="minCy">Minimum visible chunk Y (inclusive).</param>
    /// <param name="maxCx">Maximum visible chunk X (inclusive).</param>
    /// <param name="maxCy">Maximum visible chunk Y (inclusive).</param>
    /// <param name="frameNumber">Current frame number for LRU tracking.</param>
    /// <param name="loadMargin">Extra chunks to load beyond visible range.</param>
    public void UpdateVisibility(int minCx, int minCy, int maxCx, int maxCy, int frameNumber, int loadMargin = 2)
    {
        // Expand range by margin for pre-loading
        int loadMinCx = System.Math.Max(0, minCx - loadMargin);
        int loadMinCy = System.Math.Max(0, minCy - loadMargin);
        int loadMaxCx = System.Math.Min(_chunksPerAxis - 1, maxCx + loadMargin);
        int loadMaxCy = System.Math.Min(_chunksPerAxis - 1, maxCy + loadMargin);

        // Reset visibility
        Array.Clear(_visible, 0, _totalChunks);

        // Mark visible + loaded
        for (int cy = loadMinCy; cy <= loadMaxCy; cy++)
        {
            for (int cx = loadMinCx; cx <= loadMaxCx; cx++)
            {
                int idx = ChunkIndex(cx, cy);
                _visible[idx] = true;
                _lastAccessFrame[idx] = frameNumber;

                if (!_loaded[idx])
                {
                    _loaded[idx] = true;
                    _dirty[idx] = true; // Needs initial batching
                    LoadedCount++;
                    DirtyCount++;
                }
            }
        }

        // Unload chunks that haven't been visible for 120 frames (~2 seconds)
        const int UnloadThreshold = 120;
        for (int i = 0; i < _totalChunks; i++)
        {
            if (_loaded[i] && !_visible[i] && (frameNumber - _lastAccessFrame[i]) > UnloadThreshold)
            {
                _loaded[i] = false;
                _dirty[i] = false;
                LoadedCount--;
            }
        }
    }

    public bool IsLoaded(int cx, int cy) => _loaded[ChunkIndex(cx, cy)];
    public bool IsVisible(int cx, int cy) => _visible[ChunkIndex(cx, cy)];

    /// <summary>Mark all chunks as dirty (e.g., after loading a save file).</summary>
    public void MarkAllDirty()
    {
        Array.Fill(_dirty, true);
        DirtyCount = _totalChunks;
    }

    /// <summary>Get all dirty chunk coordinates for processing.</summary>
    public List<(int cx, int cy)> GetDirtyChunks()
    {
        var result = new List<(int, int)>();
        for (int cy = 0; cy < _chunksPerAxis; cy++)
        {
            for (int cx = 0; cx < _chunksPerAxis; cx++)
            {
                if (IsDirty(cx, cy))
                    result.Add((cx, cy));
            }
        }
        return result;
    }
}
