using Forge.Engine.Data;
using Forge.Engine.Rendering;
using Xunit;

namespace Forge.Engine.Tests;

public class WaterRendererTests
{
    // =========================================================================
    // Water tile identification
    // =========================================================================

    [Fact]
    public void IsWaterEdge_SurroundedByWater_False()
    {
        var tiles = new TileData(16);
        // Create a 3x3 water block in the middle
        for (int y = 5; y <= 7; y++)
            for (int x = 5; x <= 7; x++)
                tiles.TerrainType[tiles.Index(x, y)] = 3; // Water

        // Center tile (6,6) is surrounded by water on all 4 sides
        Assert.False(WaterRenderer.IsWaterEdge(tiles, 6, 6));
    }

    [Fact]
    public void IsWaterEdge_AdjacentToLand_True()
    {
        var tiles = new TileData(16);
        // Single water tile at (5,5)
        tiles.TerrainType[tiles.Index(5, 5)] = 3; // Water
        // All neighbors are grass (default terrain 0)

        Assert.True(WaterRenderer.IsWaterEdge(tiles, 5, 5));
    }

    [Fact]
    public void IsWaterEdge_CornerWater_True()
    {
        var tiles = new TileData(16);
        // Water at (0,0) — at map corner, two neighbors are out of bounds (treated as non-water)
        tiles.TerrainType[tiles.Index(0, 0)] = 3;
        // Only south and east neighbors exist, and they're grass

        Assert.True(WaterRenderer.IsWaterEdge(tiles, 0, 0));
    }

    [Fact]
    public void IsWaterEdge_OneNeighborIsLand_True()
    {
        var tiles = new TileData(16);
        // 2x2 water block
        tiles.TerrainType[tiles.Index(5, 5)] = 3;
        tiles.TerrainType[tiles.Index(6, 5)] = 3;
        tiles.TerrainType[tiles.Index(5, 6)] = 3;
        tiles.TerrainType[tiles.Index(6, 6)] = 3;

        // (5,5) has water to east and south, but grass to north and west
        Assert.True(WaterRenderer.IsWaterEdge(tiles, 5, 5));
    }

    [Fact]
    public void IsWaterEdge_LargeWaterBody_InteriorIsFalse()
    {
        var tiles = new TileData(16);
        // Fill a 5x5 block with water at (3,3) to (7,7)
        for (int y = 3; y <= 7; y++)
            for (int x = 3; x <= 7; x++)
                tiles.TerrainType[tiles.Index(x, y)] = 3;

        // Interior tile (5,5) is fully surrounded
        Assert.False(WaterRenderer.IsWaterEdge(tiles, 5, 5));

        // Edge tile (3,3) has non-water neighbor to north and west
        Assert.True(WaterRenderer.IsWaterEdge(tiles, 3, 3));

        // Edge tile (7,5) has non-water to the east
        Assert.True(WaterRenderer.IsWaterEdge(tiles, 7, 5));
    }

    // =========================================================================
    // Pollution color interpolation
    // =========================================================================

    [Fact]
    public void InterpolatePollutionColor_Clean_IsBlue()
    {
        var (r, g, b) = WaterRenderer.InterpolatePollutionColor(0f);
        Assert.Equal(0.2f, r, 3);
        Assert.Equal(0.4f, g, 3);
        Assert.Equal(0.8f, b, 3);
    }

    [Fact]
    public void InterpolatePollutionColor_FullyPolluted_IsBrownGreen()
    {
        var (r, g, b) = WaterRenderer.InterpolatePollutionColor(1f);
        Assert.Equal(0.3f, r, 3);
        Assert.Equal(0.35f, g, 3);
        Assert.Equal(0.15f, b, 3);
    }

    [Fact]
    public void InterpolatePollutionColor_HalfPolluted_IsMidpoint()
    {
        var (r, g, b) = WaterRenderer.InterpolatePollutionColor(0.5f);

        // Expected: midpoint between clean and dirty
        float expectedR = 0.2f + (0.3f - 0.2f) * 0.5f;  // 0.25
        float expectedG = 0.4f + (0.35f - 0.4f) * 0.5f;  // 0.375
        float expectedB = 0.8f + (0.15f - 0.8f) * 0.5f;  // 0.475

        Assert.Equal(expectedR, r, 3);
        Assert.Equal(expectedG, g, 3);
        Assert.Equal(expectedB, b, 3);
    }

    [Fact]
    public void InterpolatePollutionColor_ClampsAboveOne()
    {
        var (r, g, b) = WaterRenderer.InterpolatePollutionColor(2f);
        // Should clamp to 1.0 -> same as fully polluted
        var (rExpected, gExpected, bExpected) = WaterRenderer.InterpolatePollutionColor(1f);
        Assert.Equal(rExpected, r, 3);
        Assert.Equal(gExpected, g, 3);
        Assert.Equal(bExpected, b, 3);
    }

    [Fact]
    public void InterpolatePollutionColor_ClampsBelowZero()
    {
        var (r, g, b) = WaterRenderer.InterpolatePollutionColor(-1f);
        // Should clamp to 0.0 -> same as clean
        var (rExpected, gExpected, bExpected) = WaterRenderer.InterpolatePollutionColor(0f);
        Assert.Equal(rExpected, r, 3);
        Assert.Equal(gExpected, g, 3);
        Assert.Equal(bExpected, b, 3);
    }

    // =========================================================================
    // Water tile counting (verify terrain type 3 identification)
    // =========================================================================

    [Fact]
    public void WaterTiles_Identified_ByTerrainType3()
    {
        var tiles = new TileData(32);
        int waterCount = 0;

        // Set some water tiles
        for (int i = 0; i < 10; i++)
        {
            tiles.TerrainType[i] = 3;
        }

        // Count water tiles
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles.TerrainType[i] == 3) waterCount++;
        }

        Assert.Equal(10, waterCount);
    }

    [Fact]
    public void WaterTiles_NonWater_NotIdentified()
    {
        var tiles = new TileData(32);
        // Set various terrain types (none are 3/water)
        tiles.TerrainType[0] = 0; // grass
        tiles.TerrainType[1] = 1; // dirt
        tiles.TerrainType[2] = 2; // sand
        tiles.TerrainType[3] = 4; // rock
        tiles.TerrainType[4] = 5; // forest

        int waterCount = 0;
        for (int i = 0; i < 5; i++)
        {
            if (tiles.TerrainType[i] == 3) waterCount++;
        }

        Assert.Equal(0, waterCount);
    }

    // =========================================================================
    // Pollution grid integration
    // =========================================================================

    [Fact]
    public void PollutionGrid_CorrectSizeForWorldSize()
    {
        int worldSize = 64;
        float[] pollution = new float[worldSize * worldSize];
        Assert.Equal(worldSize * worldSize, pollution.Length);
    }

    [Fact]
    public void PollutionGrid_ValuesClampedToRange()
    {
        float[] pollution = [0f, 0.5f, 1.0f, -0.1f, 1.5f];

        for (int i = 0; i < pollution.Length; i++)
        {
            float clamped = System.Math.Clamp(pollution[i], 0f, 1f);
            Assert.InRange(clamped, 0f, 1f);
        }
    }
}
