#!/usr/bin/env bash
# Install Steamworks.NET into Unity Plugins (SB-4180).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
UNITY_ROOT="$REPO_ROOT/unity/CityMajor.Unity"
DEST="$UNITY_ROOT/Assets/Plugins/Steamworks.NET"
TAG="${STEAMWORKS_NET_TAG:-20.2.0}"
TMP="$(mktemp -d)"

cleanup() { rm -rf "$TMP"; }
trap cleanup EXIT

echo "[setup-steamworks] Cloning Steamworks.NET ${TAG}..."
git clone --depth 1 --branch "${TAG}" https://github.com/rlabrecque/Steamworks.NET.git "$TMP/src"

mkdir -p "$DEST"
rsync -a --delete \
  "$TMP/src/Plugins/" \
  "$DEST/Plugins/"

echo "[setup-steamworks] Installed to $DEST"
echo "[setup-steamworks] Next steps:"
echo "  1. Unity → CityMajor → Platform → Enable Steamworks.NET Define"
echo "  2. Ensure steam_appid.txt exists at $UNITY_ROOT/steam_appid.txt (Spacewar 480 for dev)"
echo "  3. Launch Steam client before Play in Editor (enable SteamBootstrap.enableInEditor)"
