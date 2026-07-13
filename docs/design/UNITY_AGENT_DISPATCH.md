# CityMajor Unity — Agent Dispatch Playbook

**Audience:** Orchestrator agent dispatching parallel subagents on the Unity port.  
**Integration branch:** `feat/unity-port-plan-2026-07-12`  
**Status tracker:** [UNITY_ORCHESTRATION.md](./UNITY_ORCHESTRATION.md)  
**Human Play gate:** [UNITY_PLAY_CHECKLIST.md](./UNITY_PLAY_CHECKLIST.md) (SB-4176)

---

## Principles

1. **One agent = one worktree = one lane branch** — never share a working directory.
2. **Lane ownership** — agents edit only paths in their lane; cross-lane changes go through orchestrator.
3. **SimCore DLL is the contract** — UI/Platform lanes consume `SimSnapshot` / `CitySimState`; they do not edit `SimHost` internals.
4. **Bootstrap is shared** — `CityMajorBootstrap.cs` changes are serialized through the orchestrator (one bootstrap PR at a time).
5. **Cherry-pick, don't long-lived merge** — lane branches are short-lived; integration tip stays Play-verified.

---

## Four parallel lanes

| Lane | Branch prefix | Worktree example | Primary deliverables |
|------|---------------|------------------|----------------------|
| **CI** | `feat/unity-ci-*` | `citymajor-unity-ci-steam-workflow` | Build scripts, GHA workflows, editor verify menus, headless build fixes |
| **UI / HUD** | `feat/unity-ui-*` | `citymajor-unity-ui-law-polish` | UI Toolkit panels, UXML/USS, HUD layout, tool-mode chrome |
| **Platform / Steam** | `feat/unity-platform-*` | `citymajor-unity-platform-cloud` | Steamworks facade, achievements, cloud save, `StreamingAssets` |
| **SimCore** | `feat/unity-simcore-*` or `feat/simcore-*` | `citymajor-unity-simcore-bulldoze` | `Forge.SimCore`, `CitySimBridge`, input tools, sim-driven rendering |

---

## File ownership boundaries

Conflicts most often hit `CityMajorBootstrap.cs`, `Assets/Plugins/Forge/Forge.SimCore.dll`, and shared UI paths. Stay in your lane.

### CI lane

| Owns | Does not own |
|------|----------------|
| `scripts/build-simcore-for-unity.sh` | `src/Forge.SimCore/**` logic (SimCore lane) |
| `scripts/build-steam-unity.sh` | `Assets/Scripts/Platform/**` (Platform lane) |
| `scripts/setup-unity.sh`, `scripts/setup-steamworks-unity.sh` | Gameplay UI scripts |
| `.github/workflows/*` (Unity additions) | `Assets/Scripts/**` except `Assets/Editor/` |
| `unity/.../Assets/Editor/CityMajorPlayVerify.cs` | `CityMajorBootstrap.cs` wiring |
| `unity/.../Assets/Scripts/Editor/CityMajorSteamBuild.cs` | |

### UI / HUD lane

| Owns | Does not own |
|------|----------------|
| `Assets/Scripts/UI/**` | `Assets/Scripts/Sim/**`, `Assets/Scripts/Platform/**` |
| `Assets/UI/**` (`*.uxml`, `*.uss`) | `src/Forge.SimCore/**` |
| Bootstrap: **UI** `GameObject` children + `AddComponent` for UI controllers only | Input tool logic (`Assets/Scripts/Input/`) |
| | `Forge.SimCore.dll` binary |

### Platform / Steam lane

| Owns | Does not own |
|------|----------------|
| `Assets/Scripts/Platform/**` | Sim rules / `SimHost` API |
| `Assets/StreamingAssets/achievements-v1.json` | General HUD layout |
| `docs/steam/**` | `scripts/build-steam-unity.sh` (CI lane) |
| Bootstrap: `SteamRichPresenceController`, `SteamAchievementTracker`, achievement toast wiring | `SaveLoadPanelController` UI layout (UI lane) — coordinate on cloud UX copy |

### SimCore lane

| Owns | Does not own |
|------|----------------|
| `src/Forge.SimCore/**` | UI Toolkit UXML |
| `Assets/Scripts/Sim/**` | Platform Steam SDK wrappers |
| `Assets/Scripts/Input/**` | Editor build pipelines |
| `Assets/Scripts/Rendering/**` (sim-driven instancers/overlays) | |
| `Assets/Plugins/Forge/Forge.SimCore.dll` (via build script) | |
| Bootstrap: sim, input, rendering `AddComponent` lines | Bootstrap UI panel `GameObject`s |

### Orchestrator-only (integration merges)

- `CityMajorBootstrap.cs` when multiple lanes land the same session
- `docs/design/UNITY_ORCHESTRATION.md`, this file
- Conflict resolution across lanes

---

## Dispatch template (copy to subagent)

```text
Worktree: ../citymajor-<topic> from feat/unity-port-plan-2026-07-12
Branch: feat/unity-<lane>-<slug>-YYYY-MM-DD
Lane: <CI|UI|Platform|SimCore>
Owns: <paths from table above>
Task: <one sentence>
Merge gate: <lane gate below>
Do NOT edit: <other lanes' paths>
Return: summary + commit SHA(s); no push to integration branch
```

---

## Per-lane merge gates

### CI

```bash
dotnet build src/Forge.SimCore/Forge.SimCore.csproj -c Release
./scripts/build-simcore-for-unity.sh
# Optional when touching Steam build:
UNITY_PATH="..." ./scripts/build-steam-unity.sh
```

Pass = scripts exit 0; no broken editor menu compile errors.

### UI / HUD

```bash
./scripts/build-simcore-for-unity.sh   # only if panel reads new snapshot fields
```

Pass = Unity Play mode: toggled panels open/close, no missing UXML errors in console, FPS acceptable.

### Platform / Steam

```bash
./scripts/setup-steamworks-unity.sh    # once per machine
# Editor: CityMajor → Platform → Enable Steamworks.NET Define
./scripts/build-simcore-for-unity.sh
```

Pass = Play with Steam client + AppId 480: init log line, save shows cloud status, achievement toast on first save.

### SimCore

```bash
./scripts/build-simcore-for-unity.sh
dotnet test src/Forge.SimCore/   # if tests exist for touched area
```

Pass = domain reload clean; Play: zone paint changes pop/funds; bulldoze clears tile; roads rebuild graph.

---

## Merge protocol (orchestrator)

```bash
# On integration worktree (citymajor-unity-port-plan)
git fetch origin
git cherry-pick <lane-sha>          # one commit at a time for risky merges
./scripts/build-simcore-for-unity.sh # if SimCore, Input, or Sim bridge touched
```

1. Cherry-pick **SimCore lane first** when a feature spans sim + UI (UI commit may depend on new snapshot fields).
2. Then Platform, then UI, then CI (docs/scripts only).
3. After each cherry-pick batch: request **human Play gate** subset from [UNITY_PLAY_CHECKLIST.md](./UNITY_PLAY_CHECKLIST.md).
4. On conflict: lane agent rebases their worktree on new integration tip; do not hand-merge across lanes without reading both sides.

---

## Post-merge checklist (integration tip)

Run on `feat/unity-port-plan-2026-07-12` after every cherry-pick batch:

| Step | Command / action | Required when |
|------|------------------|---------------|
| 1. Rebuild SimCore DLL | `./scripts/build-simcore-for-unity.sh` | Any `src/Forge.SimCore/` or `Assets/Scripts/Sim/` change |
| 2. Domain reload | Restart Unity Editor or trigger script recompile | DLL or asmdef change |
| 3. Console zero errors | Play → stop; check red lines | Always |
| 4. Play gate spot-check | Zoning, bulldoze, save, one panel toggle | Always before declaring batch done |
| 5. Steam smoke | Save → achievement toast; presence string updates | Platform lane merged |
| 6. Headless build | `./scripts/build-steam-unity.sh` | CI or Platform build script changed |
| 7. Update orchestration doc | Dashboard row + bootstrap table if new component | New bootstrap wiring |

**Full human sign-off:** all sections of [UNITY_PLAY_CHECKLIST.md](./UNITY_PLAY_CHECKLIST.md) before Phase 3 depot upload.

---

## Automation vs human gates

| Check | Owner |
|-------|-------|
| `dotnet build` SimCore | CI / SimCore lane (local or future GHA) |
| `build-simcore-for-unity.sh` | Every lane before handoff |
| `build-steam-unity.sh` | CI lane / release |
| Web `ci-smoke.yml` | Web maintenance only — **not** Unity Play substitute |
| SB-4176 Play checklist | **Human** — orchestrator cannot skip |
| MCP `read_console` errors | Agent during development |

---

## Anti-patterns

| Don't | Do instead |
|-------|------------|
| Two agents in `citymajor-unity-port-plan` | Second agent gets `citymajor-<topic>` worktree |
| UI agent adds `SimHost` method | SimCore agent exposes API; UI reads snapshot |
| Force-push integration branch | Cherry-pick or merge PR with review |
| Commit `Library/` or `Temp/` | `.gitignore` already excludes; verify `git status` |
| Skip DLL rebuild after C# sim edit | Always run `build-simcore-for-unity.sh` |

---

## Quick links

| Doc | Purpose |
|-----|---------|
| [UNITY_ORCHESTRATION.md](./UNITY_ORCHESTRATION.md) | Status dashboard + bootstrap inventory |
| [UNITY_PLAY_CHECKLIST.md](./UNITY_PLAY_CHECKLIST.md) | Human Play verification (SB-4176) |
| [UNITY_STEAM_SCOPE.md](./UNITY_STEAM_SCOPE.md) | Phase 3 Steam deliverables |
| [INSTALL_STEAMWORKS_NET.md](../steam/INSTALL_STEAMWORKS_NET.md) | Live SDK setup |
| [UNITY_V1_SCOPE.md](./UNITY_V1_SCOPE.md) | Locked v1 charter |

**Editor menus:** CityMajor → Open Play Verification Checklist · CityMajor → Open Agent Dispatch Doc
