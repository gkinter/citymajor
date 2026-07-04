using System.Numerics;
using ImGuiNET;

namespace Forge.Engine.UI.Widgets;

/// <summary>
/// Isometric minimap widget. Renders a low-resolution overview of the world
/// with a camera frustum indicator. Click to pan the camera.
/// </summary>
public static class MinimapWidget
{
    private const float DefaultSize = 200f;

    /// <summary>
    /// Draw the minimap. Returns (true, gridX, gridY) if the user clicked to pan.
    /// </summary>
    public static (bool clicked, int gridX, int gridY) Draw(
        string label,
        int worldSize,
        float cameraX,
        float cameraY,
        int cameraZoom,
        int viewportWidth,
        int viewportHeight,
        byte[]? minimapPixels = null,
        float size = DefaultSize)
    {
        bool clicked = false;
        int clickGridX = 0, clickGridY = 0;

        var drawList = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var mapSize = new Vector2(size, size);

        // Background
        drawList.AddRectFilled(pos, pos + mapSize,
            ImGui.GetColorU32(new Vector4(0.06f, 0.08f, 0.06f, 1f)));
        drawList.AddRect(pos, pos + mapSize,
            ImGui.GetColorU32(new Vector4(0.3f, 0.25f, 0.2f, 0.8f)));

        // Scale factor: world coords to minimap pixels
        float scale = size / worldSize;

        // Camera frustum rectangle
        float frustumW = viewportWidth / (float)cameraZoom * scale;
        float frustumH = viewportHeight / (float)cameraZoom * scale;
        float frustumX = pos.X + (cameraX * scale) - frustumW * 0.5f;
        float frustumY = pos.Y + (cameraY * scale) - frustumH * 0.5f;

        drawList.AddRect(
            new Vector2(frustumX, frustumY),
            new Vector2(frustumX + frustumW, frustumY + frustumH),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.8f)),
            0f, ImDrawFlags.None, 2f);

        // Click to pan
        ImGui.SetCursorScreenPos(pos);
        ImGui.InvisibleButton(label, mapSize);

        if (ImGui.IsItemClicked() || (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left)))
        {
            var mousePos = ImGui.GetMousePos();
            float relX = (mousePos.X - pos.X) / size;
            float relY = (mousePos.Y - pos.Y) / size;
            clickGridX = (int)(relX * worldSize);
            clickGridY = (int)(relY * worldSize);
            clicked = true;
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text("Click to pan camera");
            ImGui.EndTooltip();
        }

        // Advance cursor past the widget
        ImGui.SetCursorScreenPos(pos + new Vector2(0, size + 4));

        return (clicked, clickGridX, clickGridY);
    }
}
