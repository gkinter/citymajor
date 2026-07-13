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
- [ ] **Era badge** below Pop/Funds HUD shows **Modern** (blue accent) or current sim era

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
- [ ] Save → **City on Disk** achievement toast appears
- [ ] **Approval** meter (top-right, below budget) tracks mayor approval

---

## Performance smoke

- [ ] Play 2+ minutes — FPS stays above ~30 on target machine
- [ ] If stutter: confirm pedestrian layer drops first (design intent)

---

## Automation (CI vs human Play gate)

Unity v1 quality is **not** fully automated in CI today. Use this split so agents do not treat web smoke as a Unity substitute.

| Check | Automated? | Where | Notes |
|-------|------------|-------|-------|
| `Forge.SimCore` compile | ✅ | Local / future GHA | `dotnet build src/Forge.SimCore/Forge.SimCore.csproj` |
| SimCore → Unity DLL copy | ✅ | Script | `./scripts/build-simcore-for-unity.sh` — **run after every SimHost change** |
| Windows headless player build | ✅ | Script | `./scripts/build-steam-unity.sh` — needs `UNITY_PATH`; produces `Build/Steam/windows/CityMajor.exe` |
| Web routes + WebGL smoke | ✅ | `.github/workflows/ci-smoke.yml` | **Web maintenance only** — does not load Unity `Play.unity` |
| Web perf gate (≥30 FPS) | ⚠️ Manual dispatch | `.github/workflows/perf-gate.yml` | R3F `/play`; software renderer often skips — not Unity |
| Unity script compile | ❌ | Unity Editor | Domain reload; MCP `read_console` for errors |
| Panel toggles + control feel | ❌ | **This checklist** | Human or MCP-assisted Play mode |
| Bulldoze / road graph / save round-trip | ❌ | **This checklist** | Requires live sim + UI |
| Steam SDK init + achievements | ❌ | **This checklist** + [INSTALL_STEAMWORKS_NET.md](../steam/INSTALL_STEAMWORKS_NET.md) | Needs Steam client + define |
| SB-4176 sign-off | ❌ | **Human** | Blocks Phase 3 depot upload |

**Agent rule:** lane merge gates ([UNITY_AGENT_DISPATCH.md](./UNITY_AGENT_DISPATCH.md)) are necessary but **not sufficient** — orchestrator still requests human Play verification before cherry-picking the next batch to `feat/unity-port-plan-2026-07-12`.

---

## Editor menu

**CityMajor → Open Play Verification Checklist** — opens this doc in the OS default viewer.

**CityMajor → Open Agent Dispatch Doc** — opens parallel-agent playbook for orchestrator.

**CityMajor → Setup Play Scene** — adds `CityMajorBootstrap` if missing.

---

## Sign-off

| Role | Name | Date | Pass? |
|------|------|------|-------|
| Dev | | | |
| Design | | | |

When all boxes checked → mark **SB-4176** Done; unblock Phase 3 Steam (SB-4180–4181).
