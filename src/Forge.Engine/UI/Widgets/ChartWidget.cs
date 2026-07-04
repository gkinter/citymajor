using System.Numerics;
using ImGuiNET;

namespace Forge.Engine.UI.Widgets;

/// <summary>
/// Simple chart widgets (line, bar, pie) for city stats dashboards.
/// Rendered directly via ImGui draw lists.
/// </summary>
public static class ChartWidget
{
    /// <summary>
    /// Draw a line chart with optional multiple series.
    /// </summary>
    public static void LineChart(string label, float width, float height,
                                  IReadOnlyList<float[]> series,
                                  IReadOnlyList<Vector4>? colors = null,
                                  float minValue = float.NaN,
                                  float maxValue = float.NaN)
    {
        var drawList = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var size = new Vector2(width, height);

        // Background
        drawList.AddRectFilled(pos, pos + size,
            ImGui.GetColorU32(new Vector4(0.1f, 0.1f, 0.12f, 1f)));

        if (series.Count == 0 || series[0].Length < 2)
        {
            ImGui.Dummy(size);
            return;
        }

        // Compute value range across all series
        float actualMin = float.MaxValue;
        float actualMax = float.MinValue;
        foreach (var s in series)
        {
            foreach (var v in s)
            {
                if (v < actualMin) actualMin = v;
                if (v > actualMax) actualMax = v;
            }
        }

        float yMin = float.IsNaN(minValue) ? actualMin : minValue;
        float yMax = float.IsNaN(maxValue) ? actualMax : maxValue;
        float yRange = yMax - yMin;
        if (yRange <= 0) yRange = 1;

        var defaultColors = new Vector4[]
        {
            new(0.8f, 0.6f, 0.2f, 1f),
            new(0.2f, 0.7f, 0.4f, 1f),
            new(0.6f, 0.3f, 0.7f, 1f),
            new(0.3f, 0.6f, 0.8f, 1f),
        };

        for (int si = 0; si < series.Count; si++)
        {
            var data = series[si];
            var color = colors != null && si < colors.Count
                ? colors[si]
                : defaultColors[si % defaultColors.Length];
            uint lineColor = ImGui.GetColorU32(color);

            int count = data.Length;
            float xStep = width / (count - 1);

            for (int i = 0; i < count - 1; i++)
            {
                float x0 = pos.X + i * xStep;
                float y0 = pos.Y + height - ((data[i] - yMin) / yRange) * height;
                float x1 = pos.X + (i + 1) * xStep;
                float y1 = pos.Y + height - ((data[i + 1] - yMin) / yRange) * height;
                drawList.AddLine(new Vector2(x0, y0), new Vector2(x1, y1), lineColor, 2f);
            }
        }

        drawList.AddRect(pos, pos + size,
            ImGui.GetColorU32(new Vector4(0.3f, 0.25f, 0.2f, 0.5f)));

        ImGui.Dummy(size);
    }

    /// <summary>
    /// Draw a horizontal bar chart.
    /// </summary>
    public static void BarChart(string label, float width, float height,
                                 IReadOnlyList<(string name, float value, Vector4 color)> bars)
    {
        var drawList = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var size = new Vector2(width, height);

        drawList.AddRectFilled(pos, pos + size,
            ImGui.GetColorU32(new Vector4(0.1f, 0.1f, 0.12f, 1f)));

        if (bars.Count == 0)
        {
            ImGui.Dummy(size);
            return;
        }

        float maxVal = 0;
        foreach (var (_, v, _) in bars)
            if (v > maxVal) maxVal = v;
        if (maxVal <= 0) maxVal = 1;

        float barHeight = (height - 4) / bars.Count - 2;
        float yOffset = 2;

        for (int i = 0; i < bars.Count; i++)
        {
            var (name, value, color) = bars[i];
            float barWidth = (value / maxVal) * (width - 8);
            float y = pos.Y + yOffset + i * (barHeight + 2);

            drawList.AddRectFilled(
                new Vector2(pos.X + 4, y),
                new Vector2(pos.X + 4 + barWidth, y + barHeight),
                ImGui.GetColorU32(color));

            drawList.AddText(new Vector2(pos.X + 6, y + 1),
                ImGui.GetColorU32(new Vector4(1, 1, 1, 1)), name);
        }

        drawList.AddRect(pos, pos + size,
            ImGui.GetColorU32(new Vector4(0.3f, 0.25f, 0.2f, 0.5f)));

        ImGui.Dummy(size);
    }

    /// <summary>
    /// Draw a simple pie chart.
    /// </summary>
    public static void PieChart(string label, float radius,
                                 IReadOnlyList<(string name, float value, Vector4 color)> slices)
    {
        var drawList = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var center = pos + new Vector2(radius, radius);

        float total = 0;
        foreach (var (_, v, _) in slices)
            total += v;
        if (total <= 0) total = 1;

        float startAngle = -MathF.PI / 2;

        foreach (var (name, value, color) in slices)
        {
            float sweep = (value / total) * MathF.PI * 2;
            int segments = System.Math.Max(3, (int)(sweep / 0.1f));

            // Build path for filled arc
            var path = new List<Vector2> { center };
            for (int i = 0; i <= segments; i++)
            {
                float angle = startAngle + sweep * i / segments;
                path.Add(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius);
            }

            uint fillColor = ImGui.GetColorU32(color);
            for (int i = 1; i < path.Count - 1; i++)
            {
                drawList.AddTriangleFilled(path[0], path[i], path[i + 1], fillColor);
            }

            startAngle += sweep;
        }

        // Outline
        drawList.AddCircle(center, radius, ImGui.GetColorU32(new Vector4(0.3f, 0.25f, 0.2f, 0.8f)), 64);

        ImGui.Dummy(new Vector2(radius * 2, radius * 2));
    }
}
