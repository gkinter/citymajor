# CityMajor Web — Coolify Deploy Runbook & Webhook Audit

> Audit date: **2026-07-05** · branch: `feat/wasm-r3f-integration-2026-07-04` · app: **citymajor-web**

This document records how `citymajor-web` is deployed on Coolify, how to verify deploy health, and the findings from the **848e45c / 8cd8691 webhook gap** investigation.

---

## App configuration

| Field | Value |
|---|---|
| Coolify app | `citymajor-web` |
| UUID | `w134tsftvj327kp96j45kcsb` |
| Public URL | https://citymajor.apps.softblaze.net |
| Git repo | `gkinter/citymajor` |
| Watched branch | `feat/wasm-r3f-integration-2026-07-04` |
| Build pack | `dockerfile` (`/Dockerfile`) |
| Build server | `vps-cpx62` (178.105.222.41) |
| Port | 3000 |
| Build env | `BUILD_WASM=1` (build-time) |
| GitHub source | Public GitHub app (`source_id: 0`) — **not** the `digitalsoftdistribution` private GitHub App |

Related app on the **same branch** (legacy / duplicate):

| App | UUID | FQDN | Status (2026-07-05) |
|---|---|---|---|
| `citymajor-wasm-r3f-integration` | `aj75nqg1lm6vxhjp93pp8c43` | `citymajor-feat-wasm-r3f-integration-2026-07-04.apps.softblaze.net` | `exited:unhealthy` |

Only **`citymajor-web`** should be treated as the canonical preview URL for integration work.

---

## Deploy path

CityMajor has **no live production domain** — all integration deploys go through Coolify on the preview AX41 fleet (`.apps.softblaze.net`). Pushes to `feat/wasm-r3f-integration-2026-07-04` should trigger an automatic Docker build + deploy via the Coolify GitHub webhook.

Expected flow:

```
git push origin feat/wasm-r3f-integration-2026-07-04
  → GitHub push webhook → Coolify
  → deployment queue entry (commit SHA recorded)
  → docker build on vps-cpx62 (Dockerfile, BUILD_WASM=1)
  → container swap on citymajor.apps.softblaze.net
```

---

## Verification commands

```bash
# One-screen app health + recent deploys
coolify doctor citymajor-web

# Deploy history with commit SHAs (preferred — avoids REST timeout)
# MCP: coolify-logs-coolify-deployments app=citymajor-web since_min=1440 status=all

# Failed build log tail
coolify lastfail citymajor-web

# Confirm which commit is live (HTTP headers / container env — after deploy finishes)
curl -sI https://citymajor.apps.softblaze.net | head -5
```

**Do not** poll `/api/v1/deployments` in a loop — use `coolify-logs` MCP or `coolify lastlog/lastfail`.

---

## Incident: 848e45c and 8cd8691 did not reach production

### Commits under investigation

| SHA | UTC push time | Message | Coolify queue entry? | Deploy result |
|---|---|---|---|---|
| `848e45c` | 2026-07-05 10:34:38 | fix(docker): restore WASM bundle in image when BUILD_WASM=1 | **No** | N/A — never queued |
| `8cd8691` | 2026-07-05 11:26:38 | fix(smoke): restore Era quest progress aria-label for Playwright | **Yes** (×3) | **Failed** (all 3) |

Last **successful** deploy before the gap: **`e7e4d0e`** (finished ~10:29 UTC, deploy `#8138`).

Follow-up fix (integration tip at audit time): **`19749e7`** — Dockerfile wasm-stage pnpm/corepack fix; deploy `#8148` was `in_progress` when this doc was written.

### Timeline (UTC, 2026-07-05)

```
10:26  e7e4d0e  pushed — last commit that successfully deployed
10:29  deploy #8138 finished (commit e7e4d0e)
10:30–11:25  wave16 merge storm: ~18 commits pushed, ZERO Coolify queue entries
10:34  848e45c  pushed (Dockerfile WASM restore) — no deploy triggered
11:26  8cd8691  pushed
11:27  deploy #8145 queued for 8cd8691 — FAILED (corepack/pnpm in wasm stage)
11:29  deploy #8146 retry — FAILED (same error)
11:37  deploy #8147 retry — FAILED (same error)
11:38  19749e7  pushed (Dockerfile pnpm symlink fix)
11:41  deploy #8148 queued for 19749e7 — in progress
```

Production container **`last_online_at`** remained at **11:17:33 UTC** — still serving the **`e7e4d0e`** image throughout the incident.

---

## Root cause analysis

### 1. `848e45c` — true webhook / queue gap

**Evidence:** Coolify deployment DB has **no row** for commit `848e45c` (nor for any of the ~18 commits between `e7e4d0e` and `8cd8691`). The push is confirmed on GitHub (`repos/gkinter/citymajor/commits/848e45c`).

**Likely contributing factors** (not mutually exclusive):

1. **High-velocity push window** — wave16 merges landed ~18 commits in ~57 minutes. Coolify may coalesce or drop intermediate webhook deliveries when the branch HEAD moves faster than the deploy worker can dequeue. Only the first deploy attempt after the quiet period (`8cd8691`) appears in the queue.
2. **Build-node instability** — `vps-cpx62` reported `unreachable_count: 21` in Coolify server metadata around the incident window. A unreachable build node can prevent queue processing even when webhooks arrive.
3. **No `watch_paths` filter** — app config has `watch_paths: null`, so file-path filtering is **not** the cause.

**Impact of 848e45c even if it had deployed:** the commit introduced a Dockerfile regression — copying `corepack` from the `node:20-slim` base into the `dotnet/sdk` wasm stage. That pattern fails in the .NET image (missing `lib/corepack.cjs`). The later `8cd8691` deploy attempts hit exactly this error.

### 2. `8cd8691` — webhook worked; build failed (not a webhook gap)

**Evidence:** Three deployment queue entries (`#8145`, `#8146`, `#8147`) all pinned to commit `8cd8691b2f9a`.

**Build error (deploy #8147 tail):**

```
ERROR: failed to build: failed to solve: process "/bin/sh -c corepack enable && corepack prepare pnpm@10.12.1 --activate" did not complete successfully: exit code: 1
```

Root cause: **`848e45c` Dockerfile change** — wasm stage used `COPY --from=base /usr/local/bin/corepack` + `corepack prepare` inside `mcr.microsoft.com/dotnet/sdk:8.0`, which lacks the Node corepack library layout from `node:20-slim`.

**Fix:** `19749e7` replaces the broken corepack copy with corepack cache + pnpm shim symlinks (see `Dockerfile` wasm stage). Commit message references npm install; actual diff uses symlink approach — functionally addresses the partial-corepack copy bug.

### 3. Misleading symptom: "push did not auto-deploy"

| User observation | Actual state |
|---|---|
| 848e45c not live | Webhook/queue gap — deploy never started |
| 8cd8691 not live | Webhook fired; build failed 3×; old container kept running |

The live site stayed on **`e7e4d0e`** (~1 hour stale) until **`19749e7`** deploy completes.

---

## Dockerfile notes (WASM / pnpm)

- **`BUILD_WASM=1`** (Coolify build env) runs `pnpm build:wasm` in the `wasm` stage and **fails the build** if `web/public/dotnet/_framework/blazor.boot.json` is missing.
- **Do not** `COPY --from=base` only the `corepack` binary into the dotnet SDK stage — use full shim setup (`19749e7` pattern) or `npm install -g pnpm@10.12.1`.
- **`git lfs pull`** in the builder stage requires `.git` in the Docker context (documented in Dockerfile comments).

---

## Operational playbook

### After pushing to integration branch

1. Wait 60–90 s for Coolify queue entry (post-push hook or `coolify-logs-coolify-deployments app=citymajor-web since_min=30 status=all`).
2. Confirm a row exists for **your commit SHA**. If **`not_seen`** after 5 min → see gap recovery below.
3. Wait for `finished` — Next.js + WASM Docker builds routinely take **6–10 min**.
4. **Do not** push again until the current deploy finishes (rapid pushes cancel in-progress builds).

### Webhook gap recovery

If push landed on GitHub but Coolify shows **no queue entry** for your SHA:

```bash
# Manual redeploy at branch HEAD (use sparingly — see deploy-discipline rules)
coolify deploy citymajor-web --force
```

Then verify the queued commit matches `git rev-parse origin/feat/wasm-r3f-integration-2026-07-04`.

### Build failure recovery

```bash
coolify lastfail citymajor-web          # error tail
coolify-build-log citymajor-web --failed --tail 80
```

Fix Dockerfile / deps on a feature branch → merge to integration → **one** push → wait for green deploy.

---

## Recommendations

| Priority | Action |
|---|---|
| P0 | Treat **`19749e7`** deploy as unblocking — confirm `#8148` finishes green before further integration merges. |
| P1 | **Batch wave merges** — land ≤1 push per 10 min on integration branch during merge storms to avoid webhook coalescing / cancel loops. |
| P1 | Monitor **`vps-cpx62` reachability** — elevated `unreachable_count` correlated with queue gaps. |
| P2 | **Retire or disable** duplicate app `citymajor-wasm-r3f-integration` (same branch, unhealthy) to avoid confusion. |
| P2 | Add **`/api/health` or build-stamp** response header with git SHA so live-vs-expected commit is obvious without Coolify UI. |
| P3 | Consider moving `gkinter/citymajor` under the org GitHub App used by other Softblaze repos for consistent webhook delivery auditing. |

---

## Audit metadata

| Item | Value |
|---|---|
| Investigation branch | `feat/webhook-audit-2026-07-05` |
| Worktree | `../citymajor-webhook-audit` |
| Base commit (integration tip) | `19749e79d70fecb7fe815dcc67c2aaacbf791913` |
| Data sources | Coolify MCP (`get-application`, `diagnose-app`), coolify-logs MCP (deployments `#8138`–`#8148`), `gh api` commit timestamps |
| Doc-only scope | No infra code changes in this audit commit |
