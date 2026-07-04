using Forge.Engine.Data;
using Forge.Engine.Rendering;
using Forge.Engine.Simulation;
using SDL2;

namespace Forge.Engine.Input;

/// <summary>
/// Represents a single undoable action capturing tile state before and after a tool operation.
/// </summary>
public struct UndoAction
{
    public string Description;
    public (int X, int Y, byte OldTerrain, byte OldZone, byte OldRoad, ushort OldBuilding)[] TilesBefore;
    public (int X, int Y, byte NewTerrain, byte NewZone, byte NewRoad, ushort NewBuilding)[] TilesAfter;
    public int CostRefund;
}

/// <summary>
/// Undo/Redo stack with a configurable maximum depth.
/// Supports push, undo, redo, and clear operations.
/// </summary>
public sealed class UndoStack
{
    private readonly UndoAction[] _actions;
    private int _head;      // Index of the next write position (circular)
    private int _undoCount; // Number of actions that can be undone
    private int _redoCount; // Number of actions that can be redone

    public int Count => _undoCount;
    public int RedoCount => _redoCount;
    public int MaxSize { get; }
    public bool CanUndo => _undoCount > 0;
    public bool CanRedo => _redoCount > 0;

    public UndoStack(int maxSize = 50)
    {
        MaxSize = maxSize;
        _actions = new UndoAction[maxSize];
    }

    /// <summary>
    /// Push a new action onto the stack. Clears any redo history.
    /// </summary>
    public void Push(UndoAction action)
    {
        _actions[_head] = action;
        _head = (_head + 1) % MaxSize;

        if (_undoCount < MaxSize)
            _undoCount++;

        // Pushing a new action invalidates redo history
        _redoCount = 0;
    }

    /// <summary>
    /// Undo the most recent action. Returns the action, or null if nothing to undo.
    /// </summary>
    public UndoAction? Undo()
    {
        if (_undoCount == 0)
            return null;

        // Move head back to the most recent action
        _head = (_head - 1 + MaxSize) % MaxSize;
        _undoCount--;
        _redoCount++;

        return _actions[_head];
    }

    /// <summary>
    /// Redo the most recently undone action. Returns the action, or null if nothing to redo.
    /// </summary>
    public UndoAction? Redo()
    {
        if (_redoCount == 0)
            return null;

        var action = _actions[_head];
        _head = (_head + 1) % MaxSize;
        _undoCount++;
        _redoCount--;

        return action;
    }

    /// <summary>
    /// Clear all undo and redo history.
    /// </summary>
    public void Clear()
    {
        _undoCount = 0;
        _redoCount = 0;
        _head = 0;
        Array.Clear(_actions);
    }
}

/// <summary>
/// Interface for building tools (zone painter, road builder, bulldozer, etc.).
/// Each tool implements hover preview, click/drag placement, and ghost rendering.
/// </summary>
public interface ITool
{
    string Name { get; }
    string Icon { get; } // sprite name for toolbar

    void OnActivate();    // when player selects this tool
    void OnDeactivate();  // when switching to another tool

    void OnHover(int tileX, int tileY);      // mouse over tile
    void OnClick(int tileX, int tileY);       // left click
    void OnDragStart(int tileX, int tileY);   // mouse down
    void OnDrag(int tileX, int tileY);        // mouse held, moved
    void OnDragEnd(int tileX, int tileY);     // mouse up
    void OnCancel();                           // right click or Escape

    void Render(IsometricCamera camera, SpriteRenderer sprites); // draw ghost preview
    void RenderUI(); // ImGui for tool options

    // Validity
    bool IsValidPlacement(int tileX, int tileY);
    int GetCost(); // current estimated cost
}

/// <summary>
/// The tool state machine for building mode. Converts mouse input (screen coordinates)
/// to tile coordinates via the IsometricCamera, dispatches to the active tool,
/// and manages undo/redo history.
///
/// Ghost preview renders a semi-transparent sprite at the hovered tile:
/// green tint if valid placement, red tint if invalid.
/// </summary>
public sealed class ToolSystem
{
    private ITool? _activeTool;
    private readonly List<ITool> _registeredTools = new();
    private readonly Dictionary<string, ITool> _toolsByName = new();
    private readonly UndoStack _undoStack;

    // Drag state
    private bool _isDragging;
    private int _dragStartTileX;
    private int _dragStartTileY;
    private int _lastHoverTileX = -1;
    private int _lastHoverTileY = -1;

    // Reference to tile data for undo/redo operations
    private TileData? _tiles;

    /// <summary>
    /// ChunkManager reference for marking dirty chunks after tool modifications.
    /// Set by the game before tools are used.
    /// </summary>
    public ChunkManager? Chunks { get; set; }

    /// <summary>
    /// CommandQueue reference for enqueuing commands to the simulation thread.
    /// Set by the game before tools are used.
    /// </summary>
    public CommandQueue? Commands { get; set; }

    public ITool? ActiveTool => _activeTool;
    public IReadOnlyList<ITool> RegisteredTools => _registeredTools;
    public bool CanUndo => _undoStack.CanUndo;
    public bool CanRedo => _undoStack.CanRedo;
    public int UndoCount => _undoStack.Count;
    public int RedoCount => _undoStack.RedoCount;

    public ToolSystem(int undoStackSize = 50)
    {
        _undoStack = new UndoStack(undoStackSize);
    }

    /// <summary>
    /// Register a tool for use. Tools are accessible by name and index.
    /// </summary>
    public void RegisterTool(ITool tool)
    {
        _registeredTools.Add(tool);
        _toolsByName[tool.Name] = tool;
    }

    /// <summary>
    /// Set the active tool by name. Deactivates the current tool first.
    /// </summary>
    public void SetActiveTool(string name)
    {
        if (_toolsByName.TryGetValue(name, out var tool))
        {
            SetActiveToolInternal(tool);
        }
    }

    /// <summary>
    /// Set the active tool by index in the registered tools list.
    /// </summary>
    public void SetActiveTool(int index)
    {
        if (index >= 0 && index < _registeredTools.Count)
        {
            SetActiveToolInternal(_registeredTools[index]);
        }
    }

    /// <summary>
    /// Clear the active tool, returning to pointer/inspect mode.
    /// </summary>
    public void ClearTool()
    {
        if (_activeTool != null)
        {
            Console.WriteLine($"[Tool] Deactivated: {_activeTool.Name}");
            CancelDrag();
            _activeTool.OnDeactivate();
            _activeTool = null;
        }
    }

    /// <summary>
    /// Process input each frame. Converts screen coordinates to tile coordinates
    /// via the camera, then dispatches hover/click/drag events to the active tool.
    /// Also handles undo/redo keyboard shortcuts (Ctrl+Z, Ctrl+Y).
    /// </summary>
    public void HandleInput(InputManager input, IsometricCamera camera, TileData tiles)
    {
        _tiles = tiles;

        // Undo/Redo shortcuts
        bool ctrl = input.IsKeyHeld(SDL.SDL_Scancode.SDL_SCANCODE_LCTRL) ||
                     input.IsKeyHeld(SDL.SDL_Scancode.SDL_SCANCODE_RCTRL);

        if (ctrl && input.IsKeyPressed(SDL.SDL_Scancode.SDL_SCANCODE_Z))
        {
            Undo();
            return;
        }
        if (ctrl && input.IsKeyPressed(SDL.SDL_Scancode.SDL_SCANCODE_Y))
        {
            Redo();
            return;
        }

        if (_activeTool == null)
            return;

        // Convert screen mouse position to tile grid coordinates
        var (gridX, gridY) = camera.ScreenToGrid(input.MouseX, input.MouseY);
        bool onGrid = gridX >= 0 && gridX < tiles.Size && gridY >= 0 && gridY < tiles.Size;

        // Debug: log when a click reaches the tool system
        if (input.IsLeftMousePressed)
        {
            Console.WriteLine($"[Tool] Click at tile ({gridX}, {gridY}), tool active: {_activeTool.Name}, onGrid: {onGrid}, mouse: ({input.MouseX}, {input.MouseY})");
        }

        // Cancel on right click or Escape
        if (input.IsRightMousePressed || input.IsKeyPressed(SDL.SDL_Scancode.SDL_SCANCODE_ESCAPE))
        {
            if (_isDragging)
            {
                // If dragging, cancel the drag first
                CancelDrag();
            }
            else
            {
                // If not dragging, deselect the tool entirely
                ClearTool();
            }
            return;
        }

        // Hover (always fire if position changed)
        if (onGrid && (gridX != _lastHoverTileX || gridY != _lastHoverTileY))
        {
            _lastHoverTileX = gridX;
            _lastHoverTileY = gridY;
            _activeTool.OnHover(gridX, gridY);
        }

        // Drag handling
        bool leftDown = input.IsLeftMouseHeld;
        bool leftPressed = input.IsLeftMousePressed;
        bool leftReleased = input.IsMouseReleased((byte)SDL.SDL_BUTTON_LEFT);

        if (leftPressed && onGrid)
        {
            // Click and potentially start drag
            _activeTool.OnClick(gridX, gridY);
            _isDragging = true;
            _dragStartTileX = gridX;
            _dragStartTileY = gridY;
            _activeTool.OnDragStart(gridX, gridY);
        }
        else if (_isDragging && leftDown && onGrid)
        {
            // Continue drag
            _activeTool.OnDrag(gridX, gridY);
        }
        else if (_isDragging && leftReleased)
        {
            // End drag
            if (onGrid)
            {
                _activeTool.OnDragEnd(gridX, gridY);
            }
            else
            {
                _activeTool.OnDragEnd(_dragStartTileX, _dragStartTileY);
            }
            _isDragging = false;
        }
    }

    /// <summary>
    /// Render the active tool's ghost preview overlay.
    /// </summary>
    public void Render(IsometricCamera camera, SpriteRenderer sprites)
    {
        _activeTool?.Render(camera, sprites);
    }

    /// <summary>
    /// Render the active tool's ImGui options panel.
    /// </summary>
    public void RenderUI()
    {
        _activeTool?.RenderUI();
    }

    /// <summary>
    /// Undo the last tool action by restoring tile state from the undo stack.
    /// </summary>
    public void Undo()
    {
        if (_tiles == null) return;

        var action = _undoStack.Undo();
        if (action == null) return;

        // Restore tile state to "before" values
        var before = action.Value.TilesBefore;
        for (int i = 0; i < before.Length; i++)
        {
            ref var tile = ref before[i];
            if (!_tiles.InBounds(tile.X, tile.Y)) continue;

            int idx = _tiles.Index(tile.X, tile.Y);
            _tiles.TerrainType[idx] = tile.OldTerrain;
            _tiles.ZoneType[idx] = tile.OldZone;
            _tiles.RoadFlags[idx] = tile.OldRoad;
            _tiles.BuildingId[idx] = tile.OldBuilding;
        }
    }

    /// <summary>
    /// Redo the last undone tool action by re-applying the "after" tile state.
    /// </summary>
    public void Redo()
    {
        if (_tiles == null) return;

        var action = _undoStack.Redo();
        if (action == null) return;

        // Apply tile state to "after" values
        var after = action.Value.TilesAfter;
        for (int i = 0; i < after.Length; i++)
        {
            ref var tile = ref after[i];
            if (!_tiles.InBounds(tile.X, tile.Y)) continue;

            int idx = _tiles.Index(tile.X, tile.Y);
            _tiles.TerrainType[idx] = tile.NewTerrain;
            _tiles.ZoneType[idx] = tile.NewZone;
            _tiles.RoadFlags[idx] = tile.NewRoad;
            _tiles.BuildingId[idx] = tile.NewBuilding;
        }
    }

    /// <summary>
    /// Push an undo action onto the stack. Called by tool implementations after
    /// completing a placement or modification.
    /// </summary>
    public void PushUndo(UndoAction action)
    {
        _undoStack.Push(action);
    }

    /// <summary>
    /// Capture the current state of a set of tiles for use in an UndoAction.
    /// Returns an array of tile state tuples suitable for the TilesBefore field.
    /// </summary>
    public (int X, int Y, byte OldTerrain, byte OldZone, byte OldRoad, ushort OldBuilding)[]
        CaptureTileState(TileData tiles, int startX, int startY, int width, int height)
    {
        int x1 = System.Math.Max(0, startX);
        int y1 = System.Math.Max(0, startY);
        int x2 = System.Math.Min(tiles.Size, startX + width);
        int y2 = System.Math.Min(tiles.Size, startY + height);

        int count = (x2 - x1) * (y2 - y1);
        var result = new (int, int, byte, byte, byte, ushort)[count];
        int idx = 0;

        for (int y = y1; y < y2; y++)
        {
            for (int x = x1; x < x2; x++)
            {
                int ti = tiles.Index(x, y);
                result[idx++] = (x, y,
                    tiles.TerrainType[ti],
                    tiles.ZoneType[ti],
                    tiles.RoadFlags[ti],
                    tiles.BuildingId[ti]);
            }
        }

        return result;
    }

    /// <summary>
    /// Clear the undo/redo history.
    /// </summary>
    public void ClearHistory()
    {
        _undoStack.Clear();
    }

    private void SetActiveToolInternal(ITool tool)
    {
        if (_activeTool == tool) return;

        if (_activeTool != null)
        {
            CancelDrag();
            _activeTool.OnDeactivate();
        }

        _activeTool = tool;
        _activeTool.OnActivate();
        Console.WriteLine($"[Tool] Activated: {tool.Name}");
    }

    private void CancelDrag()
    {
        if (_isDragging)
        {
            _isDragging = false;
            _activeTool?.OnCancel();
        }
    }
}

/// <summary>
/// Zone painting tool. Paints residential, commercial, industrial, etc. zones
/// via click or drag-rectangle. Renders a green/red ghost overlay on hovered tiles.
/// </summary>
public sealed class ZoneTool : ITool
{
    private readonly ToolSystem _toolSystem;
    private readonly TileData _tiles;
    private byte _zoneType;
    private byte _density;
    private string _zoneName;

    // Drag rectangle state
    private bool _dragging;
    private int _dragStartX, _dragStartY;
    private int _dragEndX, _dragEndY;

    // Hover state
    private int _hoverX, _hoverY;
    private bool _hovering;

    // Cost per tile
    private int _costPerTile;
    private int _currentCost;

    public string Name { get; }
    public string Icon { get; }

    public ZoneTool(ToolSystem toolSystem, TileData tiles, string name, string icon,
                    byte zoneType, byte density, int costPerTile)
    {
        _toolSystem = toolSystem;
        _tiles = tiles;
        Name = name;
        Icon = icon;
        _zoneType = zoneType;
        _density = density;
        _costPerTile = costPerTile;
        _zoneName = name;
    }

    public void OnActivate() { _dragging = false; _hovering = false; }
    public void OnDeactivate() { _dragging = false; _hovering = false; }

    public void OnHover(int tileX, int tileY)
    {
        _hoverX = tileX;
        _hoverY = tileY;
        _hovering = true;

        if (_dragging)
        {
            _dragEndX = tileX;
            _dragEndY = tileY;
            _currentCost = CalculateDragCost();
        }
        else
        {
            _currentCost = _tiles.IsBuildable(tileX, tileY) ? _costPerTile : 0;
        }
    }

    public void OnClick(int tileX, int tileY)
    {
        // Single tile zone placement is handled by drag end
    }

    public void OnDragStart(int tileX, int tileY)
    {
        _dragging = true;
        _dragStartX = tileX;
        _dragStartY = tileY;
        _dragEndX = tileX;
        _dragEndY = tileY;
    }

    public void OnDrag(int tileX, int tileY)
    {
        _dragEndX = tileX;
        _dragEndY = tileY;
        _currentCost = CalculateDragCost();
    }

    public void OnDragEnd(int tileX, int tileY)
    {
        if (!_dragging) return;
        _dragging = false;

        _dragEndX = tileX;
        _dragEndY = tileY;

        // Compute the rectangle
        int x1 = System.Math.Min(_dragStartX, _dragEndX);
        int y1 = System.Math.Min(_dragStartY, _dragEndY);
        int x2 = System.Math.Max(_dragStartX, _dragEndX);
        int y2 = System.Math.Max(_dragStartY, _dragEndY);
        int w = x2 - x1 + 1;
        int h = y2 - y1 + 1;

        // Capture undo state
        var before = _toolSystem.CaptureTileState(_tiles, x1, y1, w, h);

        // Apply zones
        int count = _tiles.SetZoneRect(x1, y1, w, h, _zoneType, _density);

        Console.WriteLine($"[Tool] Modified zone ({x1},{y1})-({x2},{y2}): zone={_zoneType}, count={count}");

        if (count > 0)
        {
            // Capture after state
            var after = CaptureAfterState(x1, y1, w, h);

            _toolSystem.PushUndo(new UndoAction
            {
                Description = $"Zone {_zoneName} ({w}x{h})",
                TilesBefore = before,
                TilesAfter = after,
                CostRefund = count * _costPerTile
            });

            // Mark affected chunks dirty for re-rendering
            _toolSystem.Chunks?.MarkAreaDirty(x1, y1, w, h);

            // Enqueue command to sim thread
            _toolSystem.Commands?.EnqueuePlaceZone(x1, y1, w, h, _zoneType);
        }
    }

    public void OnCancel()
    {
        _dragging = false;
    }

    public void Render(IsometricCamera camera, SpriteRenderer sprites)
    {
        if (!_hovering && !_dragging) return;

        var (offsetX, offsetY) = camera.GetRenderOffset();
        float zoom = camera.SmoothZoom;
        int tw = camera.TileWidth;
        int th = camera.TileHeight;

        if (_dragging)
        {
            int x1 = System.Math.Min(_dragStartX, _dragEndX);
            int y1 = System.Math.Min(_dragStartY, _dragEndY);
            int x2 = System.Math.Max(_dragStartX, _dragEndX);
            int y2 = System.Math.Max(_dragStartY, _dragEndY);

            for (int y = y1; y <= y2; y++)
            {
                for (int x = x1; x <= x2; x++)
                {
                    RenderGhostTile(x, y, camera, sprites, tw, th, offsetX, offsetY, zoom);
                }
            }
        }
        else
        {
            RenderGhostTile(_hoverX, _hoverY, camera, sprites, tw, th, offsetX, offsetY, zoom);
        }
    }

    public void RenderUI()
    {
        // Zone tool options can be rendered via ImGui if needed
    }

    public bool IsValidPlacement(int tileX, int tileY)
    {
        return _tiles.IsBuildable(tileX, tileY);
    }

    public int GetCost() => _currentCost;

    private void RenderGhostTile(int gx, int gy, IsometricCamera camera, SpriteRenderer sprites,
                                  int tw, int th, float offsetX, float offsetY, float zoom)
    {
        var (sx, sy) = Forge.Engine.Math.IsometricMath.GridToScreen(gx, gy, tw, th, camera.Rotation);
        float drawX = sx * zoom + offsetX;
        float drawY = sy * zoom + offsetY;
        float drawW = tw * zoom;
        float drawH = th * zoom;

        bool valid = IsValidPlacement(gx, gy);
        float r = valid ? 0.2f : 0.8f;
        float g = valid ? 0.8f : 0.2f;
        float b = 0.2f;
        float a = 0.4f;

        sprites.Draw(drawX, drawY, drawW, drawH, 0f, 0f, 1f, 1f, r, g, b, a);
    }

    private int CalculateDragCost()
    {
        int x1 = System.Math.Min(_dragStartX, _dragEndX);
        int y1 = System.Math.Min(_dragStartY, _dragEndY);
        int x2 = System.Math.Max(_dragStartX, _dragEndX);
        int y2 = System.Math.Max(_dragStartY, _dragEndY);

        int cost = 0;
        for (int y = y1; y <= y2; y++)
        {
            for (int x = x1; x <= x2; x++)
            {
                if (_tiles.IsBuildable(x, y))
                    cost += _costPerTile;
            }
        }
        return cost;
    }

    private (int X, int Y, byte NewTerrain, byte NewZone, byte NewRoad, ushort NewBuilding)[]
        CaptureAfterState(int startX, int startY, int width, int height)
    {
        int x1 = System.Math.Max(0, startX);
        int y1 = System.Math.Max(0, startY);
        int x2 = System.Math.Min(_tiles.Size, startX + width);
        int y2 = System.Math.Min(_tiles.Size, startY + height);

        int count = (x2 - x1) * (y2 - y1);
        var result = new (int, int, byte, byte, byte, ushort)[count];
        int idx = 0;

        for (int y = y1; y < y2; y++)
        {
            for (int x = x1; x < x2; x++)
            {
                int ti = _tiles.Index(x, y);
                result[idx++] = (x, y,
                    _tiles.TerrainType[ti],
                    _tiles.ZoneType[ti],
                    _tiles.RoadFlags[ti],
                    _tiles.BuildingId[ti]);
            }
        }

        return result;
    }
}

/// <summary>
/// Bulldozer tool. Clears zones and buildings from tiles via click or drag.
/// </summary>
public sealed class BulldozeTool : ITool
{
    private readonly ToolSystem _toolSystem;
    private readonly TileData _tiles;

    private bool _dragging;
    private int _dragStartX, _dragStartY;
    private int _dragEndX, _dragEndY;
    private int _hoverX, _hoverY;
    private bool _hovering;
    private const int CostPerTile = 10;

    public string Name => "Bulldoze";
    public string Icon => "icon_bulldoze";

    public BulldozeTool(ToolSystem toolSystem, TileData tiles)
    {
        _toolSystem = toolSystem;
        _tiles = tiles;
    }

    public void OnActivate() { _dragging = false; _hovering = false; }
    public void OnDeactivate() { _dragging = false; _hovering = false; }

    public void OnHover(int tileX, int tileY)
    {
        _hoverX = tileX;
        _hoverY = tileY;
        _hovering = true;

        if (_dragging)
        {
            _dragEndX = tileX;
            _dragEndY = tileY;
        }
    }

    public void OnClick(int tileX, int tileY) { }

    public void OnDragStart(int tileX, int tileY)
    {
        _dragging = true;
        _dragStartX = tileX;
        _dragStartY = tileY;
        _dragEndX = tileX;
        _dragEndY = tileY;
    }

    public void OnDrag(int tileX, int tileY)
    {
        _dragEndX = tileX;
        _dragEndY = tileY;
    }

    public void OnDragEnd(int tileX, int tileY)
    {
        if (!_dragging) return;
        _dragging = false;

        _dragEndX = tileX;
        _dragEndY = tileY;

        int x1 = System.Math.Min(_dragStartX, _dragEndX);
        int y1 = System.Math.Min(_dragStartY, _dragEndY);
        int x2 = System.Math.Max(_dragStartX, _dragEndX);
        int y2 = System.Math.Max(_dragStartY, _dragEndY);
        int w = x2 - x1 + 1;
        int h = y2 - y1 + 1;

        var before = _toolSystem.CaptureTileState(_tiles, x1, y1, w, h);

        // Clear zones, roads, and buildings
        int cleared = 0;
        for (int y = y1; y <= y2; y++)
        {
            for (int x = x1; x <= x2; x++)
            {
                if (!_tiles.InBounds(x, y)) continue;
                int idx = _tiles.Index(x, y);

                bool hadContent = _tiles.ZoneType[idx] != 0
                    || _tiles.BuildingId[idx] != 0
                    || _tiles.RoadFlags[idx] != 0;

                _tiles.ZoneType[idx] = 0;
                _tiles.ZoneDensity[idx] = 0;
                _tiles.BuildingId[idx] = 0;
                _tiles.RoadFlags[idx] = 0;

                if (hadContent) cleared++;
            }
        }

        // Update road connections for neighbors at the edges of the bulldozed area
        for (int y = y1; y <= y2; y++)
        {
            for (int x = x1; x <= x2; x++)
            {
                UpdateNeighborConnectionsAfterRemoval(x, y);
            }
        }

        Console.WriteLine($"[Tool] Modified bulldoze ({x1},{y1})-({x2},{y2}): cleared={cleared}");

        if (cleared > 0)
        {
            var after = CaptureAfterState(x1, y1, w, h);
            _toolSystem.PushUndo(new UndoAction
            {
                Description = $"Bulldoze ({w}x{h})",
                TilesBefore = before,
                TilesAfter = after,
                CostRefund = cleared * CostPerTile
            });

            // Mark affected chunks dirty for re-rendering (include neighbors for road connections)
            _toolSystem.Chunks?.MarkAreaDirty(
                System.Math.Max(0, x1 - 1),
                System.Math.Max(0, y1 - 1),
                w + 2,
                h + 2);

            // Enqueue bulldoze command to sim thread
            _toolSystem.Commands?.EnqueueBulldoze(x1, y1, w, h);
        }
    }

    public void OnCancel()
    {
        _dragging = false;
    }

    public void Render(IsometricCamera camera, SpriteRenderer sprites)
    {
        if (!_hovering && !_dragging) return;

        var (offsetX, offsetY) = camera.GetRenderOffset();
        float zoom = camera.SmoothZoom;
        int tw = camera.TileWidth;
        int th = camera.TileHeight;

        if (_dragging)
        {
            int x1 = System.Math.Min(_dragStartX, _dragEndX);
            int y1 = System.Math.Min(_dragStartY, _dragEndY);
            int x2 = System.Math.Max(_dragStartX, _dragEndX);
            int y2 = System.Math.Max(_dragStartY, _dragEndY);

            for (int y = y1; y <= y2; y++)
            {
                for (int x = x1; x <= x2; x++)
                {
                    RenderGhost(x, y, camera, sprites, tw, th, offsetX, offsetY, zoom);
                }
            }
        }
        else
        {
            RenderGhost(_hoverX, _hoverY, camera, sprites, tw, th, offsetX, offsetY, zoom);
        }
    }

    public void RenderUI() { }

    public bool IsValidPlacement(int tileX, int tileY)
    {
        if (!_tiles.InBounds(tileX, tileY)) return false;
        int idx = _tiles.Index(tileX, tileY);
        return _tiles.ZoneType[idx] != 0 || _tiles.BuildingId[idx] != 0 || _tiles.RoadFlags[idx] != 0;
    }

    public int GetCost()
    {
        if (!_dragging) return IsValidPlacement(_hoverX, _hoverY) ? CostPerTile : 0;

        int x1 = System.Math.Min(_dragStartX, _dragEndX);
        int y1 = System.Math.Min(_dragStartY, _dragEndY);
        int x2 = System.Math.Max(_dragStartX, _dragEndX);
        int y2 = System.Math.Max(_dragStartY, _dragEndY);

        int cost = 0;
        for (int y = y1; y <= y2; y++)
            for (int x = x1; x <= x2; x++)
                if (IsValidPlacement(x, y))
                    cost += CostPerTile;
        return cost;
    }

    /// <summary>
    /// Remove connection flags from neighbors pointing toward a removed road tile.
    /// </summary>
    private void UpdateNeighborConnectionsAfterRemoval(int cx, int cy)
    {
        // Each neighbor that has a connection bit pointing at (cx,cy) should have it cleared
        (int nx, int ny, byte clearBit)[] neighbors =
        [
            (cx, cy - 1, 0x04), // North neighbor: clear its South connection
            (cx + 1, cy, 0x08), // East neighbor: clear its West connection
            (cx, cy + 1, 0x01), // South neighbor: clear its North connection
            (cx - 1, cy, 0x02), // West neighbor: clear its East connection
        ];

        foreach (var (nx, ny, bit) in neighbors)
        {
            if (!_tiles.InBounds(nx, ny)) continue;
            int idx = _tiles.Index(nx, ny);
            if ((_tiles.RoadFlags[idx] & 0x0F) != 0) // has existing road
            {
                _tiles.RoadFlags[idx] &= (byte)~bit; // remove connection toward demolished tile
            }
        }
    }

    private void RenderGhost(int gx, int gy, IsometricCamera camera, SpriteRenderer sprites,
                              int tw, int th, float offsetX, float offsetY, float zoom)
    {
        var (sx, sy) = Forge.Engine.Math.IsometricMath.GridToScreen(gx, gy, tw, th, camera.Rotation);
        float drawX = sx * zoom + offsetX;
        float drawY = sy * zoom + offsetY;
        float drawW = tw * zoom;
        float drawH = th * zoom;

        // Red tint for bulldoze
        bool valid = IsValidPlacement(gx, gy);
        float r = 0.9f;
        float g = valid ? 0.3f : 0.1f;
        float b = valid ? 0.2f : 0.1f;
        float a = 0.5f;

        sprites.Draw(drawX, drawY, drawW, drawH, 0f, 0f, 1f, 1f, r, g, b, a);
    }

    private (int X, int Y, byte NewTerrain, byte NewZone, byte NewRoad, ushort NewBuilding)[]
        CaptureAfterState(int startX, int startY, int width, int height)
    {
        int x1 = System.Math.Max(0, startX);
        int y1 = System.Math.Max(0, startY);
        int x2 = System.Math.Min(_tiles.Size, startX + width);
        int y2 = System.Math.Min(_tiles.Size, startY + height);

        int count = (x2 - x1) * (y2 - y1);
        var result = new (int, int, byte, byte, byte, ushort)[count];
        int idx = 0;

        for (int y = y1; y < y2; y++)
        {
            for (int x = x1; x < x2; x++)
            {
                int ti = _tiles.Index(x, y);
                result[idx++] = (x, y,
                    _tiles.TerrainType[ti],
                    _tiles.ZoneType[ti],
                    _tiles.RoadFlags[ti],
                    _tiles.BuildingId[ti]);
            }
        }

        return result;
    }
}

/// <summary>
/// Road drawing tool. Places road segments by clicking and dragging in straight lines.
/// </summary>
public sealed class RoadTool : ITool
{
    private readonly ToolSystem _toolSystem;
    private readonly TileData _tiles;

    private bool _dragging;
    private int _dragStartX, _dragStartY;
    private int _dragEndX, _dragEndY;
    private int _hoverX, _hoverY;
    private bool _hovering;
    private byte _roadLevel; // 0=dirt, 1=paved, 2=highway
    private int _costPerTile;

    // Path tiles for the current drag (straight line: horizontal then vertical)
    private readonly List<(int x, int y)> _pathTiles = new();

    public string Name { get; }
    public string Icon { get; }

    public RoadTool(ToolSystem toolSystem, TileData tiles, string name, string icon,
                    byte roadLevel, int costPerTile)
    {
        _toolSystem = toolSystem;
        _tiles = tiles;
        Name = name;
        Icon = icon;
        _roadLevel = roadLevel;
        _costPerTile = costPerTile;
    }

    public void OnActivate() { _dragging = false; _hovering = false; _pathTiles.Clear(); }
    public void OnDeactivate() { _dragging = false; _hovering = false; _pathTiles.Clear(); }

    public void OnHover(int tileX, int tileY)
    {
        _hoverX = tileX;
        _hoverY = tileY;
        _hovering = true;

        if (_dragging)
        {
            _dragEndX = tileX;
            _dragEndY = tileY;
            ComputePath();
        }
    }

    public void OnClick(int tileX, int tileY) { }

    public void OnDragStart(int tileX, int tileY)
    {
        _dragging = true;
        _dragStartX = tileX;
        _dragStartY = tileY;
        _dragEndX = tileX;
        _dragEndY = tileY;
        ComputePath();
    }

    public void OnDrag(int tileX, int tileY)
    {
        _dragEndX = tileX;
        _dragEndY = tileY;
        ComputePath();
    }

    public void OnDragEnd(int tileX, int tileY)
    {
        if (!_dragging) return;
        _dragging = false;

        _dragEndX = tileX;
        _dragEndY = tileY;
        ComputePath();

        if (_pathTiles.Count == 0) return;

        // Find bounding box of path for undo capture
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        foreach (var (px, py) in _pathTiles)
        {
            if (px < minX) minX = px;
            if (py < minY) minY = py;
            if (px > maxX) maxX = px;
            if (py > maxY) maxY = py;
        }
        int w = maxX - minX + 1;
        int h = maxY - minY + 1;

        var before = _toolSystem.CaptureTileState(_tiles, minX, minY, w, h);

        // Place road segments with connection flags
        int placed = 0;
        for (int i = 0; i < _pathTiles.Count; i++)
        {
            var (px, py) = _pathTiles[i];
            if (!_tiles.InBounds(px, py)) continue;

            int idx = _tiles.Index(px, py);
            // Only place on buildable terrain or existing road
            byte terrain = _tiles.TerrainType[idx];
            if (terrain == 3 || terrain == 4) continue; // skip water, rock

            // Compute connection flags: N=bit0, E=bit1, S=bit2, W=bit3
            byte connections = 0;

            // Check neighbors in path and existing roads
            bool hasNorth = HasRoadAt(px, py - 1, i);
            bool hasEast = HasRoadAt(px + 1, py, i);
            bool hasSouth = HasRoadAt(px, py + 1, i);
            bool hasWest = HasRoadAt(px - 1, py, i);

            if (hasNorth) connections |= 0x01;
            if (hasEast) connections |= 0x02;
            if (hasSouth) connections |= 0x04;
            if (hasWest) connections |= 0x08;

            // Set road level in bits 4-5
            byte roadFlags = (byte)(connections | ((_roadLevel & 0x03) << 4));
            _tiles.RoadFlags[idx] = roadFlags;
            placed++;

            // Update neighbor connection flags
            UpdateNeighborConnections(px, py);
        }

        Console.WriteLine($"[Tool] Modified road ({_dragStartX},{_dragStartY})->({_dragEndX},{_dragEndY}): placed={placed}, path={_pathTiles.Count} tiles");

        if (placed > 0)
        {
            var after = CaptureAfterState(minX, minY, w, h);
            _toolSystem.PushUndo(new UndoAction
            {
                Description = $"Road ({_pathTiles.Count} tiles)",
                TilesBefore = before,
                TilesAfter = after,
                CostRefund = placed * _costPerTile
            });

            // Mark affected chunks dirty for re-rendering (include neighbor tiles for connections)
            _toolSystem.Chunks?.MarkAreaDirty(
                System.Math.Max(0, minX - 1),
                System.Math.Max(0, minY - 1),
                w + 2,
                h + 2);

            // Enqueue road placement command to sim thread
            _toolSystem.Commands?.EnqueuePlaceRoad(_dragStartX, _dragStartY, _dragEndX, _dragEndY);
        }

        _pathTiles.Clear();
    }

    public void OnCancel()
    {
        _dragging = false;
        _pathTiles.Clear();
    }

    public void Render(IsometricCamera camera, SpriteRenderer sprites)
    {
        var (offsetX, offsetY) = camera.GetRenderOffset();
        float zoom = camera.SmoothZoom;
        int tw = camera.TileWidth;
        int th = camera.TileHeight;

        if (_dragging && _pathTiles.Count > 0)
        {
            foreach (var (px, py) in _pathTiles)
            {
                var (sx, sy) = Forge.Engine.Math.IsometricMath.GridToScreen(px, py, tw, th, camera.Rotation);
                float drawX = sx * zoom + offsetX;
                float drawY = sy * zoom + offsetY;
                float drawW = tw * zoom;
                float drawH = th * zoom;

                bool valid = IsValidPlacement(px, py);
                float r = valid ? 0.3f : 0.8f;
                float g = valid ? 0.7f : 0.2f;
                float b = valid ? 0.9f : 0.2f;
                float a = 0.4f;

                sprites.Draw(drawX, drawY, drawW, drawH, 0f, 0f, 1f, 1f, r, g, b, a);
            }
        }
        else if (_hovering)
        {
            var (sx, sy) = Forge.Engine.Math.IsometricMath.GridToScreen(_hoverX, _hoverY, tw, th, camera.Rotation);
            float drawX = sx * zoom + offsetX;
            float drawY = sy * zoom + offsetY;
            float drawW = tw * zoom;
            float drawH = th * zoom;

            bool valid = IsValidPlacement(_hoverX, _hoverY);
            float r = valid ? 0.3f : 0.8f;
            float g = valid ? 0.7f : 0.2f;
            float b = valid ? 0.9f : 0.2f;
            float a = 0.4f;

            sprites.Draw(drawX, drawY, drawW, drawH, 0f, 0f, 1f, 1f, r, g, b, a);
        }
    }

    public void RenderUI() { }

    public bool IsValidPlacement(int tileX, int tileY)
    {
        if (!_tiles.InBounds(tileX, tileY)) return false;
        int idx = _tiles.Index(tileX, tileY);
        byte terrain = _tiles.TerrainType[idx];
        return terrain != 3 && terrain != 4; // not water or rock
    }

    public int GetCost() => _pathTiles.Count * _costPerTile;

    /// <summary>
    /// Compute an L-shaped path from drag start to drag end (horizontal first, then vertical).
    /// </summary>
    private void ComputePath()
    {
        _pathTiles.Clear();

        int x = _dragStartX;
        int y = _dragStartY;

        // Horizontal leg
        int dx = _dragEndX > x ? 1 : (_dragEndX < x ? -1 : 0);
        while (x != _dragEndX)
        {
            _pathTiles.Add((x, y));
            x += dx;
        }

        // Vertical leg
        int dy = _dragEndY > y ? 1 : (_dragEndY < y ? -1 : 0);
        while (y != _dragEndY)
        {
            _pathTiles.Add((x, y));
            y += dy;
        }

        // Add the final tile
        _pathTiles.Add((x, y));
    }

    private bool HasRoadAt(int x, int y, int currentPathIndex)
    {
        // Check if in path
        for (int i = 0; i < _pathTiles.Count; i++)
        {
            if (_pathTiles[i].x == x && _pathTiles[i].y == y)
                return true;
        }

        // Check existing roads
        if (!_tiles.InBounds(x, y)) return false;
        int idx = _tiles.Index(x, y);
        return (_tiles.RoadFlags[idx] & 0x0F) != 0; // has any connection
    }

    private void UpdateNeighborConnections(int cx, int cy)
    {
        // Update the 4 cardinal neighbors to connect to the newly placed road
        (int nx, int ny, byte connectBit)[] neighbors =
        [
            (cx, cy - 1, 0x04), // North neighbor needs South connection
            (cx + 1, cy, 0x08), // East neighbor needs West connection
            (cx, cy + 1, 0x01), // South neighbor needs North connection
            (cx - 1, cy, 0x02), // West neighbor needs East connection
        ];

        foreach (var (nx, ny, bit) in neighbors)
        {
            if (!_tiles.InBounds(nx, ny)) continue;
            int idx = _tiles.Index(nx, ny);
            if ((_tiles.RoadFlags[idx] & 0x0F) != 0) // has existing road
            {
                _tiles.RoadFlags[idx] |= bit; // add connection back to us
            }
        }
    }

    private (int X, int Y, byte NewTerrain, byte NewZone, byte NewRoad, ushort NewBuilding)[]
        CaptureAfterState(int startX, int startY, int width, int height)
    {
        int x1 = System.Math.Max(0, startX);
        int y1 = System.Math.Max(0, startY);
        int x2 = System.Math.Min(_tiles.Size, startX + width);
        int y2 = System.Math.Min(_tiles.Size, startY + height);

        int count = (x2 - x1) * (y2 - y1);
        var result = new (int, int, byte, byte, byte, ushort)[count];
        int idx = 0;

        for (int y = y1; y < y2; y++)
        {
            for (int x = x1; x < x2; x++)
            {
                int ti = _tiles.Index(x, y);
                result[idx++] = (x, y,
                    _tiles.TerrainType[ti],
                    _tiles.ZoneType[ti],
                    _tiles.RoadFlags[ti],
                    _tiles.BuildingId[ti]);
            }
        }

        return result;
    }
}
