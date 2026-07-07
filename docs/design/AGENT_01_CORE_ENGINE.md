# AGENT 01: CORE ENGINE (Research-Informed v2)

> **Historical — pre-web pivot.** See [WEB_V1_SCOPE.md](WEB_V1_SCOPE.md) and [CLAUDE.md](../../CLAUDE.md).

## Role
Build the Godot 4 project scaffold, engine-agnostic simulation core, chunked isometric grid, camera controller, time/era manager, save/load system (FlatBuffers + Zstd), and the data bridge between pure C# simulation and Godot rendering.

**This agent runs FIRST. All other agents depend on its output.**

---

## CRITICAL ARCHITECTURE RULE

Based on deep tech stack research (see `TECH_STACK_RESEARCH.md`):

> **The simulation core MUST be a pure C# library with ZERO Godot dependencies.**
> No `using Godot;` in any simulation file. No Node inheritance. No GodotObject.
> The simulation is engine-agnostic -- it could run in Unity, MonoGame, or a console app.

This is the single most important architectural decision. It enables:
1. Threading safety (simulation on dedicated thread)
2. Engine portability (if Godot can't handle rendering at scale, swap renderers without rewriting game logic)
3. Testability (unit test simulation without launching Godot)
4. Performance (no Godot marshalling overhead in tight loops)

**Architecture diagram:**
```
┌─────────────────────────────────────────────────────────┐
│  SIMULATION CORE (Pure C#, separate .csproj)            │
│  NO using Godot; -- NO Node inheritance                 │
│                                                          │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐   │
│  │ Grid     │ │ Pop      │ │ Economy  │ │ Transport│   │
│  │ SoA data │ │ 20k HH   │ │ Leontief │ │ BPR flow │   │
│  └──────────┘ └──────────┘ └──────────┘ └──────────┘   │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐                │
│  │ Services │ │ Politics │ │ Research │                │
│  └──────────┘ └──────────┘ └──────────┘                │
│                                                          │
│  SimulationLoop: dedicated thread, ticked               │
│  Output: SimulationSnapshot (immutable, double-buffered)│
└───────────────────────┬─────────────────────────────────┘
                        │ SimulationSnapshot (read-only struct)
                        │ Contains: changed tiles, vehicle positions,
                        │ population stats, budget numbers, overlay data
┌───────────────────────┴─────────────────────────────────┐
│  DATA BRIDGE (C# with Godot refs, thin adapter)         │
│  Converts SimulationSnapshot → Godot draw calls         │
│  Enqueues player commands → simulation command queue    │
└───────────────────────┬─────────────────────────────────┘
                        │
┌───────────────────────┴─────────────────────────────────┐
│  GODOT RENDERING FRONTEND (GDScript + scenes)           │
│  • Chunked TileMap (32x32 chunks, camera-loaded)        │
│  • RenderingServer for vehicles/citizens (NOT Node2D)   │
│  • Control nodes for UI                                 │
│  • Shaders for day/night, seasons, aging, weather       │
│  Main thread only                                       │
└─────────────────────────────────────────────────────────┘
```

---

## Phase 1: Project Scaffold (Day 1)

### Tasks
1. Create Godot 4 project with C# support
2. Create TWO .csproj files:
   - `IronAndOak.Simulation.csproj` -- Pure C# class library, targets .NET 8, NO Godot refs
   - `IronAndOak.Game.csproj` -- Godot C# project, references IronAndOak.Simulation

3. Set up folder structure:
```
iron_and_oak/
├── project.godot
├── .godot/
├── IronAndOak.Simulation/          # PURE C# -- NO GODOT DEPENDENCIES
│   ├── IronAndOak.Simulation.csproj
│   ├── Core/
│   │   ├── GridData.cs              # SoA tile data
│   │   ├── ChunkManager.cs          # 32x32 chunk dirty tracking
│   │   ├── CoordinateHelper.cs      # Grid math (no Godot Vector2I)
│   │   ├── SimulationLoop.cs        # Main tick loop, thread management
│   │   ├── DoubleBuffer.cs          # Thread-safe state swap
│   │   ├── SimulationSnapshot.cs    # Immutable output for renderer
│   │   ├── CommandQueue.cs          # Player actions queued for sim
│   │   └── GameClock.cs             # Time, calendar, speed, era tracking
│   ├── Population/                  # Agent 02 writes here
│   ├── Economy/                     # Agent 03 writes here
│   ├── Transport/                   # Agent 04 writes here
│   ├── Services/                    # Agent 06 writes here
│   ├── Politics/                    # Agent 07 writes here
│   └── Research/                    # Part of Agent 02/03
├── scripts/                        # GODOT-DEPENDENT CODE
│   ├── bridge/                     # C# bridge (has using Godot;)
│   │   ├── SimulationBridge.cs     # Converts snapshot → Godot calls
│   │   └── CommandAdapter.cs       # Converts Godot input → sim commands
│   ├── game/                       # GDScript game layer
│   │   ├── build_system/
│   │   ├── zone_system/
│   │   ├── event_system/
│   │   └── policy_system/
│   ├── render/                     # GDScript render layer
│   │   ├── ChunkedTileMap.gd       # Loads/unloads 32x32 chunks
│   │   ├── VehicleRenderer.gd      # RenderingServer pooled sprites
│   │   ├── CitizenRenderer.gd      # RenderingServer pooled sprites
│   │   ├── WeatherSystem.gd
│   │   └── overlays/
│   └── ui/                         # GDScript UI
│       ├── hud/
│       ├── panels/
│       ├── overlays/
│       └── menus/
├── scenes/
│   ├── main/
│   ├── ui/
│   ├── buildings/
│   ├── vehicles/
│   └── effects/
├── assets/
│   ├── sprites/
│   ├── shaders/
│   ├── audio/
│   └── fonts/
├── data/                           # JSON content (moddable)
│   ├── buildings.json
│   ├── technologies.json
│   ├── laws.json
│   ├── events.json
│   ├── production_chains.json
│   ├── vehicles.json
│   ├── cultural_presets.json
│   └── schemas/                    # FlatBuffer schemas (.fbs)
├── mods/                           # User mod directory
│   └── README.md
└── tests/
    ├── IronAndOak.Simulation.Tests/ # xUnit tests, no Godot required
    └── test_scenes/                 # Godot test scenes
```

4. Configure autoload singletons:
   - `GameManager` (GDScript) -- top-level game state, speed controls
   - `SimulationBridge` (C#) -- bridge, reads snapshots, enqueues commands
   - `EventBus` (GDScript) -- UI signal hub (NOT simulation signals)
   - `AudioManager` (GDScript) -- music/SFX
   - `SaveManager` (GDScript) -- orchestrates save/load

### AI Tool Usage
- **Claude**: Generate both .csproj files, folder creation script, autoload skeletons
- **Claude**: Generate the `ISimulationCore` interface that defines the contract between sim and renderer
- **Kimi**: NOT needed

### Output Files
- `project.godot` configured
- `IronAndOak.Simulation/IronAndOak.Simulation.csproj` (pure C#, .NET 8, no Godot)
- `IronAndOak.Game.csproj` (Godot project, refs Simulation)
- All autoload GDScript skeletons
- `IronAndOak.Simulation/Core/ISimulationCore.cs` (master interface)

---

## Phase 2: Grid System -- SoA Layout (Day 1-2)

### Tasks
1. **Grid Data in Struct-of-Arrays** (40-60% faster iteration than AoS):
```csharp
// IronAndOak.Simulation/Core/GridData.cs
// NO using Godot; -- uses plain ints for coordinates

public sealed class GridData {
    public readonly int Width;
    public readonly int Height;

    // Struct of Arrays -- each array is contiguous for cache-friendly iteration
    public byte[] TerrainTypes;       // 0-15, one per tile
    public byte[] Elevations;         // 0-3 levels
    public byte[] ZoneTypes;          // 0=none, 1=R, 2=C, 3=I, 4=A, 5=O, 6=P
    public ushort[] BuildingTypeIds;  // 0=empty, 1-65535 building type
    public uint[] BuildingInstanceIds;// unique instance ref (0 = no building)
    public byte[] RoadTypes;         // 0=none, 1-8 road types
    public byte[] RoadConnections;   // bitmask: N,NE,E,SE,S,SW,W,NW
    public uint[] Flags;             // bitflags: power, water, sewer, internet, etc.
    public float[] LandValues;       // cached land value per tile
    public float[] PollutionLevels;  // cached pollution per tile
    public byte[] ServiceCoverage;   // packed: fire(2bit), police(2bit), health(2bit), edu(2bit)

    // Index helper -- no Godot Vector2I dependency
    public int Index(int x, int y) => y * Width + x;

    // Memory: 256x256 * ~24 bytes/tile = ~1.5 MB (fits in L2 cache)
}
```

2. **Chunk Manager** for dirty-flag updates:
```csharp
// 32x32 tile chunks, 8x8 chunk grid for 256x256 map
public sealed class ChunkManager {
    public const int ChunkSize = 32;
    private readonly bool[] _dirtyFlags;  // one per chunk
    private readonly int _chunksWide;

    public void MarkDirty(int tileX, int tileY) {
        int cx = tileX / ChunkSize;
        int cy = tileY / ChunkSize;
        _dirtyFlags[cy * _chunksWide + cx] = true;
    }

    public IEnumerable<(int cx, int cy)> GetDirtyChunks() { ... }
    public void ClearDirty() { ... }
}
```

3. **Coordinate Helper** (pure math, no Godot types):
```csharp
public static class CoordinateHelper {
    // Isometric tile size in pixels
    public const int TileWidth = 32;
    public const int TileHeight = 16;

    // Grid → Screen (isometric projection)
    public static (int screenX, int screenY) GridToScreen(int gridX, int gridY) {
        int sx = (gridX - gridY) * (TileWidth / 2);
        int sy = (gridX + gridY) * (TileHeight / 2);
        return (sx, sy);
    }

    // Screen → Grid (inverse isometric projection)
    public static (int gridX, int gridY) ScreenToGrid(float screenX, float screenY) {
        float gx = (screenX / (TileWidth / 2f) + screenY / (TileHeight / 2f)) / 2f;
        float gy = (screenY / (TileHeight / 2f) - screenX / (TileWidth / 2f)) / 2f;
        return ((int)Math.Floor(gx), (int)Math.Floor(gy));
    }
}
```

4. **Map Generation** (pure C#):
   - Simplex noise for terrain elevation (no Godot noise -- use a pure C# noise library)
   - Water bodies (rivers, lakes, coastline)
   - Forest clusters
   - Resource deposit placement (random but balanced per map type)
   - 5 map types: Valley, Coastal, Mountain, Plains, River Delta
   - 8 climate types (from CULTURAL_DNA_EMERGENCE.md)

### AI Tool Usage
- **Claude**: Generate SoA grid data, chunk manager, coordinate math, map generator
- **Claude**: Find/recommend a pure C# Simplex noise library (no Godot dependency)
- **Kimi**: NOT needed

### Output Files
- `IronAndOak.Simulation/Core/GridData.cs`
- `IronAndOak.Simulation/Core/ChunkManager.cs`
- `IronAndOak.Simulation/Core/CoordinateHelper.cs`
- `IronAndOak.Simulation/Core/MapGenerator.cs`
- `tests/IronAndOak.Simulation.Tests/GridDataTests.cs`

### Validation Criteria
- [ ] Grid creation + map gen in <100ms (test in console app, no Godot)
- [ ] Coordinate conversion is mathematically correct (unit tests)
- [ ] Chunk dirty flags work (unit tests)
- [ ] Map gen produces balanced resource distributions (unit tests)
- [ ] **NO `using Godot;` in any file** -- verified by test build of Simulation.csproj standalone

---

## Phase 3: Simulation Loop & Double Buffer (Day 2)

### Tasks
1. **SimulationLoop** (runs on dedicated thread):
```csharp
public sealed class SimulationLoop {
    private readonly DoubleBuffer<SimulationSnapshot> _buffer;
    private readonly Thread _simThread;
    private volatile bool _running;
    private volatile int _speedMultiplier = 1; // 0=pause, 1=1x, 2=2x, 4=4x, 8=8x

    // Tiered tick rates (real-time at 1x speed)
    private const float TrafficTickInterval = 0.5f;    // 2/sec
    private const float EconomyTickInterval = 86400f;   // 1/game-day (24 game-hours)
    private const float PopulationTickInterval = 2592000f; // 1/game-month (30 game-days)

    public void Start() {
        _simThread = new Thread(SimLoop) { IsBackground = true, Name = "SimulationThread" };
        _simThread.Start();
    }

    private void SimLoop() {
        while (_running) {
            float dt = CalculateDelta() * _speedMultiplier;
            if (_speedMultiplier == 0) { Thread.Sleep(16); continue; }

            // Tick each subsystem at its appropriate rate
            _trafficAccum += dt;
            _economyAccum += dt;
            _populationAccum += dt;

            if (_trafficAccum >= TrafficTickInterval) {
                _transportSystem.Tick();
                _trafficAccum -= TrafficTickInterval;
            }
            if (_economyAccum >= EconomyTickInterval) {
                _economySystem.Tick();
                _economyAccum -= EconomyTickInterval;
            }
            if (_populationAccum >= PopulationTickInterval) {
                _populationSystem.Tick();
                _researchSystem.Tick();
                _politicsSystem.Tick();
                _populationAccum -= PopulationTickInterval;
            }

            // Always: recalculate dirty land values
            _landValueCalculator.ProcessDirtyChunks();

            // Produce snapshot for renderer
            var snapshot = BuildSnapshot();
            _buffer.SwapWrite(snapshot);

            Thread.Sleep(1); // Don't burn CPU
        }
    }
}
```

2. **DoubleBuffer** (thread-safe state transfer):
```csharp
public sealed class DoubleBuffer<T> where T : class {
    private T _front; // renderer reads this
    private T _back;  // simulation writes this
    private readonly object _lock = new();

    public T ReadFront() {
        lock (_lock) return _front;
    }

    public void SwapWrite(T newBack) {
        lock (_lock) {
            _front = newBack;
        }
    }
}
```

3. **SimulationSnapshot** (immutable output for renderer):
```csharp
public sealed class SimulationSnapshot {
    // Changed tiles since last snapshot (sparse, only what changed)
    public readonly (int x, int y, byte terrain, byte zone, ushort building)[] ChangedTiles;

    // Vehicle positions for rendering (cosmetic only)
    public readonly (int screenX, int screenY, byte vehicleType, byte direction)[] Vehicles;

    // Aggregate stats for UI
    public readonly int Population;
    public readonly float Budget;
    public readonly float Happiness;
    public readonly float Approval;
    public readonly int Era;
    public readonly GameDate Date;

    // Overlay data (only sent when overlay is active)
    public readonly float[]? TrafficOverlay;     // per-road-segment congestion
    public readonly float[]? PollutionOverlay;   // per-tile pollution
    public readonly float[]? LandValueOverlay;   // per-tile land value
    public readonly float[]? CrimeOverlay;       // per-tile crime rate
    // ... more overlays as needed
}
```

4. **CommandQueue** (player actions from render thread to sim thread):
```csharp
public sealed class CommandQueue {
    private readonly ConcurrentQueue<ISimCommand> _queue = new();

    public void Enqueue(ISimCommand cmd) => _queue.Enqueue(cmd);
    public bool TryDequeue(out ISimCommand cmd) => _queue.TryDequeue(out cmd);
}

public interface ISimCommand { }
public record PlaceRoadCommand(int X, int Y, byte RoadType) : ISimCommand;
public record SetZoneCommand(int X, int Y, byte ZoneType) : ISimCommand;
public record PlaceBuildingCommand(int X, int Y, ushort BuildingTypeId) : ISimCommand;
public record DemolishCommand(int X, int Y) : ISimCommand;
public record ChangeSpeedCommand(int Speed) : ISimCommand;
public record ChangeTaxCommand(byte TaxType, float Rate) : ISimCommand;
public record EnactLawCommand(string LawId) : ISimCommand;
public record SetResearchCommand(string TechId) : ISimCommand;
```

### AI Tool Usage
- **Claude**: Generate thread-safe simulation loop, double buffer, command queue
- **Kimi**: NOT needed

### Output Files
- `IronAndOak.Simulation/Core/SimulationLoop.cs`
- `IronAndOak.Simulation/Core/DoubleBuffer.cs`
- `IronAndOak.Simulation/Core/SimulationSnapshot.cs`
- `IronAndOak.Simulation/Core/CommandQueue.cs`
- `IronAndOak.Simulation/Core/Commands.cs`
- `tests/IronAndOak.Simulation.Tests/DoubleBufferTests.cs`
- `tests/IronAndOak.Simulation.Tests/CommandQueueTests.cs`

### Validation Criteria
- [ ] Simulation loop runs at stable tick rate on background thread
- [ ] Double buffer swap is atomic (no torn reads)
- [ ] Command queue is lock-free and correct under concurrent access
- [ ] Snapshot contains only changed data (sparse updates)
- [ ] All tests pass WITHOUT launching Godot

---

## Phase 4: Chunked TileMap Renderer (Day 2-3)

### Tasks
1. **ChunkedTileMap** (GDScript, handles Godot's Y-sort performance issue):
```gdscript
# Only loads chunks visible to camera + 1-chunk buffer
# Solves the known Godot TileMap performance cliff at large scales

class_name ChunkedTileMap extends Node2D

const CHUNK_SIZE = 32
const CHUNK_BUFFER = 1  # extra chunks around viewport

var _loaded_chunks: Dictionary = {}  # {Vector2I: TileMapLayer}
var _camera: Camera2D

func _process(_delta):
    var visible = _get_visible_chunk_range()
    _load_new_chunks(visible)
    _unload_distant_chunks(visible)

func _get_visible_chunk_range() -> Rect2i:
    var cam_rect = _camera.get_viewport_rect()
    # Convert screen rect to chunk coordinates
    # Return range of chunks that should be loaded

func _load_chunk(cx: int, cy: int):
    var layer = TileMapLayer.new()
    layer.tile_set = _tile_set
    # Fill tiles from simulation snapshot for this chunk
    add_child(layer)
    _loaded_chunks[Vector2i(cx, cy)] = layer

func _unload_chunk(cx: int, cy: int):
    var layer = _loaded_chunks.get(Vector2i(cx, cy))
    if layer:
        layer.queue_free()
        _loaded_chunks.erase(Vector2i(cx, cy))
```

2. **Vehicle Renderer** (RenderingServer, NOT Node2D -- bypasses scene tree):
```gdscript
class_name VehicleRenderer extends Node2D

const MAX_VEHICLES = 500
var _vehicle_rids: Array[RID] = []

func _ready():
    # Pre-allocate 500 canvas items via RenderingServer
    for i in MAX_VEHICLES:
        var rid = RenderingServer.canvas_item_create()
        RenderingServer.canvas_item_set_parent(rid, get_canvas_item())
        _vehicle_rids.append(rid)

func update_from_snapshot(vehicles: Array):
    for i in vehicles.size():
        if i >= MAX_VEHICLES: break
        var v = vehicles[i]
        RenderingServer.canvas_item_clear(_vehicle_rids[i])
        var tex = _get_vehicle_texture(v.type, v.direction)
        RenderingServer.canvas_item_add_texture_rect(
            _vehicle_rids[i],
            Rect2(v.screen_x, v.screen_y, tex.get_width(), tex.get_height()),
            tex.get_rid()
        )
    # Hide unused vehicles
    for i in range(vehicles.size(), MAX_VEHICLES):
        RenderingServer.canvas_item_clear(_vehicle_rids[i])
```

3. **Citizen Renderer** (same RenderingServer pattern, 200 max)

### AI Tool Usage
- **Claude**: Generate ChunkedTileMap with camera-based loading
- **Claude**: Generate RenderingServer vehicle/citizen renderers
- **Research**: Godot RenderingServer docs for batch drawing best practices
- **Kimi**: NOT needed

### Output Files
- `scripts/render/ChunkedTileMap.gd`
- `scripts/render/VehicleRenderer.gd`
- `scripts/render/CitizenRenderer.gd`

### Validation Criteria
- [ ] 256x256 map renders at 60fps with chunked loading
- [ ] Camera pan/zoom doesn't cause visible chunk pop-in (buffer chunks)
- [ ] 500 vehicles render via RenderingServer without frame drops
- [ ] Y-sort isometric ordering is correct within loaded chunks

---

## Phase 5: Camera System (Day 3)

### Tasks
1. **Camera Controller** (GDScript):
   - Smooth pan: WASD/arrows, mouse drag, edge scrolling
   - 4-level zoom (updated from research -- 4 levels, not 5):
     - Level 1 (closest): Full detail, citizens, vehicle details
     - Level 2 (district): Simplified sprites, no citizens
     - Level 3 (city): Zone colors, major roads, data overlays
     - Level 4 (region): Full map, minimap-style, trade routes
   - 4-direction rotation (Q/E)
   - Minimap with viewport indicator
   - Camera bounds (can't pan beyond map)
   - All movements use smooth interpolation (lerp)

2. **LOD Manager** (tied to zoom):
   - Notifies ChunkedTileMap of detail level
   - Notifies VehicleRenderer to show/hide
   - Notifies CitizenRenderer to show/hide
   - At zoom 3-4: overlay mode, reduced tile detail

### AI Tool Usage
- **Claude**: Generate camera controller with smooth interpolation
- **Claude**: Generate LOD manager
- **Kimi**: NOT needed

### Output Files
- `scripts/render/CameraController.gd`
- `scripts/render/LODManager.gd`
- `scripts/render/Minimap.gd`
- `scenes/main/Camera2D.tscn`

---

## Phase 6: Save/Load (FlatBuffers + Zstd) (Day 3-4)

### Tasks
1. **FlatBuffer Schemas** (.fbs files):
```flatbuffers
// data/schemas/grid.fbs
namespace IronAndOak.Save;

table GridSave {
    width: int;
    height: int;
    terrain_types: [ubyte];
    elevations: [ubyte];
    zone_types: [ubyte];
    building_type_ids: [ushort];
    building_instance_ids: [uint];
    road_types: [ubyte];
    road_connections: [ubyte];
    flags: [uint];
}

// data/schemas/save.fbs
table SaveFile {
    version: int;
    timestamp: long;
    city_name: string;
    playtime_seconds: int;
    era: int;
    grid: GridSave;
    households: HouseholdsSave;
    economy: EconomySave;
    transport: TransportSave;
    politics: PoliticsSave;
    research: ResearchSave;
    events: EventsSave;
}
```

2. **Save Pipeline**:
   - Serialize simulation state to FlatBuffer (zero-copy friendly)
   - Compress with Zstd on background thread
   - Write to disk atomically (write to .tmp, rename to .ioak)
   - Capture 128x128 thumbnail screenshot
   - Target: <1 second save, ~3-5 MB file size

3. **Load Pipeline**:
   - Read file, decompress Zstd
   - FlatBuffer zero-copy access (no deserialization step)
   - Reconstruct simulation state from flat buffers
   - Version check + migration if needed

4. **Version Migration**:
   - Save header contains version number
   - Migration chain: `v1 → v2 → v3 → ... → current`
   - New fields get defaults, removed fields silently ignored
   - Unit tests verify migration from every previous version

5. **Auto-save**: Every 5 game-minutes, 3 rotating slots, async

### AI Tool Usage
- **Claude**: Generate FlatBuffer schemas, save/load pipeline, Zstd integration
- **Claude**: Research best C# FlatBuffers library (Google.FlatBuffers NuGet)
- **Claude**: Research best C# Zstd library (ZstdSharp NuGet)
- **Kimi**: NOT needed

### Output Files
- `data/schemas/*.fbs` (FlatBuffer schema definitions)
- `IronAndOak.Simulation/Core/SaveSerializer.cs`
- `IronAndOak.Simulation/Core/SaveMigrator.cs`
- `scripts/game/SaveManager.gd` (Godot-side orchestration)
- `tests/IronAndOak.Simulation.Tests/SaveRoundtripTests.cs`

### Validation Criteria
- [ ] Save + load round-trip preserves all state (unit test)
- [ ] Save file <5 MB for fully developed city
- [ ] Save time <1 second
- [ ] Load time <2 seconds
- [ ] Auto-save doesn't cause frame hitch (async)
- [ ] Version migration works from v1 to current (unit test)

---

## Phase 7: Data-Driven Content System (Day 4)

### Tasks
1. **JSON Content Loader** (pure C#):
   - Load all game content from JSON files at startup
   - Buildings, technologies, laws, events, production chains, vehicles, cultural presets
   - Validate against expected schema
   - Support mod overlays: base game loads first, then mods override/extend

2. **Content Registry** (pure C#):
```csharp
public sealed class ContentRegistry {
    public IReadOnlyList<BuildingDefinition> Buildings { get; }
    public IReadOnlyList<TechnologyDefinition> Technologies { get; }
    public IReadOnlyList<LawDefinition> Laws { get; }
    public IReadOnlyList<ProductionChainDefinition> Chains { get; }
    public IReadOnlyList<VehicleDefinition> Vehicles { get; }
    public IReadOnlyList<CulturalPreset> CulturalPresets { get; }
    public IReadOnlyList<EventDefinition> Events { get; }

    public void LoadBase(string dataPath) { ... }
    public void LoadMod(string modPath) { ... }  // overlays/extends base
}
```

3. **Mod System Foundation**:
   - Mod manifest: `mod.json` with name, version, author, dependencies
   - Mod load order: user-configurable, later mods override earlier
   - Conflict detection: warn when two mods modify the same building/tech
   - Hot-reload during development (detect file changes, reload JSON)

### AI Tool Usage
- **Claude**: Generate content loader, registry, mod overlay system
- **Claude**: Generate JSON schemas for all content types
- **Kimi**: NOT needed

### Output Files
- `IronAndOak.Simulation/Core/ContentRegistry.cs`
- `IronAndOak.Simulation/Core/ContentLoader.cs`
- `IronAndOak.Simulation/Core/ModManager.cs`
- `IronAndOak.Simulation/Core/Definitions/` (all definition record types)
- `data/schemas/` (JSON schemas for validation)

---

## Phase 8: Event Bus & Integration Testing (Day 4-5)

### Tasks
1. **UI Event Bus** (GDScript, for rendering/UI only):
```gdscript
# This is NOT the simulation event system.
# This is for UI-to-UI communication and visual feedback.
extends Node

signal tile_clicked(grid_x: int, grid_y: int)
signal tool_selected(tool_type: String)
signal panel_opened(panel_name: String)
signal panel_closed(panel_name: String)
signal overlay_toggled(overlay_type: String, enabled: bool)
signal speed_changed(new_speed: int)
signal notification_posted(message: String, priority: int)
signal era_transition_visual(old_era: int, new_era: int)
signal milestone_reached(milestone_name: String, pop: int)
```

2. **Simulation Bridge** (C# with Godot refs):
```csharp
// This is the ONLY file that has both using Godot; and
// references to IronAndOak.Simulation types.

public partial class SimulationBridge : Node {
    private SimulationLoop _sim;
    private CommandQueue _commands;

    public override void _Ready() {
        _sim = new SimulationLoop();
        _commands = new CommandQueue();
        _sim.Start(_commands);
    }

    public override void _Process(double delta) {
        // Read latest snapshot from simulation thread
        var snapshot = _sim.GetLatestSnapshot();
        if (snapshot != null) {
            // Push to Godot renderers
            GetNode<ChunkedTileMap>("/root/World/TileMap").ApplySnapshot(snapshot);
            GetNode<VehicleRenderer>("/root/World/Vehicles").UpdateFromSnapshot(snapshot);
            // Push stats to UI
            EmitSignal("stats_updated", snapshot.Population, snapshot.Budget, ...);
        }
    }

    // Called by UI when player places a road
    public void PlaceRoad(int x, int y, int roadType) {
        _commands.Enqueue(new PlaceRoadCommand(x, y, (byte)roadType));
    }
}
```

3. **Integration Test**: Wire up grid → sim loop → snapshot → chunked tilemap → screen.
   - Place a road via command queue
   - Verify it appears in next snapshot
   - Verify ChunkedTileMap renders it

### AI Tool Usage
- **Claude**: Generate event bus, simulation bridge, integration test
- **Kimi**: NOT needed

### Output Files
- `scripts/game/EventBus.gd`
- `scripts/bridge/SimulationBridge.cs`
- `scripts/bridge/CommandAdapter.cs`

---

## Dependencies

### This Agent Provides To Others
| Consumer | What | Format |
|----------|------|--------|
| ALL | Project structure, .csproj files, autoloads | Godot project |
| ALL simulation agents (02-07) | GridData, ContentRegistry, SimulationLoop | Pure C# classes |
| Agent 04 (Transport) | CoordinateHelper, GridData road arrays | Pure C# |
| Agent 05 (Zoning) | ChunkedTileMap, RenderingServer patterns | GDScript |
| Agent 08 (UI) | EventBus, SimulationBridge API | GDScript + C# |
| Agent 09 (Art) | Tile size specs, sprite requirements | Docs |

### This Agent Needs From Others
| Provider | What | When |
|----------|------|------|
| None | **This agent has NO dependencies** | Day 1 start |

---

## Handoff Document

`AGENT_01_HANDOFF.md` contains:
1. Every public class in IronAndOak.Simulation with API
2. The GOLDEN RULE: no `using Godot;` in Simulation project
3. SoA conventions (how to add new data arrays)
4. How to add new commands to CommandQueue
5. How to extend SimulationSnapshot
6. ChunkedTileMap API for renderers
7. RenderingServer patterns for pooled sprites
8. FlatBuffer schema extension guide
9. Content JSON schema docs
10. Mod system extension guide
11. Thread safety rules (what runs where)
12. Performance baselines from unit tests

---

## Estimated Duration
- **With AI**: 4-5 days
- **Without AI**: 3-4 weeks
- **Day 1-2 output unblocks ALL other agents**
- **Save system (Phase 6) can run in parallel with other agents starting**
