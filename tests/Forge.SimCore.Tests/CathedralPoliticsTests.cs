using System.Text.Json;
using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Forge.SimCore;
using Forge.SimWasm;
using Xunit;

namespace Forge.SimCore.Tests;

/// <summary>
/// Characterization tests for Cathedral P5 politics foundation (program P6 Governance).
/// Pins approval export + Herald delta bridge before faction/Herald-predicate deepen.
/// </summary>
public sealed class CathedralPoliticsTests
{
    private const string ApprovalUnrestEventsJson = """
    [
      {
        "id": "approval_unrest",
        "name": "Approval Unrest",
        "category": "political",
        "era_min": "frontier",
        "severity": 2,
        "base_probability": 0,
        "duration_months": 6,
        "effects": { "happiness": -8, "approvalRatingPenalty": -5 },
        "description": "Mayor approval slipped below the unrest threshold."
      }
    ]
    """;

    [Fact]
    public void ApplyApprovalDelta_MovesSnapshotApproval()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        float before = host.GetSnapshot().ApprovalRating;
        host.ApplyApprovalDelta(10f);
        float after = host.GetSnapshot().ApprovalRating;

        Assert.Equal(before + 0.10f, after, precision: 3);
        Assert.InRange(after, 0f, 1f);
    }

    [Fact]
    public void SnapshotDto_Approval_IsPercentScale()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        float world01 = host.State!.ApprovalRating;
        var dto = SimSnapshotDto.From(host.GetSnapshot(), host.State);

        Assert.Equal(world01 * 100f, dto.Approval, precision: 2);
        Assert.InRange(dto.Approval, 0f, 100f);

        host.ApplyApprovalDelta(-5f);
        dto = SimSnapshotDto.From(host.GetSnapshot(), host.State);
        Assert.Equal(host.State.ApprovalRating * 100f, dto.Approval, precision: 2);
    }

    [Fact]
    public void GetSnapshotJson_IncludesApprovalField()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        using var doc = JsonDocument.Parse(host.GetSnapshotJson());
        Assert.True(doc.RootElement.TryGetProperty("approval", out var approval));
        Assert.Equal(JsonValueKind.Number, approval.ValueKind);
        Assert.InRange(approval.GetSingle(), 0f, 100f);
    }

    [Fact]
    public void HeraldUnrestBucket_RequiresApprovalBelowThreshold()
    {
        // Snapshot predicate: unrest only when approval < 40 (Politics escalation band).
        Assert.True(ApprovalHeraldSystem.IsUnrestBucket(39f));
        Assert.False(ApprovalHeraldSystem.IsUnrestBucket(40f));
        Assert.False(ApprovalHeraldSystem.IsUnrestBucket(60f));

        var state = new WorldState(32);
        var events = new EventSystem(seed: 11);
        events.LoadDefinitionsFromJson(ApprovalUnrestEventsJson);
        var herald = new ApprovalHeraldSystem();

        // Healthy approval — must not fire unrest Herald (no false positive).
        state.ApprovalRating = 0.55f;
        herald.MonthlyTick(state, events);
        herald.MonthlyTick(state, events);
        Assert.False(
            events.IsEventTypeActive(ApprovalHeraldSystem.UnrestEventTypeId),
            "Expected no approval_unrest Herald when approval ≥ 40%.");

        // Sustained low approval — unrest Herald after consecutive months.
        state.ApprovalRating = 0.35f;
        herald.MonthlyTick(state, events);
        Assert.False(events.IsEventTypeActive(ApprovalHeraldSystem.UnrestEventTypeId));
        herald.MonthlyTick(state, events);
        Assert.True(
            events.IsEventTypeActive(ApprovalHeraldSystem.UnrestEventTypeId),
            "Expected approval_unrest Herald when approval stays below 40%.");
    }

    [Fact]
    public void WasmStatus_ExportsCouncilSeats()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        Assert.Equal(PoliticsSystem.CouncilSeatCount, host.Politics.CouncilSeats.Length);
        Assert.Equal(PoliticsSystem.CouncilSeatCount, host.State!.CouncilSeats.Length);

        using var doc = JsonDocument.Parse(host.GetSnapshotJson());
        Assert.True(doc.RootElement.TryGetProperty("councilSeats", out var seats));
        Assert.Equal(JsonValueKind.Array, seats.ValueKind);
        Assert.Equal(PoliticsSystem.CouncilSeatCount, seats.GetArrayLength());

        var expected = new int[PoliticsSystem.CouncilSeatCount];
        for (int i = 0; i < expected.Length; i++)
            expected[i] = host.Politics.CouncilSeats[i];

        var actual = new int[seats.GetArrayLength()];
        int idx = 0;
        foreach (var seat in seats.EnumerateArray())
        {
            Assert.Equal(JsonValueKind.Number, seat.ValueKind);
            Assert.InRange(seat.GetInt32(), 0, PoliticsSystem.FactionCount - 1);
            actual[idx++] = seat.GetInt32();
        }

        Assert.Equal(expected, actual);

        var dto = SimSnapshotDto.From(host.GetSnapshot(), host.State);
        Assert.Equal(expected, dto.CouncilSeats);
    }
}
