# CityMajor — Codebase & Project Audit

**Date:** 2026-07-27
**Scope:** Full repo at `ed18378` — web app, WASM sim bridge, C# sim, assets, CI/CD, docs
**Baseline:** [`WEB_V1_SCOPE.md`](WEB_V1_SCOPE.md) (locked charter), [`V1_MERGE_CHECKLIST.md`](V1_MERGE_CHECKLIST.md)

This audit reads the code against the locked v1 charter and flags where they diverge.
Items already tracked in `V1_MERGE_CHECKLIST.md` are marked **(tracked)** — they are
repeated here only where the audit changes their severity or scope.

---

## Scorecard

| Area | Rating | One-line |
|------|--------|----------|
| API security (identity, entitlements, Stripe) | **Strong** | HMAC cookies, timing-safe compare, prod-gated dev paths, verified webhooks |
| Server-side error handling | **Strong** | Zod at every boundary, typed results, atomic writes with backup |
| Data durability & multi-instance | **Critical** | Paid entitlements + cloud saves live in `/tmp`, single-process only |
| Account model | **Critical** | $24.99 purchases bound to an anonymous cookie, no recovery path |
| Asset pipeline | **Critical** | ~390 MB of GLBs, eagerly preloaded, zero compression |
| Renderer architecture | **Weak** | Violates the charter's own "no React state per building" rule |
| Automated verification | **Weak** | No TS tests, no typecheck/lint/build in CI, 869 C# assertions never run |
| Docs vs implementation | **Weak** | The two files agents read first describe an architecture that isn't built |
| Build & deploy | **Good** | Multi-stage Docker, LFS smudge + stub gate, non-root runner, loud failures |

---

## P0 — Launch blockers

### 1. Server state lives in `/tmp` in production

`web/lib/data-dir.ts:19` defaults `resolveDataDir()` to `/tmp/citymajor-data` when
`CITYMAJOR_DATA_DIR` is unset. Everything durable goes there:

- `saves.json` — every player's cloud saves
- `tiers.json` — every Founder Pass grant
- `narrative-quota.json` — daily LLM quotas
- `blobs/saves/{userId}/{saveId}.cmjr` — save payloads

On Coolify/Docker, `/tmp` is container-local and wiped on every redeploy or restart.
**A routine deploy erases paid entitlements and all cloud saves.** `.env.example`
documents `CITYMAJOR_DATA_DIR` as optional; for a deployment that takes money it is
mandatory, and it needs to point at a mounted volume.

Two more defects compound it:

- **Single-process only.** `withWriteLock` (`save-store.ts:305`) and the module-level
  `memoryStore` caches are per-process. Any horizontal scaling — or Next.js spawning
  more than one worker — produces divergent and lost writes.
- **`tier-store` never re-reads from disk.** `loadStore()` (`tier-store.ts:18`) returns
  the cached `memoryStore` whenever it is non-empty. A process that has ever cached a
  non-empty store will never observe a grant written by another process, so a paying
  user can be served `free` indefinitely.

**Fix:** move all three stores to a real database (Supabase is already an available
connector), or at minimum require `CITYMAJOR_DATA_DIR` on a mounted volume, drop the
never-invalidated caches, and pin the deployment to one instance until then.

### 2. Purchases and saves are bound to an anonymous cookie with no recovery path

There is no auth system anywhere in the web app — no `next-auth`, no Supabase auth, no
OAuth, no email capture. Identity is `citymajor_uid`, an HMAC-signed random UUID minted
on first request (`user-identity.ts:139`). Founder Pass is stored under that UUID, and
save ownership is scoped by it.

Consequence: clearing cookies, switching browsers, switching devices, or using private
browsing **permanently destroys a $24.99 purchase and every cloud save**, with no support
path — the system never learns an email address, and Stripe's customer record cannot be
mapped back to a UUID the user no longer holds.

This is **(tracked)** as SB-3693, but the doc classifies it "deferred until Supabase OAuth
(v1.5)." That classification is only defensible before real money is taken. Once Stripe is
live it is a launch gate, not a deferral. The minimum viable version is small: capture the
email Stripe already collects at checkout, store it against the userKey in the webhook, and
offer a magic-link "restore my purchase" route.

### 3. GLTF payload is ~390 MB and eagerly preloaded

63 GLBs under `web/public/assets/gltf/` total **390.7 MB** (45.3 MB committed as raw git
blobs, 345.4 MB behind LFS pointers). The largest are 9–11 MB each — raw Meshy *refine*
output, never post-processed:

```
11.4 MB  industrial/res_high_industrial_03.glb
11.2 MB  industrial/res_low_industrial_00.glb
11.0 MB  industrial/res_high_industrial_01.glb
10.9 MB  industrial/com_industrial_00.glb
10.0 MB  heroes/hero_frontier_city_hall.glb
 …
```

`GltfPreloader.tsx:11` calls `useGLTF.preload(path)` for **every** entry in
`allGltfPaths()` — 48 of the 55 catalog keys — the moment `/play` mounts. That is well
over 300 MB of downloads before the player does anything.

Three multipliers:

- **No compression at all.** Zero references to Draco, meshopt, or KTX2 anywhere in the
  repo. `@gltf-transform/core` is a devDependency, but the only script using it generates
  *placeholders* — there is no optimization step.
- **Out-of-scope eras are in the preload set.** `postwar`, `modern`, and `future` assets
  are preloaded, though WEB_V1_SCOPE §2 explicitly cuts them from v1.
- **Not on a CDN.** CLAUDE.md says "CDN-hosted"; they are served from `web/public/` and
  baked into the runner image, so the Docker image carries all 390 MB.

**Fix:** set a per-archetype budget (~150–250 KB), run gltf-transform `simplify` + `draco`
+ `ktx2` in a build step, gate the preload to the active era band with the rest lazy, and
move the bucket to a CDN.

### 4. CI verifies almost nothing

`ci-smoke.yml` is the only pull-request gate. It boots `pnpm dev` (the **dev** server, not
a production build) and runs a Playwright smoke script. There is no:

- `tsc --noEmit` — though it currently passes clean, nothing enforces that
- lint — and `next lint` would fail anyway: **no eslint config exists in `web/`**
- `next build` — the production bundle is never verified to compile
- `dotnet test` — **869 `[Fact]`/`[Theory]` assertions across ~35 C# test files never run**

And there are **zero TypeScript unit tests** — no vitest, no jest, no `node:test`, no
`*.test.ts` anywhere. The entire web layer is covered only by one 1,875-line end-to-end
smoke script. That includes every piece of money-handling code: `save-store`, `tier-store`,
`stripe-webhook`, `user-identity`, `narrative-quota`, `save-format`.

Telling detail: those modules already export `_resetSaveStoreForTests`,
`_resetTierStoreForTests`, `_resetStripeWebhookStateForTests`, and
`_resetNarrativeQuotaForTests` — test seams built for tests that were never written.

**Fix:** add a `verify` job running typecheck + build + `dotnet test`; add vitest with a
first suite over the six money-path modules (the seams are already there); create the
missing eslint config or drop the dead `lint` script.

---

## P1 — Major

### 5. The renderer violates the charter's own architecture rule

CLAUDE.md, AGENTS.md, and WEB_V1_SCOPE §4 all state: *"Sim snapshots → `InstancedMesh`
matrices in `useFrame` — **not** React state per building."*

`CityCanvas.tsx:175–193` does exactly the opposite. Every snapshot (up to 4 Hz) runs:

```ts
setCity(cityDataFromSnapshot(snapshot));
setZones(...); setRoads(...); setTraffic(...);
setServiceCoverage(...); setSimResources(...);
```

`cityDataFromSnapshot` (`city-data.ts:151`) rebuilds the entire `CityData` from scratch —
one fresh object per building, re-grouping into `buildingsByArchetypeKey`, re-sorting the
key list, rebuilding 64 chunk index arrays. At the 5,000-building target that is ~5k
allocations plus a full React reconciliation of the scene subtree, four times a second.

### 6. Per-frame string churn in the instancing hot loop

`BuildingInstances.useFrame` iterates every building every frame. Inside:

- `lodVisualForBuilding` allocates a fresh `LodVisual` object per building per frame
- `shadeColor` / `heatColorRamp` (`lod.ts:96`, `lod.ts:103`) build a **new hex string**
  via `getHexString()`…
- …which `c.set(visual.color)` immediately re-parses as a CSS color string
- plus a spread-clone `{...visual, scale}` whenever a spawn animation is active

At 5,000 buildings × 60 fps that is roughly **300,000 string allocations and color parses
per second**, feeding straight into GC pressure. Colors should be written as floats into
the instance color buffer; visuals should be computed into a reused struct.

Together, #5 and #6 are the most likely reason the FPS target has never been met.

### 7. The locked FPS targets have never been measured on real hardware

PERF.md's only recorded numbers are **1–2 FPS** from a headless software renderer. Every
GPU sign-off checkbox in PERF.md §4/§5 is unchecked, and the benchmark table is an empty
template. `perf-gate.yml` is `workflow_dispatch`-only and auto-skips below 15 FPS.

So "≥30 integrated / ≥60 discrete" (WEB_V1_SCOPE §4) is an aspiration with no evidence
behind it. This is **(tracked)** as SB-3703/SB-3705 as a *measurement* task — but given
#3, #5, and #6, the number will require engineering work, not just a measurement session.
Planning for it as sign-off understates the remaining effort.

### 8. SharedArrayBuffer is documented but unimplemented — while its costs are paid

Three documents describe a design that does not exist:

| Doc | Claim | Reality |
|-----|-------|---------|
| CLAUDE.md | "Web Workers + SharedArrayBuffer for ticks" | Not used |
| AGENTS.md | "WASM sim worker (SharedArrayBuffer ticks)" | Not used |
| WEB_V1_SCOPE §2 | "COOP/COEP for SharedArrayBuffer", "double-buffer snapshots" | Not used |

The actual path: C# serializes the whole snapshot to a **JSON string**
(`GetRenderSnapshot(): string`), JS parses it, and it is structured-cloned across
`postMessage`. No SAB, no `Atomics`, no double buffering.

Meanwhile `next.config.ts` sets `COOP: same-origin` + `COEP: require-corp` on **all**
routes. That forces cross-origin isolation — breaking third-party embeds and requiring
CORP headers on every asset — to enable a capability nothing uses. PERF.md is the only
doc that says so honestly ("not used in M0").

**Fix:** either implement the SAB transfer path, or drop COEP and correct the three docs.
Leaving it as-is means agents keep reading a spec the code does not follow.

### 9. Worker scans all 65,536 tiles per snapshot with string-keyed maps

`sim-worker.ts:271–318`: `collectZonesFromGrid` and `collectRoadsFromGrid` loop the full
256×256 grid on every snapshot. `mergeZones`/`mergeRoads` then build a `Map` keyed by a
**template literal** `` `${x},${z}` `` per tile, and parse each key back out with
`split(",").map(Number)`.

That is tens of thousands of string allocations and parses per snapshot, on top of the
full-grid scan, on top of the JSON round-trip from #8. A flat typed-array diff would
remove all three costs.

### 10. Founder Pass has an uncapped LLM cost vector

`entitlements.ts:29` gives `founder_pass` a `maxNarrativeEventsPerDay` of
`Number.MAX_SAFE_INTEGER`. `/api/narrative/event` has no rate limit, no concurrency
limit, and no monthly ceiling. And `context.cityName` is `z.string().optional()` —
**no maximum length** (`narrative-templates.ts:22`) — interpolated straight into the LLM
user prompt at `narrative-prompt.ts:63`.

So a single **$24.99 one-time purchase** buys unbounded calls, with client-controlled
prompt size, against the operator's OpenAI/Anthropic key. The margin on that product is
unbounded-negative by construction.

**Fix:** per-minute rate limit, hard monthly ceiling for founder tier, `.max(64)` on
`cityName` and `era`, and an input-token cap before dispatch.

### 11. Prompt injection into the Herald

Same unbounded `cityName` field, same interpolation point. A client can append instructions
that override the system prompt. Output is validated for *shape* (`LlmPayloadSchema`) but
not content, then rendered into the HUD.

Blast radius is limited — single-player, and the option allowlist (#12) blocks mechanical
effects — but it is a public endpoint spending the operator's API budget and producing
user-visible text. Delimit and length-cap the field.

### 12. LLM-generated council options have no mechanical effect

`heraldOptionToSimCommands` (`herald-option-commands.ts:63`) looks the option id up in a
26-entry hardcoded allowlist and falls back to `DEFAULT_EFFECTS = { approvalDelta: 0 }` —
a no-op.

But the system prompt instructs the model to **invent** ids: *"options is an array of 2-3
objects: { id: snake_case, … }"*. Generated ids will almost never collide with the
allowlist. So the paid tier's headline feature produces council choices that change nothing
in the simulation, while the free tier's template events work correctly.

The allowlist is the right security posture — do not remove it. Instead, have the LLM
**select from** the template's existing option ids (pass them in and constrain the output),
or let it return structured effects that are clamped server-side against per-option caps.

### 13. Free tier's template events are rate-limited, contradicting the charter

WEB_V1_SCOPE §5 and CLAUDE.md both promise: *"Templates always available when LLM quota
exhausted or API down."*

The route consumes quota and returns 429 **before** deciding whether to call the LLM
(`narrative/event/route.ts:52–79`). Since free tier has `llmEnabled: false`, a free player
gets 10 **template** events per day and then nothing — the exact scenario the charter says
must always work.

**Fix:** move the quota check so it gates only the LLM branch; serve templates unmetered.

---

## P2 — Moderate

| # | Finding | Location |
|---|---------|----------|
| 14 | Stripe idempotency ledger is a process-local `Set` — lost on restart. Harmless today (`setStoredTier` is idempotent) but provides no real protection and breaks once revocation lands. | `stripe-webhook.ts:6` |
| 15 | No refund/chargeback handling — `charge.refunded` and `customer.subscription.deleted` are explicit `unimplemented_`. A refunded Founder Pass keeps entitlements forever. | `stripe-webhook.ts:71` |
| 16 | Save payload size unbounded on the JSON path: `payload` is `z.record(z.string(), z.unknown())` with no cap. Only the CMJR path enforces the 5 MB limit. | `save-store.ts:76` |
| 17 | `personalizeNarrativeText` passes user input as a `String.replace` **replacement**, so `$&`, `` $` ``, `$1` in a city name are interpreted as patterns. Use a replacer function. | `narrative-templates.ts:57` |
| 18 | `next lint` is broken — no eslint config exists in `web/`. Nothing lints this codebase. | `web/package.json:9` |
| 19 | Two stray npm `package-lock.json` files inside a pnpm workspace; `web/wasm/` still holds a full standalone Vite spike that `build-wasm.sh` publishes a redundant second WASM bundle into. | `web/wasm/`, `web/packages/sim-types/` |
| 20 | ~45 MB of GLBs are committed as raw git blobs despite `.gitattributes` routing `*.glb` to LFS — the rule landed after those files, so history carries both. | `.gitattributes:2` |
| 21 | Local dev without git-lfs silently serves 133-byte pointer stubs as `.glb`. The Dockerfile guards this well; `pnpm dev` does not. Add the same stub check to `smoke:precheck`. | `scripts/smoke-precheck.mjs` |
| 22 | `Forge.SimCore.csproj` glob-includes `..\Forge.Game\Simulation\*.cs`, pulling `TrafficSystem.cs` (933 lines) into the WASM bundle even though `WasmSimHost` uses `WasmTrafficLite` instead. Audit the glob for other unreferenced systems — it is all first-load download weight. | `src/Forge.SimCore/Forge.SimCore.csproj:16` |
| 23 | 59 files in `docs/design/` with acknowledged contradictions (`GAP_AUDIT_DESIGN_DOCS.md` exists to reconcile them; `MESHY_ASSET_PIPELINE.md` is duplicated across `docs/` and `docs/design/`). WEB_V1_SCOPE is correctly canonical — but the two files agents read *first*, CLAUDE.md and AGENTS.md, carry the stale SharedArrayBuffer and CDN claims. | `docs/design/` |

---

## What is genuinely well built

Worth stating plainly, because the P0 list is long and the security work here is better
than the average project at this stage:

- **Identity and entitlement security is strong.** `user-identity.ts` uses HMAC-SHA256 with
  `timingSafeEqual`, rejects a specific known-committed dev placeholder secret, hard-fails
  in production without a ≥32-char secret, and refuses to read identity from any
  client-controllable header — with a comment recording the impersonation bug that taught
  the lesson. `resolve-tier.ts` disables both the mock-cookie and dev-header paths in
  production. The self-grant stub at `POST /api/me/entitlements` returns 404 in production.
  The Stripe webhook verifies signatures before touching anything, and validates the
  userKey against a UUID pattern. Save ownership is scoped by verified user id on read
  *and* delete.
- **API error handling is consistent and careful.** Every route parses with zod, returns
  typed discriminated results, logs server-side, and never leaks internals to the client.
- **`save-store` durability is thoughtful:** atomic tmp-write + rename, a `.bak` copy, real
  recovery from that backup on corrupt JSON, and a promise-based write lock.
- **The sim worker is defensively written:** `MAX_TICKS_PER_MESSAGE` guards the classic
  catch-up death spiral after a tab-hidden pause, and `sim-bridge.init` has correct
  timeout/settle/listener-cleanup handling for a worker that never posts `ready`.
- **The procedural fallback is a good call** — `/play` works with no WASM at all, which
  keeps CI and previews meaningful.
- **The Dockerfile is the strongest artifact in the repo:** multi-stage, LFS smudge plus an
  explicit pointer-stub detection gate, non-root runner, and a `BUILD_WASM` arg that fails
  loudly rather than silently shipping a procedural-only image.
- **`V1_MERGE_CHECKLIST.md` is honest.** Several P0s here are already on its NO-GO list.
  The project is not deceiving itself about its state — the gap is in severity ranking, not
  awareness.
- **`tsc --noEmit` passes clean** across the whole web package.

---

## Recommended sequence

**Before Stripe goes live** (these are the ones that convert into refunds and support load):

1. Durable storage — move stores off `/tmp` onto a real DB or mounted volume; drop the
   never-invalidated caches (#1)
2. Purchase recovery — capture the checkout email in the webhook, add a restore route (#2)
3. Rate-limit and length-cap the narrative endpoint; add a founder-tier ceiling (#10, #11)
4. Stop metering free-tier template events (#13)

**Before the perf sign-off session** (otherwise the session just re-measures a known fail):

5. Asset pipeline — compress, budget, lazy-load by era, move to CDN (#3)
6. Take sim snapshots out of React state (#5)
7. Remove per-frame string allocation from the instancing loop (#6)

**In parallel, to stop the drift:**

8. Add a CI `verify` job: typecheck + build + `dotnet test` (#4)
9. Add vitest over the six money-path modules — the test seams already exist (#4)
10. Reconcile CLAUDE.md and AGENTS.md with what the code actually does, or implement the
    SAB path and drop the JSON round-trip (#8)
11. Fix the Herald option contract so paid LLM choices affect the sim (#12)
