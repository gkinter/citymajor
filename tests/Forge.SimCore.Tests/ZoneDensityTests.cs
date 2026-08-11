using Forge.Game.Simulation;
using Forge.SimCore;
using Xunit;

namespace Forge.SimCore.Tests;

/// <summary>P2.2 — zone density brush + ZoneGrowthSystem occupant scaling (CATHEDRAL_P2).</summary>
public sealed class ZoneDensityTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void PaintZone_WithDensity_SetsZoneDensityTileField(byte density)
    {
        var host = new SimHost();
        host.Init(32, new SimHostInitOptions { SkipStarterCity = true });

        host.PaintZone(10, 10, zoneType: 1, density: density);

        int idx = host.State.Tiles.Index(10, 10);
        Assert.Equal(density, host.State.Tiles.ZoneDensity[idx]);
        Assert.Equal((byte)1, host.State.Tiles.ZoneType[idx]);
    }

    [Fact]
    public void PaintZone_WithDefaultDensity_UsesLow()
    {
        var host = new SimHost();
        host.Init(32, new SimHostInitOptions { SkipStarterCity = true });

        host.PaintZone(8, 8, zoneType: 3);

        int idx = host.State.Tiles.Index(8, 8);
        Assert.Equal((byte)1, host.State.Tiles.ZoneDensity[idx]);
    }

    [Fact]
    public void PaintZone_ClearZone_ZeroesDensity()
    {
        var host = new SimHost();
        host.Init(32, new SimHostInitOptions { SkipStarterCity = true });

        host.PaintZone(5, 5, zoneType: 1, density: 3);
        host.PaintZone(5, 5, zoneType: 0);

        int idx = host.State.Tiles.Index(5, 5);
        Assert.Equal((byte)0, host.State.Tiles.ZoneDensity[idx]);
        Assert.Equal((byte)0, host.State.Tiles.ZoneType[idx]);
    }

    /// <summary>
    /// P2.1 — PaintZone accepts office zone byte (5). Employment still requires edu≥3
    /// (see PopulationSystemTests); UI era-gates office at Industrial (zone-tiers).
    /// </summary>
    [Fact]
    public void PaintZone_OfficeZone_SetsTypeAndDensity()
    {
        var host = new SimHost();
        host.Init(32, new SimHostInitOptions { SkipStarterCity = true });

        host.PaintZone(12, 12, zoneType: 5, density: 2); // Office

        int idx = host.State.Tiles.Index(12, 12);
        Assert.Equal((byte)5, host.State.Tiles.ZoneType[idx]);
        Assert.Equal((byte)2, host.State.Tiles.ZoneDensity[idx]);
    }

    /// <summary>
    /// U3.4 / P2.1 — Park zone byte (8) paints and stores density; no organic growth
    /// (GetDemandForZone returns 0). Land-value bonus uses nearby park zones.
    /// </summary>
    [Fact]
    public void PaintZone_ParkZone_SetsTypeAndDensity()
    {
        var host = new SimHost();
        host.Init(32, new SimHostInitOptions { SkipStarterCity = true });

        host.PaintZone(14, 14, zoneType: 8, density: 1); // Park

        int idx = host.State.Tiles.Index(14, 14);
        Assert.Equal((byte)8, host.State.Tiles.ZoneType[idx]);
        Assert.Equal((byte)1, host.State.Tiles.ZoneDensity[idx]);
    }

    [Theory]
    [InlineData(1)] // Residential low
    [InlineData(2)] // Residential high
    [InlineData(3)] // Commercial
    [InlineData(4)] // Industrial
    public void CalculateMaxOccupants_Density3_DoublesEachStepVsDensity1(byte zoneType)
    {
        // CATHEDRAL_P2 §4: low ×1, medium ×2, high ×4 — each density step doubles.
        // Acceptance: "Density brush doubles max occupants at high vs low"
        // (density 3 is double×double vs density 1).
        ushort low = ZoneGrowthSystem.CalculateMaxOccupants(zoneType, density: 1);
        ushort medium = ZoneGrowthSystem.CalculateMaxOccupants(zoneType, density: 2);
        ushort high = ZoneGrowthSystem.CalculateMaxOccupants(zoneType, density: 3);

        Assert.True(low > 0, "base occupants must be positive");
        Assert.Equal((ushort)(low * 2), medium);
        Assert.Equal((ushort)(medium * 2), high);
        Assert.Equal((ushort)(low * 4), high);
    }

    [Fact]
    public void CalculateMaxOccupants_Density0_MatchesDensity1()
    {
        Assert.Equal(
            ZoneGrowthSystem.CalculateMaxOccupants(1, density: 1),
            ZoneGrowthSystem.CalculateMaxOccupants(1, density: 0));
    }
}
