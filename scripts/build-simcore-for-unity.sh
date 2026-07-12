#!/usr/bin/env bash
# Build Forge.SimCore for Unity (netstandard2.1) and copy into Assets/Plugins.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT="$REPO_ROOT/src/Forge.SimCore/Forge.SimCore.csproj"
OUT_DIR="$REPO_ROOT/src/Forge.SimCore/bin/Release/netstandard2.1"
UNITY_PLUGINS="$REPO_ROOT/unity/CityMajor.Unity/Assets/Plugins/Forge"

echo "Building Forge.SimCore (netstandard2.1)..."
dotnet build "$PROJECT" -c Release -f netstandard2.1

mkdir -p "$UNITY_PLUGINS"
cp "$OUT_DIR/Forge.SimCore.dll" "$UNITY_PLUGINS/Forge.SimCore.dll"
echo "Copied to $UNITY_PLUGINS/Forge.SimCore.dll"
