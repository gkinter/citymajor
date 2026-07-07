# IRON & OAK: Tech Stack Research & Recommendation

> **Historical — pre-web pivot.** See [WEB_V1_SCOPE.md](WEB_V1_SCOPE.md) and [CLAUDE.md](../../CLAUDE.md).

## Deep Analysis for Building a Realistic City & Economic Builder

---

## 1. ENGINE COMPARISON (for city builders specifically)

### Godot 4 (C# + GDScript) -- Current Plan

**Shipped city builders on Godot:** None of comparable scope. Dome Keeper ($6.1M revenue) handles resource management but isn't a full city builder. No commercial city builder at SimCity/Anno scale has shipped on Godot 4.

**Performance reality:**
- TileMap has [known performance issues at 500x500+ tiles](https://github.com/godotengine/godot/issues/72458), especially with Y-sorting (required for isometric)
- Y-sorted isometric tilemaps have a [huge performance impact](https://github.com/godotengine/godot/issues/74478) that worsens with map size
- A 1,500-NPC game suffered frame drops from individual `_process()` calls
- C# in Godot is [~140x faster than GDScript](https://github.com/RaidTheory/csharp-gd-inventory-test) for computation, but has marshalling overhead when calling Godot APIs
- Scene tree architecture is NOT designed for iterating 20k+ homogeneous entities -- it's designed for heterogeneous scene composition
- The [Server APIs](https://docs.godotengine.org/en/stable/tutorials/performance/using_servers.html) (RenderingServer, PhysicsServer) bypass scene tree for better performance
- Godot officially [acknowledges ECS gives "huge performance improvements"](https://godotengine.org/article/why-isnt-godot-ecs-based-game-engine/) but chose scene tree for usability

**Strengths:**
- MIT license, no runtime fees (important for slow-burn indie sales)
- Excellent 2D editor and tooling
- [Rust GDExtension](https://godot-rust.github.io/) is mature (binary compat from 4.1+, hot reload in 4.2+)
- Fast iteration cycle
- Growing community

**Weaknesses:**
- ["A lot is still missing for large projects"](https://godotengine.org/article/whats-missing-in-godot-for-aaa/) -- official admission
- No built-in ECS
- [StringName equality performance bug](https://github.com/godotengine/godot/issues/89217) with many C# scripts

---

### Unity (C#) -- The Industry Standard for City Builders

**Shipped city builders:** Cities: Skylines 1 & 2, IXION, Timberborn, Farthest Frontier, Foundation, Before We Leave, Kingdoms Reborn. **The dominant engine for commercial city builders.**

**Performance reality:**
- Unity DOTS/ECS handles [10,000+ entities at 60 FPS](https://medium.com/superstringtheory/unity-dots-ecs-performance-amazing-5a62fece23d4)
- Burst Compiler translates C# to LLVM-optimized native code (eliminates C#-vs-native gap)
- IXION [specifically used ECS](https://unity.com/ecs) for NPC simulation
- **BUT**: Cities: Skylines 2 proved DOTS [can be catastrophically misused](https://blog.paavo.me/cities-skylines-2-performance/) -- Colossal Order "completely overestimated the engine's capabilities"

**Strengths:**
- Proven for this exact genre (most city builders use Unity)
- DOTS/Burst for native-speed C# simulation
- Excellent 2D tilemap system, sprite batching, LOD
- Massive ecosystem, documentation, community
- Steam Workshop integration well-documented

**Weaknesses:**
- Runtime fee controversy (partially walked back)
- Closed source
- Larger binary sizes
- Slower iteration cycle than Godot

---

### Custom Engine (C++/Rust) -- What the Legends Use

**Shipped city builders:** OpenTTD (C++), Factorio (C++), Workers & Resources: Soviet Republic (custom C++ by ONE programmer), Dwarf Fortress (C++)

**Performance reality:** The highest-performing simulation games ALL use custom engines.
- Factorio achieved [50-100x belt optimization](https://factorio.com/blog/post/fff-176) through domain-specific data structures impossible in generic engines
- Workers & Resources runs deep economic simulation on a custom engine by [a single programmer](https://steamcommunity.com/app/784150/discussions/0/2626095078194087536/)

**Key lesson:** Domain-specific data structures beat generic engine approaches by orders of magnitude.

**Downside:** 1-3 years of engine work before any gameplay. Enormous undertaking.

---

### Bevy (Rust ECS) -- The Future?

**Shipped city builders:** [POLDERS](https://bevy.org/) (in development). No major commercial releases yet.

**Performance:** ECS scales to millions of entities. 2D rendering reportedly 2x faster than comparable engines. [godot-bevy bridge](https://bytemeadow.github.io/godot-bevy/introduction.html) exists for hybrid use.

**Downside:** Pre-1.0 (v0.17 as of March 2026), unstable API, no visual editor. Revisit in 2027-2028.

---

### Unreal Engine 5 -- Wrong Tool

No meaningful 2D isometric pipeline. Massively overengineered for pixel art. **Not recommended.**

---

## 2. HOW THE LEGENDS ACTUALLY WORK

### SimCity 4 -- Statistical Simulation (Most Relevant to Iron & Oak)

SimCity 4 was [the last true statistical city simulator](https://news.ycombinator.com/item?id=38158537). Key architecture:
- Population determined at nodes in the transportation network
- Network flow calculations for traffic (NOT individual agents)
- No individual citizens simulated
- Statistical sampling for behavior

**Lesson for Iron & Oak:** This is the correct model for 200k population. Don't simulate individuals -- simulate flows and aggregates.

---

### Cities: Skylines -- Agent-Based (The Cautionary Tale)

Uses A* pathfinding for EVERY agent. Late-game performance collapses because of per-agent simulation cost. CS2 tried DOTS but [failed at the rendering layer](https://www.pcgamer.com/games/sim/cities-skylines-2-boss-says-they-completely-overestimated-the-unity-engines-capabilities/).

**Lesson for Iron & Oak:** Agent-based looks pretty but doesn't scale. The traffic pathfinding cost is what kills performance.

---

### Factorio -- The Gold Standard of Optimization

Wube's approach was radical:
- Treat [transport belts as mathematical sequences](https://factorio.com/blog/post/fff-176), not individual items
- Store **distance between items** instead of absolute positions
- Group non-interacting transport lines for [parallel multithreaded updates](https://www.factorio.com/blog/post/fff-215)
- O(1) optimization yielded 50-100x speedup
- Custom save format with [334,257 serialization calls/ms](https://gist.github.com/Rseding91/a309cf0a30782a2e96ef081c39326f42)

**Lesson for Iron & Oak:** Generic "iterate over all entities" will never be as fast as domain-specific data structures. Design production chains to update in O(1) where possible.

---

### Workers & Resources: Soviet Republic -- Proof a Solo Dev Can Do This

Built by a single programmer on a custom C++ engine. Key economic insight:
- No fixed prices -- the cost of steel derives from iron, coal, labor, and steel mill construction cost
- 30+ commodities with realistic supply chains
- Centrally planned economy where player controls means of production

**Lesson for Iron & Oak:** Emergent pricing from input costs is both realistic and compelling. Use a Leontief input-output model.

---

### OpenTTD -- Evolved Pathfinding

Three generations of pathfinding:
1. NTP (trains only, simple)
2. NPF (A* for all vehicles, CPU-heavy)
3. [YAPF](https://wiki.openttd.org/en/Archive/Source/OpenTTDDevBlackBook/Simulation/Pathfinding) (optimized A* with caching)

**Lesson:** Pathfinding must be iteratively optimized as scale grows. Start simple, profile, optimize.

---

### Citybound -- Most Architecturally Relevant

Uses [actor-system framework (Kay)](https://aeplay.org/citybound) with type-safe message passing. Every household maintains resource inventories that drive behavior. Compiles to WebAssembly. Has been in development for years without shipping.

**Lesson:** Actor-based household simulation is elegant but shipping is hard. Keep scope manageable.

---

### Victoria 3 -- The Economy Model to Study

[Open economy with discrete weekly ticks](https://www.gamedeveloper.com/design/deep-dive-modeling-the-global-economy-in-victoria-3). 700+ regions, 100k population groups. Money is created, burned, and flows through the system. Demand driven by standard-of-living needs.

**Lesson:** The Leontief input-output matrix approach works at scale for complex economies.

---

## 3. ECS vs OOP FOR CITY SIMULATION

**ECS is the clear winner for simulation-heavy city builders**, but with nuance:

| Aspect | Scene Tree (OOP) | ECS |
|--------|-------------------|-----|
| Cache performance | Poor (scattered memory) | Excellent (contiguous arrays) |
| 20k entity iteration | Slow (virtual dispatch per entity) | Fast (data-parallel, SIMD-friendly) |
| Parallelism | Hard (shared mutable state) | Natural (systems operate on disjoint data) |
| Composition | Inheritance hierarchies | Flexible component mixing |
| Tooling | Godot editor, visual | Code-only (Bevy), or DOTS editor (Unity) |
| Real-world speedup | Baseline | [40-60% improvement](https://gameprogrammingpatterns.com/data-locality.html) |

### The Hybrid Solution

Use Godot's scene tree for what it's good at (rendering, UI, editor), and a separate ECS-like data layer for simulation:

```
┌─────────────────────────────────────────────┐
│  SIMULATION (Pure C#, no Godot deps)        │
│  SoA arrays, ECS-like iteration             │
│  Runs on dedicated thread                   │
│  ~25 MB total state, fits in L3 cache       │
├─────────────────────────────────────────────┤
│  DATA BRIDGE (minimal state transfer)       │
│  Changed tiles, vehicle positions, UI data  │
│  Double-buffered for thread safety          │
├─────────────────────────────────────────────┤
│  RENDERING (Godot scene tree)               │
│  TileMap for terrain/zones                  │
│  RenderingServer for vehicles/citizens      │
│  Control nodes for UI                       │
│  Shaders for visual effects                 │
└─────────────────────────────────────────────┘
```

This is architecturally the same pattern that [IXION used with Unity DOTS](https://unity.com/ecs) -- simulation data separated from visual representation.

---

## 4. TRAFFIC: BPR vs AGENTS

### BPR Formula Assessment

The [BPR function](https://en.wikipedia.org/wiki/Route_assignment) `t = t0 * (1 + 0.15 * (V/C)^4)` is **correct for Iron & Oak**:

| Approach | Used By | Performance | Realism | Recommendation |
|----------|---------|-------------|---------|----------------|
| Agent-based micro | Cities: Skylines | O(n) per agent | Individual behavior | NO -- kills performance at 200k |
| Flow-based macro | SimCity 4 | O(edges) per iteration | Aggregate patterns | YES -- proven at scale |
| Hybrid meso | MATSim | Middle ground | Reasonable detail | Possible future upgrade |

**Enhancement beyond basic BPR:**
- Frank-Wolfe or Method of Successive Averages for traffic assignment (converges in 3-5 iterations)
- Time-of-day demand profiles (rush hour peaks)
- Mode choice modeling (transit vs car vs walking vs cycling)
- Incremental graph updates when roads are built/destroyed (no full recompute)

**Vehicle sprites on screen are COSMETIC** -- they follow flow patterns from the traffic model, they are not simulation entities. This is how SimCity 4 did it.

---

## 5. ECONOMIC MODEL: LEONTIEF INPUT-OUTPUT

The recommended economic foundation is a [Leontief input-output model](https://en.wikipedia.org/wiki/Input%E2%80%93output_model):

```
Each production chain is a column in the technology matrix A:

x = (I - A)^(-1) * d

Where:
x = total output vector (all goods)
A = technology matrix (input requirements per unit output)
d = final demand vector (citizen consumption + exports)
```

**Combined with Workers & Resources-style emergent pricing:**
- Steel price = iron_cost + coal_cost + labor_cost + capital_depreciation
- No arbitrary fixed prices -- everything emerges from inputs
- Supply/demand imbalances create price signals
- Monthly tick resolution (not per-frame)

This gives realistic, emergent economics without agent-based market simulation overhead.

---

## 6. POPULATION: HOUSEHOLD-BASED STATISTICAL

**Don't simulate 200k individuals. Simulate ~20k households.**

Each household has:
- Income bracket, education level, life stage
- Housing reference (zone/building, not coordinate)
- Workplace reference (building or zone)
- Consumption demand vector (feeds into economic model)
- Satisfaction score (statistical, not pathfound)

Traffic = statistical zone-to-zone BPR flows (not per-household pathfinding).
Citizens on screen = cosmetic sprites following flow patterns.

This is the [Citybound approach](https://aeplay.org/citybound) at a manageable granularity.

### Memory Budget

```
20k households x 256 bytes    = 5 MB
256x256 tiles x 64 bytes x 4  = 16 MB
Road network graph             = 1 MB
Production chain state         = 1 MB
Research/politics/events       = 2 MB
─────────────────────────────────────
Total simulation state         = ~25 MB
```

**25 MB fits entirely in L3 cache on modern CPUs.** This means iteration over all simulation data is a cache-friendly operation with minimal memory stalls.

---

## 7. DATA-ORIENTED DESIGN

### Struct of Arrays (SoA) -- 40-60% Performance Improvement

```csharp
// BAD: Array of Structs (cache-unfriendly when iterating one property)
Household[] households; // each household is 256 bytes scattered across cache lines

// GOOD: Struct of Arrays (cache-friendly, SIMD-friendly)
public class HouseholdPool {
    public float[] Incomes;           // 20k floats, contiguous 80KB
    public byte[] EducationLevels;    // 20k bytes, contiguous 20KB
    public byte[] WealthClasses;      // 20k bytes, contiguous 20KB
    public ushort[] HomeZoneIds;      // 20k ushorts, contiguous 40KB
    public ushort[] WorkZoneIds;      // 20k ushorts, contiguous 40KB
    public float[] Satisfactions;     // 20k floats, contiguous 80KB
    public byte[] CulturalProfiles;   // 20k bytes, contiguous 20KB
    public uint[] Flags;             // 20k uints, contiguous 80KB
}
```

When iterating satisfaction scores for all households, the CPU loads 16 floats per cache line (64 bytes). With AoS, each cache line loads only 1 satisfaction value mixed with 240+ bytes of irrelevant data.

### Multi-Threaded Tick Architecture

```
1. READ PHASE   - All systems read previous tick's state (immutable, parallelizable)
2. WRITE PHASE  - Each system writes to its own output buffer (no contention)
3. SYNC PHASE   - Swap buffers, resolve cross-system dependencies
```

Double-buffered: simulation writes buffer A while renderer reads buffer B. Atomic swap at tick boundary.

---

## 8. SAVE/LOAD STRATEGY

### Factorio's Approach (Gold Standard)

- Custom binary format with multiple cycling buffers
- No metadata in file -- code knows exactly what to expect
- Compression on separate thread
- 334,257 serialization calls/ms

### Recommended: FlatBuffers + Zstd

**Why FlatBuffers over Protobuf/JSON/Binary:**
- [Zero-copy deserialization](https://flatbuffers.dev/benchmarks/) -- access data directly from buffer without allocation
- Designed for game development (used by Cocos2d-x, Google games)
- C# support via official .NET library
- Schema evolution with backward compatibility built in
- 10-100x faster than JSON, 2-10x faster than Protobuf for reads

**Save format:**
```
iron_and_oak.ioak (zstd compressed)
├── header          (version, timestamp, city name, checksum)
├── grid.fb         (FlatBuffer: 256x256 tiles)
├── households.fb   (FlatBuffer: 20k households)
├── economy.fb      (FlatBuffer: production chains, budget, trade)
├── transport.fb    (FlatBuffer: road graph, transit routes)
├── politics.fb     (FlatBuffer: laws, council, approval)
├── research.fb     (FlatBuffer: tech progress)
├── events.fb       (FlatBuffer: active/scheduled events)
└── thumbnail.png   (128x128 city screenshot)
```

**Estimates:** 25 MB state + zstd = ~3-5 MB save file. Save time: <1 second.

**Version migration:**
```
v3_save → migrate_v3_to_v4() → v4_save → migrate_v4_to_v5() → v5_save
New fields get defaults. Removed fields silently ignored.
```

---

## 9. MODDING ARCHITECTURE

### Data-Driven Design (Critical Foundation)

Following [OpenTTD's NewGRF system](https://deepwiki.com/OpenTTD/OpenTTD/4.5-newgrf-system):

**ALL game content defined in JSON:**
```json
// data/buildings/residential.json
{
    "id": "res_modern_apartment_01",
    "name": "Modern Apartment",
    "era": "modern",
    "zone": "residential",
    "density": "high",
    "size": [2, 2],
    "capacity": 24,
    "cost": 15000,
    "maintenance": 120,
    "sprite": "buildings/res_modern_apartment_01.png",
    "unlock_tech": "T045_high_rise_construction"
}
```

**Modders can:**
1. Add new JSON entries (new buildings, chains, policies, vehicles)
2. Override existing entries (rebalance costs, change effects)
3. Add new sprite assets (PNG + JSON metadata)
4. Write GDScript for custom behavior (leveraging Godot's natural modding path)
5. Publish to Steam Workshop

**Mod load order:** Base game loads first, then mods in user-specified order. Later mods override earlier ones. Conflict detection warns about incompatible overrides.

---

## 10. RENDERING STRATEGY

### Chunked TileMap (Mandatory at 256x256+)

Split the 256x256 grid into 32x32 chunks (64 chunks total). Only chunks visible to the camera are loaded into the scene tree. This avoids the [TileMap performance cliff](https://github.com/godotengine/godot/issues/72458).

### RenderingServer for Dynamic Entities

Bypass the scene tree for vehicles and citizens:
```gdscript
# Direct RenderingServer usage -- no Node overhead
var rid = RenderingServer.canvas_item_create()
RenderingServer.canvas_item_set_parent(rid, get_canvas_item())
RenderingServer.canvas_item_add_texture_rect(rid, rect, texture)
```

This avoids the `_process()` overhead of individual Node2D sprites for 500+ vehicles.

### LOD Zoom Levels

| Zoom | What's Visible | Sprite Detail |
|------|---------------|---------------|
| 1 (closest) | Individual buildings, citizens, vehicle details | Full sprites, animations |
| 2 (district) | Building clusters, road names, bus stops | Simplified sprites, no citizens |
| 3 (city) | Zone colors, major roads, service coverage | Colored blocks, data overlays |
| 4 (region) | Full map, trade routes, resource deposits | Minimap-style solid colors |

### Shader Performance

Day/night cycle and seasonal palettes via **palette swap shaders** (fast, no asset duplication):
- Single lookup texture per season (256-color palette)
- Shader remaps indexed colors in base sprite to seasonal palette
- Total GPU cost: negligible (1 texture sample per pixel per affected sprite)

---

## 11. GODOT 4 SPECIFIC VERDICT

### Can Godot Handle This Game?

**YES -- but only with strict architectural discipline:**

1. Simulation must be **pure C# with zero Godot dependencies**
2. Rendering must use **chunked TileMap + RenderingServer** (not individual Node2D sprites)
3. Simulation runs on **dedicated thread** (Godot supports this via C# Task/Thread)
4. Data transfer is **minimal and double-buffered** (changed tiles + vehicle positions only)
5. If specific bottlenecks emerge, port to **Rust via GDExtension** (mature, hot-reloadable)

### Godot vs Unity for THIS Game

| Factor | Godot 4 | Unity |
|--------|---------|-------|
| Genre track record | None at this scale | Dominant (CS, Timberborn, IXION) |
| 2D isometric | Good (with workarounds) | Excellent (proven) |
| Simulation perf | C# good, no Burst equivalent | DOTS + Burst = native speed |
| License | MIT, no fees ever | Runtime fee (complex, partially walked back) |
| Iteration speed | Faster (lighter editor) | Slower (heavier editor) |
| Modding | GDScript is mod-friendly | C# mod loading proven (CS1) |
| Risk if engine fails | Sim core is portable (no deps) | Locked into Unity ecosystem |
| Community for genre | Small | Large |

**The portability argument is key:** If the simulation core is a standalone C# library with no engine dependencies, it can run in Godot, Unity, MonoGame, or even a custom renderer. You're not locked in. This makes Godot the lower-risk choice -- try it first, port if needed.

---

## 12. LANGUAGE FOR SIMULATION CORE

### C# (.NET 8+) -- Recommended for Primary Development

```csharp
// .NET 8 features for high-performance simulation:
Span<T>           // stack-allocated array views, zero-copy slicing
stackalloc        // stack allocation for temporary buffers
ref struct        // no-GC value types
System.Numerics   // SIMD intrinsics (Vector<T>)
Unsafe.ReadUnaligned // raw memory access when needed
```

C# with SoA layouts and `Span<T>` achieves 80-90% of native C++ performance for data-parallel iteration. The remaining 10-20% gap matters only in the tightest inner loops.

### Rust via GDExtension -- For Hot-Path Optimization

If profiling reveals bottlenecks (traffic assignment solver, economic equilibrium, pathfinding), those specific systems can be ported to Rust:
- [godot-rust](https://godot-rust.github.io/) is mature with 3 safety tiers
- Hot reload in Godot 4.2+
- Zero-cost abstractions, no GC, guaranteed memory safety
- [Comparable to C++](https://kornel.ski/rust-c-speed) in tight loops

**Don't start with Rust.** Start with C#, profile, and port only proven bottlenecks.

---

## FINAL RECOMMENDED TECH STACK

```
┌─────────────────────────────────────────────────────────┐
│                    IRON & OAK STACK                      │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  ENGINE:        Godot 4.x (MIT, no runtime fees)         │
│  SIM LANGUAGE:  C# (.NET 8+, SoA layout, Span<T>)       │
│  RENDER LANG:   GDScript (scene tree, shaders)           │
│  HOT PATHS:     Rust via GDExtension (if needed)         │
│                                                          │
│  ARCHITECTURE:                                           │
│  ┌───────────────────────────────────────────┐           │
│  │ Simulation Core (Pure C#, no Godot deps)  │           │
│  │ • Household pool (SoA, 20k entries)       │           │
│  │ • Economic engine (Leontief I-O matrix)   │           │
│  │ • Traffic model (BPR + Frank-Wolfe)        │           │
│  │ • Production chains (buffer-based)        │           │
│  │ • Politics/laws (effect applicator)       │           │
│  │ • Cultural DNA (drift calculator)         │           │
│  │ Dedicated thread, double-buffered output  │           │
│  └─────────────────┬─────────────────────────┘           │
│                    │ minimal state delta                  │
│  ┌─────────────────┴─────────────────────────┐           │
│  │ Godot Rendering Frontend                  │           │
│  │ • Chunked TileMap (32x32 chunks)          │           │
│  │ • RenderingServer for vehicles/citizens   │           │
│  │ • Control nodes for UI panels             │           │
│  │ • Palette-swap shaders (day/night/season) │           │
│  │ Main thread only                          │           │
│  └───────────────────────────────────────────┘           │
│                                                          │
│  SAVE FORMAT:   FlatBuffers + Zstd (~3-5 MB saves)       │
│  CONTENT DATA:  JSON (buildings, techs, laws, chains)    │
│  MODDING:       JSON data + GDScript + sprite packs      │
│                                                          │
│  TRAFFIC:       Statistical BPR (NOT agent-based)        │
│  POPULATION:    20k households (NOT 200k individuals)    │
│  ECONOMY:       Leontief I-O + emergent pricing          │
│  RENDERING:     Chunked + pooled + LOD (4 zoom levels)   │
│                                                          │
│  PORTABILITY:   Sim core is engine-agnostic.             │
│                 If Godot fails at scale, port renderer    │
│                 to Unity/MonoGame/custom without          │
│                 rewriting simulation logic.               │
│                                                          │
└─────────────────────────────────────────────────────────┘
```

### Why This Stack Wins

1. **SimCity 4's proven statistical approach** for traffic and population -- not Cities: Skylines' performance-killing agent model
2. **Workers & Resources' emergent pricing** -- realistic economics without market simulation overhead
3. **Factorio's lesson applied**: domain-specific data structures (SoA, buffer-based chains) over generic entity iteration
4. **Engine-agnostic simulation core** -- the single most important architectural decision. Zero lock-in.
5. **Godot's MIT license** -- no runtime fee surprises. The engine is free forever.
6. **Rust escape hatch** -- if C# isn't fast enough for specific systems, Rust GDExtension is mature and ready
7. **FlatBuffers** -- zero-copy deserialization for fast saves, with built-in schema evolution

### What Changes in Agent Plans

Based on this research, these updates should be applied to the agent plans:

| Change | Why |
|--------|-----|
| All simulation code must be pure C# with NO `using Godot;` imports | Engine portability, threading safety |
| Use SoA layout instead of AoS for households, buildings, vehicles | 40-60% iteration performance gain |
| Traffic uses BPR + Frank-Wolfe, NOT per-agent pathfinding | SimCity 4 approach, proven at scale |
| Economic model uses Leontief I-O matrix | Victoria 3 / W&R approach, emergent pricing |
| TileMap must be chunked (32x32) with camera-based loading | Avoids Godot Y-sort performance cliff |
| Use RenderingServer directly for vehicles/citizens | Bypasses scene tree overhead for pooled sprites |
| Save format: FlatBuffers + Zstd instead of JSON + binary | Zero-copy reads, built-in schema evolution |
| All game content in JSON data files (moddable from day 1) | OpenTTD-style data-driven design |
| Rust GDExtension identified as optimization path (not initial implementation) | Profile first, optimize later |

---

## Sources

### Engine & Architecture
- [Godot TileMap Issues #72458](https://github.com/godotengine/godot/issues/72458)
- [Godot Y-Sort Performance #74478](https://github.com/godotengine/godot/issues/74478)
- [Godot Why Not ECS](https://godotengine.org/article/why-isnt-godot-ecs-based-game-engine/)
- [Godot Server API Docs](https://docs.godotengine.org/en/stable/tutorials/performance/using_servers.html)
- [Godot What's Missing for AAA](https://godotengine.org/article/whats-missing-in-godot-for-aaa/)
- [Godot C# vs GDScript Perf](https://github.com/RaidTheory/csharp-gd-inventory-test)
- [Godot Rust GDExtension](https://godot-rust.github.io/)
- [Godot-Bevy Bridge](https://bytemeadow.github.io/godot-bevy/introduction.html)
- [Unity DOTS Performance](https://medium.com/superstringtheory/unity-dots-ecs-performance-amazing-5a62fece23d4)
- [CS2 Performance Autopsy](https://blog.paavo.me/cities-skylines-2-performance/)

### Game-Specific Architecture
- [SimCity 4 Statistical Model](https://news.ycombinator.com/item?id=38158537)
- [Factorio Belt Optimization](https://factorio.com/blog/post/fff-176)
- [Factorio Multithreading](https://www.factorio.com/blog/post/fff-215)
- [Factorio Save Architecture](https://gist.github.com/Rseding91/a309cf0a30782a2e96ef081c39326f42)
- [OpenTTD Pathfinding](https://wiki.openttd.org/en/Archive/Source/OpenTTDDevBlackBook/Simulation/Pathfinding)
- [OpenTTD NewGRF](https://deepwiki.com/OpenTTD/OpenTTD/4.5-newgrf-system)
- [Workers & Resources Engine](https://steamcommunity.com/app/784150/discussions/0/2626095078194087536/)
- [Citybound Architecture](https://aeplay.org/citybound)
- [Victoria 3 Economy](https://www.gamedeveloper.com/design/deep-dive-modeling-the-global-economy-in-victoria-3)

### Performance & Data Design
- [Data-Oriented Design](https://gameprogrammingpatterns.com/data-locality.html)
- [SoA vs AoS Performance](https://medium.com/@azad217/structure-of-arrays-soa-vs-array-of-structures-aos-in-c-a-deep-dive-into-cache-optimized-13847588232e)
- [FlatBuffers Benchmarks](https://flatbuffers.dev/benchmarks/)
- [BPR / Route Assignment](https://en.wikipedia.org/wiki/Route_assignment)
- [Leontief I-O Model](https://en.wikipedia.org/wiki/Input%E2%80%93output_model)
- [Rust vs C# Benchmarks](https://programming-language-benchmarks.vercel.app/rust-vs-csharp)
