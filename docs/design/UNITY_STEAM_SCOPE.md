# Unity Steam Scope (Phase 3 — SB-4180 / SB-4181)

**Status:** Platform facade wired — install Steamworks.NET plugin for live SDK.  
**Epic:** [SB-4170](https://linear.app/softblaze/issue/SB-4170)

---

## v1 EA goals

| Item | v1 | Notes |
|------|-----|-------|
| Steam App ID + depots | ⬜ | Partner site → `SteamAppConfig.AppId` + `steam_appid.txt` |
| Steamworks.NET init | 🟡 | `SteamNativePlatform` + `STEAMWORKS_NET` define — see [INSTALL_STEAMWORKS_NET.md](../steam/INSTALL_STEAMWORKS_NET.md) |
| Achievements | 🟡 | 12 v1 rows in `StreamingAssets/achievements-v1.json` + toast UI |
| Cloud saves (CMJR) | 🟡 | `SteamCloudSave` on Save/Load when SDK live |
| Workshop / blueprints | ⬜ | v2.5 — `BlueprintSlice` chunk ready |
| Rich presence | 🟡 | `SteamRichPresenceController` — pop + approval |

---

## CI workflows

| Workflow | Trigger | Needs Unity? |
|----------|---------|--------------|
| `.github/workflows/unity-simcore.yml` | PR, `feat/**` push, manual | No — dotnet only |
| `.github/workflows/unity-steam-build.yml` | Manual (`workflow_dispatch`) | Job 1 no; Job 2 yes (self-hosted Mac or game-ci) |

`unity-simcore` is the mandatory gate: it builds `Forge.SimCore` for both `netstandard2.1` and `net8.0`, runs `scripts/build-simcore-for-unity.sh`, and asserts the DLL exists at `unity/CityMajor.Unity/Assets/Plugins/Forge/Forge.SimCore.dll`. The Steam player job runs only when `UNITY_PATH` points to a licensed installation; it prints setup instructions otherwise and does not block the workflow.

---

## Build pipeline (target)

```bash
# 1. SimCore DLL (required before player build)
./scripts/build-simcore-for-unity.sh

# 2. Steamworks.NET plugin (once per machine)
./scripts/setup-steamworks-unity.sh

# 3. Unity headless / CI (placeholder)
./scripts/build-steam-unity.sh

# 4. steamcmd app build (manual until CI wired)
#    Content root: Build/Steam/windows/
#    VDF templates: docs/steam/ (TBD)
```

---

## Unity packages

```bash
./scripts/setup-steamworks-unity.sh
# Unity menu: CityMajor → Platform → Enable Steamworks.NET Define
```

Full steps: [docs/steam/INSTALL_STEAMWORKS_NET.md](../steam/INSTALL_STEAMWORKS_NET.md)

1. [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET) — vendored under `Assets/Plugins/Steamworks.NET`
2. IL2CPP **Windows x64** standalone profile in `ProjectSettings`
3. `steam_appid.txt` at project root (480 for dev)

---

## Code touchpoints

| File | Role |
|------|------|
| `Assets/Scripts/Platform/SteamAppConfig.cs` | App ID constant |
| `Assets/Scripts/Platform/ISteamPlatform.cs` | Backend interface |
| `Assets/Scripts/Platform/SteamNativePlatform.cs` | Live SDK (`STEAMWORKS_NET`) |
| `Assets/Scripts/Platform/SteamNullPlatform.cs` | Offline / Editor default |
| `Assets/Scripts/Platform/SteamBootstrap.cs` | Init/shutdown + callbacks |
| `Assets/Scripts/Platform/SteamCloudSave.cs` | CMJR Remote Storage |
| `Assets/Scripts/Platform/SteamRichPresenceController.cs` | Pop + approval presence |
| `SteamAchievementTracker` | — | Evaluates catalog → Steam unlock + toast |
| `AchievementToastController` | — | Top-center popup queue |
| `CityMajorSteamBuild` | Editor / CI | `build-steam-unity.sh` → `Build/Steam/windows/` |
| `CityShareStub` | Future: Steam invite / spectator deep link |

---

## Play gate dependency

Complete [UNITY_PLAY_CHECKLIST.md](./UNITY_PLAY_CHECKLIST.md) (SB-4176) **before** first Steam depot upload.
