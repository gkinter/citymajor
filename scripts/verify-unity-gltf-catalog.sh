#!/usr/bin/env bash
# Assert Unity modern GLTF symlink + all GltfCatalog.ShippedKeys have on-disk .glb files.
# Run from repo root: ./scripts/verify-unity-gltf-catalog.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
GLTF_MODERN_LINK="$REPO_ROOT/unity/CityMajor.Unity/Assets/Art/Gltf/modern"
CATALOG_CS="$REPO_ROOT/unity/CityMajor.Unity/Assets/Scripts/Rendering/GltfCatalog.cs"
UNLOCKS_JSON="$REPO_ROOT/base/data/tech/unity-modern-unlocks.json"
EXPECTED_KEY_COUNT=12

read_shipped_keys() {
  if [[ -f "$CATALOG_CS" ]]; then
    awk '
      /ShippedKeys = new\[\]/ { in_array = 1; next }
      in_array && /^[[:space:]]*\};/ { exit }
      in_array && /^[[:space:]]*"[a-z0-9_]+",[[:space:]]*$/ {
        gsub(/^[[:space:]]*"/, "")
        gsub(/",?[[:space:]]*$/, "")
        print
      }
    ' "$CATALOG_CS"
    return
  fi

  if [[ -f "$UNLOCKS_JSON" ]] && command -v python3 >/dev/null 2>&1; then
    python3 - <<'PY' "$UNLOCKS_JSON"
import json, sys
with open(sys.argv[1], encoding="utf-8") as f:
    data = json.load(f)
for entry in data.get("shippedGltfCatalog", []):
    print(entry["archetypeKey"])
PY
    return
  fi

  echo "ERROR: cannot locate ShippedKeys in $CATALOG_CS or $UNLOCKS_JSON" >&2
  exit 1
}

keys_file="$(mktemp)"
trap 'rm -f "$keys_file"' EXIT
read_shipped_keys > "$keys_file"
key_count="$(wc -l < "$keys_file" | tr -d '[:space:]')"

if [[ "$key_count" != "$EXPECTED_KEY_COUNT" ]]; then
  echo "ERROR: expected $EXPECTED_KEY_COUNT ShippedKeys, parsed $key_count"
  exit 1
fi

if [[ ! -L "$GLTF_MODERN_LINK" ]]; then
  echo "ERROR: $GLTF_MODERN_LINK is not a symlink (run ./scripts/setup-unity.sh)"
  exit 1
fi

if [[ ! -d "$GLTF_MODERN_LINK" ]]; then
  echo "ERROR: $GLTF_MODERN_LINK symlink target is missing or broken"
  exit 1
fi

missing=0
while IFS= read -r key; do
  [[ -z "$key" ]] && continue
  glb="$GLTF_MODERN_LINK/${key}.glb"
  if [[ ! -f "$glb" ]]; then
    echo "ERROR: missing GLB for key '$key' (expected $glb)"
    missing=$((missing + 1))
  fi
done < "$keys_file"

if ((missing > 0)); then
  echo "GLTF catalog verify: FAIL ($missing missing of $key_count)"
  exit 1
fi

echo "OK: GLTF catalog verify passed ($key_count GLBs, modern symlink OK)"
