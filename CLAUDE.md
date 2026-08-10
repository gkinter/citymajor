# CityMajor

Deep city-builder simulation — **Unity 6 desktop / Steam** mesh 3D (Cities: Skylines lite).

**Product = Unity since 2026-07-12** ([SB-4170](https://linear.app/softblaze/issue/SB-4170) Unity v1 desktop). This is not a new Aug 2026 decision — Unity has been the shipped product surface since the Jul 12 port plan. [SB-3704](https://linear.app/softblaze/issue/SB-3704) was the Jul web/WASM R3F spike only.

**Active worktree:** `~/citymajor/citymajor-unity-port-plan` → branch `feat/unity-port-plan-2026-07-12`.

> Canonical `main` may lag. Product code + `UNITY_*` design docs live on the Unity tip above — do not treat a stale web-first `main` / `origin/HEAD` as the product default.

## Stack

| Layer | Technology |
|-------|------------|
| **Platform** | **Unity 6 + URP**, Steam desktop (macOS / Windows / Linux) |
| **Shell / UI** | UI Toolkit HUD panels |
| **3D renderer** | URP + GPU instancing (chunked LOD) |
| **Simulation** | **Forge.SimCore** native in-process (~9.4k LOC C#); dedicated sim thread |
| **Backend** | API (saves, entitlements), LLM proxy with template fallback, Steam; Solana phase 2 |
| **Assets** | Modular GLTF kits — **modern era** (`web/public/assets/gltf/modern/` paths, consumed by Unity) |

**Web (`web/` Next.js + R3F):** **Archival harness only** — historical browser prototype ([`docs/design/WEB_ARCHIVAL.md`](docs/design/WEB_ARCHIVAL.md)). No player-ship features. SB-3704 spike is frozen.

**WASM (`Forge.SimWasm`):** Optional **sim validation harness** (CI / smoke against shared snapshot contract). Not the player runtime.

**Legacy:** Forge Engine (SDL2 + OpenGL) is reference only — not shipped.

**Unity port:** [`docs/design/UNITY_V1_SCOPE.md`](docs/design/UNITY_V1_SCOPE.md) · [`docs/design/UNITY_ORCHESTRATION.md`](docs/design/UNITY_ORCHESTRATION.md) (canonical) · [`docs/design/UNITY_PORT_MEGA_PLAN.md`](docs/design/UNITY_PORT_MEGA_PLAN.md) · MCP: [`docs/UNITY_MCP_SETUP.md`](docs/UNITY_MCP_SETUP.md) · [`unity/README.md`](unity/README.md)  
**Cathedral:** [`docs/design/CATHEDRAL_PROGRAM.md`](docs/design/CATHEDRAL_PROGRAM.md) · next sprint [`docs/design/CATHEDRAL_UNITY_SPRINT.md`](docs/design/CATHEDRAL_UNITY_SPRINT.md)

## Unity v1 scope (locked)

**Full charter:** [`docs/design/UNITY_V1_SCOPE.md`](docs/design/UNITY_V1_SCOPE.md) — canonical locked v1 scope; supersedes [WEB_V1_SCOPE.md](docs/design/WEB_V1_SCOPE.md) for platform decisions.

| Parameter | Value |
|-----------|-------|
| Map | **256×256** tiles (64 chunks @ 32×32) |
| Buildings | ~**5,000** max instanced (modern archetypes) |
| Households | ~**10,000** |
| Era | **Modern only** (`svc_modern`, `com_modern`, `ind_modern`, `res_*_modern`) |
| Multiplayer | **None** in v1 |
| FPS target | ≥30 integrated GPU, ≥60 discrete GPU with LOD active |

## Monetization

**Steam base game** + **Founder Pass equivalent (TBD)** + cosmetic DLC (sim-neutral facade skins). Solana holder perks phase 2. No pay-to-win.

## Key directories

```
unity/            → Unity 6 desktop client (URP, UI Toolkit, Steam) — **SHIPPED PRODUCT**
src/Forge.*       → C# sim (Forge.SimCore — Unity native; Forge.SimWasm — optional harness)
base/data/        → Shared JSON content (tech tree, events, laws)
web/              → Archival R3F harness + GLTF kit host paths (not shipped)
docs/design/      → Game design (UNITY_V1_SCOPE, UNITY_ORCHESTRATION, WEB_ARCHIVAL, CATHEDRAL_*, …)
.cursor/mcp.json  → unity-mcp server config for Cursor
```

## Agent / remote defaults

- Prefer the Unity integration tip above — do **not** follow web-first `AGENTS.md` on stale `main` or `origin/HEAD` when it still points at the Jul web spike.
- **Do not** change `origin/HEAD` without explicit user approval. Recommended (when approved): `git remote set-head origin feat/unity-port-plan-2026-07-12`, or merge Unity to `main`.

## Architecture notes

- Sim snapshots → GPU instanced meshes on render thread — **not** per-building GameObjects.
- LOD tiers: full GLTF (street) → simplified mesh → instanced boxes → heatmap blocks.
- LLM: backend API at launch (free 10 events/day; Founder unlimited); templates always available.
- **Do not fork sim logic.** One PR to `src/Forge.SimCore` is the single source of truth.
