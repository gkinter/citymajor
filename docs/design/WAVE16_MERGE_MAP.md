# Wave 16 — Merge Map

**Status:** Active rollup (HUD + panel polish stack)  
**Integration branch:** `feat/wasm-r3f-integration-2026-07-04`  
**Base at doc time:** `848e45c` — `fix(docker): restore WASM bundle in image when BUILD_WASM=1`  
**Last updated:** 2026-07-05

Wave 16 batches parallel feature branches into five sequential merge branches (**a → e**). Each letter branch stacks on the previous tip. Fast-forward or merge into `feat/wasm-r3f-integration-2026-07-04` in order after smoke + `verify:tech-unlocks` pass.

---

## Merge order (a → e)

| Step | Label | Branch | Tip SHA | Remote |
|------|-------|--------|---------|--------|
| Base | — | `feat/wasm-r3f-integration-2026-07-04` | `848e45c` | yes |
| 1 | **a** | `feat/wave16-merge-2026-07-05` | `7d459fe` | yes |
| 2 | **b** | `feat/wave16-merge-b-2026-07-05` | `321fb5e` | yes |
| 3 | **c** | `feat/wave16-merge-c-2026-07-05` | `b83ac97` | yes |
| 4 | **d** | `feat/wave16-merge-d-2026-07-05` | `7331e49` | **local only** |
| 5 | **e** | `feat/wave16-merge-e-2026-07-05` | `3428d81` | yes |

**Apply order:** `848e45c` → **a** → **b** → **c** → **d** → **e**

At doc time, integration `@ 848e45c` already includes wave **a** and **b** (merge commit `e14b4a1`). Pending: **c**, **d**, **e**.

---

## Branch tips (full SHAs)

```
848e45ca17d6e0d0529bbdcfe5d08fe49f4bbee0  feat/wasm-r3f-integration-2026-07-04  (base)
7d459fe94c099a34c2911b358779d9d7d3a5ac93  feat/wave16-merge-2026-07-05          (a)
321fb5ef7325874b6111e70341f54fca97e27d87  feat/wave16-merge-b-2026-07-05        (b)
b83ac979d215dc83a5fcaa8302ef00bf28d2715c  feat/wave16-merge-c-2026-07-05        (c)
7331e49dd072faa6ef641580987a6d6749327898  feat/wave16-merge-d-2026-07-05        (d)
3428d811d38a8293c3ccfd2fa75d618178599143  feat/wave16-merge-e-2026-07-05        (e)
```

---

## Per-wave contents

### a — `7d459fe` (from integration base)

Zone overlay, transport toolbar, build-menu smoke, empty-city start, PR1 changelog docs.

| SHA | Summary |
|-----|---------|
| `b515ed7` | feat(zoning): distinct tier colors and toolbar legend |
| `c3fe4da` | feat(web): scaffold transport toolbar with bus/rail stubs |
| `7d2a232` | test(smoke): e2e build menu open and zoning tier select |
| `26777e3` | feat(play): empty city start via ?empty=1 or HUD toggle |
| `d3fb6a6` | docs(pr1): add wave 15 build loop to merge checklist and PR body draft |
| `7d459fe` | fix(wave16): wire TransportToolbar in PlayClient road mode |

### b — `321fb5e` (from **a** @ `7d459fe`)

Services build tabs, research unlock HUD, demand/budget/pop HUDs, meshy manifest prep, tile tooltip, minimap, help overlay.

| SHA | Summary |
|-----|---------|
| `12fbeb7` | feat(web): split build catalog into Fire/Police/Health service tabs |
| `dd0a117` | feat(hud): show zones/buildings in research unlock toast |
| `c5e3707` | feat(hud): add bottom-center RCI demand overlay |
| `6ef5d45` | feat(web): add minimal BudgetPanel HUD top-right |
| `eb894e9` | feat(web): add minimal PopulationPanel HUD |
| `d70c8dc` | feat(meshy): add pending civic/school svc_modern manifest slots |
| `df20273` | feat(play): add HUD tile hover tooltip on CityCanvas |
| `5c93d2b` | feat(hud): scaffold MinimapPanel with hud-theme placeholder |
| `321fb5e` | feat(play): add HelpPanel overlay with keyboard and build mode reference |

### c — `b83ac97` (from **b** @ `321fb5e`)

Core HUD meters: approval, happiness, time controls, save indicator, era/quality polish, camera home, chunk debug.

| SHA | Summary |
|-----|---------|
| `c004f69` | feat(web): add ApprovalMeter HUD near BudgetPanel |
| `b173b77` | feat(hud): add HappinessMeter to left HUD stack |
| `ce8db9a` | feat(web): wire time controls HUD to sim-bridge 4x speed |
| `5a93eb5` | feat(hud): add save status dot to SaveLoadControls |
| `8f50a04` | feat(hud): polish EraProgressPanel with current era name and progress bar |
| `fe03d57` | feat(play): polish quality preset HUD with Low/Med/High/Ultra |
| `e980273` | feat(web): add HUD Home button to reset camera view |
| `b83ac97` | feat(web): show loaded chunk count with ?debug=chunks |

### d — `7331e49` (from **c** @ `b83ac97`)

Panel polish + trade overlay + shop spacing. Includes PlayClient conflict resolution for help + chunk debug.

| SHA | Summary |
|-----|---------|
| `a6c746a` | feat(hud): polish ResearchPanel tech tree row styling |
| `027d061` | feat(web): polish CitizenPanel empty state and hud-theme headers |
| `f5d6b1b` | feat(hud): polish HeraldPanel loading and error states |
| `cbe2760` | polish(ui): align LawPanel with HUD slide-panel theme |
| `2728c43` | fix(web): resolve PlayClient helpOpen + chunk debug state merge |
| `429b5e3` | fix(web): polish Founder Pass card spacing on /shop |
| `7331e49` | feat(web): scaffold TradeOverlay toggle near ServicesToolbar |

### e — `3428d81` (from integration / **d** tip)

FPS tier colors, home hero CTA, economy panel summary. Resolves PlayClient merge conflict markers.

| SHA | Summary |
|-----|---------|
| `51031cc` | feat(hud): color-code FPS diagnostics by performance tier |
| `bbfed6b` | feat(web): polish home hero CTA copy for /play |
| `174d7bb` | feat(web): polish EconomyPanel summary row with treasury and tax rate |
| `3428d81` | fix(web): resolve PlayClient merge conflict markers |

---

## Worktrees

| Wave | Worktree path | Branch |
|------|---------------|--------|
| a | `citymajor-wave16-merge` | `feat/wave16-merge-2026-07-05` |
| b | `citymajor-wave16-merge-2` | `feat/wave16-merge-b-2026-07-05` |
| c | `citymajor-wave16-merge-3` | `feat/wave16-merge-c-2026-07-05` |
| d | `citymajor-wave16-merge-4` | `feat/wave16-merge-d-2026-07-05` |
| e | `citymajor-wave16-merge-5` | `feat/wave16-merge-e-2026-07-05` |
| doc | `citymajor-wave16-doc` | `feat/wave16-merge-doc-2026-07-05` |

Integration FF worktree: `citymajor-web-r3f-spike` → `feat/wasm-r3f-integration-2026-07-04`.

---

## Merge gate (each step)

1. `pnpm exec tsc --noEmit`
2. `pnpm verify:tech-unlocks`
3. Smoke suite green (46+ checks) before push to integration
4. One deploy per integration push — coordinate with deploy agents

---

## Related docs

- [V1_MERGE_CHECKLIST.md](./V1_MERGE_CHECKLIST.md) — PR #1 blocking gates
- [PR1_BODY_DRAFT.md](./PR1_BODY_DRAFT.md) — wave 15 build loop copy
