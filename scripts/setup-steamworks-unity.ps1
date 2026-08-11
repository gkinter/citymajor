# Install Steamworks.NET into Unity Plugins (SB-4180).
# Usage (from repo root):
#   powershell -ExecutionPolicy Bypass -File .\scripts\setup-steamworks-unity.ps1
# Or: .\scripts\setup-steamworks-unity.bat
#
# Optional: $env:STEAMWORKS_NET_TAG = "20.2.0"  (default)
# Dev App ID: Spacewar 480 — no partner secrets.

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$UnityRoot = Join-Path $RepoRoot "unity\CityMajor.Unity"
$Dest = Join-Path $UnityRoot "Assets\Plugins\Steamworks.NET"
$Tag = if ($env:STEAMWORKS_NET_TAG) { $env:STEAMWORKS_NET_TAG } else { "20.2.0" }
$Tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("cm-steamworks-" + [guid]::NewGuid().ToString("N"))

try {
    Write-Host "[setup-steamworks] Cloning Steamworks.NET $Tag..."
    New-Item -ItemType Directory -Force -Path $Tmp | Out-Null
    git clone --depth 1 --branch $Tag https://github.com/rlabrecque/Steamworks.NET.git (Join-Path $Tmp "src")
    if ($LASTEXITCODE -ne 0) {
        throw "[setup-steamworks] git clone failed with exit $LASTEXITCODE"
    }

    $SrcPlugins = Join-Path $Tmp "src\Plugins"
    if (-not (Test-Path $SrcPlugins)) {
        throw "[setup-steamworks] ERROR: Plugins/ missing in clone"
    }

    $DestPlugins = Join-Path $Dest "Plugins"
    if (Test-Path $DestPlugins) {
        Remove-Item -Recurse -Force $DestPlugins
    }
    New-Item -ItemType Directory -Force -Path $Dest | Out-Null
    Copy-Item -Recurse -Force $SrcPlugins $DestPlugins

    Write-Host "[setup-steamworks] Installed to $Dest"
    Write-Host "[setup-steamworks] Next steps:"
    Write-Host "  1. Unity → CityMajor → Platform → Enable Steamworks.NET Define"
    Write-Host "  2. Ensure steam_appid.txt exists at $UnityRoot\steam_appid.txt (Spacewar 480 for dev)"
    Write-Host "  3. Launch Steam client before Play in Editor (enable SteamBootstrap.enableInEditor)"
}
finally {
    if (Test-Path $Tmp) { Remove-Item -Recurse -Force $Tmp -ErrorAction SilentlyContinue }
}
