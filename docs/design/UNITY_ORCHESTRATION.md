# CityMajor Unity — Orchestration Tracker

**Branch:** `feat/unity-port-plan-2026-07-12`  
**Integration tip:** `39aca18` — CI Engine+Game tests (932) + pop growth HUD  
**Epic:** [SB-4170](https://linear.app/softblaze/issue/SB-4170) Unity v1 Modern Era Desktop  
**MCP:** `CityMajor.Unity@959bbec5` (Unity 6000.5.3f1) — `refresh_unity` OK; `read_console`/`execute_code` may timeout if bridge wedged (restart editor)

**Agent dispatch playbook:** [UNITY_AGENT_DISPATCH.md](./UNITY_AGENT_DISPATCH.md)

---

## Status dashboard

| Track | Linear | Status | Owner |
|-------|--------|--------|-------|
| Phase 1 scaffold + MCP | SB-4171 | ✅ Done | — |
| Forge.SimCore bridge | SB-4172 | ✅ `SimHost` + `CitySimBridge` @ 8 Hz; bulldoze + road graph rebuild | — |
| GLTF instancing | SB-4173 | 🟡 gltfast + catalog wired — **reimport GLBs in Unity** | Agent |
| Zone paint | SB-4174 | ✅ Done + `SimHost.PaintZone` | — |
| RCI HUD | SB-4175 | ✅ UI Toolkit `ResourcesHud` + `DemandOverlayController` + pop growth `(+N/mo)` | — |
| Bulldoze tool | — | ✅ `BulldozeTool` + `SimHost.Bulldoze` (`X`) | — |
| Demand overlay | — | ✅ Bottom-center R/C/I meters | — |
| Tool mode HUD | — | ✅ `ToolModeHudController` — active paint/road/build/bulldoze | — |
| Happiness meter | — | ✅ `HappinessMeterController` (left stack) | — |
| Approval meter | — | ✅ `ApprovalMeterController` (top-right) | — |
| Phase 2 roads | — | ✅ `RoadPaintTool` + overlay in bootstrap | — |
| Phase 2 economy panels | — | ✅ `BudgetPanelController` in bootstrap | — |
| Phase 2 research | — | ✅ `ResearchPanelController` (`R`) in bootstrap | — |
| Phase 2 Herald API | — | ✅ `HeraldPanelController` wired (`H`) | — |
| Phase 2 CMJR save | — | ✅ `SaveLoadPanelController` + Steam cloud hook | — |
| Phase 2 vehicles (v1.1) | SB-4188 | ✅ `VehicleInstancer` wired | — |
| Phase 2 pedestrians (v1.1) | SB-4187 | ✅ `PedestrianInstancer` + citizen panel (`C`) | — |
| Phase 2 rush curve | SB-4189 | ✅ `LifeSimMath.RushHourMultiplier` in SimCore + Unity | — |
| Life layer v1.1 | — | ✅ Service overlay (`V`), growth, construction props | — |
| Life layer v1.5 | — | ✅ Ambient time-of-day + audio scaffold | — |
| Social / trade stubs | SB-4183–4186 | 🟡 `TradeStrip`, `CityShareStub` (save panel), `BlueprintSlice` stub | Agent |
| Phase 3 Steam facade | SB-4180 | ✅ `SteamNativePlatform` + rich presence + cloud save hooks | — |
| Phase 3 achievements | SB-4181 | ✅ Catalog + toast + headless Windows build script | — |
| Phase 3 Steam depot | SB-4180 | ⬜ Partner App ID, depots, live SDK on CI | Agent |

---

## Subagent dispatch lanes

Parallel agents **must** use isolated worktrees and lane branch prefixes. Full file boundaries: [UNITY_AGENT_DISPATCH.md](./UNITY_AGENT_DISPATCH.md).

| Lane | Branch prefix | Owns | Merge gate |
|------|---------------|------|------------|
| **CI** | `feat/unity-ci-*` | `scripts/build-*.sh`, `.github/workflows/`, `Assets/Editor/*Build*`, `CityMajorPlayVerify.cs` | `dotnet build` SimCore + `./scripts/build-simcore-for-unity.sh`; optional `./scripts/build-steam-unity.sh` |
| **UI / HUD** | `feat/unity-ui-*` | `Assets/Scripts/UI/`, `Assets/UI/*.uxml` / `*.uss`, bootstrap UI `GameObject` wiring only | Play mode — panels toggle, no red console |
| **Platform / Steam** | `feat/unity-platform-*` | `Assets/Scripts/Platform/`, `StreamingAssets/achievements-v1.json`, `docs/steam/` | `STEAMWORKS_NET` define + save achievement toast in Play |
| **SimCore** | `feat/unity-simcore-*` or `feat/simcore-*` | `src/Forge.SimCore/`, `Assets/Scripts/Sim/`, `Assets/Scripts/Input/`, sim-facing `Rendering/` | `./scripts/build-simcore-for-unity.sh` + domain reload + Play sim verify |

**Integration branch:** `feat/unity-port-plan-2026-07-12` — orchestrator merges lane work here; feature agents do **not** commit directly to it.

---

## Merge protocol (parallel agents)

1. **Base** — create worktree from integration tip:
   ```bash
   cd ~/citymajor/citymajor
   git fetch origin feat/unity-port-plan-2026-07-12
   TOPIC=<lane>-<short-slug>
   git worktree add "../citymajor-${TOPIC}" -b "feat/unity-${TOPIC}-$(date +%Y-%m-%d)" feat/unity-port-plan-2026-07-12
   cd "../citymajor-${TOPIC}"
   ```
2. **Work** — stay inside lane file ownership; one logical change per commit.
3. **Lane verify** — run that lane's merge gate (table above) before push.
4. **Cherry-pick to integration** — orchestrator on `citymajor-unity-port-plan` worktree:
   ```bash
   git cherry-pick <sha>   # or squash-merge via PR
   ./scripts/build-simcore-for-unity.sh   # if SimCore or Input touched
   ```
5. **Integration verify** — human Play gate ([UNITY_PLAY_CHECKLIST.md](./UNITY_PLAY_CHECKLIST.md)) before batching the next cherry-pick set.
6. **Cleanup** — after merge to integration: `git worktree remove "../citymajor-${TOPIC}"` && `git branch -d feat/unity-...`

**Conflict rules:** `CityMajorBootstrap.cs` is orchestrator-owned — lane agents add **one** `AddComponent` / `Configure` line via dedicated bootstrap PR, not drive-by edits. SimCore lane owns `SimHost` API changes; UI lane consumes new snapshot fields only.

---

## Parallel workstreams (reference)

### A — SimCore native bridge (SB-4172) ✅

```
Forge.SimCore (netstandard2.1)
  └── SimHost.cs          ← shared with WASM wrapper
  └── WasmTrafficLite     ← linked from SimWasm
scripts/build-simcore-for-unity.sh → Assets/Plugins/Forge/Forge.SimCore.dll
CitySimBridge.cs        ← SimHost @ 8 Hz, PaintZone, Bulldoze, SimSnapshot → CitySimState
```

**Verify:** Play mode → paint zones → population/funds change from real sim (not sine placeholder).

### B — GLTF instancing (SB-4173) 🟡

```
com.unity.cloud.gltfast 6.12.1
GltfCatalog.cs          ← 12 modern keys from unity-modern-unlocks.json
BuildingArchetypes.cs   ← TypeId / zone → catalog key
GltfMeshCache.cs        ← AssetDatabase (editor) + glTFast (player)
BuildingInstancer.cs    ← per-catalog DrawMeshInstanced batches
```

**Verify:** `Assets/Art/Gltf/modern` symlink → `web/public/assets/gltf/modern` (12 GLBs present); open Unity once to reimport.

### C — UI Toolkit HUD (SB-4175) ✅

```
Assets/UI/ResourcesHud.uxml + .uss
ResourcesHudController.cs  ← pop, funds, hour, rush
DemandOverlayController.cs ← R/C/I demand bars
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
# SimCore DLL for Unity (required after any SimHost change)
./scripts/build-simcore-for-unity.sh

# Steam depot / headless player (Phase 3)
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

### Bootstrap components (`CityMajorBootstrap.cs`)

All rows below are **created in `Awake`** on `CityMajor_Root` unless noted.

#### Sim & grid

| Component | Key / toggle | Notes |
|-----------|--------------|-------|
| `ZoneGrid` | — | 256×256 tile buffer |
| `CitySimBridge` | — | `SimHost` tick + snapshot publish |

#### Rendering & world

| Component | Key / toggle | Notes |
|-----------|--------------|-------|
| `ZoneOverlayRenderer` | — | R/C/I zone GL quads |
| `ZoneGrowthVisualizer` | — | Building scale staging |
| `BuildingInstancer` | — | GLTF / placeholder instanced meshes |
| `RoadOverlayRenderer` | — | Road network tint |
| `VehicleInstancer` | — | GPU cubes on roads / sim vehicles |
| `PedestrianInstancer` | — | ≤200 citizen dots, happiness tint |
| `ConstructionPropInstancer` | — | Crane cubes on growing buildings |
| `ServiceCoverageOverlay` | `V` | Police / health / fire / education GL quads |
| `EdgeTrafficOverlay` | `T` | Hot edge lines between congested road tiles |
| `AmbientLifeController` | — | Day/night ambient + sun tint |
| `IsometricCameraController` | MMB pan | On `CityCamera` child |

#### Input tools

| Component | Key / toggle | Notes |
|-----------|--------------|-------|
| `ZonePaintTool` | `1`/`2`/`3`/`0` | R/C/I paint + erase |
| `RoadPaintTool` | `4` | `SimHost.PlaceRoad` |
| `BulldozeTool` | `X` | Zone/building/road clear via `SimHost.Bulldoze` |
| `BuildPlopTool` | `B` + panel | Service plop on zoned tiles |
| `CitizenPickTool` | LMB | Pick pedestrian dot → citizen panel |

#### Audio

| Component | Key / toggle | Notes |
|-----------|--------------|-------|
| `AmbientAudioController` | — | Volume scaffold (clips TBD) |

#### UI — always-on HUD

| Component | Key / toggle | Notes |
|-----------|--------------|-------|
| `ResourcesHudController` | — | Pop, funds, hour, rush multiplier, growth `(+N/mo)` |
| `EraBadgeController` | — | Era name badge (reads `SimSnapshot.Era`) |
| `DemandOverlayController` | — | Bottom-center bidirectional R/C/I meters |
| `HappinessMeterController` | — | Left-stack happiness bar |
| `BudgetPanelController` | — | Top-right economy strip |
| `ApprovalMeterController` | — | Top-right mayor approval |
| `EventTickerController` | — | Bottom HUD ticker |
| `TimeControlsController` | `Space`, `5`/`6`/`7` | Pause + 1×/2×/4× speed |
| `ToolModeHudController` | — | Active tool indicator (zone/road/build/bulldoze) |

#### UI — toggle panels

| Component | Key / toggle | Notes |
|-----------|--------------|-------|
| `ResearchPanelController` | `R` | Modern tech enqueue |
| `HeraldPanelController` | `H` | REST or template narrative |
| `CitizenPanelController` | `C` | L2 household list |
| `LawPanelController` | `L` | Ordinance catalog + sample toggle |
| `BuildPanelController` | `B` | Service plop catalog |
| `BlueprintPanelController` | `P` | District slice export stub (v2.5 Workshop) |
| `TradeStripController` | `E` | Read-only global market stub |
| `SaveLoadPanelController` | — | CMJR save/load + share stub URL |
| `HelpPanelController` | `F1` | Control reference overlay |

#### Platform (Phase 3)

| Component | Key / toggle | Notes |
|-----------|--------------|-------|
| `SteamRichPresenceController` | — | Pop + approval when SDK live |
| `SteamAchievementTracker` | — | Evaluates `achievements-v1.json` |
| `AchievementToastController` | — | Top-center unlock queue |
| `SteamWorkshopStub` | — | Log-only UGC publish stub |

#### Not in bootstrap (library stubs)

| Symbol | Notes |
|--------|-------|
| `CityShareStub` | Spectator URL from save panel |
| `BlueprintSlice` / `BlueprintSliceWriter` | v2.5 Workshop chunk header stub |

**Controls summary:** `1`/`2`/`3` zones · `4` road · `0` erase · `X` bulldoze · `R` research · `H` herald · `C` citizens · `L` laws · `B` build · `P` blueprint · `E` trade · `F1` help · `V` services · `T` edges · `Space` pause · `5`/`6`/`7` speed · LMB · MMB pan

**Play gate:** [UNITY_PLAY_CHECKLIST.md](./UNITY_PLAY_CHECKLIST.md) · **CityMajor → Open Play Verification Checklist**

---

## Phase 2 — complete ✅

| Item | Status |
|------|--------|
| Buildings ← `SimSnapshot.Buildings` | ✅ |
| Roads paint (`4`) + `PlaceRoad` | ✅ |
| Road/traffic overlay from snapshot | ✅ |
| Zone grid sync from sim | ✅ |
| Research panel (modern) | ✅ `R` toggles |
| Budget / economy panels | ✅ top-right always on |
| Herald REST | ✅ `HeraldPanelController` |
| CMJR save/load | ✅ `SaveLoadPanelController` + cloud hook |
| Bulldoze + road graph rebuild | ✅ `SimHost.Bulldoze` |
| Tool mode HUD | ✅ |
| Demand / happiness / approval meters | ✅ |

---

## Phase 3 — Steam (in progress)

| Item | Status |
|------|--------|
| Platform facade (`ISteamPlatform`) | ✅ null + native backends |
| Rich presence | ✅ wired — needs live Steam client |
| Achievements catalog + toast | ✅ 12 v1 rows |
| Cloud saves (CMJR) | 🟡 `SteamCloudSave` when SDK live |
| Headless Windows build | ✅ `./scripts/build-steam-unity.sh` |
| CI SimCore gate | ✅ `.github/workflows/unity-simcore.yml` + Engine (822) + Game (110) tests |
| Steamworks.NET install | 🟡 see install doc below |
| Partner App ID + depots | ⬜ |

### Steam install (required for live SDK)

1. Run `./scripts/setup-steamworks-unity.sh`
2. Unity: **CityMajor → Platform → Enable Steamworks.NET Define**
3. Follow full steps: **[INSTALL_STEAMWORKS_NET.md](../steam/INSTALL_STEAMWORKS_NET.md)**
4. Scope reference: [UNITY_STEAM_SCOPE.md](./UNITY_STEAM_SCOPE.md)

**Ship blocker:** complete [UNITY_PLAY_CHECKLIST.md](./UNITY_PLAY_CHECKLIST.md) (SB-4176) before first depot upload.

---

## Risks

| Risk | Mitigation |
|------|------------|
| netstandard2.1 vs Unity API | Build script + Plugins folder; no direct csproj ref in Unity |
| GLB importer not run | Open Unity once; MCP can't reimport without editor |
| ZoneGrid vs WorldState drift | Paint routes to `SimHost.PaintZone`; overlay reads grid |
| Parallel agent conflicts on bootstrap | Lane ownership + cherry-pick protocol ([UNITY_AGENT_DISPATCH.md](./UNITY_AGENT_DISPATCH.md)) |
| manifest.json gitignored | Force-add gltfast dep or narrow `.gitignore` |
