# CityMajor Steam Depot Upload Runbook

**Phase 3 partner-site checklist** — first Windows depot upload for CityMajor Unity.  
**Linear:** [SB-4180](https://linear.app/softblaze/issue/SB-4180) (Steam facade + depot) · [SB-4181](https://linear.app/softblaze/issue/SB-4181) (headless build)

Complete [UNITY_PLAY_CHECKLIST.md](../design/UNITY_PLAY_CHECKLIST.md) before uploading. Scope overview: [UNITY_STEAM_SCOPE.md](../design/UNITY_STEAM_SCOPE.md).

---

## 0. Readiness snapshot (2026-08-10)

| Gate | Status | Notes |
|------|--------|-------|
| `SteamNativePlatform` + cloud save hooks | ✅ Code complete | `unity/CityMajor.Unity/Assets/Scripts/Platform/` |
| Achievements catalog (12 × `CM_*`) | ✅ | `StreamingAssets/achievements-v1.json`; Editor: **CityMajor → Verify Achievements Catalog** |
| Headless Windows build script | ✅ | `./scripts/build-steam-unity.sh` + GHA `unity-steam-build.yml` |
| `app_build_template.vdf` | ✅ | `docs/steam/app_build_template.vdf` — placeholders only |
| SB-4176 Play checklist | 🟡 Human gate | [UNITY_PLAY_CHECKLIST.md](../design/UNITY_PLAY_CHECKLIST.md) |
| **Partner App ID** | ⛔ **Blocked** | `SteamAppConfig.AppId` and `steam_appid.txt` still **480** (Spacewar dev stub) |
| Steamworks.NET + `STEAMWORKS_NET` define | 🟡 Per machine | `./scripts/setup-steamworks-unity.sh` + Editor menu |
| First `steamcmd` depot upload | ⛔ **Blocked** | Requires real App ID + depot ID from partner site |

**Unblock depot upload:** create CityMajor app on [partner.steamgames.com](https://partner.steamgames.com), set `SteamAppConfig.AppId` + `steam_appid.txt` to `YOUR_APP_ID` (do not commit real ID), register 12 achievements (§3.4), then §4.

---

## 1. Prerequisites

| Item | Location | Notes |
|------|----------|-------|
| Steamworks partner access | [partner.steamgames.com](https://partner.steamgames.com) | App created; you have the **App ID** (use placeholder `YOUR_APP_ID` in docs/commits — never commit real IDs) |
| `SteamAppConfig.cs` | `unity/CityMajor.Unity/Assets/Scripts/Platform/SteamAppConfig.cs` | Set `AppId` to `YOUR_APP_ID` before ship build |
| `steam_appid.txt` | `unity/CityMajor.Unity/steam_appid.txt` | Single line: `YOUR_APP_ID`. Dev default is `480` (Spacewar) |
| `STEAMWORKS_NET` define | Unity scripting defines | Install plugin per [INSTALL_STEAMWORKS_NET.md](./INSTALL_STEAMWORKS_NET.md), then **CityMajor → Platform → Enable Steamworks.NET Define** |
| SimCore DLL | `Assets/Plugins/Forge/Forge.SimCore.dll` | Built by `./scripts/build-simcore-for-unity.sh` (also run inside build script) |
| `steamcmd` | Local or CI runner | [SteamCMD](https://developer.valvesoftware.com/wiki/SteamCMD) + logged-in depot account |

**Do not commit:** partner credentials, depot keys, real App IDs, or `steamcmd` login tokens.

---

## 2. Local build

From repo root:

```bash
# Once per machine
./scripts/setup-steamworks-unity.sh
# Unity: CityMajor → Platform → Enable Steamworks.NET Define

# Player build (requires UNITY_PATH or Unity Hub install)
export UNITY_PATH="/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity"
./scripts/build-steam-unity.sh
```

### Artifact paths

| Path | Contents |
|------|----------|
| `Build/Steam/windows/CityMajor.exe` | Windows x64 IL2CPP standalone player |
| `Build/Steam/windows/steam_appid.txt` | Copied from project root at build time |
| `Build/Steam/windows/unity-build.log` | Unity batch build log |
| `Build/Steam/windows/*_Data/` | Unity player data folder (required in depot) |

Editor fallback: **CityMajor → Build → Windows Steam Player** (same output directory).

Verify locally: launch **Steam client**, run `CityMajor.exe`, console shows `[CityMajor] Steam initialized (AppId=YOUR_APP_ID, user=...)`.

---

## 3. Steamworks partner site (before upload)

### 3.1 App + depots

1. **Steamworks → Apps & Packages → YOUR_APP_ID → Depots**
2. Create **Windows** depot → note **Depot ID** (`YOUR_WINDOWS_DEPOT_ID`, e.g. `YOUR_APP_ID` + `1`).
3. Set depot OS: **Windows**, architecture **64-bit**.
4. Under **Installation** → default launch option points at `CityMajor.exe`.

### 3.2 Branches

| Branch | Use |
|--------|-----|
| `default` | Public / EA release |
| `beta` | Internal QA before `setlive` |

Upload builds to `beta` first; promote to `default` only after smoke on a Steam-installed copy.

### 3.3 Steam Cloud

1. **Steamworks → YOUR_APP_ID → Steam Cloud**
2. Enable cloud for the app.
3. Add **Root override** or file rule for: **`citymajor.cmjr`**
   - Matches `SteamCloudSave.CloudFileName` / `CitySimBridge.DefaultSaveFileName`.
4. Set reasonable quota (CMJR saves are small; 1–4 MB per user is plenty for v1).

### 3.4 Achievements

Register **12** achievements in partner site. **API Name** must match `steamApiName` in `unity/CityMajor.Unity/Assets/StreamingAssets/achievements-v1.json`:

| API Name | Display name (from catalog) |
|----------|----------------------------|
| `CM_FIRST_ZONE` | First Stake |
| `CM_FIRST_ROAD` | Pavement |
| `CM_POP_1000` | Growing Town |
| `CM_POP_10000` | Metro Rising |
| `CM_BELOVED` | Beloved Mayor |
| `CM_HAPPY_CITY` | Smiles All Around |
| `CM_RESEARCH` | Bright Ideas |
| `CM_FIRST_LAW` | By Decree |
| `CM_SAVE` | City on Disk |
| `CM_FUNDS_100K` | Healthy Treasury |
| `CM_HOUSEHOLDS_50` | Neighborhoods |
| `CM_BUILDINGS_25` | Skyline |

Icons and localized strings are partner-site only; runtime unlock uses API names above.

---

## 4. Upload with steamcmd

### 4.1 Prepare VDF

Copy and edit the template (placeholders only in repo):

```bash
cp docs/steam/app_build_template.vdf Build/Steam/app_build.vdf
```

Replace in `app_build.vdf`:

- `"appid"` → `YOUR_APP_ID`
- Depot key under `"depots"` → `YOUR_WINDOWS_DEPOT_ID`
- `"contentroot"` → absolute or repo-relative path to `Build/Steam/windows/`
- `"buildoutput"` → e.g. `./Build/Steam/output/`
- `"setlive"` → `beta` for QA, or `default` when ready (empty = build only, no branch promotion)

See [app_build_template.vdf](./app_build_template.vdf) for structure.

### 4.2 Run upload

```bash
steamcmd +login YOUR_STEAM_ACCOUNT \
  +run_app_build "$(pwd)/Build/Steam/app_build.vdf" \
  +quit
```

On success, build output logs land under `Build/Steam/output/`. Confirm build ID in Steamworks **SteamPipe → Builds**.

### 4.3 Post-upload smoke

- [ ] Install via Steam client (beta branch if used)
- [ ] Game launches; Steam overlay works
- [ ] Save → `citymajor.cmjr` syncs (check Cloud status in save UI)
- [ ] Trigger one achievement (e.g. zone a tile → `CM_FIRST_ZONE`)
- [ ] Rich presence updates when population changes

---

## 5. CI note

Manual upload until SteamPipe is wired in CI. GitHub workflow `.github/workflows/unity-steam-build.yml` produces the same `Build/Steam/windows/` artifact when `UNITY_PATH` is set on a self-hosted runner; depot push remains a manual `steamcmd` step per this runbook.

---

## 6. Related docs

| Doc | Purpose |
|-----|---------|
| [INSTALL_STEAMWORKS_NET.md](./INSTALL_STEAMWORKS_NET.md) | Plugin install + `STEAMWORKS_NET` define |
| [app_build_template.vdf](./app_build_template.vdf) | steamcmd app build template |
| [UNITY_STEAM_SCOPE.md](../design/UNITY_STEAM_SCOPE.md) | Phase 3 scope + code touchpoints |
| [UNITY_PLAY_CHECKLIST.md](../design/UNITY_PLAY_CHECKLIST.md) | Gate before first depot |
