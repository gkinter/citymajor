#!/usr/bin/env bash
# Validate unity/CityMajor.Unity/Assets/StreamingAssets/achievements-v1.json
# Run from repo root: ./scripts/verify-achievements-catalog.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
CATALOG_JSON="$REPO_ROOT/unity/CityMajor.Unity/Assets/StreamingAssets/achievements-v1.json"
MIN_ENTRIES=10

if [[ ! -f "$CATALOG_JSON" ]]; then
  echo "ERROR: achievements catalog not found: $CATALOG_JSON" >&2
  exit 1
fi

if ! command -v python3 >/dev/null 2>&1; then
  echo "ERROR: python3 required for achievements catalog validation" >&2
  exit 1
fi

python3 - <<'PY' "$CATALOG_JSON" "$MIN_ENTRIES"
import json
import sys

path, min_entries = sys.argv[1], int(sys.argv[2])
errors = []

try:
    with open(path, encoding="utf-8") as f:
        data = json.load(f)
except json.JSONDecodeError as exc:
    print(f"ERROR: invalid JSON in {path}: {exc}", file=sys.stderr)
    sys.exit(1)

achievements = data
if isinstance(data, dict):
    achievements = data.get("achievements")

if not isinstance(achievements, list):
    errors.append("root must be a JSON array or an object with an 'achievements' array")
    achievements = []

if len(achievements) < min_entries:
    errors.append(f"expected at least {min_entries} entries, found {len(achievements)}")

seen_api_names = set()
for index, entry in enumerate(achievements):
    prefix = f"achievements[{index}]"

    if not isinstance(entry, dict):
        errors.append(f"{prefix}: must be an object")
        continue

    api_name = entry.get("steamApiName") or entry.get("apiName")
    display_name = entry.get("name") or entry.get("displayName")
    description = entry.get("description")

    if not api_name:
        errors.append(f"{prefix}: missing steamApiName (apiName)")
    elif not str(api_name).startswith("CM_"):
        errors.append(f"{prefix}: steamApiName must start with CM_ (got {api_name!r})")

    if not display_name:
        errors.append(f"{prefix}: missing name (displayName)")

    if not description:
        errors.append(f"{prefix}: missing description")

    if api_name:
        if api_name in seen_api_names:
            errors.append(f"{prefix}: duplicate steamApiName {api_name!r}")
        else:
            seen_api_names.add(api_name)

if errors:
    print("Achievements catalog verify: FAIL", file=sys.stderr)
    for err in errors:
        print(f"  - {err}", file=sys.stderr)
    sys.exit(1)

print(f"OK: achievements catalog verify passed ({len(achievements)} entries, no duplicate steamApiName)")
PY
