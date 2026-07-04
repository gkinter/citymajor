using System.Runtime.CompilerServices;

namespace Forge.Engine.Data;

/// <summary>
/// Generic influence map for calculating coverage areas, pollution spread, noise
/// propagation, land value fields, and other tile-based scalar fields.
///
/// Design:
/// - One float per tile, stored row-major (y * width + x).
/// - Sources are tracked so they can be added/removed incrementally.
/// - Dirty tracking per chunk: only recalculates regions that changed.
/// - Diffuse() for isotropic spreading (heat, pollution decay).
/// - Advect() for directional transport (wind-driven pollution).
/// - Bilinear interpolation for smooth overlay rendering.
/// </summary>
public sealed class InfluenceMap
{
    private readonly float[] _values;
    private readonly float[] _scratch; // double-buffer for diffusion/advection
    private readonly int _width;
    private readonly int _height;

    // Chunk-based dirty tracking
    private readonly bool[] _dirty;
    private readonly int _chunkSize;
    private readonly int _chunksX;
    private readonly int _chunksY;
    private int _dirtyCount;

    // Source tracking for incremental add/remove
    private readonly List<InfluenceSource> _sources = new();

    public int Width => _width;
    public int Height => _height;
    public int SourceCount => _sources.Count;
    public int DirtyChunkCount => _dirtyCount;

    /// <summary>
    /// Create an influence map matching the tile grid dimensions.
    /// </summary>
    /// <param name="width">Grid width in tiles.</param>
    /// <param name="height">Grid height in tiles.</param>
    /// <param name="chunkSize">Chunk size for dirty tracking (default 64, matching ChunkManager).</param>
    public InfluenceMap(int width, int height, int chunkSize = 64)
    {
        if (width < 1) throw new ArgumentOutOfRangeException(nameof(width));
        if (height < 1) throw new ArgumentOutOfRangeException(nameof(height));
        if (chunkSize < 1) throw new ArgumentOutOfRangeException(nameof(chunkSize));

        _width = width;
        _height = height;
        _chunkSize = chunkSize;
        _chunksX = (width + chunkSize - 1) / chunkSize;
        _chunksY = (height + chunkSize - 1) / chunkSize;

        _values = new float[width * height];
        _scratch = new float[width * height];
        _dirty = new bool[_chunksX * _chunksY];
    }

    /// <summary>
    /// Add an influence source and mark affected chunks dirty.
    /// </summary>
    public void AddSource(int x, int y, float strength, float radius, FalloffType falloff)
    {
        _sources.Add(new InfluenceSource(x, y, strength, radius, falloff));
        if (falloff == FalloffType.InverseSquare)
            MarkAllDirty(); // InverseSquare has infinite range
        else
            MarkSourceChunksDirty(x, y, radius);
    }

    /// <summary>
    /// Remove a matching influence source. If multiple identical sources exist,
    /// removes the first match.
    /// </summary>
    public void RemoveSource(int x, int y, float strength, float radius, FalloffType falloff)
    {
        for (int i = 0; i < _sources.Count; i++)
        {
            var s = _sources[i];
            if (s.X == x && s.Y == y &&
                MathF.Abs(s.Strength - strength) < 1e-6f &&
                MathF.Abs(s.Radius - radius) < 1e-6f &&
                s.Falloff == falloff)
            {
                _sources.RemoveAt(i);
                if (falloff == FalloffType.InverseSquare)
                    MarkAllDirty();
                else
                    MarkSourceChunksDirty(x, y, radius);
                return;
            }
        }
    }

    /// <summary>
    /// Recalculate only dirty regions by clearing and re-applying sources
    /// that overlap those chunks.
    /// </summary>
    public void Recalculate()
    {
        if (_dirtyCount == 0) return;

        // For each dirty chunk, clear its tiles then reapply all overlapping sources
        for (int cy = 0; cy < _chunksY; cy++)
        {
            for (int cx = 0; cx < _chunksX; cx++)
            {
                int chunkIdx = cy * _chunksX + cx;
                if (!_dirty[chunkIdx]) continue;

                int tileX0 = cx * _chunkSize;
                int tileY0 = cy * _chunkSize;
                int tileX1 = System.Math.Min(tileX0 + _chunkSize, _width);
                int tileY1 = System.Math.Min(tileY0 + _chunkSize, _height);

                // Clear chunk region
                for (int ty = tileY0; ty < tileY1; ty++)
                {
                    int rowBase = ty * _width;
                    for (int tx = tileX0; tx < tileX1; tx++)
                    {
                        _values[rowBase + tx] = 0f;
                    }
                }

                // Re-apply overlapping sources
                for (int si = 0; si < _sources.Count; si++)
                {
                    var src = _sources[si];
                    // Check if source radius overlaps this chunk
                    float r = src.Radius;
                    if (src.X + r < tileX0 || src.X - r >= tileX1 ||
                        src.Y + r < tileY0 || src.Y - r >= tileY1)
                        continue;

                    ApplySourceToRegion(src, tileX0, tileY0, tileX1, tileY1);
                }

                _dirty[chunkIdx] = false;
                _dirtyCount--;
            }
        }
    }

    /// <summary>
    /// Full recalculate: clear everything and re-apply all sources.
    /// Use after loading a save or when sources have changed drastically.
    /// </summary>
    public void RecalculateAll()
    {
        Array.Clear(_values, 0, _values.Length);

        for (int si = 0; si < _sources.Count; si++)
        {
            ApplySourceToRegion(_sources[si], 0, 0, _width, _height);
        }

        // Clear all dirty flags
        Array.Clear(_dirty, 0, _dirty.Length);
        _dirtyCount = 0;
    }

    /// <summary>Query the influence value at integer tile coordinates.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetValue(int x, int y)
    {
        if ((uint)x >= (uint)_width || (uint)y >= (uint)_height)
            return 0f;
        return _values[y * _width + x];
    }

    /// <summary>
    /// Bilinear interpolation for smooth overlay rendering at fractional coordinates.
    /// </summary>
    public float GetValueInterpolated(float x, float y)
    {
        if (x < 0f || y < 0f || x >= _width - 1 || y >= _height - 1)
            return GetValue((int)x, (int)y);

        int ix = (int)x;
        int iy = (int)y;
        float fx = x - ix;
        float fy = y - iy;

        float v00 = _values[iy * _width + ix];
        float v10 = _values[iy * _width + ix + 1];
        float v01 = _values[(iy + 1) * _width + ix];
        float v11 = _values[(iy + 1) * _width + ix + 1];

        float top = v00 + (v10 - v00) * fx;
        float bot = v01 + (v11 - v01) * fx;
        return top + (bot - top) * fy;
    }

    /// <summary>Get a row of values for bulk access (e.g., overlay renderer upload).</summary>
    public ReadOnlySpan<float> GetRow(int y)
    {
        if ((uint)y >= (uint)_height)
            return ReadOnlySpan<float>.Empty;
        return _values.AsSpan(y * _width, _width);
    }

    /// <summary>
    /// Get the raw backing array for GPU upload.
    /// The array is row-major, length = width * height.
    /// </summary>
    public float[] GetRawData() => _values;

    /// <summary>
    /// Isotropic diffusion step: spreads values to neighbors, then applies decay.
    /// Call once per simulation tick for continuous field evolution (pollution, heat).
    /// </summary>
    /// <param name="rate">Diffusion rate (0-1). 0.25 is a stable maximum for 4-neighbor diffusion.</param>
    /// <param name="decay">Multiplicative decay per step (e.g., 0.99 means 1% loss per tick).</param>
    public void Diffuse(float rate, float decay)
    {
        rate = System.Math.Clamp(rate, 0f, 0.25f); // stability limit for explicit diffusion
        decay = System.Math.Clamp(decay, 0f, 1f);

        for (int y = 0; y < _height; y++)
        {
            int rowBase = y * _width;
            int rowAbove = (y > 0) ? (y - 1) * _width : rowBase;
            int rowBelow = (y < _height - 1) ? (y + 1) * _width : rowBase;

            for (int x = 0; x < _width; x++)
            {
                float center = _values[rowBase + x];
                float left = (x > 0) ? _values[rowBase + x - 1] : center;
                float right = (x < _width - 1) ? _values[rowBase + x + 1] : center;
                float up = _values[rowAbove + x];
                float down = _values[rowBelow + x];

                // Standard 4-neighbor Laplacian diffusion
                float laplacian = (left + right + up + down) - 4f * center;
                _scratch[rowBase + x] = (center + rate * laplacian) * decay;
            }
        }

        // Swap buffers
        Array.Copy(_scratch, _values, _values.Length);
    }

    /// <summary>
    /// Semi-Lagrangian advection: moves field values in the wind direction.
    /// Traces each cell backward along the velocity field and samples the source.
    /// </summary>
    /// <param name="windX">Wind velocity X component (tiles per second).</param>
    /// <param name="windY">Wind velocity Y component (tiles per second).</param>
    /// <param name="dt">Time step in seconds.</param>
    public void Advect(float windX, float windY, float dt)
    {
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                // Trace backward: where did this value come from?
                float srcX = x - windX * dt;
                float srcY = y - windY * dt;

                // Clamp to grid boundaries
                srcX = System.Math.Clamp(srcX, 0f, _width - 1.001f);
                srcY = System.Math.Clamp(srcY, 0f, _height - 1.001f);

                // Bilinear interpolation from source position
                int ix = (int)srcX;
                int iy = (int)srcY;
                float fx = srcX - ix;
                float fy = srcY - iy;

                int ix1 = System.Math.Min(ix + 1, _width - 1);
                int iy1 = System.Math.Min(iy + 1, _height - 1);

                float v00 = _values[iy * _width + ix];
                float v10 = _values[iy * _width + ix1];
                float v01 = _values[iy1 * _width + ix];
                float v11 = _values[iy1 * _width + ix1];

                float top = v00 + (v10 - v00) * fx;
                float bot = v01 + (v11 - v01) * fx;
                _scratch[y * _width + x] = top + (bot - top) * fy;
            }
        }

        Array.Copy(_scratch, _values, _values.Length);
    }

    /// <summary>
    /// Directly set a value at a tile. Useful for injecting simulation-driven values
    /// without going through the source system (e.g., traffic density from pathfinding).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetValue(int x, int y, float value)
    {
        if ((uint)x < (uint)_width && (uint)y < (uint)_height)
            _values[y * _width + x] = value;
    }

    /// <summary>Clear all values to zero without removing sources.</summary>
    public void ClearValues()
    {
        Array.Clear(_values, 0, _values.Length);
    }

    /// <summary>Remove all sources and clear all values.</summary>
    public void ClearAll()
    {
        _sources.Clear();
        Array.Clear(_values, 0, _values.Length);
        Array.Clear(_dirty, 0, _dirty.Length);
        _dirtyCount = 0;
    }

    // =========================================================================
    // Internal helpers
    // =========================================================================

    private void ApplySourceToRegion(InfluenceSource src, int x0, int y0, int x1, int y1)
    {
        float r = src.Radius;
        float rSq = r * r;

        // InverseSquare has infinite range, so iterate the entire region.
        // Other falloffs clamp to source bounding box for performance.
        int minX, maxX, minY, maxY;
        if (src.Falloff == FalloffType.InverseSquare)
        {
            minX = x0;
            maxX = x1;
            minY = y0;
            maxY = y1;
        }
        else
        {
            minX = System.Math.Max(x0, (int)(src.X - r));
            maxX = System.Math.Min(x1, (int)(src.X + r) + 1);
            minY = System.Math.Max(y0, (int)(src.Y - r));
            maxY = System.Math.Min(y1, (int)(src.Y + r) + 1);
        }

        for (int ty = minY; ty < maxY; ty++)
        {
            float dy = ty - src.Y;
            float dySq = dy * dy;
            int rowBase = ty * _width;

            for (int tx = minX; tx < maxX; tx++)
            {
                float dx = tx - src.X;
                float distSq = dx * dx + dySq;

                if (distSq > rSq && src.Falloff != FalloffType.InverseSquare)
                    continue;

                float dist = MathF.Sqrt(distSq);
                float contribution = ComputeFalloff(src.Strength, dist, r, src.Falloff);
                _values[rowBase + tx] += contribution;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ComputeFalloff(float strength, float dist, float radius, FalloffType falloff)
    {
        return falloff switch
        {
            FalloffType.Linear => strength * MathF.Max(0f, 1f - dist / radius),
            FalloffType.Quadratic => strength * MathF.Max(0f, 1f - (dist * dist) / (radius * radius)),
            FalloffType.InverseSquare => strength / (1f + dist * dist),
            FalloffType.Exponential => strength * MathF.Exp(-dist / radius),
            FalloffType.Flat => dist <= radius ? strength : 0f,
            _ => 0f,
        };
    }

    private void MarkSourceChunksDirty(int sourceX, int sourceY, float radius)
    {
        int r = (int)MathF.Ceiling(radius);
        int cx0 = System.Math.Max(0, (sourceX - r) / _chunkSize);
        int cy0 = System.Math.Max(0, (sourceY - r) / _chunkSize);
        int cx1 = System.Math.Min(_chunksX - 1, (sourceX + r) / _chunkSize);
        int cy1 = System.Math.Min(_chunksY - 1, (sourceY + r) / _chunkSize);

        for (int cy = cy0; cy <= cy1; cy++)
        {
            for (int cx = cx0; cx <= cx1; cx++)
            {
                int idx = cy * _chunksX + cx;
                if (!_dirty[idx])
                {
                    _dirty[idx] = true;
                    _dirtyCount++;
                }
            }
        }
    }

    /// <summary>Mark all chunks dirty (e.g., after loading a save).</summary>
    public void MarkAllDirty()
    {
        Array.Fill(_dirty, true);
        _dirtyCount = _dirty.Length;
    }

    /// <summary>Check if a specific chunk is dirty.</summary>
    public bool IsChunkDirty(int cx, int cy)
    {
        if ((uint)cx >= (uint)_chunksX || (uint)cy >= (uint)_chunksY) return false;
        return _dirty[cy * _chunksX + cx];
    }
}

/// <summary>
/// Falloff function type for influence propagation.
/// </summary>
public enum FalloffType
{
    /// <summary>strength * (1 - dist/radius). Zero at radius.</summary>
    Linear,

    /// <summary>strength * (1 - (dist/radius)^2). Flatter near center, steeper at edges.</summary>
    Quadratic,

    /// <summary>strength / (1 + dist^2). Infinite range but rapid dropoff. Ignores radius for cutoff.</summary>
    InverseSquare,

    /// <summary>strength * e^(-dist/radius). Smooth exponential decay.</summary>
    Exponential,

    /// <summary>Full strength within radius, zero outside. Sharp boundary.</summary>
    Flat,
}

/// <summary>
/// An influence source with position, strength, radius, and falloff type.
/// </summary>
internal readonly record struct InfluenceSource(int X, int Y, float Strength, float Radius, FalloffType Falloff);
