# Wave 17 — Backlog

**Status:** Deferred (post wave-16 integration)  
**Base at doc time:** `9002eea` — `docs(checklist): add Phase 4 wave16 HUD polish and link merge map`  
**Branch:** `feat/wave17-backlog-2026-07-05`  
**Last updated:** 2026-07-05

Wave 17 tracks work intentionally deferred after wave 16 (`a → e`) lands on `feat/wasm-r3f-integration-2026-07-04`. None of these items block PR #1 merge gates; they are follow-up polish, WASM exports, and art pipeline batches.

---

## Deferred items

| # | Item | Scope | Depends on | Notes |
|---|------|-------|------------|-------|
| 1 | **Merge-d panel polishes** | Research / Citizen / Herald / Law slide panels | wave **d** `@ 7331e49` merged | First-pass theming shipped in merge-d; follow-up: loading skeletons, empty-state copy, tech-tree row density, law toggle affordances, Herald error retry UX |
| 2 | **Transport WASM paint** | Bus + rail tile placement in sim | `TransportToolbar.tsx` stubs (wave **a** `@ 7d459fe`) | Toolbar + overlay colors exist; wire `PaintTransit` (or equivalent) WASM export + worker `transit_paint` command — see AGENT_04 Phase 3 |
| 3 | **Meshy civic GLBs** | `svc_modern_01`–`svc_modern_07` | [SB-3730](https://linear.app/softblaze/issue/SB-3730) / [SB-3745](https://linear.app/softblaze/issue/SB-3745) | Manifest slots pending in `scripts/meshy/manifest.json`; Fire/Police/Health/Education build tabs reference these keys |
| 4 | **Empty-city WASM rebuild** | `skipStarterCity` init flag in published bundle | `feat/empty-city-option-2026-07-05` (`26777e3`) | Web + worker pass `skipStarterCity`; requires `dotnet publish` + Docker `BUILD_WASM=1` so preview deploys pick up `Init(worldSize, skipStarterCity)` export |

---

## Suggested apply order

1. **Empty-city WASM rebuild** — unblocks greenfield smoke without stale starter cross  
2. **Transport WASM paint** — completes road-mode transit loop started in wave **a**  
3. **Meshy civic GLBs** — art batch; can run parallel once manifest prompts are frozen  
4. **Merge-d panel polishes** — UI-only; lowest sim risk; stack after integration smoke green

---

## Merge gate (each item)

1. `pnpm exec tsc --noEmit`
2. `pnpm verify:tech-unlocks`
3. Smoke suite green (46+ checks) before push to integration
4. One deploy per integration push — coordinate with deploy agents

---

## Related docs

- [WAVE16_MERGE_MAP.md](./WAVE16_MERGE_MAP.md) — wave 16 rollup (a → e)
- [V1_MERGE_CHECKLIST.md](./V1_MERGE_CHECKLIST.md) — PR #1 blocking gates + wave 16 deferred footnote
- [MESHY_ASSET_CATALOG.md](./MESHY_ASSET_CATALOG.md) — civic `svc_modern_*` taxonomy
- [WEB_V1_SCOPE.md](./WEB_V1_SCOPE.md) §11 — empty-city dev toggle spec
