# CityMajor — Agent Guide (Unity product)

Unity 6 desktop mesh-3D city builder (**shipped product**). Web R3F is **not shipped**. **Locked v1 scope:** [`docs/design/UNITY_V1_SCOPE.md`](docs/design/UNITY_V1_SCOPE.md).  
**Canonical orchestration:** [`docs/design/UNITY_ORCHESTRATION.md`](docs/design/UNITY_ORCHESTRATION.md).

## Worktree rules (mandatory)

Never edit tracked files on `main`, `master`, `develop`, `production`, `prod`, or `staging`. The canonical clone at `~/citymajor/citymajor` stays on `main`.

**Active Unity v1 worktree:** `~/citymajor/citymajor-unity-port-plan` → branch `feat/unity-port-plan-2026-07-12`.

```bash
# Create a new isolated worktree for your task (prefer from integration tip)
TOPIC=<short-kebab-slug>
SLUG="feat/${TOPIC}-$(date +%Y-%m-%d)"
INTEG_TIP=$(git -C ../citymajor-unity-port-plan rev-parse HEAD)
git worktree add "../citymajor-${TOPIC}" -b "$SLUG" "$INTEG_TIP"
cd "../citymajor-${TOPIC}"
```

- One task = one worktree. Never share a working directory across parallel agents.
- Verify before commit: `git rev-parse --abbrev-ref HEAD` must not be a protected branch.
- After merge: `git worktree remove "../citymajor-${TOPIC}"` && `git branch -d "$SLUG"`.

## Build & verify (Unity — product)

| Command / action | Purpose |
|------------------|---------|
| Unity Hub → open `unity/CityMajor.Unity/` | Primary dev entry |
| **Play** in Editor (`Assets/Scenes/Play.unity`) | Local play-test (player experience) |
| **File → Build Settings** → macOS / Win / Linux | Desktop player builds |
| Symlink assets (once): see [`unity/README.md`](unity/README.md) | Link `base/data` + GLTF into Unity Assets |

**Sim changes:** Edit `src/Forge.SimCore/` — Unity references it in-process. Re-run Play mode after C# sim changes. Rebuild DLL: `./scripts/build-simcore-for-unity.sh`.

### Unity MCP workflow

1. Unity Editor open on `unity/CityMajor.Unity` with `Play.unity` loaded
2. Cursor MCP: `unity-mcp` via `.cursor/mcp.json` (`uvx --from mcpforunityserver mcp-for-unity`)
3. Verify: prompt *"List Unity editor state"* — must return active scene info
4. Agent tasks: scene setup, script edits, GLTF import, play-mode tests via MCP tools
5. **Never** commit `unity/CityMajor.Unity/Library/` or `Temp/`

Full setup: [`docs/UNITY_MCP_SETUP.md`](docs/UNITY_MCP_SETUP.md)

### Blender MCP (3D asset polish)

1. Blender open with **Blender MCP** addon connected (sidebar → Connect)
2. Cursor MCP: `blender` via `.cursor/mcp.json` (`uvx --python 3.11 blender-mcp`)
3. Pipeline: **Meshy batch → Blender polish → `web/public/assets/gltf/` → validate-manifest → Unity Refresh**
4. Use `scripts/blender/citymajor_export_conventions.py` for ground-center + tile scale

Full setup: [`docs/BLENDER_MCP_SETUP.md`](docs/BLENDER_MCP_SETUP.md)

## Build & verify (web / WASM — harness only)

| Command | Purpose |
|---------|---------|
| `pnpm build:wasm` | Publish `Forge.SimWasm` for optional snapshot/CI harness |
| `pnpm smoke:all` | Historical browser smoke — **not** a ship gate for player UX |
| `pnpm dev` | Local R3F spike only — archival |

**Do not** treat `/play` as the product surface. WASM/web work may validate `Forge.SimCore` exports; **player experience is Unity**. See [`docs/design/WEB_V1_SCOPE.md`](docs/design/WEB_V1_SCOPE.md) (superseded / archival).

## Key paths

```
unity/CityMajor.Unity/        → Unity 6 project (URP, UI Toolkit, Steam) — PRODUCT
unity/CityMajor.Unity/Assets/Scripts/  → Sim bridge, rendering, input, UI
src/Forge.SimCore/            → Core simulation logic (Unity native target)
src/Forge.SimWasm/            → Optional WASM harness (not player runtime)
base/data/                    → Shared JSON content
web/public/assets/gltf/modern/ → Modern era GLTF kits (content host path for Unity)
web/                            → Archival R3F spike (not shipped)
docs/design/UNITY_V1_SCOPE.md → Locked Unity v1 charter (modern era, 256×256)
docs/design/UNITY_ORCHESTRATION.md → Canonical Unity integration tracker
docs/design/CATHEDRAL_UNITY_SPRINT.md → Next Cathedral sprint (Unity HUD/tools)
docs/UNITY_MCP_SETUP.md       → Cursor ↔ Unity MCP setup
```

## Architecture reminders

- Sim snapshots → GPU instanced meshes — not per-building GameObjects.
- LOD: full GLTF → simplified mesh → instanced boxes → heatmap blocks.
- Era filter: **modern only** for v1 — Frontier/Industrial/Postwar/Future deferred.
- **Do not fork simulation logic.** One PR to `src/Forge.SimCore` updates Unity; WASM harness stays thin.
- Cathedral UI/tools land in Unity first ([`CATHEDRAL_UNITY_SPRINT.md`](docs/design/CATHEDRAL_UNITY_SPRINT.md)).

## Related docs

- [`CLAUDE.md`](CLAUDE.md) — stack summary, monetization, architecture notes
- [`docs/design/UNITY_V1_SCOPE.md`](docs/design/UNITY_V1_SCOPE.md) — **canonical locked scope**
- [`docs/design/UNITY_ORCHESTRATION.md`](docs/design/UNITY_ORCHESTRATION.md) — **canonical** integration / lanes
- [`docs/design/CATHEDRAL_PROGRAM.md`](docs/design/CATHEDRAL_PROGRAM.md) — sim depth program (Unity-first)
- [`docs/design/WEB_V1_SCOPE.md`](docs/design/WEB_V1_SCOPE.md) — superseded archival web charter
- [`docs/design/MASTER_GAME_CONCEPT.md`](docs/design/MASTER_GAME_CONCEPT.md) — sim depth, economy, narrative design
- [`unity/README.md`](unity/README.md) — Unity project quick start
