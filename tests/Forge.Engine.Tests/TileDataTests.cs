using Forge.Engine.Data;
using Xunit;

namespace Forge.Engine.Tests;

public class TileDataTests
{
    [Fact]
    public void Constructor_CorrectSize()
    {
        var tiles = new TileData(256);
        Assert.Equal(256, tiles.Size);
        Assert.Equal(256 * 256, tiles.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4097)]
    [InlineData(-1)]
    public void Constructor_InvalidSize_Throws(int size)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TileData(size));
    }

    [Fact]
    public void Index_And_FromIndex_RoundTrip()
    {
        var tiles = new TileData(128);
        int idx = tiles.Index(50, 75);
        var (x, y) = tiles.FromIndex(idx);
        Assert.Equal(50, x);
        Assert.Equal(75, y);
    }

    [Fact]
    public void InBounds_Correct()
    {
        var tiles = new TileData(64);
        Assert.True(tiles.InBounds(0, 0));
        Assert.True(tiles.InBounds(63, 63));
        Assert.False(tiles.InBounds(-1, 0));
        Assert.False(tiles.InBounds(0, 64));
        Assert.False(tiles.InBounds(64, 0));
    }

    [Fact]
    public void IsBuildable_DefaultTerrain_True()
    {
        var tiles = new TileData(64);
        // Default terrain is grass (0), which is buildable
        Assert.True(tiles.IsBuildable(10, 10));
    }

    [Fact]
    public void IsBuildable_Water_False()
    {
        var tiles = new TileData(64);
        tiles.TerrainType[tiles.Index(5, 5)] = 3; // Water
        Assert.False(tiles.IsBuildable(5, 5));
    }

    [Fact]
    public void IsBuildable_ExistingBuilding_False()
    {
        var tiles = new TileData(64);
        tiles.BuildingId[tiles.Index(5, 5)] = 1;
        Assert.False(tiles.IsBuildable(5, 5));
    }

    [Fact]
    public void IsAreaBuildable_AllGrass_True()
    {
        var tiles = new TileData(64);
        Assert.True(tiles.IsAreaBuildable(10, 10, 3, 3));
    }

    [Fact]
    public void IsAreaBuildable_OneWaterTile_False()
    {
        var tiles = new TileData(64);
        tiles.TerrainType[tiles.Index(11, 11)] = 3; // Water in the middle
        Assert.False(tiles.IsAreaBuildable(10, 10, 3, 3));
    }

    [Fact]
    public void DefaultValues_Correct()
    {
        var tiles = new TileData(32);
        // Default terrain: grass (0)
        Assert.Equal(0, tiles.TerrainType[0]);
        // Default elevation: 20 (~10m above sea level, ushort)
        Assert.Equal((ushort)20, tiles.Elevation[0]);
        // Default desirability: neutral (0.0f, float range -1 to +1)
        Assert.Equal(0.0f, tiles.Desirability[0]);
    }
}
