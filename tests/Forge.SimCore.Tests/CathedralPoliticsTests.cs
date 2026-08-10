using System.Text.Json;
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
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        // Neutral RCI / goods so approval alone drives the unrest bucket.
        const float healthcare = 0.5f;
        const float zeroDemand = 0f;
        const float zeroGoods = 0f;

        float approvalPct = host.State!.ApprovalRating * 100f;
        float lift = HeraldNarrativePredicates.LowHappinessApproval - approvalPct + 10f;
        if (lift > 0f)
            host.ApplyApprovalDelta(lift);

        var calm = SimSnapshotDto.From(host.GetSnapshot(), host.State);
        Assert.False(HeraldNarrativePredicates.IsUnrestApproval(calm.Approval));
        Assert.NotEqual(
            HeraldNarrativeBucket.HappinessLow,
            HeraldNarrativePredicates.DeriveBucket(
                healthcare,
                calm.Approval,
                Math.Max(0L, calm.CityFunds),
                zeroDemand,
                zeroDemand,
                zeroDemand,
                zeroGoods));

        float drop = calm.Approval - HeraldNarrativePredicates.LowHappinessApproval + 5f;
        host.ApplyApprovalDelta(-drop);

        var unrest = SimSnapshotDto.From(host.GetSnapshot(), host.State);
        Assert.True(HeraldNarrativePredicates.IsUnrestApproval(unrest.Approval));
        Assert.Equal(
            HeraldNarrativeBucket.HappinessLow,
            HeraldNarrativePredicates.DeriveBucket(
                healthcare,
                unrest.Approval,
                Math.Max(0L, unrest.CityFunds),
                zeroDemand,
                zeroDemand,
                zeroDemand,
                zeroGoods));
    }

    [Fact]
    public void WasmStatus_ExportsCouncilSeats()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        var dto = SimSnapshotDto.From(host.GetSnapshot(), host.State!);
        Assert.Equal(PoliticsSystem.CouncilSeatCount, dto.CouncilSeats.Length);
        Assert.All(dto.CouncilSeats, seat =>
            Assert.InRange(seat, 0, PoliticsSystem.FactionCount - 1));

        using var doc = JsonDocument.Parse(host.GetSnapshotJson());
        Assert.True(doc.RootElement.TryGetProperty("councilSeats", out var seats));
        Assert.Equal(JsonValueKind.Array, seats.ValueKind);
        Assert.Equal(PoliticsSystem.CouncilSeatCount, seats.GetArrayLength());
    }
}
