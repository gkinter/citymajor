@echo off
REM Build Forge.SimCore for Unity (netstandard2.1) → Assets/Plugins/Forge
REM Usage: scripts\build-simcore-for-unity.bat
setlocal EnableExtensions
cd /d "%~dp0.."

where dotnet >nul 2>&1
if errorlevel 1 (
  echo [build-simcore] ERROR: dotnet SDK not found on PATH.
  exit /b 1
)

echo [build-simcore] Building Forge.SimCore ^(netstandard2.1^)...
dotnet build "src\Forge.SimCore\Forge.SimCore.csproj" -c Release -f netstandard2.1
if errorlevel 1 exit /b 1

if not exist "unity\CityMajor.Unity\Assets\Plugins\Forge" mkdir "unity\CityMajor.Unity\Assets\Plugins\Forge"
copy /Y "src\Forge.SimCore\bin\Release\netstandard2.1\Forge.SimCore.dll" "unity\CityMajor.Unity\Assets\Plugins\Forge\Forge.SimCore.dll" >nul
if errorlevel 1 (
  echo [build-simcore] ERROR: failed to copy Forge.SimCore.dll
  exit /b 1
)

echo [build-simcore] Copied → unity\CityMajor.Unity\Assets\Plugins\Forge\Forge.SimCore.dll
exit /b 0
