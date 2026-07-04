using System.Numerics;
using ImGuiNET;

namespace Forge.Engine.UI.Widgets;

/// <summary>
/// Custom ImGui widget for rendering a tech tree as a node graph.
/// Nodes can be locked, unlocked, or researching, with directed edges showing dependencies.
/// </summary>
public static class TechTreeWidget
{
    public enum NodeState { Locked, Available, Researching, Completed }

    public readonly record struct TechNode(
        string Id,
        string Name,
        string Description,
        Vector2 Position,
        NodeState State,
        float ResearchProgress,
        string[] Dependencies
    );

    private static readonly Vector4 ColorLocked = new(0.3f, 0.3f, 0.3f, 1f);
    private static readonly Vector4 ColorAvailable = new(0.45f, 0.32f, 0.15f, 1f);
    private static readonly Vector4 ColorResearching = new(0.65f, 0.48f, 0.25f, 1f);
    private static readonly Vector4 ColorCompleted = new(0.2f, 0.6f, 0.3f, 1f);

    private const float NodeWidth = 140f;
    private const float NodeHeight = 60f;

    /// <summary>
    /// Render the tech tree graph. Returns the ID of a clicked node, or null.
    /// </summary>
    public static string? Draw(string label, IReadOnlyList<TechNode> nodes, Vector2 scrollOffset)
    {
        string? clicked = null;

        if (!ImGui.BeginChild(label, Vector2.Zero, ImGuiChildFlags.Borders))
        {
            ImGui.EndChild();
            return null;
        }

        var drawList = ImGui.GetWindowDrawList();
        var windowPos = ImGui.GetCursorScreenPos();

        // Build lookup for positions
        var positions = new Dictionary<string, Vector2>();
        foreach (var node in nodes)
        {
            positions[node.Id] = windowPos + node.Position + scrollOffset;
        }

        // Draw edges first (behind nodes)
        foreach (var node in nodes)
        {
            var endPos = positions[node.Id] + new Vector2(0, NodeHeight * 0.5f);
            foreach (var depId in node.Dependencies)
            {
                if (positions.TryGetValue(depId, out var depPos))
                {
                    var startPos = depPos + new Vector2(NodeWidth, NodeHeight * 0.5f);
                    uint edgeColor = node.State == NodeState.Locked
                        ? ImGui.GetColorU32(new Vector4(0.3f, 0.3f, 0.3f, 0.5f))
                        : ImGui.GetColorU32(new Vector4(0.6f, 0.5f, 0.3f, 0.8f));

                    drawList.AddLine(startPos, endPos, edgeColor, 2f);
                }
            }
        }

        // Draw nodes
        foreach (var node in nodes)
        {
            var pos = positions[node.Id];
            var size = new Vector2(NodeWidth, NodeHeight);
            var color = node.State switch
            {
                NodeState.Locked => ColorLocked,
                NodeState.Available => ColorAvailable,
                NodeState.Researching => ColorResearching,
                NodeState.Completed => ColorCompleted,
                _ => ColorLocked
            };

            // Background
            drawList.AddRectFilled(pos, pos + size, ImGui.GetColorU32(color));
            drawList.AddRect(pos, pos + size, ImGui.GetColorU32(new Vector4(0.8f, 0.7f, 0.5f, 0.6f)));

            // Name
            drawList.AddText(pos + new Vector2(4, 4), ImGui.GetColorU32(new Vector4(1, 1, 1, 1)), node.Name);

            // Progress bar for researching nodes
            if (node.State == NodeState.Researching)
            {
                var barPos = pos + new Vector2(4, NodeHeight - 12);
                var barSize = new Vector2((NodeWidth - 8) * node.ResearchProgress, 8);
                drawList.AddRectFilled(barPos, barPos + barSize,
                    ImGui.GetColorU32(new Vector4(0.8f, 0.6f, 0.2f, 1f)));
            }

            // Click detection
            ImGui.SetCursorScreenPos(pos);
            ImGui.InvisibleButton($"tech_{node.Id}", size);
            if (ImGui.IsItemClicked())
                clicked = node.Id;

            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text(node.Name);
                ImGui.TextWrapped(node.Description);
                ImGui.EndTooltip();
            }
        }

        ImGui.EndChild();
        return clicked;
    }
}
