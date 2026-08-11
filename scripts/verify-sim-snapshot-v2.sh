#!/usr/bin/env bash
# P7.4 — SIM_SNAPSHOT_V2.md §4 vs tip export surfaces (E/W/U).
# Run from repo root: ./scripts/verify-sim-snapshot-v2.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

if ! command -v python3 >/dev/null 2>&1; then
  echo "ERROR: python3 required for SIM_SNAPSHOT_V2 gap check" >&2
  exit 1
fi

exec python3 "$REPO_ROOT/scripts/verify-sim-snapshot-v2.py" "$@"
