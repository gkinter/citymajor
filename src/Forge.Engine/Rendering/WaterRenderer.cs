using Forge.Engine.Core;
using Forge.Engine.Data;
using Silk.NET.OpenGL;

namespace Forge.Engine.Rendering;

/// <summary>
/// Renders water tiles with animated UV scrolling, pollution-based color tinting,
/// specular highlights, and simple reflections. Water tiles (terrain type 3) are
/// identified during rendering and drawn with the dedicated water shader instead
/// of the standard sprite shader.
///
/// Rendering pipeline:
///   1. Identify water tiles in visible chunks.
///   2. Optionally render a reflection pass: draw buildings above water tiles into
///      a flipped FBO for use as a reflection texture.
///   3. Draw water quads using the water shader with animated UVs, pollution
///      data from the simulation's InfluenceMap, and the reflection texture.
///
/// The water shader uses two scrolling UV layers, procedural wave noise,
/// pollution color interpolation (blue to brown-green), and rippled reflections.
/// </summary>
public sealed class WaterRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly Config _config;

    // Water shader
    private ShaderProgram? _waterShader;

    // Reflection FBO
    private uint _reflectionFbo;
    private uint _reflectionTexture;
    private int _reflectionWidth;
    private int _reflectionHeight;

    // Animation state
    private float _animationTime;
    private float _gameTime;

    // Per-tile pollution data (flattened, matching tile grid)
    private float[]? _pollutionGrid;

    // Water tile quad batch (reused each frame)
    private readonly List<WaterQuad> _waterQuads = new();

    // Vertex data for GPU upload
    private uint _vao;
    private uint _vbo;
    private uint _instanceVbo;
    private bool _gpuResourcesInitialized;

    // Quad vertices (same as sprite renderer: 4 floats per vertex = pos.xy + uv.zw)
    private static readonly float[] QuadVertices =
    [
        0f, 0f, 0f, 0f, // top-left
        1f, 0f, 1f, 0f, // top-right
        1f, 1f, 1f, 1f, // bottom-right
        0f, 0f, 0f, 0f, // top-left
        1f, 1f, 1f, 1f, // bottom-right
        0f, 1f, 0f, 1f, // bottom-left
    ];

    // Maximum water tiles per frame (limits GPU buffer size)
    private const int MaxWaterTiles = 16384;

    // Instance data: 16 floats per instance (PosSize + UV + Tint)
    private readonly float[] _instanceData = new float[MaxWaterTiles * 16];
    private int _instanceCount;

    public WaterRenderer(GL gl, Config config)
    {
        _gl = gl;
        _config = config;
    }

    /// <summary>
    /// Initialize GPU resources and compile the water shader.
    /// Call after the GL context is available.
    /// </summary>
    public void Init(string waterVertSource, string waterFragSource)
    {
        _waterShader = new ShaderProgram(_gl, waterVertSource, waterFragSource);
        InitGpuResources();
        InitReflectionFbo(_config.WindowWidth / 2, _config.WindowHeight / 2);
    }

    /// <summary>
    /// Initialize with an AssetManager for shader loading.
    /// </summary>
    public void Init(AssetManager assets)
    {
        _waterShader = assets.LoadShader(
            "assets/shaders/water.vert",
            "assets/shaders/water.frag");
        if (_waterShader != null)
        {
            InitGpuResources();
            InitReflectionFbo(_config.WindowWidth / 2, _config.WindowHeight / 2);
        }
    }

    private void InitGpuResources()
    {
        // Quad VAO
        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        // Quad VBO (shared geometry)
        _vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        unsafe
        {
            fixed (float* ptr = QuadVertices)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer,
                    (nuint)(QuadVertices.Length * sizeof(float)),
                    ptr, BufferUsageARB.StaticDraw);
            }
        }

        // Attribute 0: a_QuadVertex (vec4: xy position, zw uv)
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 4, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);

        // Instance VBO
        _instanceVbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _instanceVbo);
        unsafe
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer,
                (nuint)(MaxWaterTiles * 16 * sizeof(float)),
                null, BufferUsageARB.DynamicDraw);
        }

        // Attribute 1: a_PosSize (vec4: xy position, zw size) — per instance
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 16 * sizeof(float), 0);
        _gl.VertexAttribDivisor(1, 1);

        // Attribute 2: a_UV (vec4: uv min, uv max) — per instance
        _gl.EnableVertexAttribArray(2);
        _gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, 16 * sizeof(float), 4 * sizeof(float));
        _gl.VertexAttribDivisor(2, 1);

        // Attribute 3: a_Tint (vec4: rgba) — per instance
        _gl.EnableVertexAttribArray(3);
        _gl.VertexAttribPointer(3, 4, VertexAttribPointerType.Float, false, 16 * sizeof(float), 8 * sizeof(float));
        _gl.VertexAttribDivisor(3, 1);

        _gl.BindVertexArray(0);
        _gpuResourcesInitialized = true;
    }

    private void InitReflectionFbo(int width, int height)
    {
        DestroyReflectionFbo();

        _reflectionWidth = width;
        _reflectionHeight = height;

        _reflectionFbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _reflectionFbo);

        _reflectionTexture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _reflectionTexture);
        unsafe
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                (uint)width, (uint)height, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, null);
        }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D, _reflectionTexture, 0);

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    private void DestroyReflectionFbo()
    {
        if (_reflectionFbo != 0) _gl.DeleteFramebuffer(_reflectionFbo);
        if (_reflectionTexture != 0) _gl.DeleteTexture(_reflectionTexture);
        _reflectionFbo = 0;
        _reflectionTexture = 0;
    }

    // =========================================================================
    // Per-frame update
    // =========================================================================

    /// <summary>
    /// Update animation time. Call once per frame.
    /// </summary>
    /// <param name="dt">Frame delta time in seconds.</param>
    /// <param name="gameTime">Total game time in seconds (for consistent animation).</param>
    public void Update(float dt, float gameTime)
    {
        _animationTime += dt;
        _gameTime = gameTime;
    }

    /// <summary>
    /// Accept pollution data from the simulation's InfluenceMap.
    /// The array should be sized worldSize * worldSize, one float per tile.
    /// </summary>
    public void SetPollutionData(float[] pollutionGrid)
    {
        _pollutionGrid = pollutionGrid;
    }

    // =========================================================================
    // Rendering
    // =========================================================================

    /// <summary>
    /// Render all visible water tiles using the water shader.
    /// </summary>
    /// <param name="camera">Current isometric camera.</param>
    /// <param name="tiles">Tile data for terrain type lookup.</param>
    /// <param name="chunks">Chunk manager for visibility culling.</param>
    public void Render(IsometricCamera camera, TileData tiles, ChunkManager chunks)
    {
        if (_waterShader == null || !_gpuResourcesInitialized) return;

        // Collect visible water tiles into the instance buffer
        CollectWaterTiles(camera, tiles, chunks);

        if (_instanceCount == 0) return;

        // Compute average pollution for the uniform (shader uses a single value per draw call;
        // per-tile pollution is encoded in the tint color for finer granularity)
        float avgPollution = ComputeAveragePollution();

        // Bind shader and set uniforms
        _waterShader.Use();
        _waterShader.SetUniform("u_time", _animationTime);
        _waterShader.SetUniform("u_pollution", avgPollution);
        _waterShader.SetUniform("u_resolution", (float)camera.ViewportWidth, (float)camera.ViewportHeight);
        _waterShader.SetUniform("u_reflectionStrength", 0.4f);
        _waterShader.SetUniform("u_waterTex", 0);
        _waterShader.SetUniform("u_reflectionTex", 1);

        // Bind reflection texture
        _gl.ActiveTexture(TextureUnit.Texture1);
        if (_reflectionTexture != 0)
            _gl.BindTexture(TextureTarget.Texture2D, _reflectionTexture);

        // Upload instance data
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _instanceVbo);
        unsafe
        {
            fixed (float* ptr = _instanceData)
            {
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0,
                    (nuint)(_instanceCount * 16 * sizeof(float)), ptr);
            }
        }

        // Enable blending for transparency
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        // Draw instanced
        _gl.BindVertexArray(_vao);
        _gl.DrawArraysInstanced(PrimitiveType.Triangles, 0, 6, (uint)_instanceCount);
        _gl.BindVertexArray(0);
    }

    /// <summary>
    /// Resize the reflection FBO (call when viewport changes).
    /// </summary>
    public void Resize(int width, int height)
    {
        // Reflection at half resolution for performance
        InitReflectionFbo(System.Math.Max(1, width / 2), System.Math.Max(1, height / 2));
    }

    // =========================================================================
    // Water tile collection
    // =========================================================================

    /// <summary>
    /// Scan visible chunks for water tiles (terrain type 3) and build the
    /// instance data array for GPU rendering.
    /// </summary>
    private void CollectWaterTiles(IsometricCamera camera, TileData tiles, ChunkManager chunks)
    {
        _instanceCount = 0;
        _waterQuads.Clear();

        int chunkSize = chunks.ChunkSize;
        int chunksPerAxis = chunks.ChunksPerAxis;
        var (offsetX, offsetY) = camera.GetRenderOffset();
        float zoom = camera.SmoothZoom;
        int tileW = _config.TileWidth;
        int tileH = _config.TileHeight;

        for (int cy = 0; cy < chunksPerAxis && _instanceCount < MaxWaterTiles; cy++)
        {
            for (int cx = 0; cx < chunksPerAxis && _instanceCount < MaxWaterTiles; cx++)
            {
                if (!chunks.IsVisible(cx, cy)) continue;

                int tileStartX = cx * chunkSize;
                int tileStartY = cy * chunkSize;
                int tileEndX = System.Math.Min(tileStartX + chunkSize, tiles.Size);
                int tileEndY = System.Math.Min(tileStartY + chunkSize, tiles.Size);

                for (int ty = tileStartY; ty < tileEndY && _instanceCount < MaxWaterTiles; ty++)
                {
                    int rowBase = ty * tiles.Size;
                    for (int tx = tileStartX; tx < tileEndX && _instanceCount < MaxWaterTiles; tx++)
                    {
                        int idx = rowBase + tx;
                        if (tiles.TerrainType[idx] != 3) continue; // Not water

                        // Compute isometric screen position
                        float isoX = (tx - ty) * (tileW * 0.5f);
                        float isoY = (tx + ty) * (tileH * 0.5f);

                        float screenX = isoX * zoom + offsetX;
                        float screenY = isoY * zoom + offsetY;
                        float screenW = tileW * zoom;
                        float screenH = tileH * zoom;

                        // Frustum cull
                        if (screenX + screenW < 0 || screenX > camera.ViewportWidth ||
                            screenY + screenH < 0 || screenY > camera.ViewportHeight)
                            continue;

                        // Get per-tile pollution
                        float pollution = 0f;
                        if (_pollutionGrid != null && idx < _pollutionGrid.Length)
                            pollution = System.Math.Clamp(_pollutionGrid[idx], 0f, 1f);

                        // Check if tile is at water edge (adjacent to non-water for foam)
                        float edgeFactor = IsWaterEdge(tiles, tx, ty) ? 1f : 0f;

                        // Tint: encode pollution in RGB channels, edge factor in alpha
                        float tintR = 1f - pollution * 0.3f;
                        float tintG = 1f - pollution * 0.2f;
                        float tintB = 1f;
                        float tintA = edgeFactor;

                        // Pack instance data: PosSize (4) + UV (4) + Tint (4) + padding (4)
                        int offset = _instanceCount * 16;
                        _instanceData[offset + 0] = screenX;     // pos.x
                        _instanceData[offset + 1] = screenY;     // pos.y
                        _instanceData[offset + 2] = screenW;     // size.x
                        _instanceData[offset + 3] = screenH;     // size.y
                        _instanceData[offset + 4] = 0f;          // uv.min.x
                        _instanceData[offset + 5] = 0f;          // uv.min.y
                        _instanceData[offset + 6] = 1f;          // uv.max.x
                        _instanceData[offset + 7] = 1f;          // uv.max.y
                        _instanceData[offset + 8] = tintR;       // tint.r
                        _instanceData[offset + 9] = tintG;       // tint.g
                        _instanceData[offset + 10] = tintB;      // tint.b
                        _instanceData[offset + 11] = tintA;      // tint.a (edge factor)
                        _instanceData[offset + 12] = 0f;         // reserved
                        _instanceData[offset + 13] = 0f;         // reserved
                        _instanceData[offset + 14] = 0f;         // reserved
                        _instanceData[offset + 15] = 0f;         // reserved

                        _instanceCount++;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Check if a water tile is adjacent to a non-water tile (for edge foam rendering).
    /// </summary>
    public static bool IsWaterEdge(TileData tiles, int x, int y)
    {
        // Check 4 cardinal neighbors
        if (x > 0 && tiles.TerrainType[tiles.Index(x - 1, y)] != 3) return true;
        if (x < tiles.Size - 1 && tiles.TerrainType[tiles.Index(x + 1, y)] != 3) return true;
        if (y > 0 && tiles.TerrainType[tiles.Index(x, y - 1)] != 3) return true;
        if (y < tiles.Size - 1 && tiles.TerrainType[tiles.Index(x, y + 1)] != 3) return true;
        return false;
    }

    /// <summary>
    /// Compute the average pollution across all currently visible water tiles.
    /// Used as a global shader uniform for the overall water color tint.
    /// </summary>
    private float ComputeAveragePollution()
    {
        if (_pollutionGrid == null || _instanceCount == 0) return 0f;

        // Use the average of pollution values from the collected water tiles
        // (encoded in tint channel during CollectWaterTiles)
        float sum = 0f;
        int count = 0;
        for (int i = 0; i < _instanceCount; i++)
        {
            // The tint R channel stores 1.0 - pollution*0.3, so reverse it
            float tintR = _instanceData[i * 16 + 8];
            float pollution = (1f - tintR) / 0.3f;
            sum += System.Math.Clamp(pollution, 0f, 1f);
            count++;
        }

        return count > 0 ? sum / count : 0f;
    }

    /// <summary>
    /// Interpolate water pollution color for a given pollution level.
    /// Returns the RGB color as a tuple. Useful for minimap rendering or UI display.
    /// </summary>
    public static (float r, float g, float b) InterpolatePollutionColor(float pollution)
    {
        pollution = System.Math.Clamp(pollution, 0f, 1f);

        // Clean: (0.2, 0.4, 0.8) -> Dirty: (0.3, 0.35, 0.15)
        float r = 0.2f + (0.3f - 0.2f) * pollution;
        float g = 0.4f + (0.35f - 0.4f) * pollution;
        float b = 0.8f + (0.15f - 0.8f) * pollution;

        return (r, g, b);
    }

    public void Dispose()
    {
        _waterShader?.Dispose();
        DestroyReflectionFbo();

        if (_gpuResourcesInitialized)
        {
            _gl.DeleteVertexArray(_vao);
            _gl.DeleteBuffer(_vbo);
            _gl.DeleteBuffer(_instanceVbo);
            _vao = 0;
            _vbo = 0;
            _instanceVbo = 0;
            _gpuResourcesInitialized = false;
        }
    }
}

/// <summary>
/// Internal representation of a water tile quad for batching.
/// </summary>
internal struct WaterQuad
{
    public float ScreenX;
    public float ScreenY;
    public float Width;
    public float Height;
    public float Pollution;
    public bool IsEdge;
    public int TileIndex;
}
