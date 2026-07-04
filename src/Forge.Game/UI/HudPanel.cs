using System.Numerics;
using Forge.Engine.Core;
using Forge.Engine.Simulation;
using ImGuiNET;

namespace Forge.Game.UI;

/// <summary>
/// Top-bar HUD panel: date, population, treasury, happiness, approval, speed controls, research.
/// Always visible. Reads from SimSnapshot (thread-safe).
/// </summary>
public sealed class HudPanel
{
    private static readonly string[] EraNames =
    {
        "Ancient", "Medieval", "Colonial", "Industrial", "Modern", "Future"
    };

    private static readonly string[] MonthNames =
    {
        "Jan", "Feb", "Mar", "Apr", "May", "Jun",
        "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"
    };

    private int _previousPopulation;
    private int _populationTrend; // -1 = declining, 0 = stable, +1 = growing

    /// <summary>Reference to the engine TimeManager for speed control.</summary>
    public TimeManager? Time { get; set; }

    public void Draw(SimSnapshot snapshot)
    {
        var io = ImGui.GetIO();
        float screenWidth = io.DisplaySize.X;

        // Position at top, full width
        ImGui.SetNextWindowPos(new Vector2(0, 0));
        ImGui.SetNextWindowSize(new Vector2(screenWidth, 48));

        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
                    ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar |
                    ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoBringToFrontOnFocus |
                    ImGuiWindowFlags.NoFocusOnAppearing;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12, 8));
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.08f, 0.08f, 0.12f, 0.95f));

        if (ImGui.Begin("##HudPanel", flags))
        {
            DrawDateSection(snapshot);

            ImGui.SameLine(0, 24);
            DrawPopulationSection(snapshot);

            ImGui.SameLine(0, 24);
            DrawTreasurySection(snapshot);

            ImGui.SameLine(0, 24);
            DrawHappinessSection(snapshot);

            ImGui.SameLine(0, 24);
            DrawApprovalSection(snapshot);

            ImGui.SameLine(0, 24);
            DrawSpeedControls();

            ImGui.SameLine(0, 24);
            DrawResearchSection(snapshot);
        }
        ImGui.End();

        ImGui.PopStyleColor();
        ImGui.PopStyleVar(2);

        // Track population trend
        UpdatePopulationTrend(snapshot.Population);
    }

    private void DrawDateSection(SimSnapshot snapshot)
    {
        // Parse date from DateString (YYYY-MM-DD)
        int year = 2024, month = 1, day = 1;
        if (snapshot.DateString.Length >= 10)
        {
            int.TryParse(snapshot.DateString.AsSpan(0, 4), out year);
            int.TryParse(snapshot.DateString.AsSpan(5, 2), out month);
            int.TryParse(snapshot.DateString.AsSpan(8, 2), out day);
        }

        string eraName = snapshot.Era >= 0 && snapshot.Era < EraNames.Length
            ? EraNames[snapshot.Era]
            : "Unknown";

        string monthName = month >= 1 && month <= 12 ? MonthNames[month - 1] : "???";

        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.9f, 0.85f, 0.7f, 1f));
        ImGui.Text($"{day} {monthName} Y{year}");
        ImGui.PopStyleColor();

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.6f, 1f));
        ImGui.Text($"({eraName})");
        ImGui.PopStyleColor();
    }

    private void DrawPopulationSection(SimSnapshot snapshot)
    {
        string trendArrow = _populationTrend switch
        {
            1 => " ^",
            -1 => " v",
            _ => " -"
        };

        Vector4 trendColor = _populationTrend switch
        {
            1 => new Vector4(0.3f, 0.9f, 0.3f, 1f),
            -1 => new Vector4(0.9f, 0.3f, 0.3f, 1f),
            _ => new Vector4(0.7f, 0.7f, 0.7f, 1f)
        };

        ImGui.Text("Pop:");
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.8f, 0.9f, 1f, 1f));
        ImGui.Text($"{snapshot.Population:N0}");
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, trendColor);
        ImGui.Text(trendArrow);
        ImGui.PopStyleColor();
    }

    private void DrawTreasurySection(SimSnapshot snapshot)
    {
        long netIncome = snapshot.MonthlyIncome - snapshot.MonthlyExpenses;
        Vector4 fundsColor = snapshot.CityFunds >= 0
            ? new Vector4(0.3f, 0.9f, 0.3f, 1f)
            : new Vector4(0.9f, 0.3f, 0.3f, 1f);

        string incomeStr = netIncome >= 0 ? $"+${netIncome:N0}" : $"-${Math.Abs(netIncome):N0}";
        Vector4 incomeColor = netIncome >= 0
            ? new Vector4(0.3f, 0.8f, 0.3f, 0.9f)
            : new Vector4(0.8f, 0.3f, 0.3f, 0.9f);

        ImGui.Text("Treasury:");
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, fundsColor);
        ImGui.Text($"${snapshot.CityFunds:N0}");
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, incomeColor);
        ImGui.Text($"({incomeStr}/mo)");
        ImGui.PopStyleColor();
    }

    private void DrawHappinessSection(SimSnapshot snapshot)
    {
        float happinessPercent = snapshot.Happiness * 100f;

        // Color: green > 70, yellow 40-70, red < 40
        Vector4 happyColor;
        if (happinessPercent > 70f)
            happyColor = new Vector4(0.3f, 0.9f, 0.3f, 1f);
        else if (happinessPercent >= 40f)
            happyColor = new Vector4(0.9f, 0.9f, 0.2f, 1f);
        else
            happyColor = new Vector4(0.9f, 0.3f, 0.3f, 1f);

        ImGui.Text("Happy:");
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, happyColor);
        ImGui.Text($"{happinessPercent:F0}%");
        ImGui.PopStyleColor();

        // Inline progress bar
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, happyColor);
        ImGui.ProgressBar(snapshot.Happiness, new Vector2(50, 14), "");
        ImGui.PopStyleColor();
    }

    private void DrawApprovalSection(SimSnapshot snapshot)
    {
        float approvalPercent = snapshot.ApprovalRating * 100f;

        Vector4 approvalColor;
        if (approvalPercent > 60f)
            approvalColor = new Vector4(0.3f, 0.9f, 0.3f, 1f);
        else if (approvalPercent >= 40f)
            approvalColor = new Vector4(0.9f, 0.9f, 0.2f, 1f);
        else
            approvalColor = new Vector4(0.9f, 0.3f, 0.3f, 1f);

        ImGui.Text("Approval:");
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, approvalColor);
        ImGui.Text($"{approvalPercent:F0}%");
        ImGui.PopStyleColor();
    }

    private void DrawSpeedControls()
    {
        int currentSpeed = Time?.SpeedLevel ?? 1;

        string[] labels = { "||", ">", ">>", ">>>" };
        for (int i = 0; i <= 3; i++)
        {
            bool isActive = currentSpeed == i;
            if (isActive)
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.5f, 0.8f, 1f));
            else
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.2f, 0.25f, 1f));

            if (ImGui.SmallButton(labels[i]))
            {
                Time?.SetSpeed(i);
            }
            ImGui.PopStyleColor();

            if (i < 3) ImGui.SameLine(0, 2);
        }
    }

    private void DrawResearchSection(SimSnapshot snapshot)
    {
        if (snapshot.CurrentResearchId < 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1f));
            ImGui.Text("Research: Idle");
            ImGui.PopStyleColor();
            return;
        }

        ImGui.Text("Research:");
        ImGui.SameLine();

        float progress = snapshot.CurrentResearchProgress;
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(0.2f, 0.6f, 0.9f, 1f));
        ImGui.ProgressBar(progress, new Vector2(80, 14), $"{progress * 100f:F0}%");
        ImGui.PopStyleColor();

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.7f, 0.9f, 1f));
        ImGui.Text($"{snapshot.ResearchRate:F1} RP/mo");
        ImGui.PopStyleColor();
    }

    private void UpdatePopulationTrend(int currentPopulation)
    {
        if (currentPopulation > _previousPopulation)
            _populationTrend = 1;
        else if (currentPopulation < _previousPopulation)
            _populationTrend = -1;
        else
            _populationTrend = 0;

        _previousPopulation = currentPopulation;
    }
}
