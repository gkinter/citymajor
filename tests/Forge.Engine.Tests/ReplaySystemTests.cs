using Forge.Engine.Core;
using Forge.Engine.Data;
using Forge.Engine.IO;
using Forge.Engine.Rendering;
using Forge.Engine.Simulation;
using Xunit;

namespace Forge.Engine.Tests;

public class ReplaySystemTests : IDisposable
{
    private readonly string _testDir;
    private readonly ReplaySystem _system;
    private readonly Config _config;

    public ReplaySystemTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"forge_replay_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _system = new ReplaySystem();
        _config = new Config { WorldSize = 64, TileWidth = 64, TileHeight = 32 };
    }

    public void Dispose()
    {
        _system.Dispose();
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    private WorldState CreateWorldState(int size = 64)
    {
        var state = new WorldState(size);
        state.Year = 2024;
        state.Month = 6;
        state.Population = 1000;
        state.CityFunds = 50000;
        state.Happiness = 0.7f;
        state.Era = 4;
        return state;
    }

    private IsometricCamera CreateCamera()
    {
        return new IsometricCamera(_config);
    }

    // =========================================================================
    // Recording basics
    // =========================================================================

    [Fact]
    public void StartRecording_SetsFlag()
    {
        _system.StartRecording();
        Assert.True(_system.IsRecording);
    }

    [Fact]
    public void StopRecording_ClearsFlag()
    {
        _system.StartRecording();
        _system.StopRecording();
        Assert.False(_system.IsRecording);
    }

    [Fact]
    public void CaptureFrame_AddsFrame()
    {
        _system.StartRecording();
        var state = CreateWorldState();
        var camera = CreateCamera();

        _system.CaptureFrame(state, camera);

        Assert.Equal(1, _system.TotalFrames);
    }

    [Fact]
    public void CaptureFrame_WhenNotRecording_DoesNothing()
    {
        var state = CreateWorldState();
        var camera = CreateCamera();

        _system.CaptureFrame(state, camera);

        Assert.Equal(0, _system.TotalFrames);
    }

    [Fact]
    public void CaptureFrame_StoresCorrectMetadata()
    {
        _system.StartRecording();
        var state = CreateWorldState();
        state.Year = 2030;
        state.Month = 3;
        state.Population = 5000;
        var camera = CreateCamera();

        _system.CaptureFrame(state, camera);
        var frame = _system.GetFrame(0);

        Assert.Equal(2030, frame.GameYear);
        Assert.Equal(3, frame.GameMonth);
        Assert.Equal(5000, frame.Population);
    }

    // =========================================================================
    // Delta compression
    // =========================================================================

    [Fact]
    public void DeltaCompression_OnlyChangedTilesStored()
    {
        _system.StartRecording();
        var state = CreateWorldState();
        var camera = CreateCamera();

        // First frame: keyframe, stores all tiles
        _system.CaptureFrame(state, camera);
        int firstFrameTiles = _system.GetFrame(0).ChangedTiles.Length;
        Assert.Equal(state.Tiles.Count, firstFrameTiles); // Keyframe = all tiles

        // Change only one tile
        state.Tiles.TerrainType[state.Tiles.Index(10, 10)] = 3; // Change to water
        state.Month = 7;

        // Second frame: delta, should store only 1 changed tile
        _system.CaptureFrame(state, camera);
        int secondFrameTiles = _system.GetFrame(1).ChangedTiles.Length;
        Assert.Equal(1, secondFrameTiles);
    }

    [Fact]
    public void DeltaCompression_NoChanges_EmptyDelta()
    {
        _system.StartRecording();
        var state = CreateWorldState();
        var camera = CreateCamera();

        _system.CaptureFrame(state, camera); // Keyframe
        _system.CaptureFrame(state, camera); // Delta with no changes

        var frame1 = _system.GetFrame(1);
        Assert.Empty(frame1.ChangedTiles);
    }

    [Fact]
    public void DeltaCompression_MultipleChanges_AllCaptured()
    {
        _system.StartRecording();
        var state = CreateWorldState();
        var camera = CreateCamera();

        _system.CaptureFrame(state, camera); // Keyframe

        // Change 5 tiles
        for (int i = 0; i < 5; i++)
        {
            state.Tiles.TerrainType[i] = 3;
        }

        _system.CaptureFrame(state, camera);
        Assert.Equal(5, _system.GetFrame(1).ChangedTiles.Length);
    }

    // =========================================================================
    // Playback
    // =========================================================================

    [Fact]
    public void StartPlayback_WithFrames_SetsPlaying()
    {
        _system.StartRecording();
        var state = CreateWorldState();
        var camera = CreateCamera();
        _system.CaptureFrame(state, camera);
        _system.StopRecording();

        _system.StartPlayback();
        Assert.True(_system.IsPlaying);
        Assert.Equal(0, _system.CurrentFrame);
    }

    [Fact]
    public void StartPlayback_WithoutFrames_DoesNothing()
    {
        _system.StartPlayback();
        Assert.False(_system.IsPlaying);
    }

    [Fact]
    public void StopPlayback_ClearsFlag()
    {
        _system.StartRecording();
        var state = CreateWorldState();
        var camera = CreateCamera();
        _system.CaptureFrame(state, camera);
        _system.StopRecording();

        _system.StartPlayback();
        _system.StopPlayback();
        Assert.False(_system.IsPlaying);
    }

    [Fact]
    public void UpdatePlayback_AdvancesFrames()
    {
        _system.StartRecording();
        var state = CreateWorldState();
        var camera = CreateCamera();

        // Record 5 frames
        for (int i = 0; i < 5; i++)
        {
            state.Month = i + 1;
            _system.CaptureFrame(state, camera);
        }
        _system.StopRecording();

        _system.StartPlayback();
        _system.SetPlaybackSpeed(100f); // Very fast

        // Advance enough to move forward
        var frame = _system.UpdatePlayback(10f);
        Assert.NotNull(frame);
        Assert.True(_system.CurrentFrame > 0);
    }

    // =========================================================================
    // Seek
    // =========================================================================

    [Fact]
    public void SeekToFrame_ClampsToValid()
    {
        _system.StartRecording();
        var state = CreateWorldState();
        var camera = CreateCamera();
        for (int i = 0; i < 10; i++)
            _system.CaptureFrame(state, camera);
        _system.StopRecording();

        _system.SeekToFrame(5);
        Assert.Equal(5, _system.CurrentFrame);

        _system.SeekToFrame(100);
        Assert.Equal(9, _system.CurrentFrame);

        _system.SeekToFrame(-1);
        Assert.Equal(0, _system.CurrentFrame);
    }

    [Fact]
    public void SeekToGameDate_FindsClosestFrame()
    {
        _system.StartRecording();
        var state = CreateWorldState();
        var camera = CreateCamera();

        // Record frames across different dates
        for (int m = 1; m <= 12; m++)
        {
            state.Month = m;
            state.Year = 2024;
            _system.CaptureFrame(state, camera);
        }
        _system.StopRecording();

        _system.SeekToGameDate(2024, 6);
        var frame = _system.GetFrame(_system.CurrentFrame);
        Assert.Equal(6, frame.GameMonth);
    }

    // =========================================================================
    // PlaybackProgress
    // =========================================================================

    [Fact]
    public void PlaybackProgress_AtStart_IsZero()
    {
        _system.StartRecording();
        var state = CreateWorldState();
        var camera = CreateCamera();
        for (int i = 0; i < 10; i++)
            _system.CaptureFrame(state, camera);
        _system.StopRecording();

        _system.SeekToFrame(0);
        Assert.Equal(0f, _system.PlaybackProgress);
    }

    [Fact]
    public void PlaybackProgress_AtEnd_IsOne()
    {
        _system.StartRecording();
        var state = CreateWorldState();
        var camera = CreateCamera();
        for (int i = 0; i < 10; i++)
            _system.CaptureFrame(state, camera);
        _system.StopRecording();

        _system.SeekToFrame(9);
        Assert.Equal(1f, _system.PlaybackProgress);
    }

    [Fact]
    public void PlaybackProgress_Empty_IsZero()
    {
        Assert.Equal(0f, _system.PlaybackProgress);
    }

    // =========================================================================
    // Save/Load round-trip
    // =========================================================================

    [Fact]
    public void SaveLoad_RoundTrip_PreservesFrames()
    {
        _system.StartRecording();
        var state = CreateWorldState();
        var camera = CreateCamera();

        // Record 3 frames with changes
        _system.CaptureFrame(state, camera);

        state.Tiles.TerrainType[0] = 3; // Water
        state.Month = 7;
        state.Population = 1500;
        _system.CaptureFrame(state, camera);

        state.Tiles.BuildingId[100] = 42;
        state.Month = 8;
        _system.CaptureFrame(state, camera);

        _system.StopRecording();

        // Save
        string path = Path.Combine(_testDir, "test.frpl");
        _system.SaveReplay(path);
        Assert.True(File.Exists(path));

        // Load into a new system
        var loaded = new ReplaySystem();
        loaded.LoadReplay(path);

        Assert.Equal(3, loaded.TotalFrames);

        // Verify first frame metadata
        var frame0 = loaded.GetFrame(0);
        Assert.Equal(2024, frame0.GameYear);
        Assert.Equal(6, frame0.GameMonth);
        Assert.Equal(1000, frame0.Population);

        // Verify second frame delta
        var frame1 = loaded.GetFrame(1);
        Assert.Equal(7, frame1.GameMonth);
        Assert.Equal(1500, frame1.Population);

        loaded.Dispose();
    }

    [Fact]
    public void LoadReplay_InvalidFile_Throws()
    {
        string path = Path.Combine(_testDir, "garbage.frpl");
        File.WriteAllBytes(path, [0xFF, 0xFF, 0xFF, 0xFF]);

        Assert.Throws<InvalidDataException>(() => _system.LoadReplay(path));
    }

    [Fact]
    public void LoadReplay_MissingFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(() =>
            _system.LoadReplay(Path.Combine(_testDir, "nonexistent.frpl")));
    }

    // =========================================================================
    // State reconstruction
    // =========================================================================

    [Fact]
    public void ReconstructStateAtFrame_AppliesDeltasCorrectly()
    {
        _system.StartRecording();
        var state = CreateWorldState();
        var camera = CreateCamera();

        // Frame 0 (keyframe): all grass
        _system.CaptureFrame(state, camera);

        // Frame 1: change tile (5,5) to water
        state.Tiles.TerrainType[state.Tiles.Index(5, 5)] = 3;
        _system.CaptureFrame(state, camera);

        // Frame 2: change tile (10,10) to sand + add building
        state.Tiles.TerrainType[state.Tiles.Index(10, 10)] = 2;
        state.Tiles.BuildingId[state.Tiles.Index(10, 10)] = 99;
        _system.CaptureFrame(state, camera);

        _system.StopRecording();

        // Reconstruct at frame 2
        var reconstructed = new TileData(64);
        _system.ReconstructStateAtFrame(2, reconstructed);

        Assert.Equal(3, reconstructed.TerrainType[reconstructed.Index(5, 5)]);   // Water from frame 1
        Assert.Equal(2, reconstructed.TerrainType[reconstructed.Index(10, 10)]); // Sand from frame 2
        Assert.Equal(99, reconstructed.BuildingId[reconstructed.Index(10, 10)]); // Building from frame 2
    }

    // =========================================================================
    // Capture interval
    // =========================================================================

    [Fact]
    public void ShouldCaptureThisMonth_RespectsInterval()
    {
        _system.StartRecording(captureIntervalGameMonths: 3);

        Assert.False(_system.ShouldCaptureThisMonth()); // Month 1
        Assert.False(_system.ShouldCaptureThisMonth()); // Month 2
        Assert.True(_system.ShouldCaptureThisMonth());  // Month 3
        Assert.False(_system.ShouldCaptureThisMonth()); // Month 4
        Assert.False(_system.ShouldCaptureThisMonth()); // Month 5
        Assert.True(_system.ShouldCaptureThisMonth());  // Month 6
    }

    [Fact]
    public void ShouldCaptureThisMonth_WhenNotRecording_False()
    {
        Assert.False(_system.ShouldCaptureThisMonth());
    }

    // =========================================================================
    // GetFrame boundary
    // =========================================================================

    [Fact]
    public void GetFrame_OutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _system.GetFrame(0));
    }

    // =========================================================================
    // Speed control
    // =========================================================================

    [Fact]
    public void SetPlaybackSpeed_ClampsToValid()
    {
        _system.SetPlaybackSpeed(0.01f); // Below min
        // No public getter for speed, but shouldn't crash

        _system.SetPlaybackSpeed(200f); // Above max
        // No crash

        _system.SetPlaybackSpeed(5f); // Normal
        // No crash
    }

    // =========================================================================
    // Dispose
    // =========================================================================

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var system = new ReplaySystem();
        system.Dispose();
        system.Dispose();
    }
}
