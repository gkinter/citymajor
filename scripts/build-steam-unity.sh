#!/usr/bin/env bash
# Phase 3 placeholder — Unity batch build for Steam Windows depot (SB-4181).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY_PROJECT="$ROOT/unity/CityMajor.Unity"
OUT="$ROOT/Build/Steam/windows"

echo "[build-steam-unity] SimCore DLL must be fresh:"
"$ROOT/scripts/build-simcore-for-unity.sh"

mkdir -p "$OUT"

cat <<EOF
[build-steam-unity] Next steps (manual until CI):
  1. Open Unity $UNITY_PROJECT
  2. File → Build Settings → Windows x64, IL2CPP, Development off
  3. Output to: $OUT/CityMajor.exe
  4. Run steamcmd with app build VDF (see docs/design/UNITY_STEAM_SCOPE.md)

Steam bootstrap: Assets/Scripts/Platform/SteamBootstrap.cs (stub)
EOF
