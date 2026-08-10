# CityMajor — Web Client Archival Charter

**Status:** Locked  
**Date locked:** 2026-07-12 (Unity became product); doc refreshed 2026-08-10  
**Canonical player product:** [UNITY_V1_SCOPE.md](./UNITY_V1_SCOPE.md) — Unity 6 desktop (Steam) since **2026-07-12**  
**Historical web charter:** [WEB_V1_SCOPE.md](./WEB_V1_SCOPE.md) — [SB-3704](https://linear.app/softblaze/issue/SB-3704) Jul spike  
**Unity v1 epic:** [SB-4170](https://linear.app/softblaze/issue/SB-4170)  
**Port plan:** [UNITY_PORT_MEGA_PLAN.md](./UNITY_PORT_MEGA_PLAN.md)  
**Active worktree:** `~/citymajor/citymajor-unity-port-plan` → `feat/unity-port-plan-2026-07-12`

Unity has been the shipped product surface since **2026-07-12**. Aug 2026 doc refreshes clarify archival status for agents — they are **not** a new product decision.

---

## 1. Verdict

The **Next.js + React Three Fiber + .NET WASM** tree under `web/` is an **archival validation harness**, not the player-facing product.

| Role | Owner |
|------|--------|
| **Player product (canonical)** | Unity 6 + URP — `unity/CityMajor.Unity/` ([SB-4170](https://linear.app/softblaze/issue/SB-4170)) |
| **Shared simulation** | `Forge.SimCore` (native in-process for Unity; WASM host remains for harness only) |
| **Browser client (`web/`)** | Frozen archive — sim/bridge/UI reference + smoke/perf checks ([SB-3704](https://linear.app/softblaze/issue/SB-3704) Jul spike) |

Agents must not treat `/play`, R3F HUD work, or new web shop/marketing gameplay surfaces as v1 product delivery. Prefer the Unity integration tip over stale web-first `AGENTS.md` on `main` or `origin/HEAD`.

---

## 2. Freeze rules

**Do not delete `web/`.** Keep the tree for parity checks, WASM boot triage, GLTF kit hosting reference, and historical smoke (`pnpm smoke:*`, `pnpm perf:gate`).

**No new player-facing web features.** Forbidden without an explicit Unity-first exception (docs/ops only):

- New gameplay HUD, tools, overlays, or panels on `/play`
- New eras, buildings, economy, or narrative UX aimed at browser players
- Expanding Stripe shop / Founder Pass as a browser-primary loop
- New R3F rendering features marketed as the shipped experience

**Allowed on `web/` (maintenance / harness only):**

- Critical bug fixes that unblock CI, Docker/`BUILD_WASM`, or smoke/perf gates
- Doc and script updates that clarify archival status or Unity parity
- Shared asset path fixes used by Unity import (e.g. `web/public/assets/gltf/`)
- Security / dependency patches required to keep the harness buildable

When in doubt: implement in Unity first; leave `web/` alone.

---

## 3. Relationship to other docs

| Doc | Relationship |
|-----|----------------|
| [UNITY_V1_SCOPE.md](./UNITY_V1_SCOPE.md) | **Canonical** locked scope for what ships to players (since 2026-07-12) |
| [WEB_V1_SCOPE.md](./WEB_V1_SCOPE.md) | Historical scale/era notes for the browser spike — superseded for platform |
| [WASM_SIM_BRIDGE.md](./WASM_SIM_BRIDGE.md) | Bridge contract — useful reference while Unity consumes `Forge.SimCore` natively |
| [UNITY_PORT_MEGA_PLAN.md](./UNITY_PORT_MEGA_PLAN.md) | 2026-07-12 decision: Unity primary; web maintenance → archival |

---

## 4. Directory intent (unchanged layout)

```
web/                 # archival Next.js + R3F + WASM harness (frozen for players)
web/public/dotnet/   # WASM bundle for harness boot / CI — not the Steam runtime
web/public/assets/   # GLTF kits may still feed Unity art import
unity/CityMajor.Unity/   # canonical player client
src/Forge.SimCore/   # shared sim — primary consumer is Unity
```

Deletion or relocation of `web/` is a **separate, explicit** decision — not implied by this freeze.

---

## 5. Remote / default branch (do not change without approval)

`origin/HEAD` may still advertise the Jul web spike. **Do not** run `git remote set-head` without user approval.

Recommended when approved:

```bash
git remote set-head origin feat/unity-port-plan-2026-07-12
# or merge Unity integration into main so the canonical clone matches product reality
```
