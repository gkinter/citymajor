# CityMajor

Deep city-builder simulation — **Unity 6 desktop / Steam** mesh 3D (Cities: Skylines lite). Linear: [SB-3704](https://linear.app/softblaze/issue/SB-3704).

**Product = Unity.** Player experience ships from the Unity Editor and player builds. The browser R3F client is **not shipped**.

## Stack

| Layer | Technology |
|-------|------------|
| **Platform** | **Unity 6 + URP**, Steam desktop (macOS / Windows / Linux) |
| **Shell / UI** | UI Toolkit HUD panels |
| **3D renderer** | URP + GPU instancing (chunked LOD) |
| **Simulation** | **Forge.SimCore** native in-process (~9.4k LOC C#); dedicated sim thread |
| **Backend** | API (saves, entitlements), LLM proxy with template fallback, Steam; Solana phase 2 |
| **Assets** | Modular GLTF kits — **modern era** (`web/public/assets/gltf/modern/` paths, consumed by Unity) |

**Web (`web/` Next.js + R3F):** **Archival / spike only** — historical browser prototype. No player-ship features.

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
web/              → Archival R3F spike + GLTF kit host paths (not shipped)
docs/design/      → Game design (UNITY_V1_SCOPE, UNITY_ORCHESTRATION, CATHEDRAL_*, …)
.cursor/mcp.json  → unity-mcp server config for Cursor
```

## Architecture notes

- Sim snapshots → GPU instanced meshes on render thread — **not** per-building GameObjects.
- LOD tiers: full GLTF (street) → simplified mesh → instanced boxes → heatmap blocks.
- LLM: backend API at launch (free 10 events/day; Founder unlimited); templates always available.
- **Do not fork sim logic.** One PR to `src/Forge.SimCore` is the single source of truth.
