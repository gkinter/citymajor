# Unity Play Verification Checklist (SB-4176)

**Gate:** Human Play-mode verification before Phase 3 Steam / v1 EA ship.  
**Scene:** `unity/CityMajor.Unity/Assets/Scenes/Play.unity`  
**Linear:** [SB-4176](https://linear.app/softblaze/issue/SB-4176)

---

## Prerequisites

- [ ] Unity **6000.5+** with **URP** project opened at `unity/CityMajor.Unity`
- [ ] `./scripts/build-simcore-for-unity.sh` run after any `SimHost` change (**required this commit — bulldoze + road graph rebuild**)
- [ ] `Assets/Art/Gltf/modern` symlink valid → `web/public/assets/gltf/modern` (12 GLBs)
- [ ] Domain reload after script/DLL changes (restart Editor if MCP/console wedged)

---

## Bootstrap & sim core

- [ ] Enter **Play** — no console errors (red)
- [ ] Console shows `[CityMajor]` sim init, not placeholder-only mode
- [ ] **Pop / Funds** HUD updates when painting zones (not static sine placeholder)
- [ ] **Bottom-center R/C/I** demand meters move with sim economy
- [ ] **Happiness** meter (left stack) tracks sim happiness
- [ ] **Hour** line shows `HH:MM ×rush` and changes over time

---

## Zoning & roads

- [ ] `1` / `2` / `3` — R / C / I zone paint on LMB drag
- [ ] `0` — erase zones
- [ ] `X` — bulldoze mode (clears zones, buildings, roads on tile)
- [ ] `4` — road mode; roads appear in overlay + traffic tint after sim ticks
- [ ] MMB — camera pan

---

## Panels

- [ ] `R` — Research panel lists techs; enqueue updates progress
- [ ] `H` — Herald panel fetches or shows local template story
- [ ] `C` — Citizen panel shows household L2 rows after population grows
- [ ] `L` — Laws panel shows definition count; toggle **Speed Limits** if loaded
- [ ] `B` — Build panel; select Fire/Police/Hospital; plop on **zoned** tile
- [ ] `E` — Trade stub shows monthly net
- [ ] `F1` — Help overlay lists controls

---

## Life layers (v1.1 scaffolds)

- [ ] Cosmetic **vehicles** on roads after traffic builds
- [ ] **Pedestrian dots** (green/amber/red tiers) near zones or households
- [ ] `V` — service coverage GL quads
- [ ] `T` — edge traffic lines on congested roads
- [ ] Buildings **grow in** (scale staging) when new structures appear
- [ ] Yellow **construction cranes** on low-condition buildings

---

## Time & persistence

- [ ] `Space` — pause; `5`/`6`/`7` — 1×/2×/4× sim speed
- [ ] **Save** writes `citymajor.cmjr` to persistentDataPath
- [ ] **Load** restores zones/buildings/funds
- [ ] **Share** copies spectator URL to clipboard
- [ ] **Approval** meter (top-right, below budget) tracks mayor approval

---

## Performance smoke

- [ ] Play 2+ minutes — FPS stays above ~30 on target machine
- [ ] If stutter: confirm pedestrian layer drops first (design intent)

---

## Editor menu

**CityMajor → Open Play Verification Checklist** — opens this doc in the OS default viewer.

**CityMajor → Setup Play Scene** — adds `CityMajorBootstrap` if missing.

---

## Sign-off

| Role | Name | Date | Pass? |
|------|------|------|-------|
| Dev | | | |
| Design | | | |

When all boxes checked → mark **SB-4176** Done; unblock Phase 3 Steam (SB-4180–4181).
