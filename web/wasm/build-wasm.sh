#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

# Beast sets NODE_OPTIONS with --max-semi-space-size; emscripten bundled node rejects it.
unset NODE_OPTIONS

dotnet workload restore src/Forge.SimWasm/Forge.SimWasm.csproj

if ! dotnet publish src/Forge.SimWasm/Forge.SimWasm.csproj -c Release -r browser-wasm --self-contained 2>/dev/null; then
  echo "dotnet publish failed (MSB1008 on .NET 10 SDK) — falling back to msbuild Publish"
  dotnet msbuild src/Forge.SimWasm/Forge.SimWasm.csproj \
    -t:Publish \
    -p:Configuration=Release \
    -p:RuntimeIdentifier=browser-wasm \
    -p:SelfContained=true
fi

APP_BUNDLE="src/Forge.SimWasm/bin/Release/net8.0/browser-wasm/AppBundle"
if [[ ! -d "$APP_BUNDLE/_framework" ]]; then
  echo "Missing AppBundle at $APP_BUNDLE — build failed" >&2
  exit 1
fi

rm -rf web/public/dotnet
mkdir -p web/public/dotnet
cp -R "$APP_BUNDLE"/* web/public/dotnet/
echo "WASM bundle -> web/public/dotnet/ (_framework/blazor.boot.json included)"
