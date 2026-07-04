namespace Forge.Engine.Math;

/// <summary>
/// Grid-to-screen and screen-to-grid coordinate conversion for isometric tiles (default 128x64).
/// Supports all 4 rotation orientations (0 = default, 1 = 90 CW, 2 = 180, 3 = 270 CW).
///
/// Isometric projection (standard 2:1 diamond):
///   screenX = (gridX - gridY) * (tileWidth / 2)
///   screenY = (gridX + gridY) * (tileHeight / 2)
///
/// Rotation transforms the grid coordinates before projection:
///   rot 0: (gx, gy)         -- default orientation
///   rot 1: (gy, maxG - gx)  -- 90 degrees clockwise
///   rot 2: (maxG - gx, maxG - gy) -- 180 degrees
///   rot 3: (maxG - gy, gx)  -- 270 degrees clockwise (90 CCW)
/// </summary>
public static class IsometricMath
{
    /// <summary>
    /// Convert grid coordinates to screen pixel position (top-left of the tile diamond).
    /// </summary>
    /// <param name="gridX">Grid X coordinate.</param>
    /// <param name="gridY">Grid Y coordinate.</param>
    /// <param name="tileWidth">Tile width in pixels (default 64).</param>
    /// <param name="tileHeight">Tile height in pixels (default 32).</param>
    /// <param name="rotation">Rotation index 0-3 (90-degree increments clockwise).</param>
    /// <returns>Screen (x, y) position in pixels.</returns>
    public static (float screenX, float screenY) GridToScreen(
        int gridX, int gridY, int tileWidth = 128, int tileHeight = 64, int rotation = 0)
    {
        // Apply rotation to grid coordinates
        var (rx, ry) = RotateGrid(gridX, gridY, rotation);

        float halfW = tileWidth * 0.5f;
        float halfH = tileHeight * 0.5f;

        float screenX = (rx - ry) * halfW;
        float screenY = (rx + ry) * halfH;

        return (screenX, screenY);
    }

    /// <summary>
    /// Convert screen pixel position to grid coordinates.
    /// Returns the grid cell that contains the given screen point.
    /// </summary>
    /// <param name="screenX">Screen X in pixels.</param>
    /// <param name="screenY">Screen Y in pixels.</param>
    /// <param name="tileWidth">Tile width in pixels (default 64).</param>
    /// <param name="tileHeight">Tile height in pixels (default 32).</param>
    /// <param name="rotation">Rotation index 0-3.</param>
    /// <returns>Grid (x, y) coordinates (may be negative or out of bounds).</returns>
    public static (int gridX, int gridY) ScreenToGrid(
        float screenX, float screenY, int tileWidth = 128, int tileHeight = 64, int rotation = 0)
    {
        float halfW = tileWidth * 0.5f;
        float halfH = tileHeight * 0.5f;

        // Inverse of the isometric projection:
        //   rx = (screenX / halfW + screenY / halfH) / 2
        //   ry = (screenY / halfH - screenX / halfW) / 2
        float rx = (screenX / halfW + screenY / halfH) * 0.5f;
        float ry = (screenY / halfH - screenX / halfW) * 0.5f;

        // Round to nearest grid cell
        int gridXRotated = (int)MathF.Floor(rx + 0.5f);
        int gridYRotated = (int)MathF.Floor(ry + 0.5f);

        // Reverse rotation
        return UnrotateGrid(gridXRotated, gridYRotated, rotation);
    }

    /// <summary>
    /// Apply rotation transform to grid coordinates.
    /// </summary>
    public static (int rx, int ry) RotateGrid(int gx, int gy, int rotation)
    {
        return (rotation & 3) switch
        {
            0 => (gx, gy),
            1 => (gy, -gx),
            2 => (-gx, -gy),
            3 => (-gy, gx),
            _ => (gx, gy),
        };
    }

    /// <summary>
    /// Reverse rotation transform (inverse of RotateGrid).
    /// </summary>
    public static (int gx, int gy) UnrotateGrid(int rx, int ry, int rotation)
    {
        // Inverse rotation: rotating back by (4 - rotation) mod 4
        return (rotation & 3) switch
        {
            0 => (rx, ry),
            1 => (-ry, rx),
            2 => (-rx, -ry),
            3 => (ry, -rx),
            _ => (rx, ry),
        };
    }

    /// <summary>
    /// Get the screen-space bounding box of a tile (the diamond shape).
    /// Returns (left, top, width, height) of the axis-aligned bounding box.
    /// </summary>
    public static (float left, float top, float width, float height) GetTileBounds(
        int gridX, int gridY, int tileWidth = 128, int tileHeight = 64, int rotation = 0)
    {
        var (sx, sy) = GridToScreen(gridX, gridY, tileWidth, tileHeight, rotation);
        float halfW = tileWidth * 0.5f;

        return (sx - halfW, sy, tileWidth, tileHeight);
    }

    /// <summary>
    /// Manhattan distance on the isometric grid (useful for range checks).
    /// </summary>
    public static int ManhattanDistance(int x1, int y1, int x2, int y2) =>
        System.Math.Abs(x1 - x2) + System.Math.Abs(y1 - y2);

    /// <summary>
    /// Chebyshev (chess-king) distance on the grid.
    /// </summary>
    public static int ChebyshevDistance(int x1, int y1, int x2, int y2) =>
        System.Math.Max(System.Math.Abs(x1 - x2), System.Math.Abs(y1 - y2));

    /// <summary>
    /// Get the 4 cardinal neighbors of a grid cell.
    /// </summary>
    public static (int x, int y)[] GetCardinalNeighbors(int x, int y) =>
    [
        (x, y - 1),  // North
        (x + 1, y),  // East
        (x, y + 1),  // South
        (x - 1, y),  // West
    ];

    /// <summary>
    /// Get all 8 neighbors (cardinal + diagonal) of a grid cell.
    /// </summary>
    public static (int x, int y)[] GetAllNeighbors(int x, int y) =>
    [
        (x, y - 1),      // N
        (x + 1, y - 1),  // NE
        (x + 1, y),      // E
        (x + 1, y + 1),  // SE
        (x, y + 1),      // S
        (x - 1, y + 1),  // SW
        (x - 1, y),      // W
        (x - 1, y - 1),  // NW
    ];
}
