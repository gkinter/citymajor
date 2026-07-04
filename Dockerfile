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
WORKDIR /src
COPY src/ ./src/
COPY base/ ./base/
COPY web/wasm/build-wasm.sh ./web/wasm/build-wasm.sh
RUN mkdir -p web/public/dotnet
ENV NODE_OPTIONS=
RUN if [ "$BUILD_WASM" = "1" ]; then \
      bash web/wasm/build-wasm.sh \
        || { echo "WARN: WASM build failed — runtime will use procedural fallback"; mkdir -p web/public/dotnet; }; \
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
ENV NEXT_TELEMETRY_DISABLED=1
ENV NODE_ENV=production
COPY --from=deps /app/node_modules ./node_modules
COPY --from=deps /app/web/node_modules ./web/node_modules
COPY --from=deps /app/web/packages/sim-types/node_modules ./web/packages/sim-types/node_modules
COPY . .
COPY --from=wasm /src/web/public/dotnet ./web/public/dotnet
RUN --mount=type=cache,target=/app/web/.next/cache,sharing=locked \
    pnpm --filter @citymajor/web... build

# ---------- runner: minimal standalone runtime ----------
FROM node:20-slim AS runner
WORKDIR /app
ENV NODE_ENV=production
ENV HOSTNAME=0.0.0.0
ENV PORT=3000
ENV NEXT_TELEMETRY_DISABLED=1

RUN addgroup --system --gid 1001 nodejs \
 && adduser --system --uid 1001 --ingroup nodejs nextjs

COPY --from=builder --chown=nextjs:nodejs /app/web/.next/standalone ./
COPY --from=builder --chown=nextjs:nodejs /app/web/.next/static ./web/.next/static
COPY --from=builder --chown=nextjs:nodejs /app/web/public ./web/public

USER nextjs
EXPOSE 3000

CMD ["node", "web/server.js"]
