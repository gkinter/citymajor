# Install Steamworks.NET for CityMajor Unity

```bash
./scripts/setup-steamworks-unity.sh
```

Windows:

```bat
scripts\setup-steamworks-unity.bat
```

(or `powershell -ExecutionPolicy Bypass -File .\scripts\setup-steamworks-unity.ps1`)

Then in Unity Editor:

1. **CityMajor → Platform → Enable Steamworks.NET Define**
2. Copy `unity/CityMajor.Unity/steam_appid.txt` next to the built `.exe` (or keep in project root for Editor)
3. On `SteamBootstrap` component (auto-created at runtime), check **enableInEditor** to test in Play mode
4. Launch the **Steam client** before entering Play (Spacewar AppId 480)

## Verify

- Console: `[CityMajor] Steam initialized (AppId=480, user=...)`
- Register matching API names in Steamworks partner (prefix `CM_`). Catalog: `Assets/StreamingAssets/achievements-v1.json`.
- Rich presence updates when population changes
- Save writes local CMJR + `· cloud` status when Remote Storage enabled

## Ship checklist

- Replace AppId in `SteamAppConfig.cs` and `steam_appid.txt`
- Enable Steam Cloud for `citymajor.cmjr` in Steamworks partner site
- Windows x64 IL2CPP standalone build per `UNITY_STEAM_SCOPE.md`
