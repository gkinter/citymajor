using Forge.SimCore;
using Xunit;

namespace Forge.SimCore.Tests;

/// <summary>Characterization tests for Cathedral P2 housing / rent burden (pinned before implementation).</summary>
public sealed class CathedralHousingTests
{
    [Fact]
    public void RentBurden_Increases_WhenResidentialDemandHighAndSupplyLow()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        host.State.Population = 1200;
        host.RestoreHouseholds(1200, 300);

        int center = 32;
        for (int y = center - 4; y <= center + 4; y++)
        {
            host.PlaceRoad(center, y);
            for (int x = center - 4; x <= center + 4; x++)
                host.PaintZone(x, y, zoneType: 3); // commercial only — no residential supply
        }

        for (int i = 0; i < 90; i++)
            host.Tick(1.0);

        var snap = host.GetSnapshot();
        Assert.True(snap.MeanRentBurden > 0.35f,
            "Expected elevated rent burden when housing supply lags population demand.");
    }

    [Fact]
    public void MigrationOut_WhenRentBurdenAboveThreshold()
    {
        var host = new SimHost();
        host.Init(64, new SimHostInitOptions { SkipStarterCity = true });

        host.State.Population = 800;
        host.RestoreHouseholds(800, 200);

        int popBefore = host.State.Population;

        for (int i = 0; i < 180; i++)
            host.Tick(1.0);

        Assert.True(host.State.Population < popBefore,
            "Expected net emigration when rent burden exceeds threshold for multiple months.");
    }
}
