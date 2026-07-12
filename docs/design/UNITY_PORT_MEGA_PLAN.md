# CityMajor — Unity Desktop Port Mega Plan

**Status:** Active (Unity-primary pivot)  
**Date:** 2026-07-12  
**Supersedes:** Dual-target recommendation in §0 (2026-07-12 draft) — **Unity 6 desktop is now primary v1**  
**Canonical scope:** [UNITY_V1_SCOPE.md](./UNITY_V1_SCOPE.md) — modern era only, Steam desktop  
**Linear:** TBD — recommend [SB-3704](https://linear.app/softblaze/issue/SB-3704) follow-on or new epic  
**Prerequisite read:** [WASM_SIM_BRIDGE.md](./WASM_SIM_BRIDGE.md), [DATA_BRIDGE.md](./DATA_BRIDGE.md), [TECH_STACK_RESEARCH.md](./TECH_STACK_RESEARCH.md), [CTO_IMPROVEMENT_ROADMAP_2026-07.md](./CTO_IMPROVEMENT_ROADMAP_2026-07.md)

---

## 0. Executive decision

| Question | Answer |
|----------|--------|
| **Is Unity viable for CityMajor?** | **Yes** — genre-proven, C# sim reuse, Steam/desktop path, GLTF import, GPU instancing. |
| **Primary v1 platform?** | **Unity 6 desktop (Steam)** — macOS, Windows, Linux. |
| **Web client (`web/`)?** | **Maintenance mode only** — critical fixes; no new v1 features. |
| **v1 era scope?** | **Modern only** — `svc_modern`, `com_modern`, `ind_modern`, glass towers. Frontier/Industrial/Postwar/Future **deferred**. |
| **AI dev accelerator** | [Unity MCP](https://github.com/CoplayDev/unity-mcp) — Cursor ↔ Unity Editor bridge (47 tools). Integrated in this repo — see [UNITY_MCP_SETUP.md](../UNITY_MCP_SETUP.md). |

**Decision (2026-07-12):** Pivot from dual-target to **Unity-primary**. Ship Steam EA on modern-era 256×256 scope; keep `Forge.SimCore` as shared sim library; web R3F client frozen except maintenance.

---

## 1. Current state inventory (post-pull `69b4be2`)

### 1.1 What ships today

| Layer | Implementation | LOC (approx) | Portable? |
|-------|----------------|--------------|-----------|
| **Simulation** | `Forge.SimCore` + `Forge.SimWasm` | ~9.4k C# | **High** — engine-agnostic by design |
| **Web renderer** | R3F + Three.js (`web/components/city/*`) | ~8k TS/TSX | **Low** — rewrite for Unity |
| **Web shell / HUD** | Next.js 16 + HTML overlays | ~6k TS/TSX | **Medium** — logic ports; UI rebuild |
| **WASM bridge** | `sim-worker.ts`, `sim-bridge.ts` | ~1.5k TS | **N/A** — Unity calls sim in-process |
| **Assets** | GLTF/GLB per era + Meshy pipeline | ~60+ GLBs | **High** — Unity imports GLTF natively |
| **Content** | JSON (`base/data/*`) | shared | **High** |
| **Legacy Forge** | SDL2 + OpenGL desktop | ~30k C# | **Reference only** — renderer patterns |

### 1.2 Unity v1 locked scope (canonical)

From [UNITY_V1_SCOPE.md](./UNITY_V1_SCOPE.md):

- 256×256 map, ~5k buildings, ~10k households, **modern era only**
- Unity 6 + URP, Steam desktop, `Forge.SimCore` native in-process
- Founder Pass equivalent TBD (Steam)

**Deferred to post-v1:** Frontier → Industrial era arc, Postwar/Future content, 512×1024 maps, 50k buildings, browser as active platform.

**Web charter (historical):** [WEB_V1_SCOPE.md](./WEB_V1_SCOPE.md) — superseded for platform decisions; maintenance mode only.

### 1.3 Integration branch delta

`feat/cmjr-merge-wasm-r3f` (worktree `citymajor-cmjr-merge`) adds wave-4+ polish beyond merged PR #1: CMJR saves, citizen panel, law panel, Herald LLM commands, 22/22 smoke tests. **Unity port should branch from latest integration tip**, not stale `main` alone.

---

## 2. Stack comparison matrix

Scored 1–5 for CityMajor's requirements: *deep economic sim, isometric 3D city, 5k–50k instanced buildings, AI narrative, Steam desktop, optional browser funnel.*

| Criterion | **Unity 6 + URP** | **Stay R3F web** | **Godot 4 C#** | **Bevy (Rust)** | **Electron + R3F** | **Revive Forge SDL2** |
|-----------|-------------------|------------------|----------------|-----------------|--------------------|-----------------------|
| City-builder track record | **5** (CS, IXION, Timberborn) | 2 | 2 | 1 | 2 | 3 (custom) |
| C# sim reuse | **5** (in-process) | 3 (WASM only) | 4 | 1 (rewrite) | 3 (WASM) | **5** |
| Desktop / Steam | **5** | 1 | 5 | 4 | 3 | **5** |
| Browser / zero-install | 1 | **5** | 3 (WASM export) | 2 | 4 | 0 |
| GPU instancing @ scale | **5** (GPU Instancing, DOTS opt-in) | 3 (WebGL limits) | 3 (Y-sort cliff) | 5 | 3 | 4 |
| AI agent tooling (MCP) | **5** ([unity-mcp](https://github.com/CoplayDev/unity-mcp)) | 3 (browser MCP) | 2 | 1 | 3 | 1 |
| Editor / content velocity | **5** | 2 | 4 | 1 | 2 | 1 |
| Team familiarity | 4 | **5** (current) | 3 | 1 | 4 | 2 |
| License / cost | 4 (free tier OK) | **5** | **5** (MIT) | 5 | 5 | 5 |
| CS2-style perf risk | 3 (misused DOTS) | 4 (scale capped) | 3 | 4 | 3 | 4 (you control it) |
| **Weighted total** | **4.4** | 3.2 | 3.4 | 2.6 | 3.0 | 3.5 |

### 2.1 Alternatives considered and rejected

| Stack | Verdict |
|-------|---------|
| **Unreal 5** | Wrong tool — no 2D/isometric pipeline; massive overkill ([TECH_STACK_RESEARCH.md](./TECH_STACK_RESEARCH.md)). |
| **Bevy** | Pre-1.0 API churn; no editor; revisit 2027–2028 for headless sim server only. |
| **Full Forge revival** | 30k lines maintained solo; no Steam ecosystem; already pivoted away twice. |
| **Electron wrapper** | Still WebGL — doesn't solve perf ceiling or feel native on Steam Deck. |
| **Unity **only**, drop web** | **Accepted (2026-07-12)** — web enters maintenance mode; Unity is primary v1. |

### 2.2 The "better stack" answer

**No single stack beats Unity for desktop city-building today** while keeping C#. The target architecture:

```
┌─────────────────────────────────────────────────────────────┐
│ Forge.SimCore (pure C# class library, netstandard2.1)       │
│  Economy, traffic BPR, zones, research, events, laws, trade │
└───────────────┬─────────────────────────┬───────────────────┘
                │                         │
     ┌──────────▼──────────┐   ┌──────────▼──────────┐
     │ Unity 6 client      │   │ Forge.SimWasm       │
     │ URP + UI Toolkit    │   │ + Next.js / R3F     │
     │ Steam desktop       │   │ Maintenance mode    │
     │ **PRIMARY v1**      │   │ (critical fixes)    │
     └─────────────────────┘   └─────────────────────┘
```

---

## 3. Unity target architecture

### 3.1 Project layout (new `unity/` directory)

```
unity/
├── CityMajor.Unity/           # Unity 6 project (URP 3D)
│   ├── Assets/
│   │   ├── Scripts/
│   │   │   ├── Sim/          # Thin adapter over Forge.SimCore
│   │   │   ├── Rendering/    # Chunked instancing, LOD, overlays
│   │   │   ├── Input/        # Tools: zone, road, build, bulldoze
│   │   │   └── UI/           # UI Toolkit HUD panels
│   │   ├── Scenes/
│   │   │   └── Play.unity
│   │   ├── Art/Gltf/         # Symlink → web/public/assets/gltf (modern/ for v1)
│   │   └── Data/             # Symlink to base/data JSON
│   └── Packages/manifest.json  # Includes com.coplaydev.unity-mcp
├── README.md
└── ProjectSettings/          # Created by Unity Hub on first open
```

### 3.2 Sim integration (native, not WASM)

| Web (today) | Unity (target) |
|-------------|----------------|
| `Forge.SimWasm` in Web Worker | `Forge.SimCore` referenced as **.csproj** in Unity solution |
| JSON `SimSnapshot` over postMessage | **Struct snapshot** or blittable arrays — zero JSON hot path |
| 8 Hz tick (worker throttle) | **Dedicated sim thread** (same pattern as Forge Engine) |
| `PaintZone` / `PlaceRoad` JS interop | Direct C# method calls on `SimHost` |

**Refactor steps:**

1. Extract shared types from `WasmSimHost` → `Forge.SimCore.SimHost` (already partially in `Forge.SimCore`).
2. Keep `WasmSimHost` as thin JSON export wrapper for web only.
3. Unity `CitySimBridge.cs` reads snapshots on main thread, writes to `NativeArray` for rendering.

### 3.3 Rendering port map (R3F → Unity URP)

| R3F component | Unity equivalent |
|---------------|------------------|
| `BuildingInstances` (InstancedMesh) | `Graphics.DrawMeshInstanced` or **Entities Graphics** (optional DOTS) |
| `TerrainChunks` | Tile mesh chunks + frustum culling |
| `GltfBuildingBucket` | `GLTFast` or Unity 6 built-in GLTF importer |
| `ZoneOverlay` / `RoadOverlay` | Decal projector or custom mesh overlay pass |
| `CityPostProcessing` | URP Volume (bloom, color grading) |
| `CitizenDots` | Instanced quads / GPU particles |
| LOD L0–L3 | Same tiers — swap mesh complexity per zoom band |
| `OrbitControls` | Cinemachine + custom isometric rig |

**Lesson from CS2:** Use DOTS **only** for rendering instancing if needed — keep sim on plain C# structs until profiling proves otherwise ([TECH_STACK_RESEARCH.md](./TECH_STACK_RESEARCH.md) CS2 cautionary tale).

### 3.4 UI port map (HTML HUD → UI Toolkit)

| Web HUD | Unity UI Toolkit |
|---------|------------------|
| `ResourcesHud` (RCI) | `ResourcesHud.uxml` |
| `BudgetPanel`, `EconomyPanel` | USS-themed panels (port `hud-tokens.css` → USS variables) |
| `HeraldPanel` | WebRequest to same narrative API **or** embedded template fallback |
| `OnboardingOverlay` | UI Toolkit modal sequence |
| `PlayClient.tsx` orchestration | `PlaySceneController.cs` |

**Keep Next.js APIs** for Herald LLM, saves (Supabase phase), Stripe — Unity client calls same REST endpoints with player auth token.

### 3.5 Asset pipeline reuse

Existing assets transfer directly:

- `web/public/assets/gltf/modern/**/*.glb` → Unity `Assets/Art/Gltf/modern/` (v1 era filter)
- Meshy batch scripts (`scripts/meshy/*`) unchanged — output lands in shared folder
- `web/packages/sim-types` archetype taxonomy → port to C# `BuildingArchetypes.cs` (or code-gen from JSON schema)

---

## 4. Unity MCP integration (AI-accelerated port)

[CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) bridges Cursor (and other MCP clients) to the Unity Editor.

### 4.1 What it enables for this port

| Task | MCP capability |
|------|----------------|
| Scaffold `Play.unity` scene | Create GameObjects, cameras, lights |
| Port instancing prototype | Edit C# scripts, attach components |
| Import GLTF batches | Asset management tools |
| Run play-mode tests | Test runner integration |
| Iterate HUD layouts | UI Toolkit manipulation |
| Profile frame budget | Profiler tools |

### 4.2 Setup (repo + Cursor)

Full guide: [docs/UNITY_MCP_SETUP.md](../UNITY_MCP_SETUP.md)

1. Install Unity 6 (6000.x) via Unity Hub
2. Open `unity/CityMajor.Unity` — Package Manager adds `com.coplaydev.unity-mcp`
3. `Window → MCP for Unity → Configure All Detected Clients` (or use repo `.cursor/mcp.json`)
4. Cursor MCP server: `uvx --from mcpforunityserver mcp-for-unity --transport stdio`
5. Verify: prompt Cursor *"List Unity editor state"* with Unity open on `Play.unity`

### 4.3 Agent workflow (recommended)

```
Phase spike:
  Cursor agent (plan) → unity-mcp (scene setup) → human play-test 10 min
  Repeat per subsystem: terrain → buildings → zones → HUD panel

Never:
  - Let agent run Unity MCP without Unity Editor open
  - Commit Library/ or Temp/ from Unity project
```

---

## 5. Phased migration plan

### Phase 0 — Decision gate (1 week)

| Deliverable | Owner | Done when |
|-------------|-------|-----------|
| Merge `feat/cmjr-merge-wasm-r3f` to `main` | Eng | Web v1 spine stable |
| Unity Hub + MCP smoke test | You | Cube spawns via Cursor prompt |
| `Forge.SimCore` extraction audit | Agent | WasmSimHost deps mapped |
| Linear epic: SB-XXXX Unity Desktop | PM | Charter approved |

**Go/no-go:** If Unity MCP + GLTF import works on Mac in <1 day, proceed. If Unity not installed / blocked, defer 2 weeks.

### Phase 1 — Vertical slice (3–4 weeks)

**Goal:** One playable loop in Unity — **modern era only** — zone paint → growth → instanced buildings → RCI HUD.

| # | Task | Reuse from web |
|---|------|----------------|
| 1.1 | Unity project + URP isometric camera | New |
| 1.2 | Reference `Forge.SimCore` in Unity | C# sim |
| 1.3 | 64×64 chunk terrain + picking | `TerrainChunks`, `TilePicker` logic |
| 1.4 | Instanced buildings from **modern** GLTF (L0 only) | `BuildingInstances`, `gltf-catalog` (`modern/`) |
| 1.5 | Zone paint tool | `zoning.ts`, `ZoneOverlay` |
| 1.6 | RCI demand HUD (UI Toolkit) | `ResourcesHud.tsx` |
| 1.7 | Sim tick @ 8–20 Hz on background thread | `WASM_SIM_BRIDGE` threading model |

**Exit criteria:** 256×256 map, 1k **modern** buildings @ 60 FPS on discrete GPU, zone paint works.

### Phase 2 — Feature parity with web wave-4 (6–8 weeks)

| System | Web reference | Unity target |
|--------|---------------|--------------|
| Roads + traffic overlay | `RoadOverlay`, `WasmTrafficLite` | Sim + mesh overlay |
| Research (modern subset) | `ResearchPanel` | Modern-era tech tree filter |
| Budget / economy panels | `BudgetPanel`, `EconomyPanel` | UI Toolkit |
| Herald narrative | `HeraldPanel` + `/api/narrative/event` | REST client |
| Save/load | CMJR blob format | `CmjrSave.cs` native + cloud API |
| Laws (read-only → toggle) | `LawPanel` | Sim + UI |
| Citizens L2 | `CitizenPanel`, `CitizenDots` | Instanced dots + panel |

**Exit criteria:** Modern-era gameplay loop in native build matches web `/play` depth (minus deferred eras).

### Phase 3 — Desktop productization (4–6 weeks)

| Item | Notes |
|------|-------|
| Steamworks SDK | Achievements, cloud saves, depots |
| Quality tiers + LOD L1–L3 | Port `web/PERF.md` targets |
| Full **modern** Meshy catalog import | `web/public/assets/gltf/modern/` |
| Mac / Win / Linux builds | CI via `game-ci/unity-builder` |
| Steam Deck verification | 30 FPS minimum |

**Deferred:** Postwar → Future era unlock, Steam Workshop, map scale beyond 256×256.

### Phase 4 — Post-launch ops (ongoing)

| Platform | Pipeline | Status |
|----------|----------|--------|
| **Steam** | Unity build → SteamPipe | **Primary** — active development |
| **Web** | `pnpm build` + Docker + Coolify | **Maintenance mode** — critical fixes only |
| **Shared** | `base/data/*`, Meshy scripts, `Forge.SimCore` tests | Sim + content PRs affect Unity; web WASM wrapper thin |

---

## 6. Risk register

| Risk | Severity | Mitigation |
|------|----------|------------|
| **Web regression during pivot** | Medium | Maintenance-mode charter; critical fixes only; smoke tests on `web/` |
| **CS2-style Unity perf** | High | Plain C# sim + GPU instancing; DOTS opt-in only after profiling |
| **Modern-only content gap** | Medium | Focus Meshy pipeline on `modern/`; defer other eras explicitly |
| **Sim fork (WASM vs native)** | Medium | Single `Forge.SimCore`; WASM wrapper stays thin for web maintenance |
| **Unity license / install friction** | Low | Personal/Plus tier sufficient; Hub on dev machines only |
| **Agent goes rogue in Unity MCP** | Medium | Human play-test gate per phase; Roslyn validation enabled |
| **GLTF material mismatch** | Low | Import modern batch early; URP shader graph fallback |
| **Scope creep to 1024 map** | High | Ship 256 first; scale gate in UNITY_V1_SCOPE |

---

## 7. Effort estimate

| Phase | Calendar | FTE | AI acceleration |
|-------|----------|-----|-----------------|
| Phase 0 | 1 week | 0.5 | unity-mcp setup |
| Phase 1 | 3–4 weeks | 1 | **High** — scene + script codegen |
| Phase 2 | 6–8 weeks | 1 | Medium — UI tedious, sim ports clean |
| Phase 3 | 4–6 weeks | 1 + art QA | Medium — Steam + art batch |
| **Total to Steam EA** | **~4–5 months** | 1 dev | vs ~9 months without MCP |

**Web:** Maintenance mode — no parallel v1 feature work.

---

## 8. Decision checklist

Unity desktop as **primary v1** — confirmed 2026-07-12:

- [x] Pivot decision documented ([UNITY_V1_SCOPE.md](./UNITY_V1_SCOPE.md))
- [ ] `Forge.SimCore` extraction audit complete
- [ ] Unity 6 installed; MCP smoke test passes
- [ ] Phase 1 vertical slice @ 60 FPS (modern era)
- [ ] Steam app ID registered
- [ ] Web client tagged maintenance mode in docs

**If Phase 1 fails perf or velocity:** Revisit scope (reduce building cap) before reconsidering platform.

---

## 9. Immediate next actions

1. **You:** Install Unity 6 via Unity Hub; open `unity/` project (see [unity/README.md](../../unity/README.md))
2. **You:** Restart Cursor after MCP config merge; run MCP smoke with Unity open
3. **Agent:** Extract `Forge.SimCore` shared library boundary (PR on `feat/unity-simcore-extract-*`)
4. **Agent:** Phase 1.1 — isometric camera + empty terrain via unity-mcp
5. **PM:** File Linear epic; link this doc

---

## 10. References

- [Unity MCP (CoplayDev)](https://github.com/CoplayDev/unity-mcp) — MIT, 47 MCP tools, Unity 2021.3–6.x
- [Unity MCP Server PyPI](https://pypi.org/project/mcpforunityserver/)
- [WEB_V1_SCOPE.md](./WEB_V1_SCOPE.md) — historical web charter (superseded for platform)
- [UNITY_V1_SCOPE.md](./UNITY_V1_SCOPE.md) — **canonical Unity v1 charter**
- [WASM_SIM_BRIDGE.md](./WASM_SIM_BRIDGE.md) — snapshot protocol to port
- [TECH_STACK_RESEARCH.md](./TECH_STACK_RESEARCH.md) — historical engine analysis
- [IXION Unity ECS case study](https://unity.com/ecs)
- [CS2 performance autopsy](https://blog.paavo.me/cities-skylines-2-performance/)
