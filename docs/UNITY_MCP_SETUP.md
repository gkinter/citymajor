# Unity MCP — Cursor Integration for CityMajor

Connect [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) so Cursor agents can drive the Unity Editor while porting CityMajor to a desktop client.

**Requirements:** Unity **2021.3 LTS – 6.x** · Python **3.10+** · [`uv`](https://docs.astral.sh/uv/) (provides `uvx`)

---

## 0. Automated bootstrap (recommended)

From repo root:

```bash
chmod +x scripts/setup-unity.sh   # once
./scripts/setup-unity.sh
```

The script:

- Checks `uv` / `uvx` (prints install hint if missing)
- Verifies `uvx --from mcpforunityserver mcp-for-unity --help`
- Warns if Unity Hub / Editor are not installed (Hub download link)
- Creates symlinks:
  - `unity/CityMajor.Unity/Assets/Art/Gltf/modern` → `web/public/assets/gltf/modern`
  - `unity/CityMajor.Unity/Assets/Data` → `base/data`
- Prints next steps (open Unity, MCP configure, restart Cursor)

**In Unity Editor:** menu **CityMajor → Link Shared Assets** re-creates the same symlinks (macOS).

---

## 1. Install Unity Editor

1. Install [Unity Hub](https://unity.com/download)
2. Install **Unity 6** (6000.x) with modules:
   - **Mac/Win/Linux Build Support** (your targets)
   - **WebGL** (optional — only if testing dual-target)
3. Open the CityMajor Unity project:

```bash
# From repo root — first open creates Library/ (gitignored)
open unity/CityMajor.Unity   # macOS — or add via Unity Hub
```

---

## 2. Install MCP for Unity package (Unity side)

The project's `unity/CityMajor.Unity/Packages/manifest.json` already pins:

```json
"com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#v10.0.0"
```

**In Unity Editor:**

1. Open `unity/CityMajor.Unity`
2. Wait for Package Manager to resolve `com.coplaydev.unity-mcp`
3. Menu: **Window → MCP for Unity**
4. Click **Configure All Detected Clients** (writes Cursor config if not already present)

If Package Manager fails, manually add via **Add package from git URL**:
`https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#v10.0.0`

---

## 3. Cursor MCP server (stdio)

### Option A — Project config (committed, team-shared)

This repo includes `.cursor/mcp.json` with the Unity MCP server entry. Cursor merges project-level MCP with user settings.

### Option B — User config (`~/.cursor/mcp.json`)

Add manually:

```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "uvx",
      "args": [
        "--from",
        "mcpforunityserver",
        "mcp-for-unity",
        "--transport",
        "stdio"
      ]
    }
  }
}
```

**Pin to GitHub release** (alternative):

```json
"unity-mcp": {
  "command": "uvx",
  "args": [
    "--from",
    "git+https://github.com/CoplayDev/unity-mcp@v10.0.0#subdirectory=Server",
    "mcp-for-unity",
    "--transport",
    "stdio"
  ]
}
```

### Verify `uvx` is on PATH

```bash
which uvx    # or: uv tool install ...
uvx --from mcpforunityserver mcp-for-unity --help
```

---

## 4. Smoke test

1. **Open Unity** on `unity/CityMajor.Unity` — wait for compile
2. **Restart Cursor** (MCP servers load at startup)
3. In Cursor chat, ask:

   > Using unity-mcp: report Unity editor state. If ready, create an empty GameObject named `CityMajor_Root` in the active scene.

4. Expected: Unity hierarchy shows `CityMajor_Root`; MCP returns editor readiness

### Troubleshooting

| Symptom | Fix |
|---------|-----|
| MCP server not listed | Restart Cursor; check `.cursor/mcp.json` syntax |
| `uvx` not found | `curl -LsSf https://astral.sh/uv/install.sh \| sh` |
| Unity not connected | Unity must be open; check **Window → MCP for Unity** status |
| Multiple Unity instances | Set default instance in MCP window — [multi-instance guide](https://coplaydev.github.io/unity-mcp/guides/multi-instance) |
| Roslyn script errors | Enable Roslyn validation in MCP settings |

---

## 5. Agent rules for CityMajor + Unity MCP

When using unity-mcp on this repo:

1. **Sim logic** lives in `src/Forge.SimCore/` — edit there, not duplicated in `Assets/Scripts/Sim/` adapters
2. **Do not commit** `unity/CityMajor.Unity/Library/`, `Temp/`, `Logs/`, `UserSettings/`
3. **GLTF source of truth:** `web/public/assets/gltf/` — import or symlink, don't fork meshes
4. **HUD spec:** port from `web/components/city/*` — match `web/styles/hud-tokens.css`
5. **Human gate:** run Play mode after each MCP scene mutation batch

---

## 6. Useful MCP prompts for the port

```
Create an isometric camera rig at 45° yaw, 30° pitch, targeting origin.
Import Assets/Art/Gltf/frontier/res_low_frontier_00.glb and place at (0,0,0).
Add a C# script CitySimBridge.cs that references Forge.SimCore and ticks at 8 Hz.
List all tools available for UI Toolkit editing.
Run play mode and report console errors.
```

---

## 7. Links

- [unity-mcp GitHub](https://github.com/CoplayDev/unity-mcp)
- [Tool catalog](https://coplaydev.github.io/unity-mcp/reference/tools/)
- [Discord](https://discord.gg/y4p8KfzrN4)
- [CityMajor Unity port plan](./design/UNITY_PORT_MEGA_PLAN.md)
