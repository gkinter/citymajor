# Wave 16 — Integration Merge Order

**Status:** Active — apply remaining wave letters onto WASM integration  
**Integration branch:** `feat/wasm-r3f-integration-2026-07-04`  
**Worktree:** `citymajor-web-r3f-spike`  
**Last updated:** 2026-07-05

This document specifies the **exact merge sequence** for landing Wave 16 letters **b → e** onto the WASM integration line. Apply in order; do not skip or reorder — each letter branch stacks on the previous tip.

---

## Integration base (WASM)

Apply these two commits as the integration floor before Wave 16 letters:

| Order | SHA | Branch / source | Summary |
|-------|-----|-----------------|---------|
| 1 | `848e45c` | `feat/wasm-r3f-integration-2026-07-04` | fix(docker): restore WASM bundle in image when BUILD_WASM=1 |
| 2 | `e7e4d0e` | `feat/wasm-boot-merge-2026-07-05` | fix(wasm): restore PlaceBuilding on WasmSimHost for dotnet publish |

Full SHAs:

```
848e45ca17d6e0d0529bbdcfe5d08fe49f4bbee0  integration — docker WASM bundle
e7e4d0ebec38774fbc10f51a8a6694e981061afc  wasm boot — PlaceBuilding on WasmSimHost
```

**Note:** At doc time, `848e45c` on integration already includes `e7e4d0e` and merge **b** via merge commit `e14b4a1`. Steps **c → e** below are still pending on integration.

---

## Merge order (b → e)

| Step | Label | Branch | Tip SHA | Worktree |
|------|-------|--------|---------|----------|
| 1 | **b** | `feat/wave16-merge-b-2026-07-05` | `321fb5e` | `citymajor-wave16-merge-2` |
| 2 | **c** | `feat/wave16-merge-c-2026-07-05` | `b83ac97` | `citymajor-wave16-merge-3` |
| 3 | **d** | `feat/wave16-merge-d-2026-07-05` | `7331e49` | `citymajor-wave16-merge-4` |
| 4 | **e** | `feat/wave16-merge-e-2026-07-05` | `3428d81` | `citymajor-wave16-merge-5` |

**Apply sequence:**

```
848e45c + e7e4d0e  (integration WASM base)
    → merge-b 321fb5e
    → merge-c b83ac97
    → merge-d 7331e49
    → merge-e 3428d81
```

Full tip SHAs:

```
321fb5ef7325874b6111e70341f54fca97e27d87  merge-b
b83ac979d215dc83a5fcaa8302ef00bf28d2715c  merge-c
7331e49dd072faa6ef641580987a6d6749327898  merge-d
3428d811d38a8293c3ccfd2fa75d618178599143  merge-e
```

---

## Per-step contents

### merge-b — `321fb5e` (from wave **a** @ `7d459fe`)

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

**Integration status:** merged @ `e14b4a1` (included in `848e45c` tip).

### merge-c — `b83ac97` (from **b** @ `321fb5e`)

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

**Integration status:** pending.

### merge-d — `7331e49` (from **c** @ `b83ac97`)

Panel polish, trade overlay scaffold, shop spacing. Resolves PlayClient help + chunk debug state merge.

| SHA | Summary |
|-----|---------|
| `a6c746a` | feat(hud): polish ResearchPanel tech tree row styling |
| `027d061` | feat(web): polish CitizenPanel empty state and hud-theme headers |
| `f5d6b1b` | feat(hud): polish HeraldPanel loading and error states |
| `cbe2760` | polish(ui): align LawPanel with HUD slide-panel theme |
| `2728c43` | fix(web): resolve PlayClient helpOpen + chunk debug state merge |
| `429b5e3` | fix(web): polish Founder Pass card spacing on /shop |
| `7331e49` | feat(web): scaffold TradeOverlay toggle near ServicesToolbar |

**Integration status:** pending.

### merge-e — `3428d81` (from **d** @ `7331e49`)

FPS tier colors, home hero CTA, economy panel summary. Resolves PlayClient merge conflict markers.

| SHA | Summary |
|-----|---------|
| `51031cc` | feat(hud): color-code FPS diagnostics by performance tier |
| `bbfed6b` | feat(web): polish home hero CTA copy for /play |
| `174d7bb` | feat(web): polish EconomyPanel summary row with treasury and tax rate |
| `3428d81` | fix(web): resolve PlayClient merge conflict markers |

**Integration status:** pending.

---

## Merge procedure (integration worktree)

From `citymajor-web-r3f-spike` on `feat/wasm-r3f-integration-2026-07-04`:

```bash
# Confirm base
git log --oneline -1   # expect 848e45c (includes e7e4d0e + merge-b)

# Pending steps — one merge per push, gate before next
git merge --no-ff feat/wave16-merge-c-2026-07-05 -m "merge: integrate wave16-merge-c HUD commits"
# gate → push → deploy wait

git merge --no-ff feat/wave16-merge-d-2026-07-05 -m "merge: integrate wave16-merge-d panel polish"
# gate → push → deploy wait

git merge --no-ff feat/wave16-merge-e-2026-07-05 -m "merge: integrate wave16-merge-e polish commits"
# gate → push → deploy wait
```

If re-basing from a clean WASM floor (no prior wave16 merges):

```bash
git checkout feat/wasm-r3f-integration-2026-07-04
git reset --hard 848e45c   # includes e7e4d0e
git merge --no-ff feat/wave16-merge-b-2026-07-05 -m "merge: integrate wave16-merge-b HUD commits"
git merge --no-ff feat/wave16-merge-c-2026-07-05 -m "merge: integrate wave16-merge-c HUD commits"
git merge --no-ff feat/wave16-merge-d-2026-07-05 -m "merge: integrate wave16-merge-d panel polish"
git merge --no-ff feat/wave16-merge-e-2026-07-05 -m "merge: integrate wave16-merge-e polish commits"
```

---

## Merge gate (each step)

1. `pnpm exec tsc --noEmit`
2. `pnpm verify:tech-unlocks`
3. Smoke suite green (46+ checks)
4. One deploy per integration push — coordinate with deploy agents

---

## Related docs

- [WAVE16_MERGE_MAP.md](./WAVE16_MERGE_MAP.md) — full wave **a → e** branch map and worktrees
- [V1_MERGE_CHECKLIST.md](./V1_MERGE_CHECKLIST.md) — PR #1 blocking gates
