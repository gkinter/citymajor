using Forge.Engine.Core;
using Forge.Engine.Math;

namespace Forge.Engine.Rendering;

/// <summary>
/// Isometric camera with pan (WASD/edge scroll/middle-mouse drag),
/// smooth zoom (1x-8x with lerp transition over 100ms), and rotation (Q/E 90-degree snap).
/// Uses ease-in-quad acceleration curves over 200ms with instant stop on release.
///
/// Additional features:
///   - Smooth zoom interpolation (lerp even for integer zoom levels)
///   - Frustum bounds calculation for chunk culling
///   - World bounds with elastic bounce at map edges
///   - Screen-to-world raycasting for mouse picking
/// </summary>
public sealed class IsometricCamera
{
    private readonly Config _config;

    // Position in world-pixel space (center of viewport)
    private float _x;
    private float _y;

    // Integer zoom level (1-8) and smooth interpolated zoom
    private int _zoomLevel = 2;
    private float _smoothZoom = 2f;
    private float _zoomVelocity;

    // Zoom transition timing
    private const float ZoomLerpSpeed = 15f; // Higher = faster transition (~100ms to settle)

    // Rotation in 90-degree increments (0-3)
    private int _rotation;

    // Pan velocity for smooth acceleration
    private float _panVelX;
    private float _panVelY;

    // Pan acceleration state (0.0 = no input, ramps to 1.0 over 200ms)
    private float _panAccelX;
    private float _panAccelY;

    // Middle mouse drag
    private bool _dragging;
    private int _dragStartMouseX;
    private int _dragStartMouseY;
    private float _dragStartCamX;
    private float _dragStartCamY;

    // Edge scroll zone size in pixels
    private const int EdgeScrollZone = 20;

    // Pan speed in pixels per second at 1x zoom
    private const float BasePanSpeed = 600f;

    // Acceleration ramp duration in seconds
    private const float AccelDuration = 0.2f;

    // World bounds for elastic bounce
    private float _worldMinX;
    private float _worldMinY;
    private float _worldMaxX;
    private float _worldMaxY;
    private bool _boundsInitialized;

    // Elastic bounce parameters
    private const float BounceStiffness = 8f;  // Spring constant for pulling back
    private const float BounceOvershoot = 100f; // Max pixels of allowed overshoot before hard clamp

    // Viewport dimensions (updated by renderer)
    private int _viewportWidth;
    private int _viewportHeight;

    // Cached frustum bounds (world-pixel space, updated each frame)
    private FrustumRect _frustum;

    public float X => _x;
    public float Y => _y;
    public int ZoomLevel => _zoomLevel;
    public float Zoom => _zoomLevel; // Integer zoom (for compatibility)
    public float SmoothZoom => _smoothZoom; // Interpolated zoom (for rendering)
    public int Rotation => _rotation;
    public int ViewportWidth => _viewportWidth;
    public int ViewportHeight => _viewportHeight;
    public int TileWidth => _config.TileWidth;
    public int TileHeight => _config.TileHeight;

    /// <summary>
    /// Axis-aligned bounding box in world-pixel space representing the visible area.
    /// </summary>
    public readonly struct FrustumRect
    {
        public readonly float MinX;
        public readonly float MinY;
        public readonly float MaxX;
        public readonly float MaxY;

        public FrustumRect(float minX, float minY, float maxX, float maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public float Width => MaxX - MinX;
        public float Height => MaxY - MinY;

        public bool Contains(float px, float py) =>
            px >= MinX && px <= MaxX && py >= MinY && py <= MaxY;

        public bool Intersects(float rectMinX, float rectMinY, float rectMaxX, float rectMaxY) =>
            MaxX >= rectMinX && MinX <= rectMaxX && MaxY >= rectMinY && MinY <= rectMaxY;
    }

    public IsometricCamera(Config config)
    {
        _config = config;
        _viewportWidth = config.WindowWidth;
        _viewportHeight = config.WindowHeight;

        // Center on world
        int centerTile = config.WorldSize / 2;
        var (sx, sy) = IsometricMath.GridToScreen(centerTile, centerTile, config.TileWidth, config.TileHeight, 0);
        _x = sx;
        _y = sy;

        // Calculate world bounds from tile grid extremes
        ComputeWorldBounds();
    }

    public void SetViewport(int width, int height)
    {
        _viewportWidth = width;
        _viewportHeight = height;
    }

    /// <summary>
    /// Update camera based on input state. Call once per frame.
    /// Handles smooth zoom interpolation, elastic world bounds, and frustum calculation.
    /// </summary>
    public void Update(float dt, bool left, bool right, bool up, bool down,
                       int mouseX, int mouseY, bool middleMouseDown,
                       int mouseRelX, int mouseRelY, int scrollDelta,
                       bool rotateLeft, bool rotateRight)
    {
        // --- Rotation (Q/E, 90-degree snap) ---
        if (rotateLeft)
        {
            _rotation = (_rotation + 3) % 4; // counter-clockwise
            ComputeWorldBounds();
        }
        if (rotateRight)
        {
            _rotation = (_rotation + 1) % 4; // clockwise
            ComputeWorldBounds();
        }

        // --- Zoom (scroll wheel, target is integer 1-8, rendered as smooth float) ---
        if (scrollDelta != 0)
        {
            int newZoom = _zoomLevel + scrollDelta;
            _zoomLevel = System.Math.Clamp(newZoom, 1, 8);
        }

        // Smooth zoom interpolation (spring-like lerp toward target)
        float zoomTarget = _zoomLevel;
        if (MathF.Abs(_smoothZoom - zoomTarget) > 0.001f)
        {
            _smoothZoom = Lerp(_smoothZoom, zoomTarget, 1f - MathF.Exp(-ZoomLerpSpeed * dt));
        }
        else
        {
            _smoothZoom = zoomTarget;
        }

        // --- Middle mouse drag ---
        if (middleMouseDown && !_dragging)
        {
            _dragging = true;
            _dragStartMouseX = mouseX;
            _dragStartMouseY = mouseY;
            _dragStartCamX = _x;
            _dragStartCamY = _y;
        }
        else if (!middleMouseDown)
        {
            _dragging = false;
        }

        if (_dragging)
        {
            float invZoom = 1f / _smoothZoom;
            _x = _dragStartCamX - (mouseX - _dragStartMouseX) * invZoom;
            _y = _dragStartCamY - (mouseY - _dragStartMouseY) * invZoom;
            ApplyWorldBounds(dt);
            UpdateFrustum();
            return; // Drag overrides keyboard/edge pan
        }

        // --- Keyboard + edge scroll pan with ease-in-quad acceleration ---
        float inputX = 0f;
        float inputY = 0f;

        // Keyboard input
        if (left) inputX -= 1f;
        if (right) inputX += 1f;
        if (up) inputY -= 1f;
        if (down) inputY += 1f;

        // Edge scroll (additive)
        if (mouseX >= 0 && mouseY >= 0 && mouseX < _viewportWidth && mouseY < _viewportHeight)
        {
            if (mouseX < EdgeScrollZone) inputX -= 1f;
            if (mouseX > _viewportWidth - EdgeScrollZone) inputX += 1f;
            if (mouseY < EdgeScrollZone) inputY -= 1f;
            if (mouseY > _viewportHeight - EdgeScrollZone) inputY += 1f;
        }

        // Clamp combined input to unit length
        inputX = System.Math.Clamp(inputX, -1f, 1f);
        inputY = System.Math.Clamp(inputY, -1f, 1f);

        // Acceleration ramp (ease-in-quad over 200ms)
        _panAccelX = UpdateAccel(_panAccelX, inputX != 0, dt);
        _panAccelY = UpdateAccel(_panAccelY, inputY != 0, dt);

        // Apply ease-in-quad curve: t^2
        float curveX = EaseInQuad(_panAccelX);
        float curveY = EaseInQuad(_panAccelY);

        // Speed scales inversely with zoom (zoomed in = slower pan in world space)
        float speed = BasePanSpeed / _smoothZoom;

        _panVelX = inputX * curveX * speed;
        _panVelY = inputY * curveY * speed;

        _x += _panVelX * dt;
        _y += _panVelY * dt;

        // Apply elastic world bounds
        ApplyWorldBounds(dt);

        // Update frustum cache
        UpdateFrustum();
    }

    /// <summary>
    /// Ramp acceleration from 0 to 1 over AccelDuration when active,
    /// instant stop (reset to 0) on release.
    /// </summary>
    private static float UpdateAccel(float current, bool active, float dt)
    {
        if (!active)
            return 0f; // Instant stop on release

        float next = current + dt / AccelDuration;
        return System.Math.Min(next, 1f);
    }

    /// <summary>Quadratic ease-in: f(t) = t^2</summary>
    private static float EaseInQuad(float t) => t * t;

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>
    /// Get the screen-space offset for rendering. Tiles at world position (wx, wy)
    /// should be drawn at screen position: GridToScreen(wx, wy) * zoom + offset.
    /// Uses the smooth zoom for sub-pixel-accurate rendering during transitions.
    /// </summary>
    public (float offsetX, float offsetY) GetRenderOffset()
    {
        float cx = _viewportWidth * 0.5f;
        float cy = _viewportHeight * 0.5f;
        return (cx - _x * _smoothZoom, cy - _y * _smoothZoom);
    }

    /// <summary>
    /// Convert a screen position to world grid coordinates, accounting for
    /// camera pan, zoom, and rotation.
    /// </summary>
    public (int gridX, int gridY) ScreenToGrid(int screenX, int screenY)
    {
        float invZoom = 1f / _smoothZoom;
        float worldX = (screenX - _viewportWidth * 0.5f) * invZoom + _x;
        float worldY = (screenY - _viewportHeight * 0.5f) * invZoom + _y;

        return IsometricMath.ScreenToGrid(worldX, worldY, _config.TileWidth, _config.TileHeight, _rotation);
    }

    /// <summary>
    /// Screen-to-world raycast for mouse picking. Returns the precise floating-point
    /// world coordinates (not snapped to grid) for sub-tile accuracy.
    /// Useful for placing buildings, drawing roads, and hover effects.
    /// </summary>
    /// <param name="screenX">Mouse X in screen pixels.</param>
    /// <param name="screenY">Mouse Y in screen pixels.</param>
    /// <returns>World-space coordinates in pixels and the corresponding grid cell.</returns>
    public (float worldX, float worldY, int gridX, int gridY) ScreenToWorldRaycast(int screenX, int screenY)
    {
        float invZoom = 1f / _smoothZoom;
        float worldX = (screenX - _viewportWidth * 0.5f) * invZoom + _x;
        float worldY = (screenY - _viewportHeight * 0.5f) * invZoom + _y;

        var (gridX, gridY) = IsometricMath.ScreenToGrid(worldX, worldY,
            _config.TileWidth, _config.TileHeight, _rotation);

        return (worldX, worldY, gridX, gridY);
    }

    /// <summary>
    /// Get the fractional position within the hovered tile (0,0 = top corner, 1,1 = bottom corner).
    /// Useful for determining which half of a tile is hovered for road placement etc.
    /// </summary>
    public (float fracX, float fracY) GetTileFraction(int screenX, int screenY)
    {
        float invZoom = 1f / _smoothZoom;
        float worldX = (screenX - _viewportWidth * 0.5f) * invZoom + _x;
        float worldY = (screenY - _viewportHeight * 0.5f) * invZoom + _y;

        float halfW = _config.TileWidth * 0.5f;
        float halfH = _config.TileHeight * 0.5f;

        // Get fractional rotated grid position
        float rx = (worldX / halfW + worldY / halfH) * 0.5f;
        float ry = (worldY / halfH - worldX / halfW) * 0.5f;

        // Extract fractional part (position within the tile)
        float fracX = rx - MathF.Floor(rx);
        float fracY = ry - MathF.Floor(ry);

        return (fracX, fracY);
    }

    /// <summary>
    /// Get the visible frustum in world-pixel space. Used by chunk renderer for culling.
    /// </summary>
    public FrustumRect GetFrustumBounds()
    {
        return _frustum;
    }

    /// <summary>
    /// Check if a world-space AABB is visible in the current frustum.
    /// </summary>
    public bool IsVisible(float minX, float minY, float maxX, float maxY)
    {
        return _frustum.Intersects(minX, minY, maxX, maxY);
    }

    /// <summary>
    /// Compute world bounds from the tile grid for the current rotation.
    /// The bounds encompass all tile screen positions plus padding.
    /// </summary>
    private void ComputeWorldBounds()
    {
        int ws = _config.WorldSize;
        int tw = _config.TileWidth;
        int th = _config.TileHeight;
        int rot = _rotation;

        // Check all 4 corners of the world grid to find screen-space extents
        var corners = new (int gx, int gy)[]
        {
            (0, 0),
            (ws - 1, 0),
            (0, ws - 1),
            (ws - 1, ws - 1)
        };

        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        foreach (var (gx, gy) in corners)
        {
            var (sx, sy) = IsometricMath.GridToScreen(gx, gy, tw, th, rot);
            if (sx < minX) minX = sx;
            if (sy < minY) minY = sy;
            if (sx > maxX) maxX = sx;
            if (sy > maxY) maxY = sy;
        }

        // Add tile-size padding
        float padX = tw * 2f;
        float padY = th * 2f;

        _worldMinX = minX - padX;
        _worldMinY = minY - padY;
        _worldMaxX = maxX + padX;
        _worldMaxY = maxY + padY;
        _boundsInitialized = true;
    }

    /// <summary>
    /// Apply elastic world bounds. If the camera is past the boundary, pull it back
    /// with a spring force. Allows slight overshoot for a natural feel, with a hard
    /// clamp at the maximum overshoot distance.
    /// </summary>
    private void ApplyWorldBounds(float dt)
    {
        if (!_boundsInitialized) return;

        // Calculate how far we can see from center (in world pixels)
        float halfViewW = _viewportWidth * 0.5f / _smoothZoom;
        float halfViewH = _viewportHeight * 0.5f / _smoothZoom;

        // Effective bounds: the camera center should stay within bounds shrunk by half-viewport
        float effectiveMinX = _worldMinX + halfViewW;
        float effectiveMaxX = _worldMaxX - halfViewW;
        float effectiveMinY = _worldMinY + halfViewH;
        float effectiveMaxY = _worldMaxY - halfViewH;

        // If the world is smaller than the viewport, center it
        if (effectiveMinX > effectiveMaxX)
        {
            float center = (_worldMinX + _worldMaxX) * 0.5f;
            effectiveMinX = center;
            effectiveMaxX = center;
        }
        if (effectiveMinY > effectiveMaxY)
        {
            float center = (_worldMinY + _worldMaxY) * 0.5f;
            effectiveMinY = center;
            effectiveMaxY = center;
        }

        // Spring force for X
        if (_x < effectiveMinX)
        {
            float overshoot = effectiveMinX - _x;
            _x += overshoot * BounceStiffness * dt;
            if (_x < effectiveMinX - BounceOvershoot)
                _x = effectiveMinX - BounceOvershoot;
        }
        else if (_x > effectiveMaxX)
        {
            float overshoot = _x - effectiveMaxX;
            _x -= overshoot * BounceStiffness * dt;
            if (_x > effectiveMaxX + BounceOvershoot)
                _x = effectiveMaxX + BounceOvershoot;
        }

        // Spring force for Y
        if (_y < effectiveMinY)
        {
            float overshoot = effectiveMinY - _y;
            _y += overshoot * BounceStiffness * dt;
            if (_y < effectiveMinY - BounceOvershoot)
                _y = effectiveMinY - BounceOvershoot;
        }
        else if (_y > effectiveMaxY)
        {
            float overshoot = _y - effectiveMaxY;
            _y -= overshoot * BounceStiffness * dt;
            if (_y > effectiveMaxY + BounceOvershoot)
                _y = effectiveMaxY + BounceOvershoot;
        }
    }

    /// <summary>
    /// Update the cached frustum rectangle from current camera state.
    /// </summary>
    private void UpdateFrustum()
    {
        float invZoom = 1f / _smoothZoom;
        float halfW = _viewportWidth * 0.5f * invZoom;
        float halfH = _viewportHeight * 0.5f * invZoom;

        _frustum = new FrustumRect(
            _x - halfW,
            _y - halfH,
            _x + halfW,
            _y + halfH
        );
    }

    /// <summary>
    /// Force an immediate frustum update without running the full Update() cycle.
    /// Useful after programmatic camera moves (e.g., centering on a building).
    /// </summary>
    public void RefreshFrustum()
    {
        UpdateFrustum();
    }

    /// <summary>
    /// Smoothly move the camera to center on a specific grid tile.
    /// The camera will lerp toward the target in subsequent Update() calls.
    /// For instant teleport, call this then set _x/_y directly via CenterOnWorld().
    /// </summary>
    public void CenterOnTile(int gridX, int gridY)
    {
        var (sx, sy) = IsometricMath.GridToScreen(gridX, gridY,
            _config.TileWidth, _config.TileHeight, _rotation);
        _x = sx;
        _y = sy;
        UpdateFrustum();
    }

    /// <summary>
    /// Center the camera on the world center tile.
    /// </summary>
    public void CenterOnWorld()
    {
        int center = _config.WorldSize / 2;
        CenterOnTile(center, center);
    }
}
