using Forge.Engine.Math;
using Xunit;

namespace Forge.Engine.Tests;

public class IsometricMathTests
{
    [Fact]
    public void GridToScreen_Origin_ReturnsZero()
    {
        var (sx, sy) = IsometricMath.GridToScreen(0, 0);
        Assert.Equal(0f, sx);
        Assert.Equal(0f, sy);
    }

    [Fact]
    public void GridToScreen_TileOneZero_ReturnsHalfTileWidth()
    {
        // Tile (1,0) should be at (halfW, halfH)
        var (sx, sy) = IsometricMath.GridToScreen(1, 0, 64, 32);
        Assert.Equal(32f, sx);  // (1-0) * 32
        Assert.Equal(16f, sy);  // (1+0) * 16
    }

    [Fact]
    public void GridToScreen_TileZeroOne_ReturnsNegativeHalfWidth()
    {
        var (sx, sy) = IsometricMath.GridToScreen(0, 1, 64, 32);
        Assert.Equal(-32f, sx); // (0-1) * 32
        Assert.Equal(16f, sy);  // (0+1) * 16
    }

    [Fact]
    public void ScreenToGrid_RoundTrip_AllRotations()
    {
        for (int rotation = 0; rotation < 4; rotation++)
        {
            var (sx, sy) = IsometricMath.GridToScreen(5, 3, 64, 32, rotation);
            var (gx, gy) = IsometricMath.ScreenToGrid(sx, sy, 64, 32, rotation);
            Assert.Equal(5, gx);
            Assert.Equal(3, gy);
        }
    }

    [Fact]
    public void ScreenToGrid_RoundTrip_LargeCoordinates()
    {
        for (int rotation = 0; rotation < 4; rotation++)
        {
            var (sx, sy) = IsometricMath.GridToScreen(100, 200, 64, 32, rotation);
            var (gx, gy) = IsometricMath.ScreenToGrid(sx, sy, 64, 32, rotation);
            Assert.Equal(100, gx);
            Assert.Equal(200, gy);
        }
    }

    [Fact]
    public void RotateGrid_FullCircle_ReturnsOriginal()
    {
        int gx = 7, gy = 3;

        var (r1x, r1y) = IsometricMath.RotateGrid(gx, gy, 1);
        var (r2x, r2y) = IsometricMath.RotateGrid(r1x, r1y, 1);
        var (r3x, r3y) = IsometricMath.RotateGrid(r2x, r2y, 1);
        var (r4x, r4y) = IsometricMath.RotateGrid(r3x, r3y, 1);

        Assert.Equal(gx, r4x);
        Assert.Equal(gy, r4y);
    }

    [Fact]
    public void UnrotateGrid_InverseOfRotate()
    {
        for (int rot = 0; rot < 4; rot++)
        {
            var (rx, ry) = IsometricMath.RotateGrid(10, 20, rot);
            var (gx, gy) = IsometricMath.UnrotateGrid(rx, ry, rot);
            Assert.Equal(10, gx);
            Assert.Equal(20, gy);
        }
    }

    [Fact]
    public void ManhattanDistance_Correct()
    {
        Assert.Equal(0, IsometricMath.ManhattanDistance(5, 5, 5, 5));
        Assert.Equal(4, IsometricMath.ManhattanDistance(1, 1, 3, 3));
        Assert.Equal(10, IsometricMath.ManhattanDistance(0, 0, 5, 5));
    }

    [Fact]
    public void ChebyshevDistance_Correct()
    {
        Assert.Equal(0, IsometricMath.ChebyshevDistance(5, 5, 5, 5));
        Assert.Equal(2, IsometricMath.ChebyshevDistance(1, 1, 3, 3));
        Assert.Equal(5, IsometricMath.ChebyshevDistance(0, 0, 5, 3));
    }

    [Fact]
    public void GetCardinalNeighbors_ReturnsFour()
    {
        var neighbors = IsometricMath.GetCardinalNeighbors(5, 5);
        Assert.Equal(4, neighbors.Length);
        Assert.Contains((5, 4), neighbors); // North
        Assert.Contains((6, 5), neighbors); // East
        Assert.Contains((5, 6), neighbors); // South
        Assert.Contains((4, 5), neighbors); // West
    }

    [Fact]
    public void GetAllNeighbors_ReturnsEight()
    {
        var neighbors = IsometricMath.GetAllNeighbors(5, 5);
        Assert.Equal(8, neighbors.Length);
    }
}
