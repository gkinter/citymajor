using System.Numerics;
using Forge.Engine.Data;
using Forge.Engine.Rendering;
using Silk.NET.OpenGL;

namespace Forge.Engine.Rendering;

/// <summary>
/// Renders a 200x200 pixel minimap in the bottom-left corner of the screen.
/// Shows terrain colors, buildings, roads, camera viewport rectangle, and active overlay.
/// Supports click-to-jump and drag-to-pan interactions.
/// Toggle visibility with M key.
/// </summary>
public sealed class MinimapRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly int _worldSize;

    // Minimap display size in pixels
    private const int MinimapSize = 200;
    private const int MinimapMargin = 10;
    private const int BorderWidth = 1;

    // GPU resources
    private uint _minimapTexture;
    private uint _vao;
    private uint _vbo;
    private ShaderProgram? _shader;
    private bool _gpuInitialized;

    // CPU-side pixel buffer (RGBA8, one pixel per tile)
    private readonly byte[] _pixelBuffer;

    // Visibility state
    private bool _visible = true;

    // Interaction state
    private bool _isDragging;

    // Overlay integration
    private float[]? _overlayData;
    private int _overlayType; // 0 = none, matches GameOverlaySystem.OverlayId

    /// <summary>Whether the minimap is visible.</summary>
    public bool Visible
    {
        get => _visible;
        set => _visible = value;
    }

    /// <summary>Set the active overlay data to render on the minimap. Null = show terrain.</summary>
    public void SetOverlayData(float[]? data, int overlayType)
    {
        _overlayData = data;
        _overlayType = overlayType;
    }

    /// <summary>Whether the minimap is currently handling a drag interaction.</summary>
    public bool IsDragging => _isDragging;

    // Terrain type -> RGBA color mapping
    // TerrainType: 0=grass, 1=dirt, 2=sand, 3=water, 4=rock, 5=forest, 6=marsh, 7=snow
    private static readonly (byte r, byte g, byte b)[] TerrainColors =
    [
        (90, 166, 64),   // 0 = grass (green)
        (140, 102, 64),  // 1 = dirt (brown)
        (217, 204, 140), // 2 = sand (tan)
        (51, 89, 179),   // 3 = water (blue)
        (115, 115, 115), // 4 = rock (gray)
        (34, 102, 34),   // 5 = forest (dark green)
        (76, 115, 64),   // 6 = marsh (muted green)
        (230, 230, 240), // 7 = snow (white-ish)
    ];

    // Zone type colors for buildings: 0=none, 1=res_low, 2=res_high, 3=commercial, 4=industrial, 5=office, 6=mixed, 7=agricultural
    private static readonly (byte r, byte g, byte b)[] ZoneColors =
    [
        (255, 255, 255), // 0 = no zone (white dot for generic building)
        (0, 200, 0),     // 1 = residential low (green)
        (0, 160, 0),     // 2 = residential high (green)
        (0, 100, 220),   // 3 = commercial (blue)
        (220, 200, 0),   // 4 = industrial (yellow)
        (100, 150, 220), // 5 = office (light blue)
        (180, 100, 220), // 6 = mixed use (purple)
        (140, 180, 60),  // 7 = agricultural (olive)
    ];

    public MinimapRenderer(GL gl, int worldSize)
    {
        _gl = gl;
        _worldSize = worldSize;
        _pixelBuffer = new byte[worldSize * worldSize * 4]; // RGBA
    }

    /// <summary>
    /// Initialize GPU resources (texture, VAO/VBO, shader). Call once after GL context is ready.
    /// </summary>
    public void Init()
    {
        // Create minimap texture (RGBA8, worldSize x worldSize)
        _minimapTexture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _minimapTexture);

        unsafe
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                (uint)_worldSize, (uint)_worldSize, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, null);
        }

        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        // Create fullscreen quad VAO/VBO (we'll set actual vertices at render time based on viewport)
        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        // Allocate space for 6 vertices * 4 floats (pos.xy + uv.xy)
        unsafe
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(6 * 4 * sizeof(float)),
                null, BufferUsageARB.DynamicDraw);
        }

        // Position attribute (location 0)
        _gl.EnableVertexAttribArray(0);
        unsafe
        {
            _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false,
                4 * sizeof(float), (void*)0);
        }

        // UV attribute (location 1)
        _gl.EnableVertexAttribArray(1);
        unsafe
        {
            _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false,
                4 * sizeof(float), (void*)(2 * sizeof(float)));
        }

        _gl.BindVertexArray(0);

        // Compile shader (inline, minimal)
        const string vertSrc = @"#version 330 core
layout(location = 0) in vec2 a_position;
layout(location = 1) in vec2 a_uv;
out vec2 v_uv;
void main() {
    gl_Position = vec4(a_position, 0.0, 1.0);
    v_uv = a_uv;
}";

        const string fragSrc = @"#version 330 core
in vec2 v_uv;
uniform sampler2D u_minimapTex;
out vec4 FragColor;
void main() {
    FragColor = texture(u_minimapTex, v_uv);
}";

        _shader = new ShaderProgram(_gl, vertSrc, fragSrc);
        _gpuInitialized = true;
    }

    /// <summary>
    /// Update the minimap pixel buffer from tile data. Call once per frame or when tiles change.
    /// </summary>
    public void UpdatePixels(TileData tiles)
    {
        bool hasOverlay = _overlayData != null && _overlayType > 0 && _overlayData.Length >= tiles.Count;

        for (int i = 0; i < tiles.Count; i++)
        {
            int pixelIdx = i * 4;

            if (hasOverlay)
            {
                float value = _overlayData![i];
                var (r, g, b) = GetOverlayColor(value, _overlayType);
                byte alpha = GetOverlayAlpha(value, _overlayType);

                if (value < 0.001f)
                {
                    // Fall back to terrain color for zero-value tiles even in overlay mode
                    var terrain = GetTerrainColor(tiles, i);
                    _pixelBuffer[pixelIdx + 0] = terrain.r;
                    _pixelBuffer[pixelIdx + 1] = terrain.g;
                    _pixelBuffer[pixelIdx + 2] = terrain.b;
                    _pixelBuffer[pixelIdx + 3] = 255;
                }
                else
                {
                    // Blend overlay with terrain
                    var terrain = GetTerrainColor(tiles, i);
                    float a = alpha / 255f;
                    _pixelBuffer[pixelIdx + 0] = (byte)(terrain.r * (1f - a) + r * a);
                    _pixelBuffer[pixelIdx + 1] = (byte)(terrain.g * (1f - a) + g * a);
                    _pixelBuffer[pixelIdx + 2] = (byte)(terrain.b * (1f - a) + b * a);
                    _pixelBuffer[pixelIdx + 3] = 255;
                }
            }
            else
            {
                var color = GetTileColor(tiles, i);
                _pixelBuffer[pixelIdx + 0] = color.r;
                _pixelBuffer[pixelIdx + 1] = color.g;
                _pixelBuffer[pixelIdx + 2] = color.b;
                _pixelBuffer[pixelIdx + 3] = 255;
            }
        }
    }

    /// <summary>
    /// Upload the pixel buffer to the GPU texture.
    /// </summary>
    public void UploadTexture()
    {
        if (!_gpuInitialized) return;

        _gl.BindTexture(TextureTarget.Texture2D, _minimapTexture);
        unsafe
        {
            fixed (byte* ptr = _pixelBuffer)
            {
                _gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0,
                    (uint)_worldSize, (uint)_worldSize,
                    PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
            }
        }
    }

    /// <summary>
    /// Render the minimap quad with border and camera viewport rectangle.
    /// </summary>
    public void Render(int viewportWidth, int viewportHeight, IsometricCamera camera)
    {
        if (!_visible || !_gpuInitialized || _shader == null) return;

        // Calculate minimap position in NDC (bottom-left corner)
        float mmX = MinimapMargin;
        float mmY = viewportHeight - MinimapSize - MinimapMargin;
        float mmW = MinimapSize;
        float mmH = MinimapSize;

        // Convert pixel coords to NDC (-1..1)
        float ndcX0 = (mmX / viewportWidth) * 2f - 1f;
        float ndcY0 = 1f - ((mmY + mmH) / viewportHeight) * 2f;
        float ndcX1 = ((mmX + mmW) / viewportWidth) * 2f - 1f;
        float ndcY1 = 1f - (mmY / viewportHeight) * 2f;

        // Build quad vertices (2 triangles)
        float[] verts =
        [
            ndcX0, ndcY0, 0f, 1f,
            ndcX1, ndcY0, 1f, 1f,
            ndcX1, ndcY1, 1f, 0f,
            ndcX0, ndcY0, 0f, 1f,
            ndcX1, ndcY1, 1f, 0f,
            ndcX0, ndcY1, 0f, 0f,
        ];

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        unsafe
        {
            fixed (float* ptr = verts)
            {
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0,
                    (nuint)(verts.Length * sizeof(float)), ptr);
            }
        }

        // Draw background (semi-transparent black)
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        // Bind minimap texture and draw
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _minimapTexture);

        _shader.Use();
        _shader.SetUniform("u_minimapTex", 0);

        _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        _gl.BindVertexArray(0);

        // Store minimap bounds for the UI overlay pass
        _lastMmX = mmX; _lastMmY = mmY; _lastMmW = mmW; _lastMmH = mmH;
        _lastCamera = camera;
        _lastVpW = viewportWidth; _lastVpH = viewportHeight;
    }

    // Cached minimap bounds for the UI overlay pass (set during Render, read during RenderUI)
    private float _lastMmX, _lastMmY, _lastMmW, _lastMmH;
    private int _lastVpW, _lastVpH;
    private IsometricCamera? _lastCamera;

    /// <summary>
    /// Draw the minimap border and camera viewport rectangle.
    /// MUST be called during the ImGui frame (OnRenderUI), NOT during OnRender.
    /// </summary>
    public void RenderUI()
    {
        if (!_visible || _lastCamera == null) return;
        DrawMinimapOverlay(_lastVpW, _lastVpH, _lastCamera, _lastMmX, _lastMmY, _lastMmW, _lastMmH);
    }

    /// <summary>
    /// Draw the minimap border and camera viewport rectangle using ImGui draw list.
    /// </summary>
    private void DrawMinimapOverlay(int viewportWidth, int viewportHeight,
        IsometricCamera camera, float mmX, float mmY, float mmW, float mmH)
    {
        var drawList = ImGuiNET.ImGui.GetBackgroundDrawList();

        // Border (1px, #444444)
        uint borderColor = ImGuiNET.ImGui.ColorConvertFloat4ToU32(
            new Vector4(0.267f, 0.267f, 0.267f, 1f));
        drawList.AddRect(
            new Vector2(mmX - BorderWidth, mmY - BorderWidth),
            new Vector2(mmX + mmW + BorderWidth, mmY + mmH + BorderWidth),
            borderColor, 0f, ImGuiNET.ImDrawFlags.None, BorderWidth);

        // Semi-transparent background behind the minimap texture (for the alpha=0.7 requirement)
        uint bgColor = ImGuiNET.ImGui.ColorConvertFloat4ToU32(
            new Vector4(0f, 0f, 0f, 0.7f));
        drawList.AddRectFilled(
            new Vector2(mmX, mmY),
            new Vector2(mmX + mmW, mmY + mmH),
            bgColor);

        // Camera viewport rectangle
        // Convert camera frustum (world-pixel space) to minimap pixel coordinates
        // The minimap maps tile coordinates (0..worldSize) to the minimap rect
        var frustum = camera.GetFrustumBounds();

        // We need to convert world-pixel frustum back to approximate tile coords.
        // In isometric: screenX = (gx - gy) * halfTileW, screenY = (gx + gy) * halfTileH
        // For the minimap, we use a simple mapping: tile (0,0) = top of diamond, tile (worldSize, worldSize) = bottom
        // We map the camera center to minimap coordinates instead of the frustum edges
        // because the frustum is in screen-pixel space (rotated iso), not grid space.

        // Convert camera center to grid coords
        float invZoom = 1f / camera.SmoothZoom;
        float halfViewW = camera.ViewportWidth * 0.5f * invZoom;
        float halfViewH = camera.ViewportHeight * 0.5f * invZoom;

        // Approximate grid extent visible: use the camera's screen-to-grid at corners
        // For simplicity, map the frustum bounds to normalized tile space
        // Camera X/Y are in world-pixel space. The world spans from worldMinX..worldMaxX in pixel space.
        // For an isometric map with tiles at (0..N, 0..N), the pixel bounds are:
        //   minX ~ -(N * tileW/2), maxX ~ (N * tileW/2)
        //   minY ~ 0, maxY ~ N * tileH

        float tileW = 64f; // from config, but we approximate
        float tileH = 32f;
        float halfTW = tileW * 0.5f;
        float halfTH = tileH * 0.5f;

        // Convert camera world-pixel center to approximate tile coords
        // Iso projection: sx = (gx - gy) * halfTW, sy = (gx + gy) * halfTH
        // Inverse: gx = sx/(2*halfTW) + sy/(2*halfTH), gy = sy/(2*halfTH) - sx/(2*halfTW)
        float camTileX = camera.X / (2f * halfTW) + camera.Y / (2f * halfTH);
        float camTileY = camera.Y / (2f * halfTH) - camera.X / (2f * halfTW);

        // Approximate visible tile extent (how many tiles fit in half the view)
        float viewTilesX = halfViewW / halfTW;
        float viewTilesY = halfViewH / halfTH;

        // Map tile coords to minimap pixel position
        float scaleX = mmW / _worldSize;
        float scaleY = mmH / _worldSize;

        float rectMinX = mmX + (camTileX - viewTilesX * 0.5f) * scaleX;
        float rectMinY = mmY + (camTileY - viewTilesY * 0.5f) * scaleY;
        float rectMaxX = mmX + (camTileX + viewTilesX * 0.5f) * scaleX;
        float rectMaxY = mmY + (camTileY + viewTilesY * 0.5f) * scaleY;

        // Clamp to minimap bounds
        rectMinX = System.Math.Clamp(rectMinX, mmX, mmX + mmW);
        rectMinY = System.Math.Clamp(rectMinY, mmY, mmY + mmH);
        rectMaxX = System.Math.Clamp(rectMaxX, mmX, mmX + mmW);
        rectMaxY = System.Math.Clamp(rectMaxY, mmY, mmY + mmH);

        uint viewportColor = ImGuiNET.ImGui.ColorConvertFloat4ToU32(
            new Vector4(1f, 1f, 1f, 0.8f));
        drawList.AddRect(
            new Vector2(rectMinX, rectMinY),
            new Vector2(rectMaxX, rectMaxY),
            viewportColor, 0f, ImGuiNET.ImDrawFlags.None, 1.5f);
    }

    /// <summary>
    /// Handle mouse input on the minimap. Returns true if the minimap consumed the input.
    /// </summary>
    /// <param name="mouseX">Mouse X in screen pixels.</param>
    /// <param name="mouseY">Mouse Y in screen pixels.</param>
    /// <param name="leftDown">Whether left mouse button is held.</param>
    /// <param name="leftPressed">Whether left mouse button was just pressed this frame.</param>
    /// <param name="viewportWidth">Current viewport width.</param>
    /// <param name="viewportHeight">Current viewport height.</param>
    /// <param name="camera">The camera to move on click/drag.</param>
    /// <returns>True if the minimap consumed the mouse event.</returns>
    public bool HandleInput(int mouseX, int mouseY, bool leftDown, bool leftPressed,
        int viewportWidth, int viewportHeight, IsometricCamera camera)
    {
        if (!_visible) return false;

        float mmX = MinimapMargin;
        float mmY = viewportHeight - MinimapSize - MinimapMargin;

        bool isOverMinimap = mouseX >= mmX && mouseX <= mmX + MinimapSize &&
                             mouseY >= mmY && mouseY <= mmY + MinimapSize;

        if (leftPressed && isOverMinimap)
        {
            _isDragging = true;
        }

        if (!leftDown)
        {
            _isDragging = false;
        }

        if (_isDragging && isOverMinimap)
        {
            // Convert minimap pixel to tile coordinate
            float normalizedX = (mouseX - mmX) / MinimapSize;
            float normalizedY = (mouseY - mmY) / MinimapSize;

            int tileX = (int)(normalizedX * _worldSize);
            int tileY = (int)(normalizedY * _worldSize);

            tileX = System.Math.Clamp(tileX, 0, _worldSize - 1);
            tileY = System.Math.Clamp(tileY, 0, _worldSize - 1);

            camera.CenterOnTile(tileX, tileY);
            return true;
        }

        if (isOverMinimap)
        {
            // Consume hover to prevent game interaction through minimap
            return true;
        }

        return false;
    }

    private static (byte r, byte g, byte b) GetTerrainColor(TileData tiles, int index)
    {
        byte terrain = tiles.TerrainType[index];
        if (terrain < TerrainColors.Length)
            return TerrainColors[terrain];
        return TerrainColors[0]; // default to grass
    }

    private static (byte r, byte g, byte b) GetTileColor(TileData tiles, int index)
    {
        // Buildings override terrain
        if (tiles.BuildingId[index] != 0)
        {
            byte zone = tiles.ZoneType[index];
            if (zone < ZoneColors.Length)
                return ZoneColors[zone];
            return ZoneColors[0];
        }

        // Roads shown as dark gray
        if (tiles.RoadFlags[index] != 0)
        {
            return (64, 64, 64);
        }

        // Terrain color
        return GetTerrainColor(tiles, index);
    }

    private static (byte r, byte g, byte b) GetOverlayColor(float value, int overlayType)
    {
        float t = System.Math.Clamp(value, 0f, 1f);

        return overlayType switch
        {
            1 => LerpColor((0, 200, 0), (255, 255, 0), (255, 0, 0), t),         // Traffic: green->yellow->red
            2 => LerpColor2((206, 147, 216), (106, 27, 154), t),                  // Land Value: light purple->deep purple
            3 => LerpColor2((255, 0, 0), (0, 200, 0), t),                         // Happiness: red(low)->green(high)
            4 => ((byte)(78), (byte)(52), (byte)(46)),                             // Pollution: brown
            5 => ((byte)(198), (byte)(40), (byte)(40)),                            // Crime: red
            6 => ((byte)(230), (byte)(81), (byte)(0)),                             // Fire: orange
            7 => t > 0.5f ? ((byte)(255), (byte)(230), (byte)(0)) : ((byte)(128), (byte)(128), (byte)(128)),  // Power: yellow/gray
            8 => t > 0.5f ? ((byte)(51), (byte)(102), (byte)(230)) : ((byte)(128), (byte)(128), (byte)(128)), // Water: blue/gray
            _ => ((byte)(255), (byte)(255), (byte)(255)),
        };
    }

    private static byte GetOverlayAlpha(float value, int overlayType)
    {
        float t = System.Math.Clamp(value, 0f, 1f);

        return overlayType switch
        {
            1 or 2 or 3 or 7 or 8 => 180,                          // Fixed alpha
            4 or 5 or 6 => (byte)(t * 200),                         // Proportional alpha
            _ => 128,
        };
    }

    private static (byte r, byte g, byte b) LerpColor2(
        (byte r, byte g, byte b) a, (byte r, byte g, byte b) b, float t)
    {
        return (
            (byte)(a.r + (b.r - a.r) * t),
            (byte)(a.g + (b.g - a.g) * t),
            (byte)(a.b + (b.b - a.b) * t)
        );
    }

    private static (byte r, byte g, byte b) LerpColor(
        (byte r, byte g, byte b) a, (byte r, byte g, byte b) mid, (byte r, byte g, byte b) b, float t)
    {
        if (t < 0.5f)
            return LerpColor2(a, mid, t * 2f);
        return LerpColor2(mid, b, (t - 0.5f) * 2f);
    }

    public void Dispose()
    {
        if (_gpuInitialized)
        {
            _gl.DeleteTexture(_minimapTexture);
            _gl.DeleteVertexArray(_vao);
            _gl.DeleteBuffer(_vbo);
            _shader?.Dispose();
            _gpuInitialized = false;
        }
    }
}
