# Build Forge.SimCore for Unity (netstandard2.1) and copy into Assets/Plugins.
# Usage (from repo root):
#   powershell -ExecutionPolicy Bypass -File .\scripts\build-simcore-for-unity.ps1
# Or: .\scripts\build-simcore-for-unity.bat

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $RepoRoot "src\Forge.SimCore\Forge.SimCore.csproj"
$OutDir = Join-Path $RepoRoot "src\Forge.SimCore\bin\Release\netstandard2.1"
$UnityPlugins = Join-Path $RepoRoot "unity\CityMajor.Unity\Assets\Plugins\Forge"

Write-Host "[build-simcore] Building Forge.SimCore (netstandard2.1)..."
dotnet build $Project -c Release -f netstandard2.1
if ($LASTEXITCODE -ne 0) {
    throw "[build-simcore] dotnet build failed with exit $LASTEXITCODE"
}

New-Item -ItemType Directory -Force -Path $UnityPlugins | Out-Null
$DllSrc = Join-Path $OutDir "Forge.SimCore.dll"
if (-not (Test-Path -LiteralPath $DllSrc)) {
    throw "[build-simcore] missing output DLL: $DllSrc"
}
$DllDst = Join-Path $UnityPlugins "Forge.SimCore.dll"
Copy-Item -Force $DllSrc $DllDst
Write-Host "[build-simcore] Copied → $DllDst"
