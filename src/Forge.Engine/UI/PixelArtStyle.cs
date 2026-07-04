using System.Numerics;
using ImGuiNET;

namespace Forge.Engine.UI;

/// <summary>
/// ImGui styling for a pixel-art / retro city builder aesthetic.
/// Dark theme with warm accent colors and crisp, blocky UI elements.
/// </summary>
public static class PixelArtStyle
{
    public static void Apply()
    {
        var style = ImGui.GetStyle();

        // Crisp, blocky look -- no rounding
        style.WindowRounding = 0f;
        style.FrameRounding = 0f;
        style.GrabRounding = 0f;
        style.TabRounding = 0f;
        style.ScrollbarRounding = 0f;
        style.ChildRounding = 0f;
        style.PopupRounding = 0f;

        // Spacing
        style.WindowPadding = new Vector2(8, 8);
        style.FramePadding = new Vector2(6, 4);
        style.ItemSpacing = new Vector2(8, 4);
        style.ItemInnerSpacing = new Vector2(4, 4);
        style.ScrollbarSize = 12f;
        style.GrabMinSize = 8f;

        // Borders
        style.WindowBorderSize = 1f;
        style.FrameBorderSize = 1f;
        style.PopupBorderSize = 1f;

        // Colors -- dark theme with warm accents
        var colors = style.Colors;

        // Backgrounds
        colors[(int)ImGuiCol.WindowBg] = new Vector4(0.08f, 0.08f, 0.10f, 0.95f);
        colors[(int)ImGuiCol.ChildBg] = new Vector4(0.10f, 0.10f, 0.12f, 1.00f);
        colors[(int)ImGuiCol.PopupBg] = new Vector4(0.08f, 0.08f, 0.10f, 0.98f);

        // Borders
        colors[(int)ImGuiCol.Border] = new Vector4(0.30f, 0.25f, 0.20f, 0.60f);
        colors[(int)ImGuiCol.BorderShadow] = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);

        // Frame (input fields, checkboxes)
        colors[(int)ImGuiCol.FrameBg] = new Vector4(0.15f, 0.14f, 0.13f, 1.00f);
        colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.22f, 0.20f, 0.18f, 1.00f);
        colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.28f, 0.25f, 0.22f, 1.00f);

        // Title bar
        colors[(int)ImGuiCol.TitleBg] = new Vector4(0.06f, 0.06f, 0.07f, 1.00f);
        colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.12f, 0.10f, 0.08f, 1.00f);
        colors[(int)ImGuiCol.TitleBgCollapsed] = new Vector4(0.06f, 0.06f, 0.07f, 0.50f);

        // Scrollbar
        colors[(int)ImGuiCol.ScrollbarBg] = new Vector4(0.05f, 0.05f, 0.06f, 0.85f);
        colors[(int)ImGuiCol.ScrollbarGrab] = new Vector4(0.30f, 0.25f, 0.20f, 1.00f);
        colors[(int)ImGuiCol.ScrollbarGrabHovered] = new Vector4(0.40f, 0.35f, 0.28f, 1.00f);
        colors[(int)ImGuiCol.ScrollbarGrabActive] = new Vector4(0.50f, 0.42f, 0.32f, 1.00f);

        // Buttons -- warm amber accent
        colors[(int)ImGuiCol.Button] = new Vector4(0.45f, 0.32f, 0.15f, 1.00f);
        colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.55f, 0.40f, 0.20f, 1.00f);
        colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.65f, 0.48f, 0.25f, 1.00f);

        // Headers
        colors[(int)ImGuiCol.Header] = new Vector4(0.35f, 0.28f, 0.18f, 0.60f);
        colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.45f, 0.35f, 0.22f, 0.80f);
        colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.50f, 0.40f, 0.25f, 1.00f);

        // Tabs
        colors[(int)ImGuiCol.Tab] = new Vector4(0.12f, 0.10f, 0.08f, 1.00f);
        colors[(int)ImGuiCol.TabHovered] = new Vector4(0.45f, 0.35f, 0.22f, 0.80f);

        // Separator
        colors[(int)ImGuiCol.Separator] = new Vector4(0.30f, 0.25f, 0.20f, 0.50f);

        // Slider
        colors[(int)ImGuiCol.SliderGrab] = new Vector4(0.55f, 0.40f, 0.20f, 1.00f);
        colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.65f, 0.48f, 0.25f, 1.00f);

        // Check mark
        colors[(int)ImGuiCol.CheckMark] = new Vector4(0.80f, 0.60f, 0.25f, 1.00f);

        // Text
        colors[(int)ImGuiCol.Text] = new Vector4(0.90f, 0.88f, 0.82f, 1.00f);
        colors[(int)ImGuiCol.TextDisabled] = new Vector4(0.50f, 0.48f, 0.42f, 1.00f);
    }
}
