using Forge.Engine.Simulation;

namespace Forge.Game.Tools;

/// <summary>
/// Building placement tool: select a building type, click to place.
/// Shows a ghost preview at the cursor position.
/// </summary>
public sealed class BuildingTool
{
    private ushort _buildingType;
    private bool _active;

    public ushort BuildingType { get => _buildingType; set => _buildingType = value; }
    public bool IsActive => _active;

    public void Activate(ushort buildingType)
    {
        _buildingType = buildingType;
        _active = true;
    }

    public void OnClick(int gridX, int gridY, CommandQueue commands)
    {
        if (!_active) return;
        commands.EnqueuePlaceBuilding(gridX, gridY, _buildingType);
    }

    public void Cancel()
    {
        _active = false;
    }
}
