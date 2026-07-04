using System.Numerics;
using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using ImGuiNET;

namespace Forge.Game.UI;

/// <summary>
/// Political overview panel: approval gauge, faction bars, active laws, election countdown,
/// protest status, corruption index. Toggled with G key.
/// Reads from SimSnapshot + PoliticsSystem.
/// </summary>
public sealed class PoliticsPanel
{
    public bool IsOpen { get; set; }

    /// <summary>Reference to PoliticsSystem for faction data, laws, protests, corruption.</summary>
    public PoliticsSystem? Politics { get; set; }

    /// <summary>Reference to CommandQueue for law toggle commands.</summary>
    public CommandQueue? Commands { get; set; }

    private static readonly string[] FactionNames =
    {
        "Business Owners", "Workers", "Property Owners",
        "Intelligentsia", "Religious", "Newcomers"
    };

    private static readonly Vector4[] FactionColors =
    {
        new(0.9f, 0.7f, 0.2f, 1f),  // Business - gold
        new(0.3f, 0.6f, 0.9f, 1f),  // Workers - blue
        new(0.3f, 0.8f, 0.3f, 1f),  // Property - green
        new(0.7f, 0.4f, 0.9f, 1f),  // Intelligentsia - purple
        new(0.9f, 0.5f, 0.2f, 1f),  // Religious - orange
        new(0.4f, 0.8f, 0.8f, 1f),  // Newcomers - teal
    };

    public void Draw(SimSnapshot snapshot)
    {
        if (!IsOpen) return;

        var io = ImGui.GetIO();
        float panelWidth = 440;
        float panelHeight = 540;
        float x = (io.DisplaySize.X - panelWidth) * 0.5f;
        float y = 60;

        ImGui.SetNextWindowPos(new Vector2(x, y), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(panelWidth, panelHeight), ImGuiCond.FirstUseEver);

        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.1f, 0.1f, 0.14f, 0.96f));
        ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(0.2f, 0.12f, 0.15f, 1f));
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(0.3f, 0.15f, 0.2f, 1f));

        bool open = IsOpen;
        if (ImGui.Begin("Politics [G]", ref open))
        {
            IsOpen = open;

            DrawApprovalGauge(snapshot);
            ImGui.Separator();
            DrawFactionBars();
            ImGui.Separator();
            DrawActiveLaws();
            ImGui.Separator();
            DrawElectionAndStatus(snapshot);
        }
        else
        {
            IsOpen = open;
        }
        ImGui.End();

        ImGui.PopStyleColor(3);
    }

    private void DrawApprovalGauge(SimSnapshot snapshot)
    {
        float approval = snapshot.ApprovalRating * 100f;

        ImGui.Text("Mayor Approval Rating");
        ImGui.Spacing();

        // Large approval display
        Vector4 approvalColor;
        string rating;
        if (approval >= 70f)
        {
            approvalColor = new Vector4(0.3f, 0.9f, 0.3f, 1f);
            rating = "Beloved";
        }
        else if (approval >= 50f)
        {
            approvalColor = new Vector4(0.6f, 0.9f, 0.3f, 1f);
            rating = "Popular";
        }
        else if (approval >= 35f)
        {
            approvalColor = new Vector4(0.9f, 0.9f, 0.2f, 1f);
            rating = "Tolerated";
        }
        else if (approval >= 20f)
        {
            approvalColor = new Vector4(0.9f, 0.5f, 0.2f, 1f);
            rating = "Unpopular";
        }
        else
        {
            approvalColor = new Vector4(0.9f, 0.2f, 0.2f, 1f);
            rating = "Despised";
        }

        ImGui.PushStyleColor(ImGuiCol.Text, approvalColor);
        ImGui.SetWindowFontScale(1.4f);
        ImGui.Text($"{approval:F0}%");
        ImGui.SetWindowFontScale(1.0f);
        ImGui.PopStyleColor();

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.7f, 1f));
        ImGui.Text(rating);
        ImGui.PopStyleColor();

        // Approval progress bar
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, approvalColor);
        ImGui.ProgressBar(snapshot.ApprovalRating, new Vector2(-1, 16), "");
        ImGui.PopStyleColor();
    }

    private void DrawFactionBars()
    {
        ImGui.Text("Factions");
        ImGui.Spacing();

        if (Politics == null)
        {
            ImGui.Text("No political data available.");
            return;
        }

        // Calculate max clout for scaling bars
        float maxClout = 1f;
        for (int i = 0; i < PoliticsSystem.FactionCount; i++)
        {
            float clout = Politics.Factions[i].Clout;
            if (clout > maxClout) maxClout = clout;
        }

        for (int i = 0; i < PoliticsSystem.FactionCount; i++)
        {
            ref var faction = ref Politics.Factions[i];
            float clout = faction.Clout;
            float satisfaction = faction.Satisfaction;
            int seats = Politics.GetFactionSeatCount(i);

            // Faction name with color
            ImGui.PushStyleColor(ImGuiCol.Text, FactionColors[i]);
            ImGui.Text(faction.Name ?? FactionNames[i]);
            ImGui.PopStyleColor();

            // Clout bar
            ImGui.SameLine(150);
            float cloutFraction = maxClout > 0 ? clout / maxClout : 0f;
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, FactionColors[i]);
            ImGui.ProgressBar(cloutFraction, new Vector2(120, 14), $"{clout:F0}");
            ImGui.PopStyleColor();

            // Satisfaction indicator
            ImGui.SameLine();
            Vector4 satColor = satisfaction >= 0.6f
                ? new Vector4(0.3f, 0.9f, 0.3f, 1f)
                : satisfaction >= 0.4f
                    ? new Vector4(0.9f, 0.9f, 0.2f, 1f)
                    : new Vector4(0.9f, 0.3f, 0.3f, 1f);

            ImGui.PushStyleColor(ImGuiCol.Text, satColor);
            ImGui.Text($"{satisfaction * 100f:F0}%");
            ImGui.PopStyleColor();

            // Council seats
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.6f, 1f));
            ImGui.Text($"{seats}s");
            ImGui.PopStyleColor();
        }

        // Council composition summary
        ImGui.Spacing();
        ImGui.Text("Council: ");
        ImGui.SameLine();
        for (int i = 0; i < PoliticsSystem.CouncilSeatCount; i++)
        {
            byte factionId = Politics.CouncilSeats[i];
            if (factionId < FactionColors.Length)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, FactionColors[factionId]);
                ImGui.Text("[*]");
                ImGui.PopStyleColor();
            }
            else
            {
                ImGui.Text("[?]");
            }
            if (i < PoliticsSystem.CouncilSeatCount - 1) ImGui.SameLine(0, 2);
        }
    }

    private void DrawActiveLaws()
    {
        ImGui.Text("Active Laws");
        ImGui.Spacing();

        if (Politics == null)
        {
            ImGui.Text("No political system.");
            return;
        }

        if (ImGui.BeginChild("LawsList", new Vector2(-1, 120), ImGuiChildFlags.Borders))
        {
            int enactedCount = 0;
            for (int i = 0; i < Politics.LawCount; i++)
            {
                ref var law = ref Politics.Laws[i];
                if (law.Status != PoliticsSystem.LawStatus.Enacted) continue;

                enactedCount++;

                // Law name
                ImGui.Text(law.Name ?? $"Law #{law.Id}");

                // Repeal button
                ImGui.SameLine(ImGui.GetWindowWidth() - 70);
                if (ImGui.SmallButton($"Repeal##law_{i}"))
                {
                    if (Commands != null)
                    {
                        Commands.EnqueueRepealLaw(i);
                    }
                }

                // Description tooltip
                if (ImGui.IsItemHovered() && law.Description != null)
                {
                    ImGui.BeginTooltip();
                    ImGui.Text(law.Description);
                    ImGui.Text($"Proposed by: {GetFactionName(law.ProposedByFaction)}");
                    ImGui.Text($"Votes: {law.VotesFor} for / {law.VotesAgainst} against");
                    if (law.Effects != null)
                    {
                        ImGui.Separator();
                        ImGui.Text("Effects:");
                        foreach (var effect in law.Effects)
                        {
                            string sign = effect.Modifier >= 0 ? "+" : "";
                            ImGui.Text($"  {effect.ParameterName}: {sign}{effect.Modifier:F2}");
                        }
                    }
                    ImGui.EndTooltip();
                }
            }

            if (enactedCount == 0)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.4f, 0.4f, 0.5f, 1f));
                ImGui.Text("No laws currently enacted.");
                ImGui.PopStyleColor();
            }
        }
        ImGui.EndChild();
    }

    private void DrawElectionAndStatus(SimSnapshot snapshot)
    {
        // Election countdown
        int currentYear = 2024;
        if (snapshot.DateString.Length >= 4)
        {
            int.TryParse(snapshot.DateString.AsSpan(0, 4), out currentYear);
        }

        int yearsUntilElection = snapshot.NextElectionYear - currentYear;

        ImGui.Text("Next Election:");
        ImGui.SameLine();
        if (yearsUntilElection <= 1)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.9f, 0.9f, 0.2f, 1f));
            ImGui.Text($"Year {snapshot.NextElectionYear} (SOON!)");
            ImGui.PopStyleColor();
        }
        else
        {
            ImGui.Text($"Year {snapshot.NextElectionYear} ({yearsUntilElection} years)");
        }

        ImGui.Spacing();

        // Protest status
        ImGui.Text("Protests:");
        ImGui.SameLine();
        if (Politics != null)
        {
            var phase = Politics.CurrentProtestPhase;
            if (phase == PoliticsSystem.ProtestPhase.None)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.3f, 0.9f, 0.3f, 1f));
                ImGui.Text("None - City is calm");
                ImGui.PopStyleColor();
            }
            else
            {
                Vector4 protestColor = phase switch
                {
                    PoliticsSystem.ProtestPhase.Complaint => new Vector4(0.9f, 0.9f, 0.2f, 1f),
                    PoliticsSystem.ProtestPhase.Petition => new Vector4(0.9f, 0.7f, 0.2f, 1f),
                    PoliticsSystem.ProtestPhase.Rally => new Vector4(0.9f, 0.5f, 0.2f, 1f),
                    PoliticsSystem.ProtestPhase.Protest => new Vector4(0.9f, 0.3f, 0.2f, 1f),
                    PoliticsSystem.ProtestPhase.Riot => new Vector4(1f, 0.1f, 0.1f, 1f),
                    _ => new Vector4(0.7f, 0.7f, 0.7f, 1f)
                };

                ImGui.PushStyleColor(ImGuiCol.Text, protestColor);
                ImGui.Text($"{phase} (Day {Politics.ProtestDaysAtCurrentPhase}/{PoliticsSystem.MaxProtestEscalationDays})");
                ImGui.PopStyleColor();

                if (phase >= PoliticsSystem.ProtestPhase.Rally)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.9f, 0.3f, 0.3f, 1f));
                    ImGui.TextWrapped("WARNING: Unrest is escalating. Improve approval to calm citizens.");
                    ImGui.PopStyleColor();
                }
            }
        }
        else
        {
            ImGui.Text("Unknown");
        }

        ImGui.Spacing();

        // Corruption index
        ImGui.Text("Corruption Index:");
        ImGui.SameLine();
        if (Politics != null)
        {
            float corruption = Politics.CorruptionIndex;
            Vector4 corrColor;
            string corrLabel;

            if (corruption < 15f)
            {
                corrColor = new Vector4(0.3f, 0.9f, 0.3f, 1f);
                corrLabel = "Clean";
            }
            else if (corruption < 30f)
            {
                corrColor = new Vector4(0.6f, 0.9f, 0.3f, 1f);
                corrLabel = "Minor";
            }
            else if (corruption < 50f)
            {
                corrColor = new Vector4(0.9f, 0.9f, 0.2f, 1f);
                corrLabel = "Moderate";
            }
            else if (corruption < 70f)
            {
                corrColor = new Vector4(0.9f, 0.5f, 0.2f, 1f);
                corrLabel = "Widespread";
            }
            else
            {
                corrColor = new Vector4(0.9f, 0.2f, 0.2f, 1f);
                corrLabel = "Systemic";
            }

            ImGui.PushStyleColor(ImGuiCol.Text, corrColor);
            ImGui.Text($"{corruption:F0}/100 ({corrLabel})");
            ImGui.PopStyleColor();

            // Corruption breakdown
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.6f, 1f));
            ImGui.Text($"  Lobby: {Politics.LobbyAcceptance:F1} | " +
                       $"Transparency: {Politics.TransparencyLevel:F1} | " +
                       $"Media: {Politics.MediaFreedom:F1}");
            ImGui.PopStyleColor();

            // Scandal level
            if (Politics.ScandalLevel > 0.01f)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.9f, 0.3f, 0.3f, 1f));
                ImGui.Text($"  Active Scandal: {Politics.ScandalLevel * 100f:F0}% severity");
                ImGui.PopStyleColor();
            }
        }
        else
        {
            ImGui.Text("Unknown");
        }
    }

    private static string GetFactionName(int factionId)
    {
        if (factionId < 0) return "Mayor";
        if (factionId < FactionNames.Length) return FactionNames[factionId];
        return $"Faction #{factionId}";
    }
}
