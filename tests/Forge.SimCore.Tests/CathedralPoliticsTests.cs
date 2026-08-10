using System.Text.Json;
using Forge.SimCore;
using Forge.SimWasm;
using Xunit;

namespace Forge.SimCore.Tests;

/// <summary>
/// Characterization tests for Cathedral P5 politics foundation (program P6 Governance).
/// Pins approval export + Herald delta bridge before faction/Herald-predicate deepen.
/// </summary>
[Collection("SimHost")]
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

    [Fact(Skip = "P5.5 Herald snapshot predicates not hard-gated yet")]
    public void HeraldUnrestBucket_RequiresApprovalBelowThreshold()
    {
        // Placeholder: assert sim-metrics / Herald unrest only when approval < 40.
    }

    [Fact(Skip = "P5.6 faction/council seats not exported on WASM status")]
    public void WasmStatus_ExportsCouncilSeats()
    {
        // Placeholder: status.councilSeats length == PoliticsSystem.CouncilSeatCount.
    }
}
