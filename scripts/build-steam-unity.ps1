# Unity headless Windows Steam player build (SB-4181).
# Output: <repo>/Build/Steam/windows/CityMajor.exe
#
# Usage (from repo root):
#   powershell -ExecutionPolicy Bypass -File .\scripts\build-steam-unity.ps1
# Or: .\scripts\build-steam-unity.bat
#
# Optional env:
#   UNITY_PATH  — full path to Unity.exe (overrides Hub discovery)
#   UNITY_VERSION — Hub Editor folder name filter, e.g. 6000.0.58f2
#
# Dev Steam App ID remains Spacewar 480 (steam_appid.txt). No secrets required.

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$UnityProject = Join-Path $RepoRoot "unity\CityMajor.Unity"
$OutDir = Join-Path $RepoRoot "Build\Steam\windows"
$LogFile = Join-Path $OutDir "unity-build.log"
$ExeOut = Join-Path $OutDir "CityMajor.exe"

Write-Host "[build-steam-unity] SimCore DLL..."
try {
    & (Join-Path $PSScriptRoot "build-simcore-for-unity.ps1")
} catch {
    Write-Host $_
    exit 1
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function Find-UnityEditor {
    if ($env:UNITY_PATH -and (Test-Path -LiteralPath $env:UNITY_PATH)) {
        return (Resolve-Path -LiteralPath $env:UNITY_PATH).Path
    }

    $hubRoots = @(
        (Join-Path ${env:ProgramFiles} "Unity\Hub\Editor"),
        (Join-Path ${env:ProgramFiles(x86)} "Unity\Hub\Editor"),
        (Join-Path $env:LOCALAPPDATA "Programs\Unity\Hub\Editor")
    ) | Where-Object { $_ -and (Test-Path $_) }

    $candidates = @()
    foreach ($root in $hubRoots) {
        $pattern = if ($env:UNITY_VERSION) { $env:UNITY_VERSION } else { "*" }
        Get-ChildItem -Path $root -Directory -Filter $pattern -ErrorAction SilentlyContinue |
            ForEach-Object {
                $exe = Join-Path $_.FullName "Editor\Unity.exe"
                if (Test-Path -LiteralPath $exe) { $candidates += $exe }
            }
    }

    if (-not $candidates) { return $null }
    return ($candidates | Sort-Object -Descending | Select-Object -First 1)
}

$UnityBin = Find-UnityEditor
if (-not $UnityBin) {
    Write-Host @"
[build-steam-unity] UNITY_PATH not set and Unity.exe not found under Unity Hub.
  set UNITY_PATH=C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe
  Or build from Editor: CityMajor → Build → Windows Steam Player

Output target: $ExeOut
"@
    exit 1
}

Write-Host "[build-steam-unity] Building with $UnityBin"
$unityArgs = @(
    "-batchmode",
    "-nographics",
    "-quit",
    "-projectPath", $UnityProject,
    "-executeMethod", "CityMajor.Editor.CityMajorSteamBuild.BuildWindowsCi",
    "-logFile", $LogFile
)
$proc = Start-Process -FilePath $UnityBin -ArgumentList $unityArgs -Wait -PassThru
if ($proc.ExitCode -ne 0) {
    Write-Host "[build-steam-unity] Unity exited $($proc.ExitCode) — see $LogFile"
    if (Test-Path $LogFile) { Get-Content $LogFile -Tail 40 }
    exit $proc.ExitCode
}

if (Test-Path -LiteralPath $ExeOut) {
    Write-Host "[build-steam-unity] OK → $ExeOut"
    Write-Host "[build-steam-unity] Log: $LogFile"
    exit 0
}

Write-Host "[build-steam-unity] FAILED — missing $ExeOut (see $LogFile)"
if (Test-Path $LogFile) { Get-Content $LogFile -Tail 40 }
exit 1
