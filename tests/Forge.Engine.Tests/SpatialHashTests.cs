using Forge.Engine.Data;
using Xunit;

namespace Forge.Engine.Tests;

public class SpatialHashTests
{
    [Fact]
    public void NewHash_IsEmpty()
    {
        var hash = new SpatialHash<int>();
        Assert.Equal(0, hash.EntityCount);
        Assert.Equal(0, hash.CellCount);
        Assert.Equal(0f, hash.AverageEntitiesPerCell);
    }

    [Fact]
    public void Insert_IncreasesCount()
    {
        var hash = new SpatialHash<int>();
        hash.Insert(1, 5f, 5f);
        Assert.Equal(1, hash.EntityCount);
        Assert.Equal(1, hash.CellCount);
    }

    [Fact]
    public void Remove_DecreasesCount()
    {
        var hash = new SpatialHash<int>();
        hash.Insert(1, 5f, 5f);
        bool removed = hash.Remove(1, 5f, 5f);

        Assert.True(removed);
        Assert.Equal(0, hash.EntityCount);
        Assert.Equal(0, hash.CellCount);
    }

    [Fact]
    public void Remove_NonExistent_ReturnsFalse()
    {
        var hash = new SpatialHash<int>();
        Assert.False(hash.Remove(999, 0f, 0f));
    }

    [Fact]
    public void QueryRadius_FindsEntitiesInRange()
    {
        var hash = new SpatialHash<int>(cellSize: 10f);
        hash.Insert(1, 10f, 10f);
        hash.Insert(2, 12f, 10f);
        hash.Insert(3, 100f, 100f); // Far away

        var results = new List<int>();
        hash.QueryRadius(10f, 10f, 5f, results);

        Assert.Equal(2, results.Count);
        Assert.Contains(1, results);
        Assert.Contains(2, results);
        Assert.DoesNotContain(3, results);
    }

    [Fact]
    public void QueryRadius_ExactBoundary()
    {
        var hash = new SpatialHash<int>(cellSize: 10f);
        hash.Insert(1, 0f, 0f);
        hash.Insert(2, 5f, 0f); // Exactly at radius

        var results = new List<int>();
        hash.QueryRadius(0f, 0f, 5f, results);

        Assert.Equal(2, results.Count); // 5.0 <= 5.0, included
    }

    [Fact]
    public void QueryRadius_EmptyResults()
    {
        var hash = new SpatialHash<int>(cellSize: 10f);
        hash.Insert(1, 100f, 100f);

        var results = new List<int>();
        hash.QueryRadius(0f, 0f, 5f, results);

        Assert.Empty(results);
    }

    [Fact]
    public void QueryRadius_EmptyHash()
    {
        var hash = new SpatialHash<int>(cellSize: 10f);

        var results = new List<int>();
        hash.QueryRadius(0f, 0f, 100f, results);

        Assert.Empty(results);
    }

    [Fact]
    public void QueryRect_FindsEntitiesInRange()
    {
        var hash = new SpatialHash<int>(cellSize: 10f);
        hash.Insert(1, 5f, 5f);
        hash.Insert(2, 15f, 5f);
        hash.Insert(3, 5f, 25f); // Outside Y range

        var results = new List<int>();
        hash.QueryRect(0f, 0f, 20f, 10f, results);

        Assert.Equal(2, results.Count);
        Assert.Contains(1, results);
        Assert.Contains(2, results);
    }

    [Fact]
    public void QueryRect_ExactBoundary()
    {
        var hash = new SpatialHash<int>(cellSize: 10f);
        hash.Insert(1, 10f, 10f); // On boundary

        var results = new List<int>();
        hash.QueryRect(10f, 10f, 20f, 20f, results);

        Assert.Single(results);
        Assert.Equal(1, results[0]);
    }

    [Fact]
    public void Move_SameCell_UpdatesPosition()
    {
        var hash = new SpatialHash<int>(cellSize: 100f); // Large cell so move stays in same cell
        hash.Insert(1, 5f, 5f);

        hash.Move(1, 5f, 5f, 6f, 6f);

        Assert.Equal(1, hash.EntityCount);

        // Query at new position
        var results = new List<int>();
        hash.QueryRadius(6f, 6f, 1f, results);
        Assert.Single(results);
        Assert.Equal(1, results[0]);

        // Old position should not find it if it actually moved
        results.Clear();
        hash.QueryRadius(5f, 5f, 0.5f, results);
        Assert.Empty(results);
    }

    [Fact]
    public void Move_DifferentCell_RelocatesEntity()
    {
        var hash = new SpatialHash<int>(cellSize: 10f);
        hash.Insert(1, 5f, 5f);

        hash.Move(1, 5f, 5f, 50f, 50f);

        Assert.Equal(1, hash.EntityCount);

        var results = new List<int>();
        hash.QueryRadius(50f, 50f, 1f, results);
        Assert.Single(results);

        results.Clear();
        hash.QueryRadius(5f, 5f, 1f, results);
        Assert.Empty(results);
    }

    [Fact]
    public void MultipleEntities_SameCell()
    {
        var hash = new SpatialHash<int>(cellSize: 100f);
        hash.Insert(1, 1f, 1f);
        hash.Insert(2, 2f, 2f);
        hash.Insert(3, 3f, 3f);

        Assert.Equal(3, hash.EntityCount);
        Assert.Equal(1, hash.CellCount);
        Assert.Equal(3f, hash.AverageEntitiesPerCell);

        var results = new List<int>();
        hash.QueryRadius(2f, 2f, 5f, results);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void NegativeCoordinates()
    {
        var hash = new SpatialHash<int>(cellSize: 10f);
        hash.Insert(1, -5f, -5f);
        hash.Insert(2, -15f, -15f);

        var results = new List<int>();
        hash.QueryRadius(-5f, -5f, 2f, results);

        Assert.Single(results);
        Assert.Equal(1, results[0]);
    }

    [Fact]
    public void Clear_RemovesEverything()
    {
        var hash = new SpatialHash<int>(cellSize: 10f);
        hash.Insert(1, 1f, 1f);
        hash.Insert(2, 2f, 2f);

        hash.Clear();

        Assert.Equal(0, hash.EntityCount);
        Assert.Equal(0, hash.CellCount);
    }

    [Fact]
    public void Constructor_InvalidCellSize_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpatialHash<int>(cellSize: 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpatialHash<int>(cellSize: -1f));
    }

    [Fact]
    public void LargeScale_ManyEntities()
    {
        var hash = new SpatialHash<int>(cellSize: 16f);
        for (int i = 0; i < 10000; i++)
        {
            float x = (i % 100) * 2f;
            float y = (i / 100) * 2f;
            hash.Insert(i, x, y);
        }

        Assert.Equal(10000, hash.EntityCount);

        // Query a small region
        var results = new List<int>();
        hash.QueryRadius(50f, 50f, 10f, results);

        // All results should actually be within the radius
        foreach (int id in results)
        {
            float x = (id % 100) * 2f;
            float y = (id / 100) * 2f;
            float dx = x - 50f;
            float dy = y - 50f;
            Assert.True(dx * dx + dy * dy <= 100f); // radius^2 = 100
        }

        Assert.True(results.Count > 0);
    }
}
