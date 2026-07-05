# Wave 17 — Candidate Branches

**Status:** Active — integration candidates post docs bundle  
**Base at doc time:** `d1e212c` — `docs(agents): add WASM boot triage runbook section`  
**Branch:** `feat/wave17-map-update-2026-07-05`  
**Last updated:** 2026-07-05

Wave 17 candidate branches are ready to land on `feat/wasm-r3f-integration-2026-07-04` after the Docker corepack fix. None of these should merge until preview deploys can build the WASM stage successfully.

---

## Prerequisite — merge corepack fix first

| Order | Label | Branch | Tip SHA | Summary |
|-------|-------|--------|---------|---------|
| 0 | **corepack** | `feat/docker-corepack-fix-2026-07-05` | `37df397` | fix(docker): restore pnpm in wasm stage via corepack cache |

**Merge-after-corepack rule:** Apply `37df397` to integration **before** any candidate below. Coolify preview builds fail on the Dockerfile `corepack prepare` step until this lands — smoke probes and SEO checks that hit the live FQDN are blocked without it.

Worktree: `citymajor-docker-corepack-fix` → `feat/docker-corepack-fix-2026-07-05`.

---

## Candidate branches

| # | Label | Branch | Tip SHA | Remote |
|---|-------|--------|---------|--------|
| 1 | **paint-polish** | `feat/paint-polish-bundle-2026-07-05` | `eab7ee7` | yes |
| 2 | **merge-d** | `feat/wave16-merge-d-2026-07-05` | `7331e49` | yes |
| 3 | **SEO** | `feat/wave16-merge-seo-2026-07-05` | `0c32bf9` | yes |
| 4 | **smoke-probes** | `feat/smoke-probes-bundle-2026-07-05` | `8657900` | yes |

**Suggested apply order (after corepack):**

```
37df397  (corepack fix — prerequisite)
    → paint-polish eab7ee7
    → merge-d 7331e49
    → SEO 0c32bf9
    → smoke-probes 8657900
```

---

## Branch tips (full SHAs)

```
d1e212cd023afa98f81517cb03b280f4cab1304f  feat/docs-bundle-2026-07-05        (doc base)
37df397f15763437844e8ea510d0cdebbdab52f9  feat/docker-corepack-fix-2026-07-05 (prerequisite)
eab7ee73b1308c8b0a19fa038f7619a212e19e8a  feat/paint-polish-bundle-2026-07-05
7331e49dd072faa6ef641580987a6d6749327898  feat/wave16-merge-d-2026-07-05
0c32bf941bb96d223fbe80800c36a3da84508334  feat/wave16-merge-seo-2026-07-05
86579004eb7f797bae4824288fa816b2132a757d  feat/smoke-probes-bundle-2026-07-05
```

---

## Per-candidate summary

### paint-polish — `eab7ee7`

Paint SFX balance, bulldoze haptics, tooltip/confirm flash, paint feedback docs.

| SHA | Summary |
|-----|---------|
| `8422e65` | feat(web): bulldoze tooltip and confirm flash in ZoningToolbar |
| `bbc629f` | fix(web): balance bulldoze paint SFX volume |
| `ade4898` | fix(web): balance zone/road paint SFX with bulldoze |
| `24612a0` | feat(web): bulldoze paint haptic with reduced-motion guard |
| `eab7ee7` | docs(web): document paint SFX gains and haptics policy |

Worktree: `citymajor-paint-polish-bundle`.

### merge-d — `7331e49`

Wave 16 letter **d** — panel polish + trade overlay + shop spacing.

| SHA | Summary |
|-----|---------|
| `a6c746a` | feat(hud): polish ResearchPanel tech tree row styling |
| `027d061` | feat(web): polish CitizenPanel empty state and hud-theme headers |
| `f5d6b1b` | feat(hud): polish HeraldPanel loading and error states |
| `cbe2760` | polish(ui): align LawPanel with HUD slide-panel theme |
| `2728c43` | fix(web): resolve PlayClient helpOpen + chunk debug state merge |
| `429b5e3` | fix(web): polish Founder Pass card spacing on /shop |
| `7331e49` | feat(web): scaffold TradeOverlay toggle near ServicesToolbar |

Worktree: `citymajor-wave16-merge-4`.

### SEO — `0c32bf9`

Sitemap, robots.txt, OG meta, and SEO smoke assertions.

Worktree: `citymajor-wave16-merge-f`.

### smoke-probes — `8657900`

Non-blocking smoke NOTE probes for chunk debug and adaptive DPR; sitemap/robots 200 checks.

| SHA | Summary |
|-----|---------|
| `7fbebe1` | test(smoke): NOTE when ?debug=perf AdaptiveDpr line visible |
| `3bf81bb` | test(smoke): non-blocking NOTE for ?debug=chunks Loaded chunks line |
| `8657900` | test(smoke): assert /sitemap.xml and /robots.txt return 200 |

Worktree: `citymajor-smoke-probes-merge`.

---

## Worktrees

| Label | Worktree path | Branch |
|-------|---------------|--------|
| corepack | `citymajor-docker-corepack-fix` | `feat/docker-corepack-fix-2026-07-05` |
| paint-polish | `citymajor-paint-polish-bundle` | `feat/paint-polish-bundle-2026-07-05` |
| merge-d | `citymajor-wave16-merge-4` | `feat/wave16-merge-d-2026-07-05` |
| SEO | `citymajor-wave16-merge-f` | `feat/wave16-merge-seo-2026-07-05` |
| smoke-probes | `citymajor-smoke-probes-merge` | `feat/smoke-probes-bundle-2026-07-05` |
| doc | `citymajor-wave17-map` | `feat/wave17-map-update-2026-07-05` |

---

## Merge gate (each candidate)

1. **Corepack fix merged** — preview deploy green before FQDN smoke
2. `pnpm exec tsc --noEmit`
3. `pnpm verify:tech-unlocks`
4. Smoke suite green (49+ checks) before push to integration
5. One deploy per integration push — coordinate with deploy agents

---

## Related docs

- [WAVE16_MERGE_MAP.md](./WAVE16_MERGE_MAP.md) — wave 16 rollup (a → e)
- [WAVE17_BACKLOG.md](./WAVE17_BACKLOG.md) — deferred post-integration items
- [V1_MERGE_CHECKLIST.md](./V1_MERGE_CHECKLIST.md) — PR #1 blocking gates
- [DEPLOY_WEB.md](./DEPLOY_WEB.md) — corepack Dockerfile regression audit
