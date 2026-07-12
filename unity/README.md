# CityMajor — Unity Desktop Client

Native desktop target for CityMajor (Steam). Shares **Forge.SimCore** C# simulation with the browser WASM build.

**Port plan:** [`docs/design/UNITY_PORT_MEGA_PLAN.md`](../docs/design/UNITY_PORT_MEGA_PLAN.md)  
**MCP setup:** [`docs/UNITY_MCP_SETUP.md`](../docs/UNITY_MCP_SETUP.md)

---

## Quick start

### Prerequisites

- Unity Hub + **Unity 6** (6000.x)
- .NET 8 SDK (for `Forge.SimCore` — same as repo root)
- `uv` / `uvx` (for Unity MCP in Cursor)

### First open

1. Unity Hub → **Add** → select `unity/CityMajor.Unity/`
2. Open project; wait for packages (includes [unity-mcp](https://github.com/CoplayDev/unity-mcp))
3. **Window → MCP for Unity → Configure All Detected Clients**
4. Open scene `Assets/Scenes/Play.unity` (created during Phase 1 spike)

### Link sim + data (after Phase 0 extract)

```bash
# From repo root — symlink shared content into Unity Assets
ln -sf ../../../base/data unity/CityMajor.Unity/Assets/Data
ln -sf ../../../web/public/assets/gltf unity/CityMajor.Unity/Assets/Art/Gltf
```

---

## Project structure (target)

```
CityMajor.Unity/
├── Assets/
│   ├── Scenes/Play.unity
│   ├── Scripts/
│   │   ├── Sim/CitySimBridge.cs      # Adapter → Forge.SimCore
│   │   ├── Rendering/                # Chunks, instancing, LOD
│   │   ├── Input/                    # Zone, road, build tools
│   │   └── UI/                       # UI Toolkit HUD
│   ├── Art/Gltf/                     # → web/public/assets/gltf
│   └── Data/                         # → base/data JSON
├── Packages/manifest.json            # URP + unity-mcp
└── ProjectSettings/
```

---

## Packages (manifest.json)

| Package | Purpose |
|---------|---------|
| `com.unity.render-pipelines.universal` | URP rendering |
| `com.coplaydev.unity-mcp` | Cursor ↔ Unity MCP bridge |
| `com.unity.cloud.gltf` or GLTFast | GLTF import (pick during Phase 1) |

---

## Build targets

| Platform | Phase | Notes |
|----------|-------|-------|
| macOS (Apple Silicon) | Phase 1 | Primary dev target |
| Windows x64 | Phase 3 | Steam primary audience |
| Linux x64 | Phase 3 | Steam Deck / desktop |
| WebGL | **Out of scope** | Use `web/` R3F client |

---

## Relationship to web client

| Concern | Web (`web/`) | Unity (`unity/`) |
|---------|--------------|------------------|
| Sim | `Forge.SimWasm` (JSON) | `Forge.SimCore` (in-process) |
| Renderer | Three.js / R3F | URP + GPU instancing |
| HUD | React HTML overlays | UI Toolkit |
| Deploy | Coolify / Docker | Steam |
| Assets | Same GLTF catalog | Same GLTF catalog |

**Do not fork simulation logic.** One PR to `src/Forge.SimCore` should update both clients.
