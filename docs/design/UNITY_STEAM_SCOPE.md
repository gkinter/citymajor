# Unity Steam Scope (Phase 3 — SB-4180 / SB-4181)

**Status:** Scaffold only — no Steamworks.NET package yet.  
**Epic:** [SB-4170](https://linear.app/softblaze/issue/SB-4170)

---

## v1 EA goals

| Item | v1 | Notes |
|------|-----|-------|
| Steam App ID + depots | ⬜ | Partner site → `SteamAppConfig.AppId` |
| Steamworks.NET init | 🟡 stub | `SteamBootstrap.cs` logs + lifecycle hook |
| Achievements | ⬜ | Mirror web milestone IDs when defined |
| Cloud saves (CMJR) | ⬜ | `SteamRemoteStorage` optional; local CMJR remains fallback |
| Workshop / blueprints | ⬜ | v2.5 — `BlueprintSlice` chunk ready |
| Rich presence | ⬜ | Pop + era string |

---

## Build pipeline (target)

```bash
# 1. SimCore DLL (required before player build)
./scripts/build-simcore-for-unity.sh

# 2. Unity headless / CI (placeholder)
./scripts/build-steam-unity.sh

# 3. steamcmd app build (manual until CI wired)
#    Content root: Build/Steam/windows/
#    VDF templates: docs/steam/ (TBD)
```

---

## Unity packages (not installed yet)

1. [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET) — `SteamAPI.Init`, callbacks, overlay
2. IL2CPP **Windows x64** standalone profile in `ProjectSettings`
3. Copy `steam_api64.dll` next to player executable per Steamworks doc

---

## Code touchpoints

| File | Role |
|------|------|
| `Assets/Scripts/Platform/SteamAppConfig.cs` | App ID constant |
| `Assets/Scripts/Platform/SteamBootstrap.cs` | Init/shutdown + `DontDestroyOnLoad` |
| `SaveLoadPanelController` | Future: sync CMJR to Steam Cloud |
| `CityShareStub` | Future: Steam invite / spectator deep link |

---

## Play gate dependency

Complete [UNITY_PLAY_CHECKLIST.md](./UNITY_PLAY_CHECKLIST.md) (SB-4176) **before** first Steam depot upload.
