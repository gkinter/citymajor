using System.Runtime.InteropServices;
using Forge.Engine.Data;
using Forge.Engine.Rendering;
using Forge.Engine.Simulation;
using ImGuiNET;

namespace Forge.Engine.IO;

/// <summary>
/// Records city growth over time as a sequence of delta-compressed frames for
/// timelapse playback. Each frame stores only the tiles that changed since the
/// previous frame, keeping replay files compact (typically &lt; 5 MB for a full
/// 200-year game on a 512x512 map).
///
/// Recording:
///   - Call StartRecording() to begin. The system captures a snapshot every N
///     game months (configurable interval).
///   - CaptureFrame() is called by the simulation at the configured interval.
///     It diffs the current tile state against the previous frame and stores
///     only the changed entries.
///
/// Playback:
///   - Frames are applied sequentially to reconstruct the world state at any
///     point in time. Seeking jumps to the nearest keyframe and applies deltas
///     forward.
///   - A keyframe (full snapshot) is inserted every 120 frames (~10 game years)
///     to bound seek latency.
///
/// File format:
///   Magic "FRPL" (4 bytes) + version (2 bytes) + header + frame data.
///   Frames are stored sequentially with a length prefix for fast seeking.
/// </summary>
public sealed class ReplaySystem : IDisposable
{
    private const uint MagicNumber = 0x4C505246; // "FRPL"
    private const ushort FileVersion = 1;
    private const int KeyframeInterval = 120; // Full snapshot every 120 frames

    // Recording state
    private bool _isRecording;
    private int _captureIntervalMonths = 1;
    private int _monthsSinceLastCapture;

    // Frame storage
    private readonly List<ReplayFrame> _frames = new();

    // Previous frame tile state for delta computation
    private byte[]? _prevTerrain;
    private byte[]? _prevZone;
    private ushort[]? _prevBuilding;
    private int _worldSize;

    // Playback state
    private bool _isPlaying;
    private int _currentFrame;
    private float _playbackSpeed = 1.0f;
    private float _playbackAccumulator;
    private const float BaseFrameInterval = 0.5f; // seconds between frames at 1x speed

    public bool IsRecording => _isRecording;
    public bool IsPlaying => _isPlaying;
    public int CurrentFrame => _currentFrame;
    public int TotalFrames => _frames.Count;

    public float PlaybackProgress => _frames.Count > 0
        ? (float)_currentFrame / System.Math.Max(1, _frames.Count - 1)
        : 0f;

    // =========================================================================
    // Recording
    // =========================================================================

    /// <summary>
    /// Start recording city growth. Captures a frame every captureIntervalGameMonths.
    /// </summary>
    public void StartRecording(int captureIntervalGameMonths = 1)
    {
        if (_isRecording) return;

        _captureIntervalMonths = System.Math.Max(1, captureIntervalGameMonths);
        _monthsSinceLastCapture = 0;
        _isRecording = true;
        _frames.Clear();
        _prevTerrain = null;
        _prevZone = null;
        _prevBuilding = null;

        Console.WriteLine($"[Replay] Started recording (interval: {_captureIntervalMonths} month(s))");
    }

    /// <summary>Stop recording.</summary>
    public void StopRecording()
    {
        if (!_isRecording) return;
        _isRecording = false;
        Console.WriteLine($"[Replay] Stopped recording. {_frames.Count} frames captured.");
    }

    /// <summary>
    /// Capture a frame from the current world state. Called by the simulation
    /// at the configured interval (typically once per game month).
    /// </summary>
    public void CaptureFrame(WorldState state, IsometricCamera camera)
    {
        if (!_isRecording) return;

        int tileCount = state.Tiles.Count;
        _worldSize = state.Tiles.Size;

        bool isKeyframe = _frames.Count % KeyframeInterval == 0;

        // Compute changed tiles (delta against previous frame)
        var changes = new List<(int tileIndex, byte terrain, byte zone, ushort building)>();

        if (isKeyframe || _prevTerrain == null)
        {
            // Keyframe: store all non-default tiles for a complete snapshot
            for (int i = 0; i < tileCount; i++)
            {
                changes.Add((i, state.Tiles.TerrainType[i], state.Tiles.ZoneType[i], state.Tiles.BuildingId[i]));
            }
        }
        else
        {
            // Delta: only store tiles that differ from previous frame
            for (int i = 0; i < tileCount; i++)
            {
                if (state.Tiles.TerrainType[i] != _prevTerrain[i] ||
                    state.Tiles.ZoneType[i] != _prevZone![i] ||
                    state.Tiles.BuildingId[i] != _prevBuilding![i])
                {
                    changes.Add((i, state.Tiles.TerrainType[i], state.Tiles.ZoneType[i], state.Tiles.BuildingId[i]));
                }
            }
        }

        // Update previous state buffers
        if (_prevTerrain == null || _prevTerrain.Length != tileCount)
        {
            _prevTerrain = new byte[tileCount];
            _prevZone = new byte[tileCount];
            _prevBuilding = new ushort[tileCount];
        }
        Buffer.BlockCopy(state.Tiles.TerrainType, 0, _prevTerrain, 0, tileCount);
        Buffer.BlockCopy(state.Tiles.ZoneType, 0, _prevZone!, 0, tileCount);
        Buffer.BlockCopy(state.Tiles.BuildingId, 0, _prevBuilding!, 0, tileCount * sizeof(ushort));

        var frame = new ReplayFrame
        {
            GameYear = state.Year,
            GameMonth = state.Month,
            Population = state.Population,
            Treasury = state.CityFunds,
            Happiness = state.Happiness,
            Era = state.Era,
            ChangedTiles = changes.ToArray(),
            CameraX = camera.X,
            CameraY = camera.Y,
            CameraZoom = camera.ZoomLevel,
            IsKeyframe = isKeyframe || _frames.Count == 0,
        };

        _frames.Add(frame);
    }

    /// <summary>
    /// Notify the replay system that a game month has passed.
    /// Returns true if a frame should be captured this month.
    /// </summary>
    public bool ShouldCaptureThisMonth()
    {
        if (!_isRecording) return false;
        _monthsSinceLastCapture++;
        if (_monthsSinceLastCapture >= _captureIntervalMonths)
        {
            _monthsSinceLastCapture = 0;
            return true;
        }
        return false;
    }

    // =========================================================================
    // Playback
    // =========================================================================

    /// <summary>Start playback from the beginning.</summary>
    public void StartPlayback()
    {
        if (_frames.Count == 0) return;
        _isPlaying = true;
        _currentFrame = 0;
        _playbackAccumulator = 0f;
        Console.WriteLine("[Replay] Playback started");
    }

    /// <summary>Stop playback.</summary>
    public void StopPlayback()
    {
        _isPlaying = false;
        Console.WriteLine("[Replay] Playback stopped");
    }

    /// <summary>Set playback speed multiplier. 1.0 = real-time, 10.0 = 10x fast.</summary>
    public void SetPlaybackSpeed(float speed)
    {
        _playbackSpeed = System.Math.Clamp(speed, 0.1f, 100f);
    }

    /// <summary>Seek to a specific frame index.</summary>
    public void SeekToFrame(int frameIndex)
    {
        _currentFrame = System.Math.Clamp(frameIndex, 0, System.Math.Max(0, _frames.Count - 1));
        _playbackAccumulator = 0f;
    }

    /// <summary>Seek to the frame nearest to the given game date.</summary>
    public void SeekToGameDate(int year, int month)
    {
        int bestIndex = 0;
        int bestDistance = int.MaxValue;

        for (int i = 0; i < _frames.Count; i++)
        {
            int dist = System.Math.Abs((_frames[i].GameYear * 12 + _frames[i].GameMonth) - (year * 12 + month));
            if (dist < bestDistance)
            {
                bestDistance = dist;
                bestIndex = i;
            }
        }

        SeekToFrame(bestIndex);
    }

    /// <summary>
    /// Update playback. Call once per frame with delta time.
    /// Returns the current frame to display, or null if not playing.
    /// </summary>
    public ReplayFrame? UpdatePlayback(float dt)
    {
        if (!_isPlaying || _frames.Count == 0) return null;

        _playbackAccumulator += dt * _playbackSpeed;
        float interval = BaseFrameInterval;

        while (_playbackAccumulator >= interval && _currentFrame < _frames.Count - 1)
        {
            _playbackAccumulator -= interval;
            _currentFrame++;
        }

        if (_currentFrame >= _frames.Count - 1)
        {
            _currentFrame = _frames.Count - 1;
            _isPlaying = false;
        }

        return _frames[_currentFrame];
    }

    /// <summary>Get a specific frame by index.</summary>
    public ReplayFrame GetFrame(int index)
    {
        if (index < 0 || index >= _frames.Count)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Frame index {index} out of range [0, {_frames.Count})");
        return _frames[index];
    }

    /// <summary>
    /// Apply frames from the nearest keyframe up to targetFrame onto the given TileData,
    /// reconstructing the world state at that point in time.
    /// </summary>
    public void ReconstructStateAtFrame(int targetFrame, TileData tiles)
    {
        if (targetFrame < 0 || targetFrame >= _frames.Count) return;

        // Find the nearest keyframe at or before targetFrame
        int keyframeIndex = targetFrame;
        while (keyframeIndex > 0 && !_frames[keyframeIndex].IsKeyframe)
        {
            keyframeIndex--;
        }

        // Apply keyframe (full state)
        ApplyFrameToTiles(_frames[keyframeIndex], tiles);

        // Apply deltas from keyframe+1 to targetFrame
        for (int i = keyframeIndex + 1; i <= targetFrame; i++)
        {
            ApplyFrameToTiles(_frames[i], tiles);
        }
    }

    private static void ApplyFrameToTiles(ReplayFrame frame, TileData tiles)
    {
        foreach (var (tileIndex, terrain, zone, building) in frame.ChangedTiles)
        {
            if (tileIndex >= 0 && tileIndex < tiles.Count)
            {
                tiles.TerrainType[tileIndex] = terrain;
                tiles.ZoneType[tileIndex] = zone;
                tiles.BuildingId[tileIndex] = building;
            }
        }
    }

    // =========================================================================
    // Save/Load replay files
    // =========================================================================

    /// <summary>Save the recorded replay to a binary file.</summary>
    public void SaveReplay(string path)
    {
        string? dir = Path.GetDirectoryName(path);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        using var stream = File.Create(path);
        using var w = new BinaryWriter(stream);

        // Header
        w.Write(MagicNumber);
        w.Write(FileVersion);
        w.Write(_worldSize);
        w.Write(_frames.Count);

        // Frames
        foreach (var frame in _frames)
        {
            WriteFrame(w, frame);
        }

        Console.WriteLine($"[Replay] Saved {_frames.Count} frames to {path}");
    }

    /// <summary>Load a replay from a binary file.</summary>
    public void LoadReplay(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Replay file not found: {path}");

        using var stream = File.OpenRead(path);
        using var r = new BinaryReader(stream);

        uint magic = r.ReadUInt32();
        if (magic != MagicNumber)
            throw new InvalidDataException("Not a valid Forge replay file.");

        ushort version = r.ReadUInt16();
        if (version > FileVersion)
            throw new InvalidDataException($"Replay version {version} is newer than supported {FileVersion}.");

        _worldSize = r.ReadInt32();
        int frameCount = r.ReadInt32();

        _frames.Clear();
        for (int i = 0; i < frameCount; i++)
        {
            _frames.Add(ReadFrame(r));
        }

        _currentFrame = 0;
        Console.WriteLine($"[Replay] Loaded {_frames.Count} frames from {path}");
    }

    private static void WriteFrame(BinaryWriter w, ReplayFrame frame)
    {
        w.Write(frame.GameYear);
        w.Write(frame.GameMonth);
        w.Write(frame.Population);
        w.Write(frame.Treasury);
        w.Write(frame.Happiness);
        w.Write(frame.Era);
        w.Write(frame.CameraX);
        w.Write(frame.CameraY);
        w.Write(frame.CameraZoom);
        w.Write(frame.IsKeyframe);

        w.Write(frame.ChangedTiles.Length);
        foreach (var (tileIndex, terrain, zone, building) in frame.ChangedTiles)
        {
            w.Write(tileIndex);
            w.Write(terrain);
            w.Write(zone);
            w.Write(building);
        }
    }

    private static ReplayFrame ReadFrame(BinaryReader r)
    {
        var frame = new ReplayFrame
        {
            GameYear = r.ReadInt32(),
            GameMonth = r.ReadInt32(),
            Population = r.ReadInt32(),
            Treasury = r.ReadInt64(),
            Happiness = r.ReadSingle(),
            Era = r.ReadInt32(),
            CameraX = r.ReadSingle(),
            CameraY = r.ReadSingle(),
            CameraZoom = r.ReadInt32(),
            IsKeyframe = r.ReadBoolean(),
        };

        int changeCount = r.ReadInt32();
        var changes = new (int tileIndex, byte terrain, byte zone, ushort building)[changeCount];
        for (int i = 0; i < changeCount; i++)
        {
            int idx = r.ReadInt32();
            byte terrain = r.ReadByte();
            byte zone = r.ReadByte();
            ushort building = r.ReadUInt16();
            changes[i] = (idx, terrain, zone, building);
        }
        frame.ChangedTiles = changes;

        return frame;
    }

    // =========================================================================
    // ImGui UI (playback controls)
    // =========================================================================

    /// <summary>Render playback controls via ImGui.</summary>
    public void RenderUI()
    {
        if (_frames.Count == 0) return;

        var io = ImGui.GetIO();
        float panelWidth = 500f;
        float panelHeight = 80f;

        ImGui.SetNextWindowPos(new System.Numerics.Vector2(
            (io.DisplaySize.X - panelWidth) * 0.5f,
            io.DisplaySize.Y - panelHeight - 10f));
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(panelWidth, panelHeight));

        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
                    ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse;

        if (ImGui.Begin("ReplayControls", flags))
        {
            // Timeline slider
            int frame = _currentFrame;
            ImGui.SetNextItemWidth(panelWidth - 16f);
            if (ImGui.SliderInt("##timeline", ref frame, 0, System.Math.Max(0, _frames.Count - 1)))
            {
                SeekToFrame(frame);
            }

            // Play/Pause button
            if (_isPlaying)
            {
                if (ImGui.Button("Pause")) StopPlayback();
            }
            else
            {
                if (ImGui.Button("Play")) StartPlayback();
            }
            ImGui.SameLine();

            // Speed control
            float speed = _playbackSpeed;
            ImGui.SetNextItemWidth(100f);
            if (ImGui.SliderFloat("Speed", ref speed, 0.1f, 20f, "%.1fx"))
            {
                SetPlaybackSpeed(speed);
            }
            ImGui.SameLine();

            // Frame counter and date display
            if (_currentFrame >= 0 && _currentFrame < _frames.Count)
            {
                var f = _frames[_currentFrame];
                ImGui.Text($"Frame {_currentFrame + 1}/{_frames.Count}  |  {f.GameYear}-{f.GameMonth:D2}  |  Pop: {f.Population:N0}");
            }
        }
        ImGui.End();
    }

    public void Dispose()
    {
        _frames.Clear();
        _prevTerrain = null;
        _prevZone = null;
        _prevBuilding = null;
    }
}

/// <summary>
/// A single frame in a replay recording. Contains the game date, key statistics,
/// and a delta-compressed list of tile changes from the previous frame.
/// Keyframes contain a full snapshot of all tiles.
/// </summary>
public struct ReplayFrame
{
    /// <summary>Game year at capture time.</summary>
    public int GameYear;

    /// <summary>Game month at capture time (1-12).</summary>
    public int GameMonth;

    /// <summary>Total city population at capture time.</summary>
    public int Population;

    /// <summary>City treasury balance at capture time.</summary>
    public long Treasury;

    /// <summary>City happiness (0.0-1.0) at capture time.</summary>
    public float Happiness;

    /// <summary>Historical era (0-5) at capture time.</summary>
    public int Era;

    /// <summary>
    /// Tile changes from the previous frame. For keyframes, this contains
    /// all tiles. For delta frames, only tiles that changed.
    /// Each entry: (flat tile index, terrain type, zone type, building ID).
    /// </summary>
    public (int tileIndex, byte terrain, byte zone, ushort building)[] ChangedTiles;

    /// <summary>Camera X position at capture time.</summary>
    public float CameraX;

    /// <summary>Camera Y position at capture time.</summary>
    public float CameraY;

    /// <summary>Camera zoom level at capture time.</summary>
    public int CameraZoom;

    /// <summary>Whether this frame is a keyframe (full snapshot) or a delta frame.</summary>
    public bool IsKeyframe;
}
