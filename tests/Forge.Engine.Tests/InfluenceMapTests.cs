using Forge.Engine.Data;
using Xunit;

namespace Forge.Engine.Tests;

public class InfluenceMapTests
{
    // =========================================================================
    // Source add / falloff
    // =========================================================================

    [Fact]
    public void AddSource_LinearFalloff_CorrectValues()
    {
        var map = new InfluenceMap(32, 32, chunkSize: 16);
        map.AddSource(16, 16, strength: 10f, radius: 5f, FalloffType.Linear);
        map.RecalculateAll();

        // Center should have full strength
        Assert.Equal(10f, map.GetValue(16, 16), precision: 3);

        // At distance 2.5 from center, linear: 10 * (1 - 2.5/5) = 5
        // Tile (18, 16) is distance 2, so: 10 * (1 - 2/5) = 6
        float atDist2 = map.GetValue(18, 16);
        Assert.True(atDist2 > 0f && atDist2 < 10f, $"Expected between 0 and 10, got {atDist2}");
        Assert.Equal(6f, atDist2, precision: 2);

        // Beyond radius should be ~0
        float atDist6 = map.GetValue(22, 16);
        Assert.Equal(0f, atDist6, precision: 3);
    }

    [Fact]
    public void AddSource_FlatFalloff_UniformWithinRadius()
    {
        var map = new InfluenceMap(32, 32);
        map.AddSource(10, 10, strength: 5f, radius: 3f, FalloffType.Flat);
        map.RecalculateAll();

        // Inside radius: full strength
        Assert.Equal(5f, map.GetValue(10, 10), precision: 3);
        Assert.Equal(5f, map.GetValue(11, 10), precision: 3);
        Assert.Equal(5f, map.GetValue(10, 12), precision: 3);

        // Outside radius: zero
        Assert.Equal(0f, map.GetValue(14, 10), precision: 3);
    }

    [Fact]
    public void AddSource_QuadraticFalloff_StrongerNearCenter()
    {
        var map = new InfluenceMap(32, 32);
        map.AddSource(16, 16, strength: 8f, radius: 4f, FalloffType.Quadratic);
        map.RecalculateAll();

        float center = map.GetValue(16, 16);
        float atDist1 = map.GetValue(17, 16); // dist=1: 8*(1 - 1/16) = 7.5
        float atDist3 = map.GetValue(19, 16); // dist=3: 8*(1 - 9/16) = 3.5

        Assert.Equal(8f, center, precision: 2);
        Assert.True(atDist1 > atDist3);
        Assert.True(atDist3 > 0f);
    }

    [Fact]
    public void AddSource_ExponentialFalloff_SmoothDecay()
    {
        var map = new InfluenceMap(32, 32);
        map.AddSource(16, 16, strength: 10f, radius: 5f, FalloffType.Exponential);
        map.RecalculateAll();

        float center = map.GetValue(16, 16);
        float atDist1 = map.GetValue(17, 16);
        float atDist4 = map.GetValue(20, 16);

        // Exponential: 10 * e^(-d/5)
        Assert.Equal(10f, center, precision: 2);
        Assert.True(atDist1 < center);
        Assert.True(atDist4 < atDist1);
        Assert.True(atDist4 > 0f); // never quite zero
    }

    [Fact]
    public void AddSource_InverseSquare_NeverReachesZero()
    {
        var map = new InfluenceMap(32, 32);
        map.AddSource(16, 16, strength: 10f, radius: 5f, FalloffType.InverseSquare);
        map.RecalculateAll();

        // InverseSquare extends beyond radius
        float far = map.GetValue(0, 16);
        Assert.True(far > 0f, "InverseSquare should produce nonzero values at distance");
    }

    // =========================================================================
    // Source removal
    // =========================================================================

    [Fact]
    public void RemoveSource_ClearsInfluence()
    {
        var map = new InfluenceMap(16, 16, chunkSize: 8);
        map.AddSource(8, 8, strength: 5f, radius: 3f, FalloffType.Linear);
        map.RecalculateAll();
        Assert.True(map.GetValue(8, 8) > 0f);

        map.RemoveSource(8, 8, strength: 5f, radius: 3f, FalloffType.Linear);
        map.Recalculate();
        Assert.Equal(0f, map.GetValue(8, 8), precision: 5);
    }

    [Fact]
    public void RemoveSource_NonexistentSource_NoChange()
    {
        var map = new InfluenceMap(16, 16);
        map.AddSource(8, 8, strength: 5f, radius: 3f, FalloffType.Linear);
        map.RecalculateAll();
        float before = map.GetValue(8, 8);

        // Remove a source that doesn't exist (different params)
        map.RemoveSource(8, 8, strength: 99f, radius: 1f, FalloffType.Flat);
        map.Recalculate();

        // Nothing should change since no dirty chunks were marked for this specific nonexistent source
        // (Actually remove would have no match so no dirty marking, recalculate sees no dirty)
        Assert.Equal(before, map.GetValue(8, 8), precision: 3);
    }

    // =========================================================================
    // Multiple sources
    // =========================================================================

    [Fact]
    public void MultipleSources_Additive()
    {
        var map = new InfluenceMap(32, 32);
        map.AddSource(10, 10, strength: 5f, radius: 3f, FalloffType.Flat);
        map.AddSource(10, 10, strength: 3f, radius: 3f, FalloffType.Flat);
        map.RecalculateAll();

        Assert.Equal(8f, map.GetValue(10, 10), precision: 3);
    }

    // =========================================================================
    // Dirty tracking
    // =========================================================================

    [Fact]
    public void DirtyTracking_AddSourceMarksDirty()
    {
        var map = new InfluenceMap(128, 128, chunkSize: 64);
        Assert.Equal(0, map.DirtyChunkCount);

        map.AddSource(32, 32, strength: 5f, radius: 10f, FalloffType.Linear);
        Assert.True(map.DirtyChunkCount > 0);
        Assert.True(map.IsChunkDirty(0, 0));
    }

    [Fact]
    public void Recalculate_ClearsDirtyFlags()
    {
        var map = new InfluenceMap(128, 128, chunkSize: 64);
        map.AddSource(32, 32, strength: 5f, radius: 10f, FalloffType.Linear);
        Assert.True(map.DirtyChunkCount > 0);

        map.Recalculate();
        Assert.Equal(0, map.DirtyChunkCount);
    }

    [Fact]
    public void RecalculateAll_ClearsAllDirty()
    {
        var map = new InfluenceMap(128, 128, chunkSize: 64);
        map.MarkAllDirty();
        Assert.Equal(4, map.DirtyChunkCount); // 128/64 = 2x2 = 4 chunks

        map.RecalculateAll();
        Assert.Equal(0, map.DirtyChunkCount);
    }

    // =========================================================================
    // Diffusion
    // =========================================================================

    [Fact]
    public void Diffuse_SpreadsValues()
    {
        var map = new InfluenceMap(8, 8, chunkSize: 8);
        map.SetValue(4, 4, 100f);

        // Before diffusion: neighbors are 0
        Assert.Equal(0f, map.GetValue(5, 4));

        map.Diffuse(rate: 0.2f, decay: 1f);

        // After diffusion: center decreased, neighbors increased
        float center = map.GetValue(4, 4);
        float neighbor = map.GetValue(5, 4);

        Assert.True(center < 100f, "Center should decrease after diffusion");
        Assert.True(neighbor > 0f, "Neighbors should gain value from diffusion");
    }

    [Fact]
    public void Diffuse_WithDecay_ReducesTotal()
    {
        var map = new InfluenceMap(8, 8, chunkSize: 8);
        map.SetValue(4, 4, 100f);

        map.Diffuse(rate: 0.1f, decay: 0.9f);

        // Sum of all values should be less than 100 due to decay
        float sum = 0f;
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                sum += map.GetValue(x, y);

        Assert.True(sum < 100f, $"Total should be less than 100 with decay, got {sum}");
        Assert.True(sum > 0f, "Total should still be positive");
    }

    // =========================================================================
    // Advection
    // =========================================================================

    [Fact]
    public void Advect_MovesValues()
    {
        var map = new InfluenceMap(16, 16, chunkSize: 16);

        // Set a column of values
        for (int y = 0; y < 16; y++)
            map.SetValue(4, y, 10f);

        // Advect with eastward wind (positive X)
        map.Advect(windX: 2f, windY: 0f, dt: 1f);

        // Original column should be reduced (values moved east)
        float original = map.GetValue(4, 8);
        float downstream = map.GetValue(6, 8);

        Assert.True(downstream > original, $"Values should move downstream. Original={original}, Downstream={downstream}");
    }

    // =========================================================================
    // Interpolation
    // =========================================================================

    [Fact]
    public void GetValueInterpolated_BilinearBetweenTiles()
    {
        var map = new InfluenceMap(4, 4, chunkSize: 4);
        map.SetValue(0, 0, 10f);
        map.SetValue(1, 0, 20f);
        map.SetValue(0, 1, 0f);
        map.SetValue(1, 1, 0f);

        // Midpoint between (0,0)=10 and (1,0)=20 at y=0 should be 15
        float mid = map.GetValueInterpolated(0.5f, 0f);
        Assert.Equal(15f, mid, precision: 2);

        // Quarter point at (0.5, 0.5): top = 15, bot = 0, result = 7.5
        float quarter = map.GetValueInterpolated(0.5f, 0.5f);
        Assert.Equal(7.5f, quarter, precision: 2);
    }

    // =========================================================================
    // Row access and raw data
    // =========================================================================

    [Fact]
    public void GetRow_ReturnsCorrectSlice()
    {
        var map = new InfluenceMap(8, 8, chunkSize: 8);
        map.SetValue(3, 2, 42f);

        var row = map.GetRow(2);
        Assert.Equal(8, row.Length);
        Assert.Equal(42f, row[3], precision: 3);
    }

    [Fact]
    public void GetRawData_MatchesValues()
    {
        var map = new InfluenceMap(4, 4, chunkSize: 4);
        map.SetValue(1, 2, 7f);

        float[] raw = map.GetRawData();
        Assert.Equal(16, raw.Length);
        Assert.Equal(7f, raw[2 * 4 + 1], precision: 3);
    }

    // =========================================================================
    // Edge cases
    // =========================================================================

    [Fact]
    public void GetValue_OutOfBounds_ReturnsZero()
    {
        var map = new InfluenceMap(8, 8);
        Assert.Equal(0f, map.GetValue(-1, 0));
        Assert.Equal(0f, map.GetValue(0, 8));
        Assert.Equal(0f, map.GetValue(100, 100));
    }

    [Fact]
    public void ClearAll_RemovesEverything()
    {
        var map = new InfluenceMap(16, 16);
        map.AddSource(8, 8, 5f, 3f, FalloffType.Linear);
        map.RecalculateAll();
        Assert.True(map.GetValue(8, 8) > 0f);

        map.ClearAll();
        Assert.Equal(0, map.SourceCount);
        Assert.Equal(0f, map.GetValue(8, 8));
        Assert.Equal(0, map.DirtyChunkCount);
    }

    [Fact]
    public void Constructor_InvalidSize_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new InfluenceMap(0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InfluenceMap(10, -1));
    }
}
