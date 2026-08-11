# Unity Play Verification Checklist (SB-4176)

**Gate:** Human Play-mode verification before Phase 3 Steam / v1 EA ship.  
**Scene:** `unity/CityMajor.Unity/Assets/Scenes/Play.unity`  
**Linear:** [SB-4176](https://linear.app/softblaze/issue/SB-4176)

**Fast smoke first:** [UNITY_PLAY_SMOKE.md](./UNITY_PLAY_SMOKE.md) — open scene → Play → road → zone → buildings → HUD → FPS (**CityMajor → Smoke**).

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
- [ ] Pop line shows growth subline `(+N/mo)` after ~8 sim ticks
- [ ] **Bottom-center R/C/I** demand meters move with sim economy
- [ ] **Happiness** meter (left stack) tracks sim happiness
- [ ] **Hour** line shows `HH:MM ×rush` and changes over time
- [ ] **Era badge** below Pop/Funds HUD shows **Modern** (blue accent) or current sim era

---

## Zoning & roads

- [ ] `1` / `2` / `3` — R / C / I zone paint on LMB drag
- [ ] `5` / `6` / `7` / `8` — Office / Mixed / Ag / Park when era + research unlock (locked otherwise)
- [ ] `0` — erase zones
- [ ] `D` — cycle density Low → Med → High among unlocked levels (Med=T029, High=Industrial+T031; also bottom Zoning toolbar)
- [ ] Bottom **Zoning** toolbar — tier buttons + Low/Med/High; locked density buttons disabled; paints via `CitySimBridge.PaintZone`
- [ ] `X` — bulldoze mode (clears zones, buildings, roads on tile)
- [ ] `4` — road mode; roads appear in overlay + traffic tint after sim ticks
- [ ] RoadPaintTool inspector — optional **Paint Bridge** / **Paint Tunnel** (P1.3) / **Paint Ramp** (P1.5)
- [ ] ZonePaintTool inspector — **Zone Density** 1–3 paints through `CitySimBridge.PaintZone`
- [ ] MMB — camera pan

---

## Panels

- [ ] `R` — Research panel lists techs; enqueue updates progress
- [ ] `H` — Herald panel fetches or shows local template story
- [ ] `C` — Citizen panel shows household L2 rows after population grows
- [ ] `L` — Laws panel shows definition count; toggle **Speed Limits** if loaded
- [ ] `B` — Build panel; select Fire/Police/Hospital; plop on **zoned** tile
- [ ] `P` — Blueprint panel exports CMJR chunk 0x02 header stub
- [ ] `E` — Trade strip shows income/expense/net + RCI demand proxy
- [ ] `F1` — Help overlay lists controls
- [ ] First Play shows onboarding overlay; **Got it** dismisses and does not return

---

## Onboarding & first-run

- [ ] Fresh PlayerPrefs (or delete `citymajor.onboarding.v1`) → welcome overlay appears
- [ ] **Got it** hides overlay; second Play session skips it

---

## Life layers (v1.1 scaffolds)

- [ ] Cosmetic **vehicles** on roads after traffic builds
- [ ] **Pedestrian dots** (green/amber/red tiers) near zones or households
- [ ] `V` — service coverage GL quads
- [ ] `U` — utility stress overlay (power/water strain tint)
- [ ] `T` — edge traffic lines on congested roads
- [ ] `F` — trade friction corridor heat on market-zone boundaries (cyan→magenta)
- [ ] `E` — Economy panel: shortages/surpluses, **partition price min–max**, production flows
- [ ] Cathedral metrics strip shows **Markets N · Spread ×…** when multi-zone
- [ ] Bottom-center **demand strip** shows R/C/I + **goods shortage** bar + **utility stress** row
- [ ] **Event ticker** (top) shows Herald headline from sim metrics (not static placeholder)
- [ ] Zoom camera out (distance ≥ 200) — buildings switch to **box LOD** instanced cubes
- [ ] Buildings **grow in** (scale staging) when new structures appear
- [ ] Constructing buildings render at **partial height** with amber tint
- [ ] Yellow **construction cranes** on low-condition buildings

---

## Time & persistence

- [ ] `Space` — pause; `[`/`]`/`\` (or keypad 1/2/4) — 1×/2×/4× sim speed (`5`/`6`/`7`/`8` are Office/Mixed/Ag/Park)
- [ ] **Save** writes `citymajor.cmjr` to persistentDataPath
- [ ] **Load** restores zones/buildings/funds
- [ ] **Share** copies spectator URL to clipboard
- [ ] Save panel shows **Cloud:** offline / ready / uploaded (Steam stub vs live)
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
| `Forge.Engine.Tests` + `Forge.Game.Tests` | ✅ | `.github/workflows/unity-simcore.yml` | 833 + 110 tests on `ubuntu-latest` |
| Modern GLTF catalog on disk | ✅ | `scripts/verify-unity-gltf-catalog.sh` + GHA | 12 keys; Editor: **CityMajor → Verify GLTF Catalog** |
| Achievements catalog JSON | ✅ | `scripts/verify-achievements-catalog.sh` | Editor: **CityMajor → Verify Achievements Catalog** |
| Rendering parity (box LOD, demand foundation, ticker) | ✅ | Editor menu | **CityMajor → Verify Rendering Parity** |
| Zone era / density gates (U3.4) | ✅ | Editor menu | **CityMajor → Verify Zone Era Gates (U3.4)** |
| Play scene bootstrap + build settings | ✅ | Editor + CLI | **Verify Play Scene Bootstrap**; `./scripts/verify-unity-play-gate.sh` |
| Cathedral Sprint 3 surfaces present | ✅ | Editor + CLI | **Verify Cathedral Surfaces (U3.6)** — HUD/overlays/UXML on disk |
| Play gate batch (all of above) | ✅ | Editor + CLI | **CityMajor → Run Play Gate Batch Checks (U3.6)** · `./scripts/verify-unity-play-gate.sh` |
| Play Mode enter guards | ✅ | Editor `InitializeOnLoad` | Blocks Play if SimCore DLL / SimHost type / bootstrap / build settings fail |
| Web vitest (sim-metrics, event-catalog, news-ticker, play-keyboard) | ✅ | `.github/workflows/unity-simcore.yml` | `pnpm test` in `web/` |
| SimCore → Unity DLL copy | ✅ | Script | `./scripts/build-simcore-for-unity.sh` — **run after every SimHost change** |
| Windows headless player build | ✅ | Script | `./scripts/build-steam-unity.sh` or Windows `scripts\build-steam-unity.bat` / `.ps1` — needs Hub install or `UNITY_PATH`; produces `Build/Steam/windows/CityMajor.exe` |
| Web routes + WebGL smoke | ✅ | `.github/workflows/ci-smoke.yml` | **Web maintenance only** — does not load Unity `Play.unity` |
| Web perf gate (≥30 FPS) | ⚠️ Manual dispatch | `.github/workflows/perf-gate.yml` | R3F `/play`; software renderer often skips — not Unity |
| Unity script compile | ❌ | Unity Editor | Domain reload; MCP `read_console` for errors |
| Panel toggles + control feel | ❌ | **This checklist** | Human or MCP-assisted Play mode |
| Bulldoze / road graph / save round-trip | ❌ | **This checklist** | Requires live sim + UI |
| Steam SDK init + achievements | ❌ | **This checklist** + [INSTALL_STEAMWORKS_NET.md](../steam/INSTALL_STEAMWORKS_NET.md) | Needs Steam client + define |
| SB-4176 sign-off | ❌ | **Human** | Blocks Phase 3 depot upload |

**Agent rule:** lane merge gates ([UNITY_AGENT_DISPATCH.md](./UNITY_AGENT_DISPATCH.md)) are necessary but **not sufficient** — orchestrator still requests human Play verification before cherry-picking the next batch to `feat/unity-port-plan-2026-07-12`.

**U3.6 note:** Automation hardens *prerequisites* and *surface presence*. It does **not** replace human SB-4176 sign-off for feel, panels, save/load, or FPS.

---

## Cathedral Sprint 2/3 subset (agent prep → human sign)

Run **CityMajor → Smoke → Run Smoke Prep** (batch + enter guards), then human Play for:

| # | Surface | Keys / UI | Pass |
|---|---------|-----------|------|
| C1 | Road tiers + bridge/tunnel/ramp | RoadTypeToolbar + ToolMode HUD; reject toast on illegal merge / invalid ramp; inspector flags → `PlaceRoad` | ☐ |
| C2 | Economy truth + partition spread | `E` — goods, chain, min–max spread; Cathedral **Markets · Spread** | ☐ |
| C3 | Congestion + friction overlays | `T` edge traffic; `F` friction corridors | ☐ |
| C4 | Park + density era gates | `8` Park (T112); `D` Med T029 / High Industrial+T031 | ☐ |
| C5 | Commute / O-D readouts | Cathedral HUD `Commute · O-D · Sat` + `Abandoned N` (P2.6) + `Evt tax×…` when events fire (P7.5) | ☐ |
| C6 | No placeholder sim | Console `[CityMajor] SimHost ready` (not placeholder-only Error) | ☐ |

When C1–C6 pass on integration tip → tick SB-4176 Cathedral rows above + sign-off table.

---

## Editor menu

**CityMajor → Smoke → …** — fast Play Mode smoke (see [UNITY_PLAY_SMOKE.md](./UNITY_PLAY_SMOKE.md)).

**CityMajor → Run Play Gate Batch Checks (U3.6)** — SimCore DLL + SimHost type + build settings + bootstrap + catalogs + Cathedral surfaces.

**CityMajor → Open Play Verification Checklist** — opens this doc in the OS default viewer.

**CityMajor → Open Agent Dispatch Doc** — opens parallel-agent playbook for orchestrator.

**CityMajor → Run Preflight Checks** — same diagnostics as batch (alias for agents).

**CityMajor → Verify GLTF Catalog** — asserts modern symlink + 12 shipped GLBs.

**CityMajor → Verify Achievements Catalog** — StreamingAssets achievements JSON.

**CityMajor → Verify Rendering Parity** — box LOD field, camera Distance, demand foundation UXML, EventTicker UXML.

**CityMajor → Verify Zone Era Gates (U3.4)** — Park byte 8 + density Med/High tech gates.

**CityMajor → Verify Play Scene Bootstrap** — `Play.unity` in build settings + `CityMajorBootstrap`.

**CityMajor → Verify Cathedral Surfaces (U3.6)** — Economy/Friction/Traffic/Cathedral HUD types + road/tool UXML.

**CityMajor → Play Gate → Skip Enter Guards (toggle)** — bypass enter-Play blockers (session EditorPrefs).

**CityMajor → Setup Play Scene** — adds `CityMajorBootstrap` if missing.

**CLI:** `./scripts/verify-unity-play-gate.sh` (`STRICT=1` fails on catalog warnings).

---

## Sign-off

| Role | Name | Date | Pass? |
|------|------|------|-------|
| Dev | | | |
| Design | | | |

When all boxes checked → mark **SB-4176** Done; unblock Phase 3 Steam (SB-4180–4181).
