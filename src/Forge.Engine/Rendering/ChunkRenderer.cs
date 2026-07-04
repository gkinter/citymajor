using Forge.Engine.Core;
using Forge.Engine.Data;
using Forge.Engine.Math;
using Silk.NET.OpenGL;

namespace Forge.Engine.Rendering;

/// <summary>
/// Chunk-based terrain renderer with GPU vertex buffer management.
/// Each chunk is rendered from a dedicated vertex buffer (default 64x64 tiles).
/// Features:
///   - Frustum culling based on camera position + zoom
///   - Dirty chunk tracking: only re-uploads modified chunks to GPU
///   - LRU cache for GPU buffers (max 64 loaded simultaneously)
///   - LOD levels: zoom 1-2 = full detail, zoom 3-4 = simplified, zoom 5+ = color blocks
///   - Procedural terrain textures with per-tile variant selection
/// </summary>
public sealed class ChunkRenderer : IDisposable
{
    private readonly Config _config;
    private readonly ChunkManager _chunks;
    private readonly TileData _tiles;
    private readonly GL _gl;

    // Procedural terrain textures (optional — falls back to flat tint colors if null)
    private ProceduralTerrainTextures? _terrainTextures;

    // Per-chunk GPU buffer cache
    private readonly ChunkGpuEntry[] _gpuCache;
    private readonly int _maxCachedChunks;

    // LRU tracking: O(1) touch and eviction via linked list + lookup dictionary
    private readonly LinkedList<int> _lruOrder = new();
    private readonly Dictionary<int, LinkedListNode<int>> _lruLookup = new();

    // Cached loaded chunk count to avoid O(n) scan in EnsureCacheSpace
    private int _loadedChunkCount;

    // Per-chunk vertex data staging buffer (reused across uploads)
    // 64x64 tiles * 6 verts * 8 floats (pos.xy, uv.xy, tint.rgba) = 196,608 floats max
    private const int FloatsPerVertex = 8;
    private const int VerticesPerTile = 6;
    private readonly float[] _stagingBuffer;

    // Shader for chunk rendering
    private ShaderProgram? _shader;

    // Per-frame state passed to BuildAndUploadChunk
    private float _gameTime;
    private float _currentZoom;

    // Water animation: quantized time step to avoid rebuilding every frame.
    // Updates ~10 times per second for smooth-looking water oscillation.
    private int _waterAnimStep = -1;

    // Stats for profiling
    private int _chunksDrawnLastFrame;
    private int _chunksCulledLastFrame;
    private int _chunksUploadedLastFrame;

    public int ChunksDrawnLastFrame => _chunksDrawnLastFrame;
    public int ChunksCulledLastFrame => _chunksCulledLastFrame;
    public int ChunksUploadedLastFrame => _chunksUploadedLastFrame;

    /// <summary>Terrain type to tint color variants (multiple shades per terrain, selected by noise).</summary>
    private static readonly (float r, float g, float b)[][] TerrainColorVariants =
    [
        // 0 = grass: #4CAF50, #43A047, #388E3C (3 shades)
        [(0.298f, 0.686f, 0.314f), (0.263f, 0.627f, 0.278f), (0.220f, 0.557f, 0.235f)],
        // 1 = dirt: #5D4037, #6D4C41, #795548 (3 shades)
        [(0.365f, 0.251f, 0.216f), (0.427f, 0.298f, 0.255f), (0.475f, 0.333f, 0.282f)],
        // 2 = sand: #D7CCC8, #BCAAA4 (2 shades)
        [(0.843f, 0.800f, 0.784f), (0.737f, 0.667f, 0.643f)],
        // 3 = water: #1976D2 deep, #42A5F5 shallow
        [(0.098f, 0.463f, 0.824f), (0.259f, 0.647f, 0.961f)],
        // 4 = rock: #616161, #757575, #9E9E9E (3 shades by elevation)
        [(0.380f, 0.380f, 0.380f), (0.459f, 0.459f, 0.459f), (0.620f, 0.620f, 0.620f)],
        // 5 = forest: #1B5E20, #2E7D32, #388E3C (3 shades with seasonal tint)
        [(0.106f, 0.369f, 0.125f), (0.180f, 0.490f, 0.196f), (0.220f, 0.557f, 0.235f)],
    ];

    /// <summary>Legacy single-color lookup (index 0 variant) for backward compat.</summary>
    private static (float r, float g, float b) GetTerrainBaseColor(int terrainType)
    {
        if (terrainType < TerrainColorVariants.Length && TerrainColorVariants[terrainType].Length > 0)
            return TerrainColorVariants[terrainType][0];
        return (0.298f, 0.686f, 0.314f); // fallback: grass
    }

    /// <summary>Simple hash-based noise for per-tile color variation. Returns 0-1.</summary>
    internal static float TileNoise(int gx, int gy)
    {
        // Fast integer hash (Robert Jenkins' 32-bit mix)
        uint hash = (uint)(gx * 374761393 + gy * 668265263);
        hash = (hash ^ (hash >> 13)) * 1274126177;
        hash ^= hash >> 16;
        return (hash & 0xFFFF) / 65535f;
    }

    /// <summary>
    /// A sub-batch within a chunk that shares a single texture binding.
    /// Tiles are sorted by (terrainType, variant) so all tiles sharing a texture
    /// are contiguous in the VBO, enabling multi-draw with texture switches.
    /// </summary>
    private struct TextureBatch
    {
        public int TerrainType;
        public int Variant;
        public int FirstVertex;
        public int VertexCount;
    }

    /// <summary>Max distinct (terrainType, variant) combinations per chunk.</summary>
    private const int MaxBatchesPerChunk = 6 * 4; // 6 terrain types * 4 variants

    private struct ChunkGpuEntry
    {
        public uint Vao;
        public uint Vbo;
        public int VertexCount;
        public int LastAccessFrame;
        public bool Allocated;
        public int LodLevel; // LOD at which this was built

        /// <summary>Sub-batches sorted by texture for multi-draw rendering.</summary>
        public TextureBatch[] Batches;
        public int BatchCount;
    }

    public ChunkRenderer(GL gl, Config config, ChunkManager chunks, TileData tiles, int maxCachedChunks = 64)
    {
        _gl = gl;
        _config = config;
        _chunks = chunks;
        _tiles = tiles;
        _maxCachedChunks = maxCachedChunks;

        int totalChunks = config.TotalChunks;
        _gpuCache = new ChunkGpuEntry[totalChunks];

        int chunkSize = config.ChunkSize;
        _stagingBuffer = new float[chunkSize * chunkSize * VerticesPerTile * FloatsPerVertex];
    }

    public void Init(ShaderProgram shader)
    {
        _shader = shader;
    }

    /// <summary>
    /// Set the procedural terrain textures to use for rendering.
    /// When set, tiles are UV-mapped to their terrain texture instead of using flat tint colors.
    /// </summary>
    public void SetTerrainTextures(ProceduralTerrainTextures textures)
    {
        _terrainTextures = textures;
        // Force all chunks to rebuild with the new texture-based vertex data
        _chunks.MarkAllDirty();
    }

    /// <summary>
    /// Render all visible terrain chunks. Handles frustum culling, dirty re-upload,
    /// LRU eviction, and LOD selection internally.
    /// </summary>
    /// <param name="camera">The isometric camera for view transforms.</param>
    /// <param name="atlas">Texture atlas (used as fallback when no procedural textures).</param>
    /// <param name="gameTime">Total elapsed game time in seconds, used for water animation.</param>
    public void Render(IsometricCamera camera, TextureAtlas atlas, float gameTime = 0f)
    {
        _chunksDrawnLastFrame = 0;
        _chunksCulledLastFrame = 0;
        _chunksUploadedLastFrame = 0;

        _gameTime = gameTime;

        // Water animation: mark all chunks dirty when the quantized time step advances.
        // 10 steps/sec gives smooth-looking oscillation without per-frame rebuilds.
        int waterStep = (int)(gameTime * 10f);
        if (waterStep != _waterAnimStep)
        {
            _waterAnimStep = waterStep;
            _chunks.MarkAllDirty();
        }

        var frustum = camera.GetFrustumBounds();
        int tileW = _config.TileWidth;
        int tileH = _config.TileHeight;
        int rotation = camera.Rotation;
        float zoom = camera.SmoothZoom;
        var (offsetX, offsetY) = camera.GetRenderOffset();

        int lodLevel = GetLodLevel(zoom);
        _currentZoom = zoom;

        // Determine visible chunk range from frustum
        int chunkSize = _config.ChunkSize;
        int chunksPerAxis = _config.ChunksPerAxis;

        var (minGx, minGy) = IsometricMath.ScreenToGrid(frustum.MinX, frustum.MinY, tileW, tileH, rotation);
        var (maxGx, maxGy) = IsometricMath.ScreenToGrid(frustum.MaxX, frustum.MaxY, tileW, tileH, rotation);

        // Expand bounds conservatively (isometric projection means corners don't map 1:1)
        int pad = chunkSize;
        int gxMin = System.Math.Min(minGx, maxGx) - pad;
        int gyMin = System.Math.Min(minGy, maxGy) - pad;
        int gxMax = System.Math.Max(minGx, maxGx) + pad;
        int gyMax = System.Math.Max(minGy, maxGy) + pad;

        int minCx = System.Math.Max(0, gxMin / chunkSize);
        int minCy = System.Math.Max(0, gyMin / chunkSize);
        int maxCx = System.Math.Min(chunksPerAxis - 1, gxMax / chunkSize);
        int maxCy = System.Math.Min(chunksPerAxis - 1, gyMax / chunkSize);

        // Update chunk manager visibility for LRU tracking
        int frameNumber = camera.ViewportWidth > 0 ? (int)(DateTime.UtcNow.Ticks / 166667) : 0;

        // Ensure shader is active
        _shader?.Use();

        // Set projection uniforms
        if (_shader != null)
        {
            float[] ortho = BuildOrthoMatrix(camera.ViewportWidth, camera.ViewportHeight);
            _shader.SetUniformMatrix4("u_projection", ortho);
            _shader.SetUniform("u_offset", offsetX, offsetY);
            _shader.SetUniform("u_zoom", zoom);
        }

        bool useTextures = _terrainTextures != null;

        // Render chunks in back-to-front order for correct isometric overlap
        for (int cy = minCy; cy <= maxCy; cy++)
        {
            for (int cx = minCx; cx <= maxCx; cx++)
            {
                // Frustum cull: check if the chunk's screen-space AABB intersects the viewport
                if (!IsChunkVisible(cx, cy, chunkSize, tileW, tileH, rotation, zoom, offsetX, offsetY, camera))
                {
                    _chunksCulledLastFrame++;
                    continue;
                }

                int chunkIdx = _chunks.ChunkIndex(cx, cy);

                // Check if we need to (re)build this chunk's GPU data
                bool needsUpload = !_gpuCache[chunkIdx].Allocated
                                   || _chunks.IsDirty(cx, cy)
                                   || _gpuCache[chunkIdx].LodLevel != lodLevel;

                if (needsUpload)
                {
                    EnsureCacheSpace(chunkIdx);
                    BuildAndUploadChunk(cx, cy, chunkIdx, lodLevel, tileW, tileH, rotation, zoom, offsetX, offsetY);
                    _chunks.ClearDirty(cx, cy);
                    _chunksUploadedLastFrame++;
                }

                // Update LRU
                _gpuCache[chunkIdx].LastAccessFrame = frameNumber;
                TouchLru(chunkIdx);

                // Draw
                ref var entry = ref _gpuCache[chunkIdx];
                if (entry.VertexCount > 0)
                {
                    _gl.BindVertexArray(entry.Vao);

                    if (useTextures && entry.BatchCount > 0)
                    {
                        // Draw sub-batches, switching texture between each
                        for (int bi = 0; bi < entry.BatchCount; bi++)
                        {
                            ref var batch = ref entry.Batches[bi];
                            if (batch.VertexCount <= 0) continue;

                            _terrainTextures!.Bind(_gl, batch.TerrainType, batch.Variant, 0);
                            _gl.DrawArrays(PrimitiveType.Triangles,
                                batch.FirstVertex, (uint)batch.VertexCount);
                        }
                    }
                    else
                    {
                        // Fallback: single draw with atlas texture (flat color mode)
                        _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)entry.VertexCount);
                    }

                    _chunksDrawnLastFrame++;
                }
            }
        }

        _gl.BindVertexArray(0);
    }

    /// <summary>
    /// Determine LOD level from current zoom.
    /// 0 = full detail (zoom 1-2), 1 = simplified (zoom 3-4), 2 = color blocks (zoom 5+).
    /// </summary>
    private static int GetLodLevel(float zoom)
    {
        if (zoom <= 2f) return 0;
        if (zoom <= 4f) return 1;
        return 2;
    }

    /// <summary>
    /// Check if a chunk's AABB is within the camera viewport.
    /// Uses a conservative screen-space bounding box.
    /// </summary>
    private bool IsChunkVisible(int cx, int cy, int chunkSize, int tileW, int tileH,
                                int rotation, float zoom, float offsetX, float offsetY,
                                IsometricCamera camera)
    {
        // Compute screen positions of the 4 chunk corners
        int baseX = cx * chunkSize;
        int baseY = cy * chunkSize;
        int endX = baseX + chunkSize;
        int endY = baseY + chunkSize;

        // Check all 4 corners of the chunk in grid space
        Span<(int gx, int gy)> corners = stackalloc (int, int)[]
        {
            (baseX, baseY),
            (endX, baseY),
            (baseX, endY),
            (endX, endY)
        };

        float screenMinX = float.MaxValue;
        float screenMinY = float.MaxValue;
        float screenMaxX = float.MinValue;
        float screenMaxY = float.MinValue;

        for (int i = 0; i < 4; i++)
        {
            var (sx, sy) = IsometricMath.GridToScreen(corners[i].gx, corners[i].gy, tileW, tileH, rotation);
            float drawX = sx * zoom + offsetX;
            float drawY = sy * zoom + offsetY;

            if (drawX < screenMinX) screenMinX = drawX;
            if (drawY < screenMinY) screenMinY = drawY;
            if (drawX > screenMaxX) screenMaxX = drawX;
            if (drawY > screenMaxY) screenMaxY = drawY;
        }

        // Add tile-size padding for the isometric diamond overlap
        float tilePad = tileW * zoom;
        screenMinX -= tilePad;
        screenMinY -= tilePad;
        screenMaxX += tilePad;
        screenMaxY += tilePad;

        // AABB vs viewport test
        return screenMaxX >= 0 && screenMinX <= camera.ViewportWidth
            && screenMaxY >= 0 && screenMinY <= camera.ViewportHeight;
    }

    /// <summary>
    /// Temporary tile info collected during the first pass before sorting by texture.
    /// Per-vertex colors enable smooth terrain blending across tile boundaries.
    /// </summary>
    private struct TileVertexInfo
    {
        public int TerrainType;
        public int Variant;
        // Diamond corner positions
        public float TopX, TopY;
        public float RightX, RightY;
        public float BottomX, BottomY;
        public float LeftX, LeftY;
        // Per-vertex blended tint colors (each corner blends with neighboring tiles)
        public float TopR, TopG, TopB;
        public float RightR, RightG, RightB;
        public float BottomR, BottomG, BottomB;
        public float LeftR, LeftG, LeftB;
        public float TintA;
    }

    // Reusable list to avoid allocation per chunk build
    private readonly List<TileVertexInfo> _tileInfos = new(64 * 64);

    /// <summary>
    /// Compute the tint color for a single tile at (gx, gy).
    /// Returns the raw RGB tint before any per-vertex neighbor blending.
    /// </summary>
    private (float r, float g, float b) ComputeTileTint(int gx, int gy, bool useTextures)
    {
        if (!_tiles.InBounds(gx, gy))
            return GetTerrainBaseColor(0);

        int tileIdx = _tiles.Index(gx, gy);
        byte terrainType = _tiles.TerrainType[tileIdx];
        ushort elevation = _tiles.Elevation[tileIdx];
        float noise = TileNoise(gx, gy);

        float tintR, tintG, tintB;

        if (useTextures)
        {
            float elevBrightness = 0.88f + (elevation / 65535f) * 0.24f;
            float brightnessNoise = TileNoise(gx + 7919, gy + 6271);
            float brightnessMod = 0.97f + brightnessNoise * 0.06f;
            float brightness = elevBrightness * brightnessMod;
            tintR = brightness;
            tintG = brightness;
            tintB = brightness;
        }
        else
        {
            if (terrainType < TerrainColorVariants.Length)
            {
                var variants = TerrainColorVariants[terrainType];
                int variantIdx = (int)(noise * variants.Length) % variants.Length;
                (tintR, tintG, tintB) = variants[variantIdx];
            }
            else
            {
                (tintR, tintG, tintB) = GetTerrainBaseColor(0);
            }

            float brightnessNoise = TileNoise(gx + 7919, gy + 6271);
            float brightnessMod = 0.95f + brightnessNoise * 0.10f;
            float elevBrightness = 0.85f + (elevation / 65535f) * 0.3f;
            float totalBrightness = elevBrightness * brightnessMod;
            tintR *= totalBrightness;
            tintG *= totalBrightness;
            tintB *= totalBrightness;
        }

        // Water animation
        if (terrainType == 3)
        {
            float waterPhase = MathF.Sin(_gameTime * MathF.PI) * 0.05f;
            float waterMod = 1f + waterPhase;
            tintR *= waterMod;
            tintG *= waterMod;
            tintB *= waterMod;
        }

        return (tintR, tintG, tintB);
    }

    /// <summary>
    /// Compute the blended vertex color for a diamond corner by averaging the tile's
    /// own tint with the tints of its diagonal and adjacent neighbors at that corner.
    /// This creates smooth AoE2-style terrain transitions across tile boundaries.
    /// </summary>
    private (float r, float g, float b) BlendVertexColor(
        float selfR, float selfG, float selfB,
        int gx, int gy,
        int dxA, int dyA, int dxB, int dyB, int dxC, int dyC,
        bool useTextures)
    {
        // Average self + up to 3 neighbors (adjacent, adjacent, diagonal)
        var (rA, gA, bA) = ComputeTileTint(gx + dxA, gy + dyA, useTextures);
        var (rB, gB, bB) = ComputeTileTint(gx + dxB, gy + dyB, useTextures);
        var (rC, gC, bC) = ComputeTileTint(gx + dxC, gy + dyC, useTextures);

        // Weighted blend: self gets 40%, each neighbor gets 20%
        const float ws = 0.40f;
        const float wn = 0.20f;
        return (
            selfR * ws + rA * wn + rB * wn + rC * wn,
            selfG * ws + gA * wn + gB * wn + gC * wn,
            selfB * ws + bA * wn + bB * wn + bC * wn
        );
    }

    /// <summary>
    /// Build vertex data for a chunk and upload to GPU.
    /// When procedural textures are available, tiles are sorted by (terrainType, variant)
    /// so each texture can be bound once per sub-batch.
    /// LOD 0: every tile rendered as a full isometric diamond (6 verts) with neighbor blending.
    /// LOD 1: every 2x2 tile block rendered as one larger diamond.
    /// LOD 2: every 4x4 tile block rendered as a single colored quad.
    /// </summary>
    private void BuildAndUploadChunk(int cx, int cy, int chunkIdx, int lodLevel,
                                     int tileW, int tileH, int rotation,
                                     float zoom, float offsetX, float offsetY)
    {
        int chunkSize = _config.ChunkSize;
        int baseX = cx * chunkSize;
        int baseY = cy * chunkSize;

        int step = lodLevel switch
        {
            0 => 1,
            1 => 2,
            _ => 4,
        };

        float tileScale = step;
        float halfW = tileW * 0.5f;
        float halfH = tileH * 0.5f;

        bool useTextures = _terrainTextures != null;

        // Collect tile info
        _tileInfos.Clear();

        for (int ly = 0; ly < chunkSize; ly += step)
        {
            for (int lx = 0; lx < chunkSize; lx += step)
            {
                int gx = baseX + lx;
                int gy = baseY + ly;

                if (gx >= _tiles.Size || gy >= _tiles.Size)
                    continue;

                // Get terrain type and elevation
                int sampleX = System.Math.Min(gx + step / 2, _tiles.Size - 1);
                int sampleY = System.Math.Min(gy + step / 2, _tiles.Size - 1);
                int tileIdx = _tiles.Index(sampleX, sampleY);
                byte terrainType = _tiles.TerrainType[tileIdx];

                float noise = TileNoise(gx, gy);
                int variant = 0;

                // Compute this tile's base tint
                var (tintR, tintG, tintB) = ComputeTileTint(gx, gy, useTextures);

                if (useTextures)
                {
                    int variantCount = _terrainTextures!.GetVariantCount(terrainType);
                    variant = variantCount > 0 ? (int)(noise * variantCount) % variantCount : 0;
                }

                // Road rendering: override terrain color with road surface color
                float totalBrightnessForRoad = useTextures
                    ? tintR // brightness is uniform for textured mode
                    : 1f;   // already baked into tint for flat mode
                byte roadFlags = _tiles.RoadFlags[tileIdx];
                int roadLevel = (roadFlags >> 4) & 0x03;
                bool hasRoadConnection = (roadFlags & 0x0F) != 0;
                bool isRoad = hasRoadConnection || roadLevel > 0;

                // Whether this tile should use per-vertex neighbor blending
                bool doBlend = !isRoad && lodLevel == 0;

                if (isRoad)
                {
                    int effectiveRoad = roadLevel > 0 ? roadLevel : 1;
                    switch (effectiveRoad)
                    {
                        case 1: tintR = 0.553f; tintG = 0.431f; tintB = 0.388f; break;
                        case 2: tintR = 0.471f; tintG = 0.565f; tintB = 0.612f; break;
                        case 3: tintR = 0.216f; tintG = 0.278f; tintB = 0.310f; break;
                    }

                    if (!useTextures)
                    {
                        ushort elevation = _tiles.Elevation[tileIdx];
                        float elevBrightness = 0.85f + (elevation / 65535f) * 0.3f;
                        float brightnessNoise = TileNoise(gx + 7919, gy + 6271);
                        float brightnessMod = 0.95f + brightnessNoise * 0.10f;
                        float tb = elevBrightness * brightnessMod;
                        tintR *= tb;
                        tintG *= tb;
                        tintB *= tb;
                    }
                    else
                    {
                        tintR *= totalBrightnessForRoad;
                        tintG *= totalBrightnessForRoad;
                        tintB *= totalBrightnessForRoad;
                    }

                    // Roads use the atlas (white) texture, not terrain texture
                    variant = -1; // Sentinel: render with atlas, not terrain texture
                }
                else
                {
                    // Zone color overlay (30% blend, only when not a road)
                    byte zoneType = _tiles.ZoneType[tileIdx];
                    if (zoneType != 0)
                    {
                        float zoneR, zoneG, zoneB;
                        switch (zoneType)
                        {
                            case 1:
                            case 2:
                                zoneR = 0.298f; zoneG = 0.686f; zoneB = 0.314f; break;
                            case 3:
                                zoneR = 0.259f; zoneG = 0.647f; zoneB = 0.961f; break;
                            case 4:
                                zoneR = 1.000f; zoneG = 0.702f; zoneB = 0.000f; break;
                            case 5:
                                zoneR = 0.671f; zoneG = 0.278f; zoneB = 0.737f; break;
                            default:
                                zoneR = tintR; zoneG = tintG; zoneB = tintB; break;
                        }
                        tintR = tintR * 0.7f + zoneR * 0.3f;
                        tintG = tintG * 0.7f + zoneG * 0.3f;
                        tintB = tintB * 0.7f + zoneB * 0.3f;
                    }
                }

                // Building footprint darkening
                if (_tiles.BuildingId[tileIdx] != 0)
                {
                    tintR *= 0.75f;
                    tintG *= 0.75f;
                    tintB *= 0.75f;
                }

                // Compute per-vertex blended colors for smooth terrain transitions
                float topR, topG, topB;
                float rightR, rightG, rightB;
                float bottomR, bottomG, bottomB;
                float leftR, leftG, leftB;

                if (doBlend)
                {
                    // Top vertex: blend with N, NW, NE neighbors
                    (topR, topG, topB) = BlendVertexColor(tintR, tintG, tintB,
                        gx, gy, 0, -1, -1, 0, -1, -1, useTextures);
                    // Right vertex: blend with E, NE, SE neighbors
                    (rightR, rightG, rightB) = BlendVertexColor(tintR, tintG, tintB,
                        gx, gy, 1, 0, 1, -1, 0, -1, useTextures);
                    // Bottom vertex: blend with S, SE, SW neighbors
                    (bottomR, bottomG, bottomB) = BlendVertexColor(tintR, tintG, tintB,
                        gx, gy, 0, 1, 1, 0, 1, 1, useTextures);
                    // Left vertex: blend with W, NW, SW neighbors
                    (leftR, leftG, leftB) = BlendVertexColor(tintR, tintG, tintB,
                        gx, gy, -1, 0, -1, -1, -1, 1, useTextures);
                }
                else
                {
                    // No blending: uniform color across all vertices
                    topR = tintR; topG = tintG; topB = tintB;
                    rightR = tintR; rightG = tintG; rightB = tintB;
                    bottomR = tintR; bottomG = tintG; bottomB = tintB;
                    leftR = tintR; leftG = tintG; leftB = tintB;
                }

                var (sx, sy) = IsometricMath.GridToScreen(gx, gy, tileW, tileH, rotation);
                float drawX = sx * zoom + offsetX;
                float drawY = sy * zoom + offsetY;

                float dw = halfW * zoom * tileScale;
                float dh = halfH * zoom * tileScale;

                _tileInfos.Add(new TileVertexInfo
                {
                    TerrainType = terrainType,
                    Variant = variant,
                    TopX = drawX, TopY = drawY,
                    RightX = drawX + dw, RightY = drawY + dh,
                    BottomX = drawX, BottomY = drawY + dh * 2f,
                    LeftX = drawX - dw, LeftY = drawY + dh,
                    TopR = topR, TopG = topG, TopB = topB,
                    RightR = rightR, RightG = rightG, RightB = rightB,
                    BottomR = bottomR, BottomG = bottomG, BottomB = bottomB,
                    LeftR = leftR, LeftG = leftG, LeftB = leftB,
                    TintA = 1f,
                });
            }
        }

        // Sort by (terrainType, variant) so tiles sharing a texture are contiguous.
        // variant == -1 (roads) sorts first, drawn with the atlas white texture.
        if (useTextures && _tileInfos.Count > 0)
        {
            _tileInfos.Sort((a, b) =>
            {
                int cmp = a.TerrainType.CompareTo(b.TerrainType);
                return cmp != 0 ? cmp : a.Variant.CompareTo(b.Variant);
            });
        }

        // Write sorted vertices into the staging buffer and track sub-batches
        int vertexCount = 0;
        int floatIdx = 0;

        ref var entry = ref _gpuCache[chunkIdx];
        entry.Batches ??= new TextureBatch[MaxBatchesPerChunk];
        int batchCount = 0;

        int currentTerrainType = int.MinValue;
        int currentVariant = int.MinValue;
        int batchStartVertex = 0;
        int batchVertexCount = 0;

        for (int i = 0; i < _tileInfos.Count; i++)
        {
            var tile = _tileInfos[i];

            // Check if we need to start a new batch
            if (useTextures && (tile.TerrainType != currentTerrainType || tile.Variant != currentVariant))
            {
                // Close previous batch
                if (batchVertexCount > 0 && batchCount < MaxBatchesPerChunk)
                {
                    entry.Batches[batchCount++] = new TextureBatch
                    {
                        TerrainType = currentTerrainType,
                        Variant = currentVariant,
                        FirstVertex = batchStartVertex,
                        VertexCount = batchVertexCount,
                    };
                }

                currentTerrainType = tile.TerrainType;
                currentVariant = tile.Variant;
                batchStartVertex = vertexCount;
                batchVertexCount = 0;
            }

            float u0 = 0f, v0 = 0f, u1 = 1f, v1 = 1f;

            // Triangle 1: Top, Right, Left — each vertex has its own blended color
            WriteVertex(ref floatIdx, tile.TopX, tile.TopY,
                u0 + (u1 - u0) * 0.5f, v0, tile.TopR, tile.TopG, tile.TopB, tile.TintA);
            WriteVertex(ref floatIdx, tile.RightX, tile.RightY,
                u1, v0 + (v1 - v0) * 0.5f, tile.RightR, tile.RightG, tile.RightB, tile.TintA);
            WriteVertex(ref floatIdx, tile.LeftX, tile.LeftY,
                u0, v0 + (v1 - v0) * 0.5f, tile.LeftR, tile.LeftG, tile.LeftB, tile.TintA);

            // Triangle 2: Left, Right, Bottom — same per-vertex colors
            WriteVertex(ref floatIdx, tile.LeftX, tile.LeftY,
                u0, v0 + (v1 - v0) * 0.5f, tile.LeftR, tile.LeftG, tile.LeftB, tile.TintA);
            WriteVertex(ref floatIdx, tile.RightX, tile.RightY,
                u1, v0 + (v1 - v0) * 0.5f, tile.RightR, tile.RightG, tile.RightB, tile.TintA);
            WriteVertex(ref floatIdx, tile.BottomX, tile.BottomY,
                u0 + (u1 - u0) * 0.5f, v1, tile.BottomR, tile.BottomG, tile.BottomB, tile.TintA);

            vertexCount += 6;
            batchVertexCount += 6;
        }

        // Close final batch
        if (useTextures && batchVertexCount > 0 && batchCount < MaxBatchesPerChunk)
        {
            entry.Batches[batchCount++] = new TextureBatch
            {
                TerrainType = currentTerrainType,
                Variant = currentVariant,
                FirstVertex = batchStartVertex,
                VertexCount = batchVertexCount,
            };
        }

        entry.BatchCount = batchCount;

        // Allocate GPU objects if needed
        if (!entry.Allocated)
        {
            entry.Vao = _gl.GenVertexArray();
            entry.Vbo = _gl.GenBuffer();
            entry.Allocated = true;
            _loadedChunkCount++;

            _gl.BindVertexArray(entry.Vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, entry.Vbo);

            // Position (x, y)
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false,
                FloatsPerVertex * sizeof(float), 0);

            // UV (u, v)
            _gl.EnableVertexAttribArray(1);
            _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false,
                FloatsPerVertex * sizeof(float), 2 * sizeof(float));

            // Tint (r, g, b, a)
            _gl.EnableVertexAttribArray(2);
            _gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false,
                FloatsPerVertex * sizeof(float), 4 * sizeof(float));

            _gl.BindVertexArray(0);
        }

        // Upload vertex data
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, entry.Vbo);
        unsafe
        {
            fixed (float* ptr = _stagingBuffer)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer,
                    (nuint)(floatIdx * sizeof(float)),
                    ptr, BufferUsageARB.DynamicDraw);
            }
        }

        entry.VertexCount = vertexCount;
        entry.LodLevel = lodLevel;
    }

    private void WriteVertex(ref int idx, float x, float y, float u, float v,
                             float r, float g, float b, float a)
    {
        _stagingBuffer[idx++] = x;
        _stagingBuffer[idx++] = y;
        _stagingBuffer[idx++] = u;
        _stagingBuffer[idx++] = v;
        _stagingBuffer[idx++] = r;
        _stagingBuffer[idx++] = g;
        _stagingBuffer[idx++] = b;
        _stagingBuffer[idx++] = a;
    }

    /// <summary>
    /// Ensure the LRU cache has room. If at capacity, evict the least-recently-used chunk.
    /// </summary>
    private void EnsureCacheSpace(int neededIdx)
    {
        if (_gpuCache[neededIdx].Allocated)
            return; // Already has a slot

        while (_loadedChunkCount >= _maxCachedChunks && _lruOrder.Count > 0)
        {
            var firstNode = _lruOrder.First!;
            int evictIdx = firstNode.Value;
            _lruOrder.RemoveFirst();
            _lruLookup.Remove(evictIdx);

            if (_gpuCache[evictIdx].Allocated && evictIdx != neededIdx)
            {
                FreeChunkGpu(evictIdx);
            }
        }
    }

    private void FreeChunkGpu(int chunkIdx)
    {
        ref var entry = ref _gpuCache[chunkIdx];
        if (!entry.Allocated) return;

        _gl.DeleteVertexArray(entry.Vao);
        _gl.DeleteBuffer(entry.Vbo);
        entry.Vao = 0;
        entry.Vbo = 0;
        entry.VertexCount = 0;
        entry.Allocated = false;
        entry.LodLevel = -1;
        entry.BatchCount = 0;
        _loadedChunkCount--;
    }

    private void TouchLru(int chunkIdx)
    {
        if (_lruLookup.TryGetValue(chunkIdx, out var node))
        {
            _lruOrder.Remove(node);
            _lruOrder.AddLast(node);
        }
        else
        {
            var newNode = _lruOrder.AddLast(chunkIdx);
            _lruLookup[chunkIdx] = newNode;
        }
    }

    /// <summary>Build a simple orthographic projection matrix for 2D rendering.</summary>
    private static float[] BuildOrthoMatrix(int width, int height)
    {
        // Maps (0,0)-(width,height) to clip space (-1,-1)-(1,1)
        float l = 0f;
        float r = width;
        float b = height;
        float t = 0f;

        return
        [
            2f / (r - l), 0f, 0f, 0f,
            0f, 2f / (t - b), 0f, 0f,
            0f, 0f, -1f, 0f,
            (r + l) / (l - r), (t + b) / (b - t), 0f, 1f,
        ];
    }

    /// <summary>Free all allocated GPU chunk buffers.</summary>
    public void Dispose()
    {
        for (int i = 0; i < _gpuCache.Length; i++)
        {
            FreeChunkGpu(i);
        }
        _lruOrder.Clear();
        _lruLookup.Clear();
    }
}
