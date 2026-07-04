using Forge.Engine.Simulation;

namespace Forge.Game.Tools;

/// <summary>
/// Road placement tool: click-drag to draw road segments.
/// </summary>
public sealed class RoadTool
{
    private bool _drawing;
    private int _startX, _startY;

    public void OnMouseDown(int gridX, int gridY)
    {
        _drawing = true;
        _startX = gridX;
        _startY = gridY;
    }

    public void OnMouseUp(int gridX, int gridY, CommandQueue commands)
    {
        if (!_drawing) return;
        _drawing = false;
        commands.EnqueuePlaceRoad(_startX, _startY, gridX, gridY);
    }

    public void Cancel()
    {
        _drawing = false;
    }
}
