using Forge.Engine.Data;
using Xunit;

namespace Forge.Engine.Tests;

public class ChunkManagerTests
{
    [Fact]
    public void Constructor_CorrectChunkCount()
    {
        var cm = new ChunkManager(512, 64);
        Assert.Equal(8, cm.ChunksPerAxis);
        Assert.Equal(64, cm.TotalChunks);
    }

    [Fact]
    public void TileToChunk_CorrectMapping()
    {
        var cm = new ChunkManager(512, 64);

        Assert.Equal((0, 0), cm.TileToChunk(0, 0));
        Assert.Equal((0, 0), cm.TileToChunk(63, 63));
        Assert.Equal((1, 0), cm.TileToChunk(64, 0));
        Assert.Equal((1, 1), cm.TileToChunk(64, 64));
        Assert.Equal((7, 7), cm.TileToChunk(511, 511));
    }

    [Fact]
    public void MarkDirty_IncrementsDirtyCount()
    {
        var cm = new ChunkManager(256, 64);
        Assert.Equal(0, cm.DirtyCount);

        cm.MarkDirty(0, 0);
        Assert.Equal(1, cm.DirtyCount);

        cm.MarkDirty(1, 0);
        Assert.Equal(2, cm.DirtyCount);
    }

    [Fact]
    public void MarkDirty_Idempotent()
    {
        var cm = new ChunkManager(256, 64);
        cm.MarkDirty(0, 0);
        cm.MarkDirty(0, 0);
        Assert.Equal(1, cm.DirtyCount);
    }

    [Fact]
    public void ClearDirty_DecrementsDirtyCount()
    {
        var cm = new ChunkManager(256, 64);
        cm.MarkDirty(0, 0);
        cm.MarkDirty(1, 0);
        Assert.Equal(2, cm.DirtyCount);

        cm.ClearDirty(0, 0);
        Assert.Equal(1, cm.DirtyCount);
        Assert.False(cm.IsDirty(0, 0));
        Assert.True(cm.IsDirty(1, 0));
    }

    [Fact]
    public void MarkTileDirty_MarksCorrectChunk()
    {
        var cm = new ChunkManager(512, 64);

        cm.MarkTileDirty(100, 200);
        Assert.True(cm.IsDirty(1, 3)); // 100/64=1, 200/64=3
    }

    [Fact]
    public void MarkAreaDirty_MarksMultipleChunks()
    {
        var cm = new ChunkManager(512, 64);

        // Area spanning from tile (60,60) to (70,70) crosses chunk boundaries
        cm.MarkAreaDirty(60, 60, 11, 11);

        Assert.True(cm.IsDirty(0, 0));  // (60..63, 60..63)
        Assert.True(cm.IsDirty(1, 0));  // (64..70, 60..63)
        Assert.True(cm.IsDirty(0, 1));  // (60..63, 64..70)
        Assert.True(cm.IsDirty(1, 1));  // (64..70, 64..70)
    }

    [Fact]
    public void MarkAllDirty_MarksEverything()
    {
        var cm = new ChunkManager(256, 64);
        cm.MarkAllDirty();
        Assert.Equal(cm.TotalChunks, cm.DirtyCount);
    }

    [Fact]
    public void UpdateVisibility_LoadsVisibleChunks()
    {
        var cm = new ChunkManager(512, 64);
        Assert.Equal(0, cm.LoadedCount);

        cm.UpdateVisibility(1, 1, 3, 3, frameNumber: 1, loadMargin: 0);

        // 3x3 area should be loaded
        Assert.True(cm.IsLoaded(1, 1));
        Assert.True(cm.IsLoaded(2, 2));
        Assert.True(cm.IsLoaded(3, 3));
        Assert.False(cm.IsLoaded(0, 0)); // Outside range
        Assert.Equal(9, cm.LoadedCount);
    }

    [Fact]
    public void GetDirtyChunks_ReturnsCorrectList()
    {
        var cm = new ChunkManager(256, 64);
        cm.MarkDirty(0, 0);
        cm.MarkDirty(2, 3);

        var dirty = cm.GetDirtyChunks();
        Assert.Equal(2, dirty.Count);
        Assert.Contains((0, 0), dirty);
        Assert.Contains((2, 3), dirty);
    }
}
