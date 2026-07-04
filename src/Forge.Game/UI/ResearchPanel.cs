using System.Numerics;
using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using ImGuiNET;

namespace Forge.Game.UI;

/// <summary>
/// Tech tree browser: available technologies, current research, queue, funding slider,
/// RP breakdown. Toggled with T key.
/// Reads from SimSnapshot + ResearchSystem. Enqueues research commands via CommandQueue.
/// </summary>
public sealed class ResearchPanel
{
    public bool IsOpen { get; set; }

    /// <summary>Reference to ResearchSystem for tech definitions, queue, and RP data.</summary>
    public ResearchSystem? Research { get; set; }

    /// <summary>Reference to CommandQueue for research commands.</summary>
    public CommandQueue? Commands { get; set; }

    /// <summary>Reference to the live WorldState for tech unlock checks (read-only on UI thread).</summary>
    public WorldState? State { get; set; }

    // Local funding slider state (FundingMultiplier: 0.0 to 2.0)
    private float _fundingSlider = 1.0f;

    // Selected tech for detail view
    private int _selectedTechId = -1;

    // Category filter
    private int _selectedCategory;
    private static readonly string[] CategoryFilters =
    {
        "All", "Infrastructure", "Economy", "Military", "Culture",
        "Science", "Government", "Agriculture", "Industry",
        "Maritime", "Education", "Religion", "Medicine",
        "Energy", "Transport", "Communication", "Environment", "Space"
    };

    public void Draw(SimSnapshot snapshot)
    {
        if (!IsOpen) return;

        var io = ImGui.GetIO();
        float panelWidth = 500;
        float panelHeight = 520;
        float x = (io.DisplaySize.X - panelWidth) * 0.5f;
        float y = 60;

        ImGui.SetNextWindowPos(new Vector2(x, y), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(panelWidth, panelHeight), ImGuiCond.FirstUseEver);

        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.1f, 0.1f, 0.14f, 0.96f));
        ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(0.1f, 0.15f, 0.25f, 1f));
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(0.12f, 0.2f, 0.35f, 1f));

        bool open = IsOpen;
        if (ImGui.Begin("Research [T]", ref open))
        {
            IsOpen = open;

            DrawCurrentResearch(snapshot);
            ImGui.Separator();
            DrawResearchQueue();
            ImGui.Separator();
            DrawFundingAndRP(snapshot);
            ImGui.Separator();
            DrawAvailableTechs(snapshot);
        }
        else
        {
            IsOpen = open;
        }
        ImGui.End();

        ImGui.PopStyleColor(3);
    }

    private void DrawCurrentResearch(SimSnapshot snapshot)
    {
        ImGui.Text("Current Research");
        ImGui.Spacing();

        if (Research == null || Research.ResearchQueue[0] == -1)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.6f, 1f));
            ImGui.Text("No active research. Select a technology below.");
            ImGui.PopStyleColor();
            return;
        }

        int techId = Research.ResearchQueue[0];
        var tech = Research.Technologies[techId];
        if (tech == null) return;

        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.3f, 0.7f, 0.9f, 1f));
        ImGui.Text(tech.Name);
        ImGui.PopStyleColor();

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.6f, 1f));
        ImGui.Text($"[{tech.Category}]");
        ImGui.PopStyleColor();

        // Progress bar
        float progress = Research.GetCurrentResearchFraction();
        float cost = Research.GetEffectiveCost(techId);
        float currentRP = Research.QueueProgress[0];

        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(0.2f, 0.5f, 0.9f, 1f));
        ImGui.ProgressBar(progress, new Vector2(-1, 20), $"{currentRP:F0} / {cost:F0} RP ({progress * 100f:F0}%)");
        ImGui.PopStyleColor();

        // Estimated time
        float rpPerMonth = snapshot.ResearchRate;
        if (rpPerMonth > 0.01f)
        {
            float remaining = cost - currentRP;
            float monthsLeft = remaining / rpPerMonth;
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.7f, 1f));
            ImGui.Text($"Est. completion: {monthsLeft:F1} months");
            ImGui.PopStyleColor();
        }
    }

    private void DrawResearchQueue()
    {
        ImGui.Text("Research Queue (up to 3)");
        ImGui.Spacing();

        if (Research == null) return;

        bool hasQueued = false;
        for (int i = 0; i < ResearchSystem.MaxResearchQueue; i++)
        {
            int techId = Research.ResearchQueue[i];
            if (techId == -1) continue;

            hasQueued = true;
            var tech = Research.Technologies[techId];
            if (tech == null) continue;

            float cost = Research.GetEffectiveCost(techId);
            float progress = cost > 0 ? Research.QueueProgress[i] / cost : 0f;

            string prefix = i == 0 ? ">>>" : $"  {i + 1}.";

            ImGui.Text($"{prefix} {tech.Name}");
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(0.2f, 0.4f, 0.7f, 0.8f));
            ImGui.ProgressBar(progress, new Vector2(100, 14), $"{progress * 100f:F0}%");
            ImGui.PopStyleColor();

            // Remove from queue button
            ImGui.SameLine();
            if (ImGui.SmallButton($"X##dequeue_{i}"))
            {
                Research.DequeueResearch(techId);
            }
        }

        if (!hasQueued)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.4f, 0.4f, 0.5f, 1f));
            ImGui.Text("Queue empty");
            ImGui.PopStyleColor();
        }
    }

    private void DrawFundingAndRP(SimSnapshot snapshot)
    {
        ImGui.Text("Research Funding & RP Generation");
        ImGui.Spacing();

        // Funding slider (0% to 200%)
        if (Research != null)
        {
            _fundingSlider = Research.FundingMultiplier;
        }

        ImGui.Text($"Funding: {_fundingSlider * 100f:F0}%");
        ImGui.PushItemWidth(-1);
        if (ImGui.SliderFloat("##FundingSlider", ref _fundingSlider, 0f, 2.0f, "%.0f%%"))
        {
            if (Research != null)
            {
                Research.FundingMultiplier = _fundingSlider;
            }
        }
        ImGui.PopItemWidth();

        ImGui.Spacing();

        // RP generation breakdown
        if (Research != null)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.7f, 0.9f, 1f));
            ImGui.Text($"RP/month: {snapshot.ResearchRate:F1}");
            ImGui.PopStyleColor();

            ImGui.Spacing();
            ImGui.Text("RP Sources:");

            float baseRP = Research.CalculateBaseRP();
            float effectiveRP = Research.CalculateEffectiveRP();

            DrawRPSource("Libraries", Research.LibraryCount, ResearchSystem.RpPerLibrary);
            DrawRPSource("Universities", Research.UniversityCount, ResearchSystem.RpBasePerUniversity);
            DrawRPSource("Professors", Research.ProfessorCount, ResearchSystem.RpPerProfessor);
            DrawRPSource("Research Labs", Research.ResearchLabCount, ResearchSystem.RpPerResearchLab);
            DrawRPSource("Tech Campuses", Research.TechCampusCount, ResearchSystem.RpPerTechCampus);
            DrawRPSource("Educated Pop", Research.EducatedPopulation, ResearchSystem.RpPerEducatedCitizen);
            DrawRPSource("Heavy Industry", Research.HeavyIndustryCount, ResearchSystem.RpPerHeavyIndustry);

            ImGui.Spacing();
            ImGui.Text($"Base RP: {baseRP:F1}");
            ImGui.Text($"Multipliers: funding({Research.FundingMultiplier:F1}x) " +
                       $"edu({Research.EducationLevelMultiplier:F1}x) " +
                       $"spec({Research.SpecializationMultiplier:F1}x) " +
                       $"collab({Research.CollaborationMultiplier:F1}x)");
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.3f, 0.9f, 0.7f, 1f));
            ImGui.Text($"Effective RP: {effectiveRP:F1}");
            ImGui.PopStyleColor();
        }
    }

    private void DrawAvailableTechs(SimSnapshot snapshot)
    {
        ImGui.Text("Available Technologies");
        ImGui.Spacing();

        // Category filter
        ImGui.PushItemWidth(150);
        ImGui.Combo("Filter", ref _selectedCategory, CategoryFilters, CategoryFilters.Length);
        ImGui.PopItemWidth();

        ImGui.Spacing();

        if (Research == null || State == null) return;

        // Get available techs
        var available = Research.GetAvailableTechs(State);

        // Filter by selected category
        string filter = _selectedCategory > 0 ? CategoryFilters[_selectedCategory] : "";

        if (ImGui.BeginChild("TechList", new Vector2(-1, 150), ImGuiChildFlags.Borders))
        {
            int displayedCount = 0;
            foreach (int techId in available)
            {
                var tech = Research.Technologies[techId];
                if (tech == null) continue;

                // Era filter: only show techs for current or lower era
                if (tech.Era > snapshot.Era + 1) continue;

                // Category filter
                if (filter.Length > 0 && !string.Equals(tech.Category, filter, StringComparison.OrdinalIgnoreCase))
                    continue;

                displayedCount++;
                bool isSelected = _selectedTechId == techId;
                bool isQueued = IsInQueue(techId);

                // Color based on era
                Vector4 textColor = tech.Era <= snapshot.Era
                    ? new Vector4(0.9f, 0.9f, 0.9f, 1f)
                    : new Vector4(0.6f, 0.6f, 0.7f, 1f);

                ImGui.PushStyleColor(ImGuiCol.Text, textColor);

                if (ImGui.Selectable($"{tech.Name} ({tech.Category}) - {tech.Cost:F0} RP##tech_{techId}", isSelected))
                {
                    _selectedTechId = techId;
                }

                ImGui.PopStyleColor();

                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text(tech.Name);
                    ImGui.Text(tech.Description);
                    ImGui.Text($"Cost: {Research.GetEffectiveCost(techId):F0} RP");
                    ImGui.Text($"Era: {GetEraName(tech.Era)}");
                    if (tech.Prerequisites.Length > 0)
                    {
                        ImGui.Text("Prerequisites:");
                        foreach (int prereq in tech.Prerequisites)
                        {
                            var prereqTech = Research.Technologies[prereq];
                            string status = State.IsTechUnlocked(prereq) ? "[OK]" : "[LOCKED]";
                            ImGui.Text($"  {status} {prereqTech?.Name ?? $"Tech #{prereq}"}");
                        }
                    }
                    if (tech.EurekaCondition != null)
                    {
                        bool hasEureka = Research.EurekaBonuses.ContainsKey(techId);
                        ImGui.Text($"Eureka: {tech.EurekaCondition} {(hasEureka ? "[ACTIVE]" : "")}");
                    }
                    ImGui.EndTooltip();
                }

                // Queue button on the same line
                if (!isQueued)
                {
                    ImGui.SameLine(ImGui.GetWindowWidth() - 80);
                    if (ImGui.SmallButton($"Queue##q_{techId}"))
                    {
                        Research.EnqueueResearch(techId, State);
                    }
                }
                else
                {
                    ImGui.SameLine(ImGui.GetWindowWidth() - 80);
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.3f, 0.7f, 0.3f, 1f));
                    ImGui.Text("Queued");
                    ImGui.PopStyleColor();
                }
            }

            if (displayedCount == 0)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.6f, 1f));
                ImGui.Text("No technologies available with current filter.");
                ImGui.PopStyleColor();
            }
        }
        ImGui.EndChild();
    }

    private static void DrawRPSource(string label, int count, float rpPer)
    {
        if (count <= 0) return;
        float total = count * rpPer;
        ImGui.Text($"  {label}: {count} x {rpPer:F1} = {total:F1} RP");
    }

    private bool IsInQueue(int techId)
    {
        if (Research == null) return false;
        for (int i = 0; i < ResearchSystem.MaxResearchQueue; i++)
        {
            if (Research.ResearchQueue[i] == techId) return true;
        }
        return false;
    }

    private static string GetEraName(int era)
    {
        return era switch
        {
            0 => "Ancient",
            1 => "Medieval",
            2 => "Colonial",
            3 => "Industrial",
            4 => "Modern",
            5 => "Future",
            _ => "Unknown"
        };
    }
}
