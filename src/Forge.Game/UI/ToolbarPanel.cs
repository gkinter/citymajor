using System.Numerics;
using Forge.Engine.Input;
using ImGuiNET;

namespace Forge.Game.UI;

/// <summary>
/// Bottom toolbar: tool selection buttons, active tool indicator, tool options, cost preview,
/// undo/redo. Always visible. Reads from ToolSystem for tool state.
/// </summary>
public sealed class ToolbarPanel
{
    /// <summary>Reference to the ToolSystem for tool state and actions.</summary>
    public ToolSystem? Tools { get; set; }

    // Road type dropdown
    private int _selectedRoadType;
    private static readonly string[] RoadTypes = { "Dirt Road", "Paved Road", "Highway", "Boulevard" };

    // Zone density dropdown
    private int _selectedDensity;
    private static readonly string[] DensityLevels = { "Low", "Medium", "High" };

    public void Draw()
    {
        var io = ImGui.GetIO();
        float screenWidth = io.DisplaySize.X;
        float screenHeight = io.DisplaySize.Y;
        float toolbarHeight = 72;

        // Position at bottom, full width
        ImGui.SetNextWindowPos(new Vector2(0, screenHeight - toolbarHeight));
        ImGui.SetNextWindowSize(new Vector2(screenWidth, toolbarHeight));

        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
                    ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar |
                    ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoBringToFrontOnFocus |
                    ImGuiWindowFlags.NoFocusOnAppearing;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12, 8));
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.08f, 0.08f, 0.12f, 0.95f));

        if (ImGui.Begin("##ToolbarPanel", flags))
        {
            DrawToolButtons();

            ImGui.SameLine(0, 20);
            DrawSeparatorLine();
            ImGui.SameLine(0, 20);

            DrawToolOptions();

            // Undo/Redo on the right side
            float undoX = screenWidth - 160;
            ImGui.SameLine(undoX);
            DrawUndoRedo();
        }
        ImGui.End();

        ImGui.PopStyleColor();
        ImGui.PopStyleVar(2);
    }

    private void DrawToolButtons()
    {
        if (Tools == null) return;

        var buttonSize = new Vector2(52, 52);
        var tools = Tools.RegisteredTools;
        var activeTool = Tools.ActiveTool;

        for (int i = 0; i < tools.Count; i++)
        {
            var tool = tools[i];
            bool isActive = activeTool == tool;

            // Determine button label and category
            string label = GetToolLabel(tool.Name);
            string shortcut = GetToolShortcut(tool.Name);

            // Style active tool
            if (isActive)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.5f, 0.8f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.25f, 0.55f, 0.85f, 1f));
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f));
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.18f, 0.18f, 0.22f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.25f, 0.25f, 0.3f, 1f));
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.8f, 0.8f, 0.8f, 1f));
            }

            if (ImGui.Button($"{label}\n{shortcut}##tool_{i}", buttonSize))
            {
                if (isActive)
                    Tools.ClearTool();
                else
                    Tools.SetActiveTool(i);
            }

            ImGui.PopStyleColor(3);

            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text($"{tool.Name}");
                int cost = tool.GetCost();
                if (cost > 0)
                    ImGui.Text($"Cost: ${cost:N0}");
                ImGui.EndTooltip();
            }

            if (i < tools.Count - 1)
                ImGui.SameLine(0, 4);
        }
    }

    private void DrawToolOptions()
    {
        if (Tools?.ActiveTool == null)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.4f, 0.4f, 0.5f, 1f));
            ImGui.Text("Select a tool to begin building");
            ImGui.PopStyleColor();
            return;
        }

        var tool = Tools.ActiveTool;
        string name = tool.Name;

        // Cost preview
        int cost = tool.GetCost();
        if (cost > 0)
        {
            ImGui.Text("Cost:");
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.9f, 0.8f, 0.2f, 1f));
            ImGui.Text($"${cost:N0}");
            ImGui.PopStyleColor();
            ImGui.SameLine(0, 16);
        }

        // Tool-specific options
        if (name == "Road")
        {
            ImGui.Text("Type:");
            ImGui.SameLine();
            ImGui.PushItemWidth(120);
            ImGui.Combo("##RoadType", ref _selectedRoadType, RoadTypes, RoadTypes.Length);
            ImGui.PopItemWidth();
        }
        else if (name is "Residential" or "Commercial" or "Industrial")
        {
            ImGui.Text("Density:");
            ImGui.SameLine();
            ImGui.PushItemWidth(100);
            ImGui.Combo("##Density", ref _selectedDensity, DensityLevels, DensityLevels.Length);
            ImGui.PopItemWidth();
        }

        // Active tool indicator
        ImGui.SameLine(0, 16);
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.3f, 0.8f, 0.9f, 1f));
        ImGui.Text($"[{name}]");
        ImGui.PopStyleColor();
    }

    private void DrawUndoRedo()
    {
        if (Tools == null) return;

        var undoSize = new Vector2(52, 28);

        // Undo button
        bool canUndo = Tools.CanUndo;
        if (!canUndo)
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.4f);

        if (ImGui.Button($"Undo ({Tools.UndoCount})##undo", undoSize) && canUndo)
        {
            Tools.Undo();
        }
        if (!canUndo)
            ImGui.PopStyleVar();

        ImGui.SameLine(0, 4);

        // Redo button
        bool canRedo = Tools.CanRedo;
        if (!canRedo)
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.4f);

        if (ImGui.Button($"Redo ({Tools.RedoCount})##redo", undoSize) && canRedo)
        {
            Tools.Redo();
        }
        if (!canRedo)
            ImGui.PopStyleVar();
    }

    private static void DrawSeparatorLine()
    {
        var drawList = ImGui.GetWindowDrawList();
        var cursorPos = ImGui.GetCursorScreenPos();
        uint color = ImGui.GetColorU32(new Vector4(0.3f, 0.3f, 0.4f, 0.6f));
        drawList.AddLine(
            new Vector2(cursorPos.X, cursorPos.Y),
            new Vector2(cursorPos.X, cursorPos.Y + 52),
            color, 1f);
    }

    private static string GetToolLabel(string toolName)
    {
        return toolName switch
        {
            "Road" => "Road",
            "Residential" => "Res",
            "Commercial" => "Com",
            "Industrial" => "Ind",
            "Bulldoze" => "Demo",
            _ => toolName.Length > 4 ? toolName[..4] : toolName
        };
    }

    private static string GetToolShortcut(string toolName)
    {
        return toolName switch
        {
            "Road" => "[R]",
            "Residential" => "[1]",
            "Commercial" => "[2]",
            "Industrial" => "[3]",
            "Bulldoze" => "[X]",
            _ => ""
        };
    }
}
