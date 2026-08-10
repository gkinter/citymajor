using System.Text.Json;
using Forge.Game.Simulation;
using Forge.SimCore;
using Forge.SimWasm;
using Xunit;

namespace Forge.SimCore.Tests;

/// <summary>
/// Characterization: Cathedral HUD metrics that are exported on the save API
/// (<see cref="SimHost.GetSnapshotJson"/> / <see cref="SimHost.LoadSnapshotFromJson"/>)
/// must survive snapshot restore — MeanRentBurden, councilSeats, mode shares.
/// </summary>
[Collection("SimHost")]
public sealed class CathedralSaveLoadTests
{
    [Fact]
    public void SnapshotRoundTrip_PreservesMeanRentBurdenCouncilSeatsAndModeShares()
    {
        var host = new SimHost();
        host.Init(64);

        for (int i = 0; i < 8; i++)
            host.Tick(WasmConfig.TrafficLiteInterval);
        for (int i = 0; i < 30; i++)
            host.Tick(1.0);

        // Mutate seats away from boot defaults so restore cannot pass by Init coincidence.
        Assert.Equal(PoliticsSystem.CouncilSeatCount, host.Politics.CouncilSeats.Length);
        host.Politics.CouncilSeats[0] = (byte)PoliticsSystem.FactionId.Intelligentsia;
        host.Politics.CouncilSeats[1] = (byte)PoliticsSystem.FactionId.Religious;
        host.Politics.CouncilSeats[2] = (byte)PoliticsSystem.FactionId.Newcomers;
        host.Politics.SyncCouncilToWorld(host.State!);

        string json = host.GetSnapshotJson();
        var saved = JsonSerializer.Deserialize(json, SnapshotJsonContext.Default.SimSnapshotDto);
        Assert.NotNull(saved);
        Assert.True(saved!.MeanRentBurden > 0f, "expected non-zero MeanRentBurden in save payload");
        Assert.Equal(PoliticsSystem.CouncilSeatCount, saved.CouncilSeats.Length);
        Assert.Equal((int)PoliticsSystem.FactionId.Intelligentsia, saved.CouncilSeats[0]);
        Assert.Equal((int)PoliticsSystem.FactionId.Religious, saved.CouncilSeats[1]);
        Assert.Equal((int)PoliticsSystem.FactionId.Newcomers, saved.CouncilSeats[2]);
        Assert.InRange(
            saved.CarModeShare + saved.TransitModeShare + saved.WalkModeShare,
            0.999f, 1.001f);
        Assert.True(saved.TransitModeShare > 0f || saved.WalkModeShare > 0f,
            "expected non-default mode split so restore is observable");

        using (var doc = JsonDocument.Parse(json))
        {
            Assert.True(doc.RootElement.TryGetProperty("meanRentBurden", out _));
            Assert.True(doc.RootElement.TryGetProperty("councilSeats", out _));
            Assert.True(doc.RootElement.TryGetProperty("carModeShare", out _));
            Assert.True(doc.RootElement.TryGetProperty("transitModeShare", out _));
            Assert.True(doc.RootElement.TryGetProperty("walkModeShare", out _));
        }

        Assert.True(host.LoadSnapshotFromJson(json));

        Assert.Equal(saved.MeanRentBurden, host.State!.MeanRentBurden, precision: 4);
        Assert.Equal(saved.MeanRentBurden, host.Population.MeanRentBurden, precision: 4);

        for (int i = 0; i < PoliticsSystem.CouncilSeatCount; i++)
        {
            Assert.Equal(saved.CouncilSeats[i], host.Politics.CouncilSeats[i]);
            Assert.Equal(saved.CouncilSeats[i], host.State.CouncilSeats[i]);
        }

        var (carAfter, transitAfter, walkAfter) = host.CollectModeShares();
        Assert.Equal(saved.CarModeShare, carAfter, precision: 4);
        Assert.Equal(saved.TransitModeShare, transitAfter, precision: 4);
        Assert.Equal(saved.WalkModeShare, walkAfter, precision: 4);

        var roundTrip = JsonSerializer.Deserialize(
            host.GetSnapshotJson(),
            SnapshotJsonContext.Default.SimSnapshotDto);
        Assert.NotNull(roundTrip);
        Assert.Equal(saved.MeanRentBurden, roundTrip!.MeanRentBurden, precision: 4);
        Assert.Equal(saved.CouncilSeats, roundTrip.CouncilSeats);
        Assert.Equal(saved.CarModeShare, roundTrip.CarModeShare, precision: 4);
        Assert.Equal(saved.TransitModeShare, roundTrip.TransitModeShare, precision: 4);
        Assert.Equal(saved.WalkModeShare, roundTrip.WalkModeShare, precision: 4);
    }
}
