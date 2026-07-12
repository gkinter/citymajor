#!/usr/bin/env bash
# CityMajor Unity pivot — bootstrap shared assets + MCP prerequisites.
# Run from repo root: ./scripts/setup-unity.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY_PROJECT="$REPO_ROOT/unity/CityMajor.Unity"
UNITY_ASSETS="$UNITY_PROJECT/Assets"
GLTF_MODERN_LINK="$UNITY_ASSETS/Art/Gltf/modern"
DATA_LINK="$UNITY_ASSETS/Data"
GLTF_MODERN_TARGET="$REPO_ROOT/web/public/assets/gltf/modern"
DATA_TARGET="$REPO_ROOT/base/data"

info()  { printf '\033[1;34m→\033[0m %s\n' "$*"; }
ok()    { printf '\033[1;32m✓\033[0m %s\n' "$*"; }
warn()  { printf '\033[1;33m!\033[0m %s\n' "$*"; }
fail()  { printf '\033[1;31m✗\033[0m %s\n' "$*" >&2; exit 1; }

link_shared() {
  local target="$1" link="$2"
  mkdir -p "$(dirname "$link")"
  if [[ -L "$link" ]]; then
    rm -f "$link"
  elif [[ -e "$link" ]]; then
    warn "Skipping $link — exists and is not a symlink (remove manually if needed)"
    return 0
  fi
  # Relative target for portability (matches Assets/… layout in repo)
  local rel
  rel="$(python3 -c "import os,sys; print(os.path.relpath(sys.argv[1], sys.argv[2]))" "$target" "$(dirname "$link")")"
  ln -sfn "$rel" "$link"
  ok "Linked $link → $rel ($target)"
}

info "CityMajor Unity setup (repo: $REPO_ROOT)"

# --- uv / uvx (Unity MCP stdio server) ---
if command -v uvx >/dev/null 2>&1; then
  ok "uvx found: $(command -v uvx)"
elif command -v uv >/dev/null 2>&1; then
  ok "uv found: $(command -v uv) (uvx should be available)"
else
  warn "uv/uvx not found — required for Unity MCP in Cursor"
  echo "  Install: curl -LsSf https://astral.sh/uv/install.sh | sh"
  echo "  Docs:    https://docs.astral.sh/uv/"
fi

if command -v uvx >/dev/null 2>&1; then
  info "Verifying mcp-for-unity via uvx…"
  if uvx --from mcpforunityserver mcp-for-unity --help >/dev/null 2>&1; then
    ok "mcp-for-unity responds to --help"
  else
    warn "uvx mcp-for-unity --help failed (network or package issue)"
    echo "  Retry: uvx --from mcpforunityserver mcp-for-unity --help"
  fi
fi

# --- Unity Hub / Editor ---
UNITY_HUB="/Applications/Unity Hub.app"
UNITY_EDITORS=()
if [[ -d "/Applications/Unity/Hub/Editor" ]]; then
  while IFS= read -r d; do
    UNITY_EDITORS+=("$d")
  done < <(find "/Applications/Unity/Hub/Editor" -maxdepth 1 -mindepth 1 -type d 2>/dev/null | sort)
fi

if [[ -d "$UNITY_HUB" ]]; then
  ok "Unity Hub installed"
else
  warn "Unity Hub not found at $UNITY_HUB"
  echo "  Download: https://unity.com/download"
fi

if ((${#UNITY_EDITORS[@]} > 0)); then
  ok "Unity Editor(s): ${UNITY_EDITORS[*]##*/}"
else
  warn "No Unity Editor under /Applications/Unity/Hub/Editor"
  echo "  Install Unity 6 (6000.x) via Hub — see docs/UNITY_MCP_SETUP.md"
  echo "  Download Hub: https://unity.com/download"
fi

# --- Shared asset symlinks ---
[[ -d "$GLTF_MODERN_TARGET" ]] || fail "Missing GLTF modern era: $GLTF_MODERN_TARGET"
[[ -d "$DATA_TARGET" ]] || fail "Missing base data: $DATA_TARGET"

info "Linking shared assets into Unity project…"
link_shared "$GLTF_MODERN_TARGET" "$GLTF_MODERN_LINK"
link_shared "$DATA_TARGET" "$DATA_LINK"

echo ""
ok "Unity project scaffold ready at $UNITY_PROJECT"
echo ""
echo "Next steps:"
echo "  1. Unity Hub → Add → $UNITY_PROJECT"
echo "  2. Open project (Unity 6 / 6000.x); wait for Package Manager (URP + unity-mcp)"
echo "  3. Menu: Window → MCP for Unity → Configure All Detected Clients"
echo "  4. Restart Cursor so .cursor/mcp.json loads the unity-mcp server"
echo "  5. Optional: CityMajor → Link Shared Assets (re-run symlinks from Editor)"
echo ""
echo "Docs: docs/UNITY_MCP_SETUP.md · unity/README.md"
