#!/usr/bin/env bash
# Unity headless Windows Steam player build (SB-4181).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_PROJECT="$ROOT/unity/CityMajor.Unity"
OUT="$ROOT/Build/Steam/windows"
LOG="$OUT/unity-build.log"

echo "[build-steam-unity] SimCore DLL..."
"$ROOT/scripts/build-simcore-for-unity.sh"

mkdir -p "$OUT"

UNITY_BIN="${UNITY_PATH:-}"
if [[ -z "$UNITY_BIN" ]]; then
  if [[ "$(uname -s)" == "Darwin" ]]; then
    UNITY_BIN="$(ls -d /Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity 2>/dev/null | sort -V | tail -1 || true)"
  else
    UNITY_BIN="$(command -v Unity || command -v unity || true)"
  fi
fi

if [[ -z "$UNITY_BIN" || ! -x "$UNITY_BIN" ]]; then
  cat <<EOF
[build-steam-unity] UNITY_PATH not set and Unity binary not found.
  export UNITY_PATH="/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity"
  Or build from Editor: CityMajor → Build → Windows Steam Player

Output target: $OUT/CityMajor.exe
EOF
  exit 1
fi

echo "[build-steam-unity] Building with $UNITY_BIN"
"$UNITY_BIN" \
  -batchmode \
  -nographics \
  -quit \
  -projectPath "$UNITY_PROJECT" \
  -executeMethod CityMajor.Editor.CityMajorSteamBuild.BuildWindowsCi \
  -logFile "$LOG"

if [[ -f "$OUT/CityMajor.exe" ]]; then
  echo "[build-steam-unity] OK → $OUT/CityMajor.exe"
  echo "[build-steam-unity] Log: $LOG"
else
  echo "[build-steam-unity] FAILED — see $LOG"
  tail -n 40 "$LOG" 2>/dev/null || true
  exit 1
fi
