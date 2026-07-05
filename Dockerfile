# syntax=docker/dockerfile:1.6
# CityMajor web — pnpm monorepo + optional WASM sim bundle
# Builds @citymajor/web (Next.js 16 standalone) with workspace @citymajor/sim-types

FROM node:20-slim AS base
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates && rm -rf /var/lib/apt/lists/*
RUN corepack enable && corepack prepare pnpm@10.12.1 --activate
WORKDIR /app

# ---------- wasm: optional .NET browser-wasm publish → web/public/dotnet/ ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS wasm
ARG BUILD_WASM=1
# browser-wasm / emscripten tooling expects `python` on PATH
RUN apt-get update \
 && apt-get install -y --no-install-recommends python3 \
 && ln -sf /usr/bin/python3 /usr/bin/python \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /src
# pnpm build:wasm parity: monorepo root + wasm script (same entry as beast/pnpm build:wasm)
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
COPY web/package.json ./web/
COPY src/ ./src/
COPY base/ ./base/
COPY web/wasm/ ./web/wasm/
COPY --from=base /usr/local/bin/node /usr/local/bin/node
COPY --from=base /usr/local/lib/node_modules /usr/local/lib/node_modules
COPY --from=base /usr/local/bin/corepack /usr/local/bin/corepack
RUN corepack enable && corepack prepare pnpm@10.12.1 --activate
RUN mkdir -p web/public/dotnet
ENV NODE_OPTIONS=
RUN if [ "$BUILD_WASM" = "1" ]; then \
      pnpm build:wasm \
        && test -f web/public/dotnet/_framework/blazor.boot.json \
        || { echo "ERROR: BUILD_WASM=1 but web/public/dotnet/_framework/blazor.boot.json missing" >&2; exit 1; }; \
    else \
      echo "BUILD_WASM=0 — skipping WASM; procedural fallback at runtime"; \
    fi

# ---------- deps: install workspace deps ----------
FROM base AS deps
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
COPY web/package.json ./web/
COPY web/packages/sim-types/package.json ./web/packages/sim-types/
RUN --mount=type=cache,target=/root/.local/share/pnpm/store \
    pnpm install --frozen-lockfile

# ---------- builder: Next.js production build ----------
FROM base AS builder
ARG NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY=
ENV NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY=$NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY
ENV NEXT_TELEMETRY_DISABLED=1
ENV NODE_ENV=production
# Git LFS: GLBs under web/public/assets/gltf/ are LFS-tracked (.gitattributes).
# Build contexts checked out without LFS (e.g. Coolify clones) hold ~130-byte
# pointer stubs instead of real meshes. Install git-lfs so the builder can
# fetch+smudge the real binaries; repo is public → no auth needed.
RUN apt-get update \
 && apt-get install -y --no-install-recommends git-lfs \
 && git lfs install --skip-smudge \
 && rm -rf /var/lib/apt/lists/*
COPY --from=deps /app/node_modules ./node_modules
COPY --from=deps /app/web/node_modules ./web/node_modules
COPY --from=deps /app/web/packages/sim-types/node_modules ./web/packages/sim-types/node_modules
COPY . .
# Fetch LFS objects and smudge GLBs in place. No-op when the context already
# has real binaries (CI checks out with lfs:true). Skipped when .git is absent
# or a worktree file — the stub check below is the real gate. Requires .git
# in context (see .dockerignore) for Coolify clones that don't fetch LFS.
RUN if [ -d .git ]; then git lfs pull; \
    else echo "No .git directory in context — skipping lfs pull, relying on stub check"; fi
# Fail fast if any GLB is still an LFS pointer stub — prevents shipping an
# image that passes file-exists checks but fails at R3F load time.
RUN stub="$(find web/public/assets/gltf -name '*.glb' -print0 \
           | xargs -0 grep -Il 'git-lfs' 2>/dev/null || true)"; \
    if [ -n "$stub" ]; then \
      echo "ERROR: LFS pointer stubs detected — git lfs pull did not smudge:" >&2; \
      printf '%s\n' "$stub" >&2; \
      exit 1; \
    fi
COPY --from=wasm /src/web/public/dotnet ./web/public/dotnet
# wasm stage already published; builder image has no .NET SDK
ARG BUILD_WASM=1
ENV SKIP_WASM_BUILD=1
RUN if [ "$BUILD_WASM" = "1" ]; then \
      test -f web/public/dotnet/_framework/blazor.boot.json \
        || { echo "ERROR: WASM bundle not copied into builder stage" >&2; exit 1; }; \
    fi
RUN --mount=type=cache,target=/app/web/.next/cache,sharing=locked \
    pnpm --filter @citymajor/web... build

# ---------- runner: minimal standalone runtime ----------
FROM node:20-slim AS runner
WORKDIR /app
ENV NODE_ENV=production
ENV HOSTNAME=0.0.0.0
ENV PORT=3000
ENV NEXT_TELEMETRY_DISABLED=1

RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/* \
 && addgroup --system --gid 1001 nodejs \
 && adduser --system --uid 1001 --ingroup nodejs nextjs

COPY --from=builder --chown=nextjs:nodejs /app/web/.next/standalone ./
COPY --from=builder --chown=nextjs:nodejs /app/web/.next/static ./web/.next/static
COPY --from=builder --chown=nextjs:nodejs /app/web/public ./web/public

USER nextjs
EXPOSE 3000

CMD ["node", "web/server.js"]
