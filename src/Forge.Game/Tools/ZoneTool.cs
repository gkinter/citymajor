using Forge.Engine.Simulation;

namespace Forge.Game.Tools;

/// <summary>
/// Zone painting tool: click-drag to paint residential/commercial/industrial zones.
/// </summary>
public sealed class ZoneTool
{
    private bool _painting;
    private int _startX, _startY;
    private ushort _zoneType = 1; // Default: residential_low

    public ushort ZoneType { get => _zoneType; set => _zoneType = value; }

    public void OnMouseDown(int gridX, int gridY)
    {
        _painting = true;
        _startX = gridX;
        _startY = gridY;
    }

    public void OnMouseUp(int gridX, int gridY, CommandQueue commands)
    {
        if (!_painting) return;
        _painting = false;

        int x = System.Math.Min(_startX, gridX);
        int y = System.Math.Min(_startY, gridY);
        int w = System.Math.Abs(gridX - _startX) + 1;
        int h = System.Math.Abs(gridY - _startY) + 1;

        commands.EnqueuePlaceZone(x, y, w, h, _zoneType);
    }

    public void Cancel()
    {
        _painting = false;
    }
}
