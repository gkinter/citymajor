# CityMajor Unity — Orchestration Tracker

**Branch:** `feat/unity-port-plan-2026-07-12`  
**Epic:** [SB-4170](https://linear.app/softblaze/issue/SB-4170) Unity v1 Modern Era Desktop  
**MCP:** `CityMajor.Unity@959bbec5` (Unity 6000.5.3f1) — `refresh_unity` OK; `read_console`/`execute_code` may timeout if bridge wedged (restart editor)

---

## Status dashboard

| Track | Linear | Status | Owner |
|-------|--------|--------|-------|
| Phase 1 scaffold + MCP | SB-4171 | ✅ Done | — |
| Forge.SimCore bridge | SB-4172 | 🟡 DLL + CitySimBridge wired — verify Play | Agent |
| GLTF instancing | SB-4173 | 🟡 gltfast + catalog — reimport GLBs in Unity | Agent |
| Zone paint | SB-4174 | ✅ Done + SimHost.PaintZone | — |
| RCI HUD | SB-4175 | 🟡 UI Toolkit landed — verify in Play | Agent |
| Phase 2 roads | — | ✅ `RoadPaintTool` + overlay in bootstrap | Agent |
| Phase 2 economy panels | — | ✅ `BudgetPanelController` in bootstrap | Agent |
| Phase 2 research | — | ✅ `ResearchPanelController` (`R`) in bootstrap | Agent |
| Phase 2 Herald API | — | 🟡 Wired in bootstrap — pending recompile / Play | Agent |
| Phase 2 CMJR save | — | 🟡 Wired in bootstrap — pending recompile / Play | Agent |
| Phase 2 vehicles (v1.1) | SB-4188 | 🟡 `VehicleInstancer` wired | Agent |
| Phase 2 pedestrians (v1.1) | SB-4187 | 🟡 `PedestrianInstancer` + citizen panel (`C`) | Agent |
| Phase 2 rush curve | SB-4189 | 🟡 `LifeSimMath.RushHourMultiplier` in SimCore + Unity | Agent |
| Life layer v1.1 | — | 🟡 Service overlay (`V`), growth, construction props | Agent |
| Life layer v1.5 | — | 🟡 Ambient time-of-day + audio scaffold | Agent |
| Social / trade stubs | SB-4183–4186 | 🟡 `TradeStrip`, `CityShareStub`, `BlueprintSlice` | Agent |
| Phase 3 Steam | SB-4180–4181 | 🟡 `SteamBootstrap` stub + scope doc | Agent |

---

## Parallel workstreams (active)

### A — SimCore native bridge (SB-4172)

```
Forge.SimCore (netstandard2.1)
  └── SimHost.cs          ← shared with WASM wrapper
  └── WasmTrafficLite     ← linked from SimWasm
scripts/build-simcore-for-unity.sh → Assets/Plugins/Forge/Forge.SimCore.dll
CitySimBridge.cs        ← SimHost @ 8 Hz, PaintZone, SimSnapshot → CitySimState
```

**Verify:** Play mode → paint zones → population/funds change from real sim (not sine placeholder).

### B — GLTF instancing (SB-4173)

```
com.unity.cloud.gltfast 6.12.1
GltfCatalog.cs          ← 12 modern keys from unity-modern-unlocks.json
BuildingArchetypes.cs   ← TypeId / zone → catalog key
GltfMeshCache.cs        ← AssetDatabase (editor) + glTFast (player)
BuildingInstancer.cs    ← per-catalog DrawMeshInstanced batches
```

**Verify:** `Assets/Art/Gltf/modern` symlink → `web/public/assets/gltf/modern` (12 GLBs present).

### C — UI Toolkit HUD (SB-4175)

```
Assets/UI/ResourcesHud.uxml + .uss
ResourcesHudController.cs  ← replace IMGUI OnGUI
Port web ResourcesHud.tsx layout + RCI bars
```

**Verify:** HUD renders in Play mode without IMGUI flicker.

---

## Agent workflow (MCP)

1. **Read** `mcpforunity://instances` — must be `instance_count >= 1`
2. **Compile check** `read_console` types `error` after script/DLL changes
3. **Scene ops** `manage_scene` load/save `Play.unity`
4. **Menu** `execute_menu_item` for CityMajor setup menus
5. **Play test** `manage_editor` action `play` / `stop` (human confirms feel)

**Never:** commit `Library/`, `Temp/`, or generated `.meta` noise without review.

---

## Build commands

```bash
# SimCore DLL for Unity
./scripts/build-simcore-for-unity.sh

# Steam depot placeholder (Phase 3)
./scripts/build-steam-unity.sh

# Full sim (CI)
dotnet build src/Forge.SimCore/Forge.SimCore.csproj
dotnet build src/Forge.SimWasm/Forge.SimWasm.csproj

# Unity symlinks
./scripts/setup-unity.sh
```

---

## Stack (locked for large sim)

| Layer | Technology | Rationale |
|-------|------------|-----------|
| **Simulation** | `Forge.SimCore` SoA + systems | Shared with WASM; scales to 10k households without Unity DOTS |
| **Sim thread** | Plain C# @ 8 Hz → `SimSnapshot` | Double-buffer pattern; Burst opt-in on hot loops later |
| **Rendering** | URP + `Graphics.DrawMeshInstanced` | 5k buildings, 20–40 draws/chunk; Entities Graphics only if profiling demands |
| **Overlays** | GL mesh passes (zones, roads) | No GameObject per tile |
| **UI** | UI Toolkit | Desktop HUD panels |

**Not v1:** Full DOTS/ECS sim rewrite (CS2 lesson — misapplied DOTS hurts more than plain C#).

---

## Living city vision

North star: real **SimCity feeling** — traffic you can read, people at street zoom, districts that breathe.  
Design: [`CITY_ECOSYSTEM_VISION.md`](./CITY_ECOSYSTEM_VISION.md) · v1 ships road heat + buildings; **v1.1** scaffolds wired in bootstrap (vehicles, pedestrians, citizen panel, rush curve).

### Life layer scaffolds (bootstrap)

| Component | Key / toggle | Notes |
|-----------|--------------|-------|
| `VehicleInstancer` | — | GPU cubes on roads / sim vehicles |
| `PedestrianInstancer` | — | ≤200 citizen dots, happiness tint |
| `CitizenPanelController` | `C` | L2 household list from `SimHost.GetPopulationL2()` |
| `CitizenPickTool` | LMB | Pick dot → select household in panel |
| `ServiceCoverageOverlay` | `V` | Police / health / fire / education GL quads |
| `EdgeTrafficOverlay` | `T` | Hot edge lines between congested road tiles |
| `ZoneGrowthVisualizer` | — | Building scale staging |
| `ConstructionPropInstancer` | — | Crane cubes on growing buildings |
| `AmbientLifeController` | — | Day/night ambient + sun tint |
| `AmbientAudioController` | — | Volume scaffold (clips TBD) |
| `TradeStripController` | `E` | Read-only global market stub |
| `BuildPanelController` | `B` | Service plop catalog (Fire/Police/Health…) |
| `LawPanelController` | `L` | Ordinance catalog + sample toggle |
| `DemandOverlayController` | — | Bottom-center bidirectional R/C/I meters |
| `BulldozeTool` | `X` | Zone clear via `SimHost.Bulldoze` |
| `EventTickerController` | — | Bottom HUD ticker |
| `CityShareStub` / `BlueprintSlice` | — | v1.5 / v2.5 API stubs (no backend) |

## Phase 2 — in progress

| Item | Status |
|------|--------|
| Buildings ← `SimSnapshot.Buildings` | ✅ |
| Roads paint (`4` toggle) + `PlaceRoad` | ✅ |
| Road/traffic overlay from snapshot | ✅ |
| Zone grid sync from sim | ✅ |
| Research panel (modern) | ✅ `R` toggles |
| Budget / economy panels | ✅ top-right always on |
| Herald REST | 🟡 `HeraldPanelController` wired (`H`) — not in last DLL build |
| CMJR save/load | 🟡 `SaveLoadPanelController` wired — not in last DLL build |

**Controls:** `1`/`2`/`3` zones · `4` road · `0` erase · `X` bulldoze · `R` research · `H` herald · `C` citizens · `L` laws · `B` build · `E` trade · `F1` help · `V` services · `T` edges · `Space` pause · `5`/`6`/`7` speed · LMB · MMB pan

**Play gate:** [UNITY_PLAY_CHECKLIST.md](./UNITY_PLAY_CHECKLIST.md) · **CityMajor → Open Play Verification Checklist**

---

## Risks

| Risk | Mitigation |
|------|------------|
| netstandard2.1 vs Unity API | Build script + Plugins folder; no direct csproj ref in Unity |
| GLB importer not run | Open Unity once; MCP can't reimport without editor |
| ZoneGrid vs WorldState drift | Paint routes to `SimHost.PaintZone`; overlay reads grid (sync TBD) |
| manifest.json gitignored | Force-add gltfast dep or narrow `.gitignore` |
