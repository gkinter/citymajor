using System.Text.Json;
using Forge.SimCore;
using Forge.SimWasm;
using Xunit;

namespace Forge.SimCore.Tests;

/// <summary>
/// Characterization tests for Cathedral P4 mode-choice stub:
/// WasmTrafficLite flat 65% car → simple 3-mode MNL (car/transit/walk).
/// </summary>
[Collection("SimHost")]
public sealed class CathedralModeChoiceTests
{
    [Fact]
    public void LiteModeShares_ProbabilitiesSumToOne()
    {
        WasmTrafficLite.ComputeLiteModeShares(
            12f, out float pCar, out float pTransit, out float pWalk);

        float sum = pCar + pTransit + pWalk;
        Assert.InRange(sum, 0.999f, 1.001f);
        Assert.True(pCar >= 0f && pTransit >= 0f && pWalk >= 0f);
    }

    [Fact]
    public void LiteModeShares_MidDistance_CarNearFormerFlatShare()
    {
        // Former DefaultCarShare was 0.65; ASC calibrated for ~10–20 tile trips.
        WasmTrafficLite.ComputeLiteModeShares(
            12f, out float pCar, out float pTransit, out float pWalk);

        Assert.InRange(pCar, 0.55f, 0.75f);
        Assert.True(pCar > pTransit);
        Assert.True(pCar > pWalk);
    }

    [Fact]
    public void LiteModeShares_ShortTrip_WalkShareHigherThanLongTrip()
    {
        WasmTrafficLite.ComputeLiteModeShares(
            2f, out _, out _, out float walkShort);
        WasmTrafficLite.ComputeLiteModeShares(
            40f, out _, out _, out float walkLong);

        Assert.True(walkShort > walkLong,
            $"short walk share ({walkShort:F3}) should exceed long ({walkLong:F3})");
        Assert.True(walkShort > 0.05f, "very short trips should retain some walk share");
        Assert.Equal(0f, walkLong, precision: 3);
    }

    [Fact]
    public void LiteModeShares_LongerTrip_RaisesCarRelativeToWalk()
    {
        WasmTrafficLite.ComputeLiteModeShares(
            3f, out float carShort, out _, out float walkShort);
        WasmTrafficLite.ComputeLiteModeShares(
            30f, out float carLong, out _, out float walkLong);

        Assert.True(carLong > carShort || walkLong < walkShort);
        Assert.True(walkShort > walkLong);
    }

    [Fact]
    public void TrafficLite_AfterTick_ExposesValidCityModeShares()
    {
        var host = new SimHost();
        host.Init(64);

        for (int i = 0; i < 12; i++)
            host.Tick(WasmConfig.TrafficLiteInterval);

        float sum = host.Traffic.CarModeShare
            + host.Traffic.TransitModeShare
            + host.Traffic.WalkModeShare;

        Assert.InRange(sum, 0.999f, 1.001f);
        Assert.True(host.Traffic.CarModeShare > 0.4f);
        Assert.True(host.Traffic.CarModeShare < 0.9f);
        Assert.True(host.Traffic.TransitModeShare > 0f);
    }

    [Fact]
    public void TrafficLite_BuildingPairOd_StillDrivesAssignment()
    {
        // P4.1 O-D path must remain intact under the mode-choice stub:
        // household home/work pairs still feed AssignAllOrNothing (mode shares
        // update from real trips; coverage stays ≥90%).
        var host = new SimHost();
        host.Init(64);

        var auditBefore = host.Population.AuditCommuters(host.State);
        Assert.True(auditBefore.AssignedCommuters > 0,
            "starter city bootstrap should assign home/work pairs (P4.1)");
        Assert.True(auditBefore.Coverage >= 0.9f);

        for (int i = 0; i < 8; i++)
            host.Tick(WasmConfig.TrafficLiteInterval);

        // Mode shares prove O-D trips reached the MNL splitter (not gravity-empty).
        Assert.InRange(
            host.Traffic.CarModeShare + host.Traffic.TransitModeShare + host.Traffic.WalkModeShare,
            0.999f, 1.001f);
        Assert.True(host.Traffic.CarModeShare > 0.4f && host.Traffic.CarModeShare < 0.9f);
        Assert.True(host.Traffic.TransitModeShare > 0f);

        var auditAfter = host.Population.AuditCommuters(host.State);
        Assert.Equal(auditBefore.AssignedCommuters, auditAfter.AssignedCommuters);
        Assert.True(auditAfter.Coverage >= 0.9f);
    }

    [Fact]
    public void SnapshotJson_ExportsModeShares()
    {
        var host = new SimHost();
        host.Init(64);
        for (int i = 0; i < 8; i++)
            host.Tick(WasmConfig.TrafficLiteInterval);

        var (car, transit, walk) = host.CollectModeShares();
        Assert.InRange(car + transit + walk, 0.999f, 1.001f);

        var snapDto = JsonSerializer.Deserialize(
            host.GetSnapshotJson(),
            SnapshotJsonContext.Default.SimSnapshotDto);
        Assert.NotNull(snapDto);
        Assert.Equal(car, snapDto!.CarModeShare, precision: 4);
        Assert.Equal(transit, snapDto.TransitModeShare, precision: 4);
        Assert.Equal(walk, snapDto.WalkModeShare, precision: 4);
    }

    [Fact]
    public void DeriveBusCoverage_ZeroLines_IsZeroStub()
    {
        Assert.Equal(0f, WasmTrafficLite.DeriveBusCoverage(0));
        Assert.Equal(0f, WasmTrafficLite.DeriveBusCoverage(-3));
    }

    [Fact]
    public void DeriveBusCoverage_ScalesTowardFullAtRefLines()
    {
        Assert.Equal(0.5f, WasmTrafficLite.DeriveBusCoverage(WasmTrafficLite.BusCoverageRefLines / 2), precision: 3);
        Assert.Equal(1f, WasmTrafficLite.DeriveBusCoverage(WasmTrafficLite.BusCoverageRefLines), precision: 3);
        Assert.Equal(1f, WasmTrafficLite.DeriveBusCoverage(WasmTrafficLite.BusCoverageRefLines * 3), precision: 3);
    }

    [Fact]
    public void LiteModeShares_HigherBusCoverage_RaisesTransitShare()
    {
        WasmTrafficLite.ComputeLiteModeShares(
            12f, busCoverage: 0f, out _, out float transitNone, out _);
        WasmTrafficLite.ComputeLiteModeShares(
            12f, busCoverage: 1f, out _, out float transitFull, out _);

        Assert.True(transitFull > transitNone,
            $"full coverage transit ({transitFull:F3}) should exceed zero coverage ({transitNone:F3})");
        Assert.InRange(transitNone + 0f, 0f, 1f);
        Assert.InRange(transitFull, 0f, 1f);

        WasmTrafficLite.ComputeLiteModeShares(
            12f, busCoverage: 1f, out float car, out float transit, out float walk);
        Assert.InRange(car + transit + walk, 0.999f, 1.001f);
    }

    [Fact]
    public void Snapshot_ExportsTransitLineStubZeros()
    {
        var host = new SimHost();
        host.Init(64);

        Assert.Equal(0, host.State!.TransitLineCount);
        Assert.Equal(0f, host.State.BusCoverage);

        for (int i = 0; i < 4; i++)
            host.Tick(WasmConfig.TrafficLiteInterval);

        // Still no transit graph → stub remains 0 after traffic ticks.
        Assert.Equal(0, host.State.TransitLineCount);
        Assert.Equal(0f, host.State.BusCoverage);

        var snapDto = JsonSerializer.Deserialize(
            host.GetSnapshotJson(),
            SnapshotJsonContext.Default.SimSnapshotDto);
        Assert.NotNull(snapDto);
        Assert.Equal(0, snapDto!.TransitLineCount);
        Assert.Equal(0f, snapDto.BusCoverage, precision: 4);
    }

    [Fact]
    public void TrafficLite_TransitLineCount_RaisesCityTransitShare()
    {
        var hostNone = new SimHost();
        hostNone.Init(64);
        for (int i = 0; i < 8; i++)
            hostNone.Tick(WasmConfig.TrafficLiteInterval);
        float transitNone = hostNone.Traffic.TransitModeShare;

        var hostFull = new SimHost();
        hostFull.Init(64);
        hostFull.State!.TransitLineCount = WasmTrafficLite.BusCoverageRefLines;
        for (int i = 0; i < 8; i++)
            hostFull.Tick(WasmConfig.TrafficLiteInterval);

        Assert.Equal(1f, hostFull.State.BusCoverage, precision: 3);
        Assert.Equal(WasmTrafficLite.BusCoverageRefLines, hostFull.State.TransitLineCount);
        Assert.True(hostFull.Traffic.TransitModeShare > transitNone,
            $"lined transit share ({hostFull.Traffic.TransitModeShare:F3}) should exceed stub ({transitNone:F3})");
        Assert.InRange(
            hostFull.Traffic.CarModeShare
            + hostFull.Traffic.TransitModeShare
            + hostFull.Traffic.WalkModeShare,
            0.999f, 1.001f);
    }
}
