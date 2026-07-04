# Research: 18 Custom Engine Architecture

f, 1.00f);
    colors[(int)ImGuiCol.TabHovered]     = new Vector4(0.45f, 0.30f, 0.18f, 1.00f);
    colors[(int)ImGuiCol.ScrollbarBg]    = new Vector4(0.10f, 0.08f, 0.06f, 1.00f);
    colors[(int)ImGuiCol.ScrollbarGrab]  = new Vector4(0.40f, 0.30f, 0.20f, 1.00f);
}
```

### 5.4 Docking Layout

```csharp
public static class PanelLayout
{
    // Default docking layout — restored on first launch or "Reset Layout"
    public static void BuildDefaultLayout(uint dockspaceId)
    {
        ImGui.DockBuilderRemoveNode(dockspaceId);
        ImGui.DockBuilderAddNode(dockspaceId, ImGuiDockNodeFlags.DockSpace);
        ImGui.DockBuilderSetNodeSize(dockspaceId, ImGui.GetMainViewport().Size);

        // Split: left sidebar (20%), center game view (60%), right sidebar (20%)
        uint leftId, centerId, rightId;
        ImGui.DockBuilderSplitNode(dockspaceId, ImGuiDir.Left, 0.20f, out leftId, out centerId);
        ImGui.DockBuilderSplitNode(centerId, ImGuiDir.Right, 0.25f, out rightId, out centerId);

        // Bottom bar (resource bar, speed controls) — 5% of center
        uint bottomId;
        ImGui.DockBuilderSplitNode(centerId, ImGuiDir.Down, 0.05f, out bottomId, out centerId);

        // Assign panels to dock nodes
        ImGui.DockBuilderDockWindow("Minimap", leftId);
        ImGui.DockBuilderDockWindow("District Info", leftId);
        ImGui.DockBuilderDockWindow("Build Menu", leftId);
        ImGui.DockBuilderDockWindow("##GameView", centerId);       // game viewport
        ImGui.DockBuilderDockWindow("Inspector", rightId);
        ImGui.DockBuilderDockWindow("Budget", rightId);
        ImGui.DockBuilderDockWindow("Tech Tree", rightId);
        ImGui.DockBuilderDockWindow("Resource Bar", bottomId);

        ImGui.DockBuilderFinish(dockspaceId);
    }
}
```

### 5.5 Performance With Many Panels

ImGui renders efficiently by default — 7 panels is trivial (a few hundred draw commands). The real concern is data queries backing each panel. Rules:

- Panels query simulation state from the **front buffer** (read-only, no locks)
- Expensive queries (e.g., "top 10 buildings by income") are cached per-panel, refreshed every 500ms via a stale timer
- Minimap is a pre-rendered texture (updated once per second or on camera move), not drawn per-frame with ImGui primitives
- Graphs (budget history, population curve) use ring buffers with 360 entries (1 per in-game month for 30 years), drawn with `ImGui.PlotLines`

### 5.6 Custom Widgets

```csharp
// ─── Isometric Minimap ───
// Rendered to a 256x128 texture, updated once per second
// Each pixel = 4x4 tile block, colored by zone type
// Camera rectangle drawn as overlay
// Clickable — teleports camera to clicked position
public static void DrawMinimap(uint minimapTexture, IsometricCamera camera, WorldState state)
{
    var size = new Vector2(256, 128);
    ImGui.Image((IntPtr)minimapTexture, size);

    // Draw camera rectangle overlay using ImDrawList
    var drawList = ImGui.GetWindowDrawList();
    var pos = ImGui.GetItemRectMin();
    var camRect = camera.GetMinimapRect(size, state.MapWidth, state.MapHeight);
    drawList.AddRect(
        pos + camRect.Min, pos + camRect.Max,
        ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 0, 1)), // yellow
        0, ImDrawFlags.None, 2.0f
    );

    // Click to teleport
    if (ImGui.IsItemClicked())
    {
        var mousePos = ImGui.GetMousePos() - pos;
        int tileX = (int)(mousePos.X / size.X * state.MapWidth);
        int tileY = (int)(mousePos.Y / size.Y * state.MapHeight);
        camera.CenterOn(tileX, tileY);
    }
}

// ─── Tech Tree Graph ───
// Node-based graph rendered with ImDrawList
// Each node: rectangle with icon + name + era color
// Connections: bezier curves between nodes
// Scrollable/zoomable sub-region
public struct TechNode
{
    public ushort Id;
    public string Name;
    public Vector2 GraphPosition; // layout position in graph space
    public byte Era;              // determines color
    public bool Unlocked;
    public bool Available;        // prerequisites met
    public ushort[] Prerequisites;
}

public static void DrawTechTree(Span<TechNode> nodes)
{
    ImGui.BeginChild("TechTreeCanvas", new Vector2(0, 400), ImGuiChildFlags.Border,
        ImGuiWindowFlags.HorizontalScrollbar);

    var drawList = ImGui.GetWindowDrawList();
    var origin = ImGui.GetCursorScreenPos();

    foreach (ref var node in nodes)
    {
        var pos = origin + node.GraphPosition;
        var size = new Vector2(120, 40);
        uint bgColor = node.Unlocked ? 0xFF225522u :
                        node.Available ? 0xFF443322u : 0xFF222222u;

        drawList.AddRectFilled(pos, pos + size, bgColor);
        drawList.AddRect(pos, pos + size, 0xFF665544u, 0, ImDrawFlags.None, 2.0f);
        drawList.AddText(pos + new Vector2(4, 4), 0xFFDDCCAAu, node.Name);

        // Draw prerequisite lines
        foreach (var preId in node.Prerequisites)
        {
            ref var pre = ref nodes[preId];
            var preCenter = origin + pre.GraphPosition + new Vector2(120, 20);
            var nodeLeft = pos + new Vector2(0, 20);
            drawList.AddBezierCubic(preCenter, preCenter + new Vector2(30, 0),
                nodeLeft - new Vector2(30, 0), nodeLeft, 0xFF887766u, 2.0f);
        }
    }

    ImGui.EndChild();
}

// ─── Heatmap Legend ───
// Vertical gradient bar with labeled ticks
public static void DrawHeatmapLegend(string label, float minVal, float maxVal, string unit)
{
    ImGui.Text(label);
    var drawList = ImGui.GetWindowDrawList();
    var pos = ImGui.GetCursorScreenPos();
    float height = 200, width = 20;

    for (int i = 0; i < (int)height; i++)
    {
        float t = 1.0f - (i / height); // top = max
        uint color = HeatColor(t);
        drawList.AddLine(pos + new Vector2(0, i), pos + new Vector2(width, i), color);
    }

    // Tick labels
    drawList.AddText(pos + new Vector2(width + 4, 0), 0xFFDDCCAAu,
        $"{maxVal:F0}{unit}");
    drawList.AddText(pos + new Vector2(width + 4, height / 2 - 6), 0xFFDDCCAAu,
        $"{(minVal + maxVal) / 2:F0}{unit}");
    drawList.AddText(pos + new Vector2(width + 4, height - 12), 0xFFDDCCAAu,
        $"{minVal:F0}{unit}");

    ImGui.Dummy(new Vector2(width + 60, height));
}
```

---

## 6. Audio Architecture

### 6.1 Backend Selection

**miniaudio** (via MiniAudio.NET bindings), not SDL2_mixer. Reasons:
- Lower latency (WASAPI/CoreAudio/ALSA direct)
- Built-in node graph for mixing, effects, routing
- No channel limit (SDL_mixer hard caps at N channels)
- Streaming decoder for large music files (no full-load to RAM)
- Single-header C library, easy P/Invoke

### 6.2 Audio Bus Architecture

```
┌────────────────────────────────────────┐
│              Master Bus                 │
│  (volume: master slider, compressor)    │
│                                         │
│  ├── Music Bus                         │
│  │   ├── Stem: Percussion              │
│  │   ├── Stem: Bass                    │
│  │   ├── Stem: Melody                  │
│  │   ├── Stem: Ambient Pad             │
│  │   └── Stem: Era-specific Layer      │
│  │   (crossfade between track sets)    │
│  │                                     │
│  ├── Ambience Bus                      │
│  │   ├── City hum (zoom-dependent vol) │
│  │   ├── Nature (wind, birds, rain)    │
│  │   ├── Industrial (machines, steam)  │
│  │   └── Crowd murmur (pop-scaled)     │
│  │                                     │
│  ├── SFX Bus                           │
│  │   ├── UI clicks, notifications      │
│  │   ├── Construction sounds           │
│  │   ├── Vehicle horns, engines        │
│  │   └── Disasters (fire, flood)       │
│  │                                     │
│  └── Voice Bus (future: advisor VO)    │
└────────────────────────────────────────┘
```

### 6.3 Adaptive Music System

```csharp
public sealed class AdaptiveMusicEngine
{
    // Each "track" is 4-5 stems that play simultaneously
    // Stems are faded in/out based on city state
    private MusicTrackSet _currentSet;
    private MusicTrackSet _nextSet;
    private float _crossfadeProgress; // 0.0 = current, 1.0 = next
    private const float CROSSFADE_DURATION = 4.0f; // seconds

    public struct MusicTrackSet
    {
        public AudioStream Percussion;  // always plays (foundation)
        public AudioStream Bass;        // fades in at pop > 1000
        public AudioStream Melody;      // fades in during prosperity
        public AudioStream AmbientPad;  // always plays (atmosphere)
        public AudioStream EraLayer;    // changes with tech era
    }

    public void Update(float deltaTime, CityState city)
    {
        // Stem volume based on city state
        float popFactor = Math.Clamp(city.Population / 10000f, 0f, 1f);
        float prosperityFactor = city.AverageHappiness;
        float tensionFactor = Math.Clamp(city.CrimeRate + city.DisasterThreat, 0f, 1f);

        _currentSet.Bass.Volume = popFactor;
        _currentSet.Melody.Volume = prosperityFactor * (1f - tensionFactor);
        _currentSet.EraLayer.Volume = 0.7f;

        // Switch track set when era changes or every 5 in-game years
        if (ShouldSwitchTrack(city))
        {
            _nextSet = LoadTrackSetForEra(city.CurrentEra);
            _crossfadeProgress = 0f;
        }

        if (_crossfadeProgress < 1f)
        {
            _crossfadeProgress += deltaTime / CROSSFADE_DURATION;
            float current = 1f - _crossfadeProgress;
            float next = _crossfadeProgress;
            SetBusVolume(_currentSet, current);
            SetBusVolume(_nextSet, next);

            if (_crossfadeProgress >= 1f)
            {
                StopSet(_currentSet);
                _currentSet = _nextSet;
            }
        }
    }
}
```

### 6.4 Spatial Ambience (Zoom-Dependent)

```csharp
public sealed class AmbienceManager
{
    // Ambience volume scales inversely with zoom level
    // Close zoom: hear individual sounds (hammering, horse hooves)
    // Far zoom: hear city hum, wind, abstracted noise

    public void Update(float zoomLevel, IsometricFrustum visibleArea, WorldState state)
    {
        float closeWeight = Math.Clamp(1f - (zoomLevel - 1f) / 3f, 0f, 1f); // 1x=1.0, 4x+=0.0
        float farWeight = 1f - closeWeight;

        // Count visible tile types for ambience mix
        int industrialTiles = 0, parkTiles = 0, waterTiles = 0, roadTiles = 0;
        SampleVisibleTiles(visibleArea, state, ref industrialTiles, ref parkTiles,
            ref waterTiles, ref roadTiles);

        float total = industrialTiles + parkTiles + waterTiles + roadTiles + 1;
        SetAmbienceVolume("industrial_hum", (industrialTiles / total) * farWeight);
        SetAmbienceVolume("nature_birds", (parkTiles / total) * closeWeight);
        SetAmbienceVolume("water_lapping", (waterTiles / total) * closeWeight);
        SetAmbienceVolume("traffic_noise", (roadTiles / total) * farWeight);
        SetAmbienceVolume("city_hum", farWeight * 0.5f);
        SetAmbienceVolume("wind", farWeight * 0.3f);
    }
}
```

### 6.5 Sound Effect Pool

```csharp
public sealed class SfxPool
{
    private const int MAX_CONCURRENT_SFX = 32;

    private readonly struct SfxInstance
    {
        public readonly uint SoundId;
        public readonly float Volume;
        public readonly byte Priority;   // 0=low (ambience), 1=normal, 2=high (UI), 3=critical (disaster)
        public readonly float TimeLeft;
    }

    private readonly SfxInstance[] _active = new SfxInstance[MAX_CONCURRENT_SFX];
    private int _activeCount;

    public void Play(uint soundId, float volume = 1f, byte priority = 1)
    {
        if (_activeCount >= MAX_CONCURRENT_SFX)
        {
            // Evict lowest priority, oldest sound
            int evictIdx = FindLowestPriority();
            Stop(_active[evictIdx]);
            _active[evictIdx] = default;
            _activeCount--;
        }

        // Dedup: don't play the same sound if already playing within 50ms
        for (int i = 0; i < _activeCount; i++)
        {
            if (_active[i].SoundId == soundId && _active[i].TimeLeft > 0.95f)
                return; // skip duplicate
        }

        _active[_activeCount++] = new SfxInstance
        {
            SoundId = soundId,
            Volume = volume,
            Priority = priority,
            TimeLeft = GetSoundDuration(soundId)
        };

        PlayOnBus(soundId, volume, "sfx");
    }
}
```

### 6.6 Audio Memory Budget

| Asset type | Format | Count | Avg size | Total |
|-----------|--------|-------|----------|-------|
| Music stems | OGG Vorbis, streamed | 50 | 3 MB (on disk, ~200KB buffered) | 10 MB buffered |
| Ambience loops | OGG, streamed | 20 | 1 MB | 4 MB buffered |
| SFX (short) | WAV, pre-loaded | 200 | 50 KB | 10 MB |
| SFX (medium) | OGG, streamed | 50 | 200 KB | 2 MB buffered |
| **Total audio RAM** | — | — | — | **~26 MB** |

---

## 7. Save/Load System

### 7.1 Binary Format

```
File layout: ironoak_save.ioas

┌─────────────────────────────┐
│ FileHeader (64 bytes)        │
├─────────────────────────────┤
│ ChunkDirectory (8 bytes x N) │  ← offset table for each data section
├─────────────────────────────┤
│ Section: WorldMeta           │  ← LZ4 compressed
│ Section: TileData            │  ← LZ4 compressed (largest, ~32MB → ~3MB)
│ Section: Buildings           │  ← LZ4 compressed
│ Section: Households          │  ← LZ4 compressed
│ Section: Citizens            │  ← LZ4 compressed
│ Section: Vehicles            │  ← LZ4 compressed
│ Section: CityState           │  ← economy, budget, policies, stats
│ Section: TechTree            │  ← unlock states
│ Section: History             │  ← graphs, milestones, event log
│ Section: ModState            │  ← active mods + mod-specific data
├─────────────────────────────┤
│ Checksum (CRC32)             │
└─────────────────────────────┘
```

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct SaveFileHeader // 64 bytes
{
    public uint Magic;                // 'IOAS' (Iron Oak Auto Save / Iron Oak Save)
    public ushort FormatVersion;      // current: 1
    public ushort EngineVersion;      // for migration compatibility
    public uint Flags;                // bit 0: compressed, bit 1: has mods, bit 2: ironman
    public uint SectionCount;         // number of data sections
    public long SaveTimestamp;        // Unix timestamp
    public uint SimTick;              // current simulation tick
    public uint PopulationAtSave;     // for save browser preview
    public uint CityFundsAtSave;      // for save browser preview
    public ushort MapWidth;
    public ushort MapHeight;
    public unsafe fixed byte CityName[24]; // UTF-8, null-terminated
    // 4+2+2+4+4+8+4+4+4+2+2+24 = 64 bytes
}

public struct SectionEntry // 8 bytes
{
    public uint SectionType;          // enum: 0=WorldMeta, 1=Tiles, 2=Buildings, ...
    public uint CompressedOffset;     // byte offset from start of file
}
```

### 7.2 Compression Strategy

Use **LZ4** (via K4os.Compression.LZ4) — fastest decompression, good ratios for structured data.

Expected compression ratios:
| Section | Raw size | Compressed | Ratio |
|---------|----------|-----------|-------|
| TileData (1M tiles) | 32 MB | ~3 MB | 10:1 (terrain is repetitive) |
| Buildings (50K) | 6.4 MB | ~1.2 MB | 5:1 |
| Households (100K) | 6.4 MB | ~1.5 MB | 4:1 |
| Citizens (500K) | 16 MB | ~4 MB | 4:1 |
| Vehicles (10K) | 640 KB | ~150 KB | 4:1 |
| CityState + Tech + History | ~500 KB | ~100 KB | 5:1 |
| **Total** | **~62 MB** | **~10 MB** | **6:1** |

Target: **< 15 MB** typical save, **< 20 MB** worst case (highly developed city).

### 7.3 Save Pipeline (Background Thread)

```csharp
public sealed class SaveSystem
{
    private readonly Thread _saveThread;
    private readonly ManualResetEventSlim _saveRequested = new(false);
    private volatile SaveRequest? _pendingRequest;

    public void RequestSave(string path, SaveType type)
    {
        // Snapshot the front buffer state (read-only copy of minimal data)
        // This runs on the main thread but takes < 1ms (just pointer copies for pinned arrays)
        var snapshot = new SaveSnapshot
        {
            Header = BuildHeader(),
            TileDataPtr = _simState.Front.Tiles, // pinned array, safe to read from another thread
            Buildings = _simState.Front.Buildings.AsSpan().ToArray(), // shallow copy
            Households = _simState.Front.Households.AsSpan().ToArray(),
            Citizens = _simState.Front.Citizens.AsSpan().ToArray(),
            Vehicles = _simState.Front.Vehicles.AsSpan().ToArray(),
            CityState = _simState.Front.CityState.DeepCopy()
        };

        _pendingRequest = new SaveRequest(path, type, snapshot);
        _saveRequested.Set();
    }

    private void SaveThreadLoop()
    {
        while (_running)
        {
            _saveRequested.Wait();
            _saveRequested.Reset();

            if (_pendingRequest is { } request)
            {
                PerformSave(request);
                _pendingRequest = null;
            }
        }
    }

    private void PerformSave(SaveRequest request)
    {
        using var file = File.Create(request.Path + ".tmp");
        using var writer = new BinaryWriter(file);

        // Write header
        writer.WriteStruct(request.Snapshot.Header);

        // Write section directory (placeholder offsets, backfill later)
        long directoryPos = file.Position;
        var sections = new SectionEntry[8];
        writer.Write(MemoryMarshal.AsBytes(sections.AsSpan()));

        // Write each section: compress → write
        sections[0] = WriteCompressedSection(writer, 0, request.Snapshot.TileDataPtr);
        sections[1] = WriteCompressedSection(writer, 1, request.Snapshot.Buildings);
        sections[2] = WriteCompressedSection(writer, 2, request.Snapshot.Households);
        sections[3] = WriteCompressedSection(writer, 3, request.Snapshot.Citizens);
        sections[4] = WriteCompressedSection(writer, 4, request.Snapshot.Vehicles);
        // ... remaining sections

        // Backfill directory with actual offsets
        file.Position = directoryPos;
        writer.Write(MemoryMarshal.AsBytes(sections.AsSpan()));

        // Write CRC32 at end
        file.Position = file.Length;
        uint crc = ComputeCrc32(request.Path + ".tmp");
        writer.Write(crc);

        writer.Flush();
        file.Close();

        // Atomic rename (prevents corruption on crash)
        File.Move(request.Path + ".tmp", request.Path, overwrite: true);
    }
}
```

### 7.4 Timing Budget

| Operation | Time |
|-----------|------|
| Snapshot creation (main thread) | < 1ms |
| LZ4 compress 62MB | ~200ms |
| Write 10-15MB to SSD | ~50ms |
| CRC32 computation | ~20ms |
| **Total (background thread)** | **~300ms** |

Well under the 2-second target. Main thread stutter: < 1ms (just the snapshot).

**Autosave**: every 5 in-game years (configurable). Uses the same background pipeline — player never notices.

### 7.5 Version Migration

```csharp
public static class SaveMigration
{
    // Each migration is a function: byte[] → byte[] for a specific section
    private static readonly Dictionary<(int fromVersion, int toVersion), Action<BinaryReader, BinaryWriter>>
        _migrations = new()
    {
        { (1, 2), MigrateV1ToV2 },
        { (2, 3), MigrateV2ToV3 },
    };

    public static SaveFileHeader MigrateIfNeeded(string path)
    {
        var header = ReadHeader(path);
        if (header.FormatVersion == CURRENT_VERSION) return header;

        // Chain migrations: v1 → v2 → v3 → ... → current
        int version = header.FormatVersion;
        while (version < CURRENT_VERSION)
        {
            if (!_migrations.TryGetValue((version, version + 1), out var migrator))
                throw new SaveCorruptException($"No migration path from v{version} to v{version + 1}");

            ApplyMigration(path, migrator);
            version++;
        }

        return ReadHeader(path); // re-read updated header
    }

    private static void MigrateV1ToV2(BinaryReader reader, BinaryWriter writer)
    {
        // Example: V2 adds NoisePollution field to TileData
        // Read V1 TileData (28 bytes), write V2 TileData (32 bytes) with NoisePollution = 0
    }
}
```

---

## 8. Input System

### 8.1 Abstraction Layer

```csharp
public enum InputAction
{
    // Camera
    CameraPanUp, CameraPanDown, CameraPanLeft, CameraPanRight,
    CameraZoomIn, CameraZoomOut,
    CameraRotateCW, CameraRotateCCW,

    // Game
    PauseToggle, SpeedUp, SpeedDown,
    Undo, Redo,
    QuickSave, QuickLoad,

    // Tools
    ToolSelect, ToolBulldoze,
    ToolZoneRes, ToolZoneCom, ToolZoneInd,
    ToolRoad, ToolRail, ToolWater, ToolPower,

    // UI
    ToggleBudget, ToggleTechTree, ToggleOverlay,
    MenuOpen, MenuClose,

    // Mouse (virtual)
    PrimaryClick, SecondaryClick, MiddleClick,
    DragStart, DragEnd,
}

public sealed class InputManager
{
    private readonly Dictionary<InputAction, List<InputBinding>> _bindings = new();
    private readonly float[] _actionValues;  // analog value per action (0.0 or 1.0 for digital, -1.0 to 1.0 for axis)
    private readonly bool[] _actionPressed;  // true on the frame it was first pressed
    private readonly bool[] _actionHeld;     // true while held
    private readonly bool[] _actionReleased; // true on the frame it was released

    // Input buffering for smooth camera
    private readonly Queue<InputEvent> _eventBuffer = new(64);

    // Context stack (building mode suppresses camera rotation, etc.)
    private readonly Stack<InputContext> _contextStack = new();

    public void Update(float deltaTime)
    {
        // Poll SDL2 events
        while (SDL.SDL_PollEvent(out var e) != 0)
        {
            // Pass to ImGui first
            if (ImGuiProcessEvent(e)) continue; // ImGui consumed it

            ProcessSDLEvent(e);
        }

        // Apply buffered input
        Array.Clear(_actionPressed);
        Array.Clear(_actionReleased);

        while (_eventBuffer.TryDequeue(out var evt))
        {
            if (IsContextAllowed(evt.Action))
            {
                ApplyEvent(evt);
            }
        }
    }

    // Camera panning with input buffering (smooth even at low FPS)
    public Vector2 GetCameraPanVector()
    {
        float x = GetActionValue(InputAction.CameraPanRight) - GetActionValue(InputAction.CameraPanLeft);
        float y = GetActionValue(InputAction.CameraPanDown) - GetActionValue(InputAction.CameraPanUp);

        // Also add edge-pan (mouse near screen edge)
        var mousePos = GetMousePosition();
        var screenSize = GetScreenSize();
        const float EDGE = 20f;
        if (mousePos.X < EDGE) x -= 1f;
        if (mousePos.X > screenSize.X - EDGE) x += 1f;
        if (mousePos.Y < EDGE) y -= 1f;
        if (mousePos.Y > screenSize.Y - EDGE) y += 1f;

        return new Vector2(x, y);
    }
}

public struct InputBinding
{
    public InputDeviceType Device;  // Keyboard, Mouse, Gamepad
    public int KeyOrButton;         // SDL keycode, mouse button, or gamepad button
    public InputModifiers Modifiers; // Ctrl, Shift, Alt
}

[Flags]
public enum InputContext
{
    Default       = 1 << 0,
    BuildingMode  = 1 << 1,
    InfoMode      = 1 << 2,
    MenuOpen      = 1 << 3,
    Bulldoze      = 1 << 4,
    ZonePaint     = 1 << 5,
    DialogOpen    = 1 << 6,
}
```

### 8.2 Rebindable Keys

```csharp
public sealed class KeybindConfig
{
    // Serialized to JSON: keybinds.json
    // Default bindings loaded from embedded resource, user overrides from file
    private Dictionary<InputAction, InputBinding[]> _userBindings;

    public void LoadDefaults()
    {
        Bind(InputAction.CameraPanUp, SDL.SDL_Scancode.SDL_SCANCODE_W);
        Bind(InputAction.CameraPanDown, SDL.SDL_Scancode.SDL_SCANCODE_S);
        Bind(InputAction.CameraPanLeft, SDL.SDL_Scancode.SDL_SCANCODE_A);
        Bind(InputAction.CameraPanRight, SDL.SDL_Scancode.SDL_SCANCODE_D);
        Bind(InputAction.CameraZoomIn, SDL.SDL_Scancode.SDL_SCANCODE_EQUALS); // +
        Bind(InputAction.CameraZoomOut, SDL.SDL_Scancode.SDL_SCANCODE_MINUS);
        Bind(InputAction.PauseToggle, SDL.SDL_Scancode.SDL_SCANCODE_SPACE);
        Bind(InputAction.SpeedUp, SDL.SDL_Scancode.SDL_SCANCODE_PERIOD);
        Bind(InputAction.SpeedDown, SDL.SDL_Scancode.SDL_SCANCODE_COMMA);
        Bind(InputAction.Undo, SDL.SDL_Scancode.SDL_SCANCODE_Z, InputModifiers.Ctrl);
        Bind(InputAction.Redo, SDL.SDL_Scancode.SDL_SCANCODE_Y, InputModifiers.Ctrl);
        // ... etc
    }

    public void SaveToFile(string path)
    {
        var json = JsonSerializer.Serialize(_userBindings, _jsonOpts);
        File.WriteAllText(path, json);
    }

    public void LoadFromFile(string path)
    {
        if (!File.Exists(path)) return;
        var json = File.ReadAllText(path);
        _userBindings = JsonSerializer.Deserialize<Dictionary<InputAction, InputBinding[]>>(json, _jsonOpts)
            ?? new();
    }
}
```

### 8.3 Gamepad Support

```csharp
// Gamepad mapping (SDL GameController API)
// Left stick: camera pan
// Right stick: cursor movement (for console-style building placement)
// Triggers: zoom in/out
// A: confirm/place, B: cancel/deselect, X: bulldoze, Y: toggle overlay
// D-pad: cycle tool categories
// Bumpers: speed up/down
// Start: pause, Select: menu

// Gamepad cursor: a virtual mouse pointer controlled by right stick
// Rendered as a custom crosshair sprite
// Snaps to tile grid for building placement
```

---

## 9. Networking (Multiplayer)

### 9.1 Architecture

Authoritative server model. The server runs the full simulation; clients send commands and receive state snapshots.

```
┌──────────────┐     ENet/UDP      ┌──────────────────┐
│   Client A   │ ◄───────────────► │                  │
│  (renderer   │                   │  Server           │
│   + input    │     ENet/UDP      │  (headless sim    │
│   + UI)      │ ◄───────────────► │   + state sync    │
├──────────────┤                   │   + validation)    │
│   Client B   │ ◄───────────────► │                  │
└──────────────┘                   └──────────────────┘
```

### 9.2 Transport Layer

```csharp
public interface INetTransport : IDisposable
{
    void Connect(string host, ushort port);
    void Disconnect();
    void SendReliable(ReadOnlySpan<byte> data);    // commands, chat
    void SendUnreliable(ReadOnlySpan<byte> data);  // cursor positions
    bool TryReceive(out byte[] data, out int channel);
    void Flush();
    NetworkStats GetStats();
}

public sealed class ENetTransport : INetTransport
{
    // ENet via ENet-CSharp NuGet package
    // 2 channels: 0 = reliable ordered (commands), 1 = unreliable (cursors, visual sync)
    // Max 8 peers (co-op city, not MMO)
    // Tick rate: 10 Hz (matches sim tick rate)
}

public sealed class SteamNetTransport : INetTransport
{
    // Steam Networking Sockets (Steamworks.NET)
    // Provides relay, NAT punch-through, encryption
    // Same 2-channel semantic
    // Preferred when running through Steam
}
```

### 9.3 Protocol

```csharp
// All messages prefixed with: [ushort messageType][uint sequence][uint timestamp]

public enum NetMessageType : ushort
{
    // Client → Server
    PlayerCommand       = 0x0100, // game command (place building, set budget, etc.)
    PlayerCursorPos     = 0x0101, // unreliable, for showing other player cursors
    ChatMessage         = 0x0102,
    RequestFullSync     = 0x0103, // on connect or desync

    // Server → Client
    StateSnapshot       = 0x0200, // quarterly city snapshot (delta-compressed)
    CommandAck          = 0x0201, // confirm command applied
    CommandReject       = 0x0202, // command rejected (permissions, validation)
    FullSyncResponse    = 0x0203, // entire world state (on connect)
    PlayerCursors       = 0x0204, // all player cursor positions
    ChatBroadcast       = 0x0205,
    SimTickSync         = 0x0206, // current tick + speed, keeps clients in sync
}
```

### 9.4 State Synchronization

Quarterly snapshots (every ~90 in-game days):
1. Server computes delta from last snapshot (only changed tiles, buildings, households)
2. Delta-encode: for each changed entity, send (index, new_value)
3. LZ4 compress the delta
4. Typical quarterly delta: ~20-50 KB (only a fraction of entities change per quarter)

Full sync (on connect): send entire WorldState, LZ4 compressed (~10 MB). Streamed over reliable channel in 64KB chunks.

```csharp
public struct StateSnapshot
{
    public uint SnapshotTick;
    public uint BaseTick;           // delta relative to this snapshot

    // Delta entries
    public DeltaEntry<TileData>[] TileDeltas;       // (index, newValue)
    public DeltaEntry<BuildingData>[] BuildingDeltas;
    public DeltaEntry<HouseholdData>[] HouseholdDeltas;
    // Vehicles and citizens are NOT synced per-entity —
    // they're derived from simulation state. Only sync aggregate stats.

    public CityStateSnapshot CityState; // budget, population, happiness, etc.
}

public struct DeltaEntry<T> where T : unmanaged
{
    public int Index;
    public T Value;
}
```

### 9.5 Headless Server Mode

```csharp
public sealed class HeadlessServer
{
    // No SDL window, no OpenGL, no ImGui, no audio
    // Just: simulation loop + network layer + save system

    public void Run(ServerConfig config)
    {
        var world = LoadOrCreateWorld(config);
        var sim = new SimulationState(world);
        var scheduler = new JobScheduler(workerCount: Environment.ProcessorCount - 1);
        var network = new ServerNetworkManager(config.Port, config.MaxPlayers);
        var save = new SaveSystem(sim);

        var tickTimer = new PreciseTimer(100); // 10 Hz

        while (_running)
        {
            network.PollEvents();
            network.ProcessPlayerCommands(sim);
            scheduler.RunTick(sim);
            sim.Swap();

            if (IsQuarterBoundary(sim.CurrentTick))
            {
                var snapshot = BuildDeltaSnapshot(sim);
                network.BroadcastSnapshot(snapshot);
            }

            if (IsAutosaveTime(sim.CurrentTick))
            {
                save.RequestSave(config.SavePath, SaveType.AutoSave);
            }

            tickTimer.WaitForNextTick();
        }
    }
}
```

### 9.6 Permissions

Co-op multiplayer with role-based permissions:

| Role | Can build | Can bulldoze | Can set budget | Can pass laws | Can save |
|------|-----------|-------------|---------------|--------------|---------|
| Mayor (host) | Yes | Yes | Yes | Yes | Yes |
| Councilor | Yes | Own district | No | Vote only | No |
| Observer | No | No | No | No | No |

Players can be assigned districts. Councilors can only build/bulldoze within their assigned district.

---

## 10. Modding Architecture

### 10.1 Data Mod Layer (JSON)

All game data defined in JSON files. Engine reads from a layered virtual filesystem:

```
Load order:
  1. base/data/         ← engine defaults (read-only)
  2. mods/core_fix/data/ ← community patch (if active)
  3. mods/my_mod/data/   ← user mod
  4. mods/another_mod/   ← another user mod (later = higher priority)

Conflict resolution: last-writer-wins per key. If two mods define building "warehouse_large",
the mod loaded later wins. Mod manager UI shows conflicts.
```

```csharp
public sealed class VirtualFileSystem
{
    private readonly List<IModSource> _sources = new(); // ordered by priority

    public void Mount(IModSource source, int priority)
    {
        _sources.Add(source);
        _sources.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }

    public string? ReadText(string virtualPath)
    {
        // Search from highest priority to lowest
        for (int i = _sources.Count - 1; i >= 0; i--)
        {
            if (_sources[i].TryReadText(virtualPath, out var text))
                return text;
        }
        return null;
    }

    public T? LoadJson<T>(string virtualPath) where T : class
    {
        var text = ReadText(virtualPath);
        return text != null ? JsonSerializer.Deserialize<T>(text) : null;
    }
}
```

Data file examples:

```json
// data/buildings/residential.json
{
  "buildings": [
    {
      "id": "house_wooden_small",
      "category": "residential",
      "era": "colonial",
      "footprint": [2, 2],
      "height": 1,
      "maxHouseholds": 2,
      "constructionCost": 500,
      "monthlyUpkeep": 5,
      "powerConsumption": 0,
      "waterConsumption": 1.0,
      "sprites": {
        "base": "buildings/house_wooden_small",
        "construction": "buildings/house_wooden_small_construction",
        "damaged": "buildings/house_wooden_small_damaged",
        "variants": 3
      },
      "requirements": {
        "tech": null,
        "terrain": ["grass", "dirt"],
        "nearRoad": true
      },
      "effects": {
        "pollution": 0.0,
        "noise": 0.1,
        "landValueBonus": 0.02
      }
    }
  ]
}

// data/tech/tree.json
{
  "technologies": [
    {
      "id": "steam_power",
      "era": "industrial",
      "researchCost": 5000,
      "prerequisites": ["iron_working", "coal_mining"],
      "unlocks": {
        "buildings": ["factory_steam", "power_plant_coal"],
        "bonuses": [{ "type": "industrial_output", "value": 1.5 }]
      }
    }
  ]
}

// data/laws/policies.json
{
  "policies": [
    {
      "id": "child_labor_ban",
      "era": "industrial",
      "effects": {
        "happiness": 0.05,
        "industrialOutput": -0.1,
        "educationAccess": 0.2
      },
      "monthlyBudgetImpact": -200,
      "prerequisiteTech": "public_education"
    }
  ]
}
```

### 10.2 Script Mods (C#)

```csharp
// Mods implement this interface, compiled as a .NET assembly (.dll)
public interface IGameMod
{
    ModManifest Manifest { get; }
    void OnLoad(IModAPI api);
    void OnUnload();
    void OnTick(uint simTick);  // called every sim tick (careful with performance)
}

// The API surface exposed to mods — sandboxed, no direct state access
public interface IModAPI
{
    // Data registration
    void RegisterBuilding(BuildingDefinition def);
    void RegisterTechnology(TechDefinition def);
    void RegisterPolicy(PolicyDefinition def);
    void RegisterEvent(GameEventDefinition def);

    // Read-only queries
    int GetPopulation();
    float GetCityFunds();
    float GetHappiness();
    TileInfo GetTileInfo(int x, int y);
    BuildingInfo? GetBuildingInfo(ushort buildingId);

    // Actions (validated by engine — mods can't cheat)
    bool TryPlaceBuilding(string buildingTypeId, int x, int y);
    void TriggerEvent(string eventId);
    void ShowNotification(string message, NotificationSeverity severity);
    void AddToResourcePool(string resourceId, float amount);

    // UI extension points
    void RegisterPanel(string panelId, Action drawCallback); // ImGui draw callback
    void RegisterOverlay(string overlayId, Func<int, int, float> valueFunc); // per-tile overlay

    // Hooks
    void OnBuildingPlaced(Action<BuildingPlacedEvent> handler);
    void OnTechUnlocked(Action<TechUnlockedEvent> handler);
    void OnYearEnd(Action<YearEndEvent> handler);
    void OnDisaster(Action<DisasterEvent> handler);
}
```

### 10.3 Mod Sandboxing

.NET 8 does not support AppDomains. Instead, use `AssemblyLoadContext` for isolation:

```csharp
public sealed class ModLoader
{
    private readonly Dictionary<string, ModInstance> _loadedMods = new();

    public void LoadMod(string modPath)
    {
        var manifest = LoadManifest(Path.Combine(modPath, "mod.json"));

        // Create isolated AssemblyLoadContext
        var context = new ModAssemblyLoadContext(modPath, isCollectible: true);
        var assembly = context.LoadFromAssemblyPath(Path.Combine(modPath, manifest.AssemblyName));

        // Find the IGameMod implementation
        var modType = assembly.GetTypes().FirstOrDefault(t => typeof(IGameMod).IsAssignableFrom(t));
        if (modType == null) throw new ModLoadException($"No IGameMod found in {manifest.Id}");

        var mod = (IGameMod)Activator.CreateInstance(modType)!;

        // Validate: mod assembly cannot reference System.IO, System.Net, System.Diagnostics.Process
        ValidateModSecurity(assembly);

        var api = new SandboxedModAPI(manifest.Id, _worldState, _commandQueue);
        mod.OnLoad(api);

        _loadedMods[manifest.Id] = new ModInstance(mod, context, api);
    }

    public void UnloadMod(string modId)
    {
        if (_loadedMods.TryGetValue(modId, out var instance))
        {
            instance.Mod.OnUnload();
            instance.Api.Dispose(); // unhook all event handlers
            instance.Context.Unload(); // GC will collect the assembly
            _loadedMods.Remove(modId);
        }
    }

    private void ValidateModSecurity(Assembly assembly)
    {
        // Scan IL for forbidden type references
        var forbidden = new HashSet<string>
        {
            "System.IO.File", "System.IO.Directory", "System.IO.Path",
            "System.Net.Http.HttpClient", "System.Net.Sockets",
            "System.Diagnostics.Process",
            "System.Reflection.Assembly",
            "System.Runtime.InteropServices.Marshal"
        };

        foreach (var type in assembly.GetTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static |
                         BindingFlags.Public | BindingFlags.NonPublic))
            {
                // Check method body for references to forbidden types
                // (simplified — production would use Mono.Cecil for IL inspection)
            }
        }
    }
}

public sealed class ModAssemblyLoadContext : AssemblyLoadContext
{
    private readonly string _modPath;

    public ModAssemblyLoadContext(string modPath, bool isCollectible)
        : base(isCollectible: isCollectible)
    {
        _modPath = modPath;
    }

    protected override Assembly? Load(AssemblyName name)
    {
        // Only allow loading assemblies from the mod's own directory
        // and the game's shared API assembly
        var candidate = Path.Combine(_modPath, name.Name + ".dll");
        if (File.Exists(candidate))
            return LoadFromAssemblyPath(candidate);

        return null; // fall back to default context for shared assemblies
    }
}
```

### 10.4 Asset Mods

```
mods/my_building_mod/
  mod.json           ← manifest
  data/
    buildings/
      my_buildings.json
  assets/
    sprites/
      buildings/
        my_factory.png
        my_factory_construction.png
    audio/
      sfx/
        my_factory_ambient.ogg
```

The engine's asset loader checks VFS before loading any sprite or audio file. Mods can override base game assets by placing files at the same virtual path.

```csharp
// Sprite loading with VFS
public Texture2D LoadSprite(string virtualPath)
{
    // Check mod VFS first, then base game
    byte[]? data = _vfs.ReadBytes($"assets/sprites/{virtualPath}.png");
    if (data == null)
        throw new AssetNotFoundException(virtualPath);

    return CreateTextureFromPNG(data);
}
```

### 10.5 Steam Workshop

```csharp
public sealed class WorkshopIntegration
{
    // Uses Steamworks.NET for Workshop API
    // Upload: zip mod directory → create Workshop item → upload
    // Download: subscribe in Steam → mod appears in mods/ directory
    // Updates: Steam handles download, engine detects version change on next launch

    public async Task PublishMod(string modPath)
    {
        var manifest = LoadManifest(modPath);
        var item = await SteamUGC.CreateItem(GAME_APP_ID, EWorkshopFileType.Community);

        var update = SteamUGC.StartItemUpdate(GAME_APP_ID, item.PublishedFileId);
        SteamUGC.SetItemTitle(update, manifest.Name);
        SteamUGC.SetItemDescription(update, manifest.Description);
        SteamUGC.SetItemContent(update, modPath);
        SteamUGC.SetItemPreview(update, Path.Combine(modPath, "preview.png"));
        SteamUGC.SetItemTags(update, manifest.Tags);

        await SteamUGC.SubmitItemUpdate(update, "Initial upload");
    }

    public List<InstalledMod> GetSubscribedMods()
    {
        uint count = SteamUGC.GetNumSubscribedItems();
        var ids = new PublishedFileId_t[count];
        SteamUGC.GetSubscribedItems(ids, count);

        var mods = new List<InstalledMod>();
        foreach (var id in ids)
        {
            if (SteamUGC.GetItemInstallInfo(id, out _, out string folder, 1024, out _))
            {
                mods.Add(new InstalledMod(id, folder));
            }
        }
        return mods;
    }
}
```

### 10.6 Mod Load Order and Conflict Detection

```csharp
public sealed class ModManager
{
    // mod_order.json in user config directory
    // Lists mod IDs in load order. Mods later in the list override earlier ones.
    private List<string> _loadOrder;

    public struct ModConflict
    {
        public string ModA;
        public string ModB;
        public string ConflictType;  // "overrides_building", "overrides_tech", etc.
        public string ConflictKey;   // the specific ID being overridden
    }

    public List<ModConflict> DetectConflicts()
    {
        var conflicts = new List<ModConflict>();
        var claimedKeys = new Dictionary<string, string>(); // key → first mod that claims it

        foreach (var modId in _loadOrder)
        {
            var manifest = GetManifest(modId);
            foreach (var key in manifest.ModifiedKeys) // e.g., "building:warehouse_large"
            {
                if (claimedKeys.TryGetValue(key, out var existingMod))
                {
                    conflicts.Add(new ModConflict
                    {
                        ModA = existingMod,
                        ModB = modId,
                        ConflictType = key.Split(':')[0],
                        ConflictKey = key
                    });
                }
                claimedKeys[key] = modId;
            }
        }
        return conflicts;
    }
}
```

---

## 11. Full Memory Budget

| System | Budget | Notes |
|--------|--------|-------|
| Tile data | 32 MB | 1M tiles x 32B |
| Entity pools | 30 MB | buildings + households + citizens + vehicles |
| Derived maps | 20 MB | traffic, services, power, water, spatial indices |
| Double-buffer overhead | 10 MB | selective, not full copy |
| **Simulation subtotal** | **92 MB** | |
| Chunk VBOs (GPU) | 15 MB | 25 visible chunks, all LODs |
| Texture atlases (GPU) | 256 MB | shared VRAM |
| Render targets / FBOs | 30 MB | scene FBO + overlay + post-process |
| **Rendering subtotal** | **301 MB** | mostly VRAM |
| Audio buffers | 26 MB | see audio section |
| ImGui | 5 MB | draw lists + font atlas |
| Chunk streaming cache | 8 MB | CPU-side tile data for 64 chunks |
| .NET runtime overhead | 50 MB | GC heap, JIT, stacks |
| **Misc subtotal** | **89 MB** | |
| **TOTAL RAM** | **~480 MB** | |
| **TOTAL VRAM** | **~300 MB** | |

Well under the 2 GB combined target.

---

## 12. Engine Bootstrap and Main Loop

```csharp
public sealed class ForgeEngine : IDisposable
{
    private SDL.SDL_WindowFlags _windowFlags;
    private IntPtr _window;
    private IntPtr _glContext;

    private SimulationState _simState;
    private JobScheduler _scheduler;
    private StreamManager _streamManager;
    private Renderer _renderer;
    private InputManager _input;
    private AdaptiveMusicEngine _music;
    private AmbienceManager _ambience;
    private SaveSystem _saveSystem;
    private ModManager _modManager;
    private CommandQueue _commandQueue;

    private Thread _simThread;
    private volatile bool _running;

    public void Initialize(EngineConfig config)
    {
        // 1. SDL init
        SDL.SDL_Init(SDL.SDL_INIT_VIDEO | SDL.SDL_INIT_AUDIO | SDL.SDL_INIT_GAMECONTROLLER);
        SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_CONTEXT_MAJOR_VERSION, 4);
        SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_CONTEXT_MINOR_VERSION, 3);
        SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_CONTEXT_PROFILE_MASK,
            (int)SDL.SDL_GLprofile.SDL_GL_CONTEXT_PROFILE_CORE);

        _window = SDL.SDL_CreateWindow("Iron & Oak",
            SDL.SDL_WINDOWPOS_CENTERED, SDL.SDL_WINDOWPOS_CENTERED,
            config.WindowWidth, config.WindowHeight,
            SDL.SDL_WindowFlags.SDL_WINDOW_OPENGL | SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE);

        _glContext = SDL.SDL_GL_CreateContext(_window);
        SDL.SDL_GL_SetSwapInterval(config.VSync ? 1 : 0);

        // 2. Load OpenGL function pointers (via Silk.NET.OpenGL or hand-rolled)
        GL.LoadBindings();

        // 3. Init subsystems
        _input = new InputManager(_window);
        _modManager = new ModManager();
        _modManager.LoadActiveMods();

        _simState = new SimulationState(config.MapWidth, config.MapHeight);
        _commandQueue = new CommandQueue();
        _scheduler = new JobScheduler();

        _renderer = new Renderer(_simState, config);
        _streamManager = new StreamManager(_simState, _renderer);
        _saveSystem = new SaveSystem(_simState);

        _music = new AdaptiveMusicEngine();
        _ambience = new AmbienceManager();

        // 4. ImGui init
        ImGuiSetup.Initialize(_window, _glContext);
        ImGuiSetup.ConfigurePixelStyle(ImGui.GetIO());
        ApplyIronOakStyle();

        // 5. Start simulation thread
        _simThread = new Thread(SimulationLoop)
        {
            Name = "SimulationThread",
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal
        };
        _running = true;
        _simThread.Start();
    }

    public void RunMainLoop()
    {
        var stopwatch = Stopwatch.StartNew();
        double previousTime = 0;

        while (_running)
        {
            double currentTime = stopwatch.Elapsed.TotalSeconds;
            float deltaTime = (float)(currentTime - previousTime);
            previousTime = currentTime;

            // 1. Input
            _input.Update(deltaTime);
            if (_input.IsActionPressed(InputAction.MenuClose))
                _running = false; // or open quit dialog

            // 2. Process input → commands
            ProcessPlayerInput(deltaTime);

            // 3. Update camera
            _renderer.Camera.Update(deltaTime, _input);

            // 4. Stream chunks
            _streamManager.Update(_renderer.Camera.GetFrustum(), _renderer.Camera.Velocity);

            // 5. Update audio
            _music.Update(deltaTime, GetCityStateSnapshot());
            _ambience.Update(_renderer.Camera.ZoomLevel, _renderer.Camera.GetFrustum(), _simState.Front);

            // 6. Begin ImGui frame
            ImGuiSetup.NewFrame(deltaTime);

            // 7. Draw UI panels (reads from front buffer)
            DrawAllPanels();

            // 8. Render
            _renderer.BeginFrame();
            _renderer.RenderScene(_simState.Front); // terrain, buildings, vehicles, overlays, particles, water
            _renderer.PostProcess();
            ImGuiSetup.Render(); // ImGui on top
            _renderer.EndFrame();

            // 9. Swap
            SDL.SDL_GL_SwapWindow(_window);
        }
    }

    private void SimulationLoop()
    {
        var tickTimer = new PreciseTimer(targetTickMs: 100); // 10 Hz

        while (_running)
        {
            // Execute one full simulation tick
            _scheduler.RunTick(_simState.Back, _commandQueue);

            // Swap buffers
            _simState.Swap();

            // Wait for next tick
            tickTimer.WaitForNextTick();
        }
    }

    private void ProcessPlayerInput(float deltaTime)
    {
        // Translate UI/input actions into game commands
        if (_input.IsActionPressed(InputAction.PauseToggle))
            _commandQueue.Enqueue(new TogglePauseCommand());

        if (_input.IsActionPressed(InputAction.SpeedUp))
            _commandQueue.Enqueue(new ChangeSpeedCommand(1));

        // Building placement, zone painting, etc. handled by tool-specific input processors
        _activeToolProcessor?.ProcessInput(_input, _commandQueue, _renderer.Camera);
    }
}
```

---

## 13. Project Structure

```
IronAndOak/
├── IronAndOak.sln
├── src/
│   ├── Engine/                          # Core engine library
│   │   ├── Engine.csproj
│   │   ├── Core/
│   │   │   ├── ForgeEngine.cs           # Main engine class + bootstrap
│   │   │   ├── SimulationState.cs       # Double-buffered world state
│   │   │   ├── WorldState.cs            # Entity pools + tile array
│   │   │   └── GameTime.cs              # Tick counter, speed multiplier
│   │   ├── ECS/
│   │   │   ├── EntityPool.cs            # Generic SoA pool
│   │   │   ├── TileData.cs
│   │   │   ├── BuildingData.cs
│   │   │   ├── HouseholdData.cs
│   │   │   ├── CitizenData.cs
│   │   │   ├── VehicleData.cs
│   │   │   └── SpatialGrid.cs           # Spatial index
│   │   ├── Jobs/
│   │   │   ├── JobScheduler.cs
│   │   │   ├── SimJob.cs                # Base class
│   │   │   ├── TrafficSimJob.cs
│   │   │   ├── EconomyTickJob.cs
│   │   │   ├── PopulationTickJob.cs
│   │   │   ├── LandValuePropagationJob.cs
│   │   │   ├── ServiceCoverageJob.cs
│   │   │   └── PollutionPropagationJob.cs
│   │   ├── Rendering/
│   │   │   ├── Renderer.cs              # Main render pipeline
│   │   │   ├── IsometricCamera.cs
│   │   │   ├── ChunkRenderer.cs         # Per-chunk VBO management
│   │   │   ├── VehicleRenderer.cs       # GPU instancing
│   │   │   ├── ParticleSystem.cs        # GPU compute particles
│   │   │   ├── OverlayRenderer.cs       # Compute shader overlays
│   │   │   ├── WaterRenderer.cs
│   │   │   ├── PostProcessor.cs         # Bloom, vignette, CRT, day/night
│   │   │   ├── TextureAtlas.cs          # Texture array management
│   │   │   └── Shaders/
│   │   │       ├── isometric_tile.vert
│   │   │       ├── isometric_tile.frag
│   │   │       ├── vehicle_instanced.vert
│   │   │       ├── vehicle_instanced.frag
│   │   │       ├── overlay_compute.comp
│   │   │       ├── particle_compute.comp
│   │   │       ├── particle_render.vert/frag
│   │   │       ├── water.vert/frag
│   │   │       └── post_process.vert/frag
│   │   ├── Streaming/
│   │   │   ├── StreamManager.cs
│   │   │   ├── ChunkLoader.cs           # Background thread disk I/O
│   │   │   └── ChunkCache.cs            # LRU cache
│   │   ├── Audio/
│   │   │   ├── AudioEngine.cs           # miniaudio wrapper
│   │   │   ├── AdaptiveMusicEngine.cs
│   │   │   ├── AmbienceManager.cs
│   │   │   └── SfxPool.cs
│   │   ├── Input/
│   │   │   ├── InputManager.cs
│   │   │   ├── InputAction.cs
│   │   │   ├── InputBinding.cs
│   │   │   ├── InputContext.cs
│   │   │   └── KeybindConfig.cs
│   │   ├── UI/
│   │   │   ├── ImGuiSetup.cs            # Init, style, fonts
│   │   │   ├── PanelLayout.cs           # Docking defaults
│   │   │   ├── Panels/
│   │   │   │   ├── MinimapPanel.cs
│   │   │   │   ├── BudgetPanel.cs
│   │   │   │   ├── TechTreePanel.cs
│   │   │   │   ├── InspectorPanel.cs
│   │   │   │   ├── BuildMenuPanel.cs
│   │   │   │   ├── DistrictPanel.cs
│   │   │   │   └── ResourceBarPanel.cs
│   │   │   └── Widgets/
│   │   │       ├── HeatmapLegend.cs
│   │   │       ├── TechTreeGraph.cs
│   │   │       └── IsometricMinimap.cs
│   │   ├── SaveLoad/
│   │   │   ├── SaveSystem.cs
│   │   │   ├── SaveFileFormat.cs