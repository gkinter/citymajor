@echo off
REM Unity headless Windows Steam player → Build\Steam\windows\CityMajor.exe
REM Usage: scripts\build-steam-unity.bat
REM Optional: set UNITY_PATH=C:\Program Files\Unity\Hub\Editor\<ver>\Editor\Unity.exe
REM Dev App ID: Spacewar 480 (steam_appid.txt). No secrets.
setlocal EnableExtensions EnableDelayedExpansion
cd /d "%~dp0.."

set "OUT=%CD%\Build\Steam\windows"
set "LOG=%OUT%\unity-build.log"
set "PROJECT=%CD%\unity\CityMajor.Unity"
set "EXE=%OUT%\CityMajor.exe"

echo [build-steam-unity] SimCore DLL...
call "%~dp0build-simcore-for-unity.bat"
if errorlevel 1 exit /b 1

if not exist "%OUT%" mkdir "%OUT%"

REM Resolve Unity.exe: UNITY_PATH env, else newest Hub Editor install.
set "UNITY_BIN=%UNITY_PATH%"
if defined UNITY_BIN if exist "%UNITY_BIN%" goto :have_unity

set "UNITY_BIN="
for %%R in (
  "%ProgramFiles%\Unity\Hub\Editor"
  "%ProgramFiles(x86)%\Unity\Hub\Editor"
  "%LOCALAPPDATA%\Programs\Unity\Hub\Editor"
) do (
  if exist %%~R (
    for /f "delims=" %%D in ('dir /b /ad /o-n "%%~R" 2^>nul') do (
      if not defined UNITY_BIN if exist "%%~R\%%D\Editor\Unity.exe" (
        set "UNITY_BIN=%%~R\%%D\Editor\Unity.exe"
      )
    )
  )
)

:have_unity
if not defined UNITY_BIN (
  echo [build-steam-unity] UNITY_PATH not set and Unity.exe not found under Unity Hub.
  echo   set UNITY_PATH=C:\Program Files\Unity\Hub\Editor\^<version^>\Editor\Unity.exe
  echo   Or build from Editor: CityMajor → Build → Windows Steam Player
  echo.
  echo Output target: %EXE%
  exit /b 1
)
if not exist "%UNITY_BIN%" (
  echo [build-steam-unity] UNITY_PATH does not exist: %UNITY_BIN%
  exit /b 1
)

echo [build-steam-unity] Building with %UNITY_BIN%
"%UNITY_BIN%" -batchmode -nographics -quit -projectPath "%PROJECT%" -executeMethod CityMajor.Editor.CityMajorSteamBuild.BuildWindowsCi -logFile "%LOG%"
set "RC=%ERRORLEVEL%"

if not exist "%EXE%" (
  echo [build-steam-unity] FAILED — missing %EXE% ^(exit %RC%^) — see %LOG%
  if exist "%LOG%" powershell -NoProfile -Command "Get-Content -LiteralPath '%LOG%' -Tail 40"
  exit /b 1
)

if not "%RC%"=="0" (
  echo [build-steam-unity] Unity exited %RC% but exe exists — check %LOG%
)

echo [build-steam-unity] OK → %EXE%
echo [build-steam-unity] Log: %LOG%
exit /b 0
