using System.Text.Json;
using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Forge.SimCore;
using Forge.SimWasm;
using Xunit;

namespace Forge.SimCore.Tests;

/// <summary>
/// Cathedral P6 — events/narrative foundation: active event count export for Herald HUD.
/// </summary>
public sealed class CathedralEventsTests
{
    private const string SampleEventsJson = """
    [
      {
        "id": "housing_crisis",
        "name": "Housing Crisis",
        "category": "social",
        "era_min": "frontier",
        "severity": 3,
        "base_probability": 0,
        "duration_months": 6,
        "effects": { "happiness": -15 },
        "description": "Skyrocketing rents push families to the margins."
      },
      {
        "id": "approval_unrest",
        "name": "Approval Unrest",
        "category": "political",
        "era_min": "frontier",
        "severity": 2,
        "base_probability": 0,
        "duration_months": 6,
        "effects": { "happiness": -8 },
        "description": "Mayor approval slipped below the unrest threshold."
      }
    ]
    """;

    [Fact]
    public void ActiveEventCount_MatchesActiveEvents_WhenNoneActive()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        Assert.Equal(0, host.Events.ActiveEventCount);

        var dto = SimSnapshotDto.From(host.GetSnapshot(), host.State!, host.Events);
        Assert.Equal(0, dto.ActiveEventCount);
        Assert.Empty(dto.ActiveEvents);

        using var doc = JsonDocument.Parse(host.GetSnapshotJson());
        Assert.True(doc.RootElement.TryGetProperty("activeEventCount", out var count));
        Assert.Equal(0, count.GetInt32());
    }

    [Fact]
    public void ActiveEventCount_Increments_WhenHeraldEventTriggered()
    {
        var state = new WorldState(32);
        var events = new EventSystem(seed: 19);
        events.LoadDefinitionsFromJson(SampleEventsJson);

        Assert.Equal(0, events.ActiveEventCount);
        Assert.True(events.TriggerEvent("housing_crisis", state, severityOverride: 0.8f));
        Assert.Equal(1, events.ActiveEventCount);
        Assert.True(events.IsEventTypeActive("housing_crisis"));

        Assert.True(events.TriggerEvent("approval_unrest", state, severityOverride: 0.7f));
        Assert.Equal(2, events.ActiveEventCount);

        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });
        // Characterization via DTO path used by WASM snapshot/status export.
        var dto = SimSnapshotDto.From(host.GetSnapshot(), host.State!, events);
        Assert.Equal(2, dto.ActiveEventCount);
        Assert.Equal(2, dto.ActiveEvents.Length);
        Assert.Contains(dto.ActiveEvents, e => e.TypeId == "housing_crisis");
        Assert.Contains(dto.ActiveEvents, e => e.TypeId == "approval_unrest");
    }

    [Fact]
    public void GetSnapshotJson_ExportsActiveEventCountField()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        using var doc = JsonDocument.Parse(host.GetSnapshotJson());
        Assert.True(doc.RootElement.TryGetProperty("activeEventCount", out var count));
        Assert.Equal(JsonValueKind.Number, count.ValueKind);
        Assert.Equal(0, count.GetInt32());

        Assert.True(doc.RootElement.TryGetProperty("activeEvents", out var arr));
        Assert.Equal(JsonValueKind.Array, arr.ValueKind);
        Assert.Equal(count.GetInt32(), arr.GetArrayLength());
    }
}
