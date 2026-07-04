namespace Forge.Engine.Input;

/// <summary>
/// Manages cursor state, grid snapping, and cursor icon for different tools.
/// </summary>
public sealed class CursorManager
{
    public enum CursorMode
    {
        Default,
        Crosshair,
        Bulldoze,
        Place,
        Drag
    }

    private CursorMode _mode = CursorMode.Default;
    private int _gridX;
    private int _gridY;
    private bool _onGrid;

    public CursorMode Mode
    {
        get => _mode;
        set => _mode = value;
    }

    /// <summary>Current grid cell the cursor is hovering over.</summary>
    public int GridX => _gridX;
    public int GridY => _gridY;

    /// <summary>Whether the cursor is currently over valid grid space.</summary>
    public bool OnGrid => _onGrid;

    /// <summary>
    /// Update cursor grid position from screen coordinates via camera.
    /// </summary>
    public void Update(int screenX, int screenY, Rendering.IsometricCamera camera, int worldSize)
    {
        var (gx, gy) = camera.ScreenToGrid(screenX, screenY);

        _onGrid = gx >= 0 && gx < worldSize && gy >= 0 && gy < worldSize;
        _gridX = System.Math.Clamp(gx, 0, worldSize - 1);
        _gridY = System.Math.Clamp(gy, 0, worldSize - 1);
    }
}
