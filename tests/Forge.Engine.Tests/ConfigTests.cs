using Forge.Engine.Core;
using Xunit;

namespace Forge.Engine.Tests;

public class ConfigTests
{
    [Fact]
    public void DefaultValues_AreSane()
    {
        var config = new Config();

        Assert.Equal("Forge Engine", config.WindowTitle);
        Assert.Equal(1280, config.WindowWidth);
        Assert.Equal(720, config.WindowHeight);
        Assert.False(config.Fullscreen);
        Assert.True(config.VSync);
        Assert.Equal(60, config.TargetFps);
        Assert.True(config.FixedTimestep > 0);
        Assert.True(config.MaxUpdatesPerFrame > 0);
        Assert.Equal(512, config.WorldSize);
        Assert.Equal(64, config.ChunkSize);
        Assert.Equal(128, config.TileWidth);
        Assert.Equal(64, config.TileHeight);
        Assert.Equal("base", config.BasePath);
        Assert.True(config.DebugOverlay);
        Assert.True(config.MasterVolume > 0f && config.MasterVolume <= 1f);
        Assert.Equal(7777, config.NetworkPort);
    }

    [Fact]
    public void ChunksPerAxis_DerivedCorrectly()
    {
        var config = new Config { WorldSize = 512, ChunkSize = 64 };
        Assert.Equal(8, config.ChunksPerAxis);
    }

    [Fact]
    public void TotalChunks_DerivedCorrectly()
    {
        var config = new Config { WorldSize = 512, ChunkSize = 64 };
        Assert.Equal(64, config.TotalChunks); // 8 * 8
    }

    [Fact]
    public void TotalTiles_DerivedCorrectly()
    {
        var config = new Config { WorldSize = 512 };
        Assert.Equal(262144, config.TotalTiles); // 512 * 512
    }

    [Fact]
    public void SmallWorldSize_DerivedPropertiesCorrect()
    {
        var config = new Config { WorldSize = 16, ChunkSize = 4 };
        Assert.Equal(4, config.ChunksPerAxis);
        Assert.Equal(16, config.TotalChunks);
        Assert.Equal(256, config.TotalTiles);
    }

    [Fact]
    public void LargeWorldSize_DerivedPropertiesCorrect()
    {
        var config = new Config { WorldSize = 1024, ChunkSize = 128 };
        Assert.Equal(8, config.ChunksPerAxis);
        Assert.Equal(64, config.TotalChunks);
        Assert.Equal(1048576, config.TotalTiles);
    }

    [Fact]
    public void FixedTimestep_DefaultIs60Hz()
    {
        var config = new Config();
        Assert.Equal(1.0 / 60.0, config.FixedTimestep, precision: 10);
    }

    [Fact]
    public void MaxUpdatesPerFrame_DefaultIs5()
    {
        var config = new Config();
        Assert.Equal(5, config.MaxUpdatesPerFrame);
    }

    [Fact]
    public void CustomValues_AppliedCorrectly()
    {
        var config = new Config
        {
            WindowTitle = "Test Game",
            WindowWidth = 1920,
            WindowHeight = 1080,
            Fullscreen = true,
            VSync = false,
            TargetFps = 144,
            WorldSize = 256,
            ChunkSize = 32,
            MasterVolume = 0.5f,
            NetworkPort = 9999,
        };

        Assert.Equal("Test Game", config.WindowTitle);
        Assert.Equal(1920, config.WindowWidth);
        Assert.Equal(1080, config.WindowHeight);
        Assert.True(config.Fullscreen);
        Assert.False(config.VSync);
        Assert.Equal(144, config.TargetFps);
        Assert.Equal(256, config.WorldSize);
        Assert.Equal(32, config.ChunkSize);
        Assert.Equal(0.5f, config.MasterVolume);
        Assert.Equal(9999, config.NetworkPort);
    }

    [Fact]
    public void TileWidth_DefaultIs128()
    {
        var config = new Config();
        Assert.Equal(128, config.TileWidth);
    }

    [Fact]
    public void TileHeight_DefaultIs64()
    {
        var config = new Config();
        Assert.Equal(64, config.TileHeight);
    }

    [Fact]
    public void HeroTileDimensions_Defaults()
    {
        var config = new Config();
        Assert.Equal(256, config.HeroTileWidth);
        Assert.Equal(128, config.HeroTileHeight);
    }

    [Fact]
    public void VisualQuality_Defaults()
    {
        var config = new Config();
        Assert.True(config.EnableNormalMapping);
        Assert.True(config.EnableSSAO);
        Assert.True(config.EnableTiltShift);
        Assert.True(config.EnableBloom);
        Assert.Equal(0.5f, config.ShadowSoftness);
    }
}
