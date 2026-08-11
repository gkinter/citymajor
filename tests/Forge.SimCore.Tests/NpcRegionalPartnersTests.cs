using Forge.Game.Simulation;
using Forge.SimCore;
using Forge.SimWasm;
using Xunit;

namespace Forge.SimCore.Tests;

/// <summary>SB-3728 — regional map NPC partner markers for bilateral trade.</summary>
public sealed class NpcRegionalPartnersTests
{
    [Fact]
    public void Catalog_HasFiveNpcTowns_WithDistinctSpecialties()
    {
        Assert.Equal(5, NpcRegionalPartners.Count);
        Assert.Equal(5, NpcRegionalPartners.All.Count);

        Assert.True(NpcRegionalPartners.TryGet(0, out var coal));
        Assert.Equal("Coal Ridge", coal.Name);
        Assert.Equal(Good.Coal, coal.ExportSpecialty);
        Assert.Equal(Good.Food, coal.ImportDemand);

        Assert.True(NpcRegionalPartners.TryGet(1, out var harbor));
        Assert.Equal("Harbor Vale", harbor.Name);
        Assert.Equal(Good.Electronics, harbor.ExportSpecialty);

        Assert.False(NpcRegionalPartners.IsKnownPartner(-1));
        Assert.False(NpcRegionalPartners.IsKnownPartner(5));
        Assert.False(NpcRegionalPartners.TryGet(99, out _));
    }

    [Fact]
    public void Markers_LieOutsidePlayerClaimCenter()
    {
        foreach (var m in NpcRegionalPartners.All)
        {
            Assert.InRange(m.RegionalX, 0f, 1f);
            Assert.InRange(m.RegionalY, 0f, 1f);
            // Not sitting on the player claim centroid
            float dx = m.RegionalX - NpcRegionalPartners.PlayerCenterX;
            float dy = m.RegionalY - NpcRegionalPartners.PlayerCenterY;
            Assert.True(MathF.Sqrt(dx * dx + dy * dy) > 0.15f, m.Name);
        }
    }

    [Fact]
    public void FreightBaseMonths_FromRegionalDistance_ClampsOneToThree()
    {
        for (int id = 0; id < NpcRegionalPartners.Count; id++)
        {
            int months = NpcRegionalPartners.FreightBaseMonths(id);
            Assert.InRange(months, 1, 3);
        }

        Assert.Equal(1, NpcRegionalPartners.FreightBaseMonths(-1));
        // Harbor Vale is mid-range east → base 2
        Assert.Equal(2, NpcRegionalPartners.FreightBaseMonths(1));
        // Ironhaven / Coal Ridge farther → base 3
        Assert.Equal(3, NpcRegionalPartners.FreightBaseMonths(2));
        Assert.Equal(3, NpcRegionalPartners.FreightBaseMonths(0));
    }

    [Fact]
    public void ComputeFreightMonths_UsesRegionalDistancePlusCongestion()
    {
        // Harbor Vale base 2 + delay 1 → +2 → 4
        Assert.Equal(4, TradeSystem.ComputeFreightMonths(1, 1f));
        // Grain Crossing base 2 + delay 0 → 2
        Assert.Equal(2, TradeSystem.ComputeFreightMonths(3, 0f));
        // Ironhaven base 3 + delay 1 → 5 (clamp)
        Assert.Equal(5, TradeSystem.ComputeFreightMonths(2, 1f));
        Assert.Equal(1, TradeSystem.ComputeFreightMonths(-1, 1f));
    }

    [Fact]
    public void SimHost_PublishesNpcPartnerCount_AndRejectsUnknownPartner()
    {
        var host = new SimHost();
        host.Init(32, new SimHostInitOptions { SkipStarterCity = true });

        Assert.Equal(NpcRegionalPartners.Count, host.State!.NpcPartnerCount);
        Assert.Equal(NpcRegionalPartners.Count, host.GetNpcPartners().Count);

        Assert.False(host.CreateBilateralTradeRoute(99, Good.Steel, 50f, 10f, 12));
        Assert.Equal(0, host.State.BilateralRouteCount);

        Assert.True(host.CreateBilateralTradeRoute(0, Good.Coal, -40f, 0f, 12)); // import coal from Ridge
        Assert.Equal(1, host.State.BilateralRouteCount);
        Assert.Equal(NpcRegionalPartners.Count, host.State.NpcPartnerCount);

        var snap = host.GetSnapshot();
        Assert.Equal(NpcRegionalPartners.Count, snap.NpcPartnerCount);
        Assert.Equal(1, snap.BilateralRouteCount);

        var dto = SimSnapshotDto.From(snap, host.State);
        Assert.Equal(NpcRegionalPartners.Count, dto.NpcPartnerCount);
    }
}
