using Forge.SimCore;
using Forge.SimWasm;
using Xunit;

namespace Forge.SimCore.Tests;

[Collection("SimHost")]
public sealed class GoodsImbalanceTests
{
    [Fact]
    public void AfterInitAndTicks_SnapshotHasFiniteGoodsIndices()
    {
        var host = new SimHost();
        host.Init(128);

        for (int i = 0; i < 60; i++)
            host.Tick(1.0);

        var snap = host.GetSnapshot();

        Assert.False(float.IsNaN(snap.GoodsShortageIndex));
        Assert.False(float.IsNaN(snap.GoodsSurplusIndex));
        Assert.False(float.IsInfinity(snap.GoodsShortageIndex));
        Assert.False(float.IsInfinity(snap.GoodsSurplusIndex));
        Assert.InRange(snap.GoodsShortageIndex, 0f, 1f);
        Assert.InRange(snap.GoodsSurplusIndex, 0f, 1f);
    }

    [Fact]
    public void GetTopImbalances_ReturnsArrays_AndSnapshotMirrorsState()
    {
        var host = new SimHost();
        host.Init(128);

        for (int i = 0; i < 40; i++)
            host.Tick(1.0);

        var (shortages, surpluses) = host.Economy.GetTopImbalances(5);
        Assert.NotNull(shortages);
        Assert.NotNull(surpluses);

        var snap = host.GetSnapshot();
        Assert.Equal(shortages.Length, snap.ShortageGoods.Length);
        Assert.Equal(surpluses.Length, snap.SurplusGoods.Length);

        for (int i = 0; i < shortages.Length; i++)
        {
            Assert.Equal(shortages[i].GoodId, snap.ShortageGoods[i].GoodId);
            Assert.Equal(shortages[i].Magnitude, snap.ShortageGoods[i].Score, 4);
        }

        for (int i = 0; i < surpluses.Length; i++)
        {
            Assert.Equal(surpluses[i].GoodId, snap.SurplusGoods[i].GoodId);
            Assert.Equal(surpluses[i].Magnitude, snap.SurplusGoods[i].Score, 4);
        }
    }
}
