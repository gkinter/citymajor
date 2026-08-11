#!/usr/bin/env bash
# U3.6 — CLI Play gate batch checks (no Unity Editor required).
# Run from repo root: ./scripts/verify-unity-play-gate.sh
# Exit 0 = blockers clear. Warnings print but do not fail (unless STRICT=1).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
UNITY_ROOT="$REPO_ROOT/unity/CityMajor.Unity"
SIMCORE_DLL="$UNITY_ROOT/Assets/Plugins/Forge/Forge.SimCore.dll"
PLAY_SCENE="$UNITY_ROOT/Assets/Scenes/Play.unity"
BUILD_SETTINGS="$UNITY_ROOT/ProjectSettings/EditorBuildSettings.asset"
BOOTSTRAP_GUID="6a757abf4f71b45e090c461e84794a68"
STRICT="${STRICT:-0}"

blockers=0
warnings=0

say_ok() { echo "OK: $*"; }
say_block() { echo "BLOCK: $*" >&2; blockers=$((blockers + 1)); }
say_warn() { echo "WARN: $*" >&2; warnings=$((warnings + 1)); }

echo "== CityMajor Unity Play gate (U3.6) =="

if [[ -f "$SIMCORE_DLL" ]]; then
  size_kb=$(( $(wc -c < "$SIMCORE_DLL") / 1024 ))
  say_ok "Forge.SimCore.dll present (${size_kb} KB)"
else
  say_block "Forge.SimCore.dll missing — run ./scripts/build-simcore-for-unity.sh"
fi

if [[ -f "$PLAY_SCENE" ]]; then
  if grep -q "$BOOTSTRAP_GUID" "$PLAY_SCENE"; then
    say_ok "Play.unity contains CityMajorBootstrap (guid $BOOTSTRAP_GUID)"
  else
    say_block "Play.unity missing CityMajorBootstrap — open Editor → CityMajor → Setup Play Scene"
  fi
else
  say_block "Play.unity missing at $PLAY_SCENE"
fi

if [[ -f "$BUILD_SETTINGS" ]]; then
  if grep -q "path: Assets/Scenes/Play.unity" "$BUILD_SETTINGS"; then
    # enabled: 1 should appear on the prior line for that entry
    if awk '
      /path: Assets\/Scenes\/Play\.unity/ { if (prev ~ /enabled: 1/) ok=1 }
      { prev=$0 }
      END { exit ok ? 0 : 1 }
    ' "$BUILD_SETTINGS"; then
      say_ok "EditorBuildSettings lists Play.unity (enabled)"
    else
      say_block "Play.unity in build settings but disabled (or malformed entry)"
    fi
  else
    say_block "Play.unity missing from EditorBuildSettings.asset"
  fi
else
  say_block "EditorBuildSettings.asset missing"
fi

# Cathedral Sprint 3 surface scripts (file presence — compile checked in Editor)
for rel in \
  "Assets/Scripts/UI/CathedralMetricsHudController.cs" \
  "Assets/Scripts/UI/EconomyPanelController.cs" \
  "Assets/Scripts/Rendering/FrictionCorridorOverlay.cs" \
  "Assets/Scripts/Rendering/EdgeTrafficOverlay.cs" \
  "Assets/Scripts/UI/RoadTypeToolbarController.cs" \
  "Assets/Scripts/UI/ToolModeHudController.cs" \
  "Assets/Scripts/UI/ZoningToolbarController.cs" \
  "Assets/UI/RoadTypeToolbar.uxml" \
  "Assets/UI/ToolModeHud.uxml" \
  "Assets/UI/ZoningToolbar.uxml"
do
  if [[ -f "$UNITY_ROOT/$rel" ]]; then
    say_ok "$rel"
  else
    say_block "missing Cathedral surface: $rel"
  fi
done

echo "-- nested: GLTF catalog --"
if ! "$SCRIPT_DIR/verify-unity-gltf-catalog.sh"; then
  say_warn "GLTF catalog verify failed (buildings may fall back to box LOD)"
fi

echo "-- nested: achievements catalog --"
if ! "$SCRIPT_DIR/verify-achievements-catalog.sh"; then
  say_warn "Achievements catalog verify failed"
fi

echo "== summary: $blockers blocker(s), $warnings warning(s) =="

if (( blockers > 0 )); then
  echo "FAIL: fix blockers before SB-4176 Play / player build" >&2
  exit 1
fi

if (( warnings > 0 )) && [[ "$STRICT" == "1" ]]; then
  echo "FAIL: STRICT=1 and warnings present" >&2
  exit 1
fi

echo "PASS: Play gate CLI blockers clear (human SB-4176 still required)"
exit 0
