# CityMajor — Simulation Foundation Charter

**Status:** Active engineering program (sim-first)  
**Date:** 2026-07-16  
**Branch:** `feat/unity-port-plan-2026-07-12`  
**Canonical roadmap:** [Linear — CityMajor Canonical Roadmap](https://linear.app/softblaze/document/citymajor-canonical-roadmap-v1-full-vision-eb7277adafd0) · [SB-3708](https://linear.app/softblaze/issue/SB-3708)  
**Specs:** [`SIMULATION_ARCHITECTURE.md`](./SIMULATION_ARCHITECTURE.md) · [`SIM_V1_GAP_MATRIX.md`](./SIM_V1_GAP_MATRIX.md) · [`MASTER_GAME_CONCEPT.md`](./MASTER_GAME_CONCEPT.md) · [`CTO_IMPROVEMENT_ROADMAP_2026-07.md`](./CTO_IMPROVEMENT_ROADMAP_2026-07.md)

---

## 1. Honest inventory (do not greenfield)

The deep sim **already exists** as ~10k LOC under `src/Forge.Game/Simulation/`:

| System | LOC (approx) | Wired in `SimHost`? | Fidelity today |
|--------|--------------|---------------------|----------------|
| `EconomySystem` | ~1,016 | Daily + monthly | Leontief I-O, 45 goods, RCI demand — **live** |
| `PopulationSystem` | ~1,189 | Staggered tick + monthly | Employment, satisfaction, migration — **live** |
| `TrafficSystem` | ~933 | **No** — excluded from `Forge.SimCore.csproj` | Full 500-zone FW + MNL — **desktop only** |
| `WasmTrafficLite` | ~520 | Yes @ lite schedule | 64-zone FW, gravity O-D — **degraded** |
| `ZoneGrowthSystem` | ~895 | Daily + monthly upgrades | RCI-driven spawn — **live** |
| `ServiceSystem` | ~1,010 | Daily + monthly | Coverage maps — **live** |
| `BudgetSystem` | ~488 | Monthly | Tax + expense ledger — **live** |
| `TradeSystem` | ~352 | Monthly global market | Auto import/export — **live**; bilateral routes shallow |
| `PoliticsSystem` | ~718 | Daily + monthly | Approval — **live**; some scores hardcoded |
| `EventSystem` | ~941 | Daily | Effects → happiness/approval only |
| `ResearchSystem` | ~834 | Monthly | Tech queue + era — **live** |
| `LawSystem` | ~271 | Catalog + toggles | Effects still shallow |
| `CulturalDNASystem` | ~784 | Yearly | Preset + drift — **live** |
| `ProductionChain` | ~422 | Via Economy | Chains run inside economy daily |

**Pools already at v1 scale in SimHost:** 10,240 households / 5,120 buildings.

**CTO roadmap takeaway:** “The sim is still ahead of the shipped player experience.” Foundation work means **deepen + prove + export**, not rewrite.

---

## 2. Goal of this program

Build a **detailed, accurate, extensive** simulation foundation that clients (Unity, WASM) can trust:

1. **Economy** — goods markets, production chains, RCI, budget, trade balance are coherent month-to-month.
2. **Traffic** — congestion responds to land use + time-of-day; tile heat is assignment-truthful.
3. **City building** — zone growth, services, land value, upgrades form a closed demand→supply→congestion loop.
4. **Population** — employment matching and satisfaction drive migration and politics.
5. **Contracts** — characterization tests pin behavior so deepen passes don’t silently regress.

Unity HUD / Steam / CI are **consumers** of this foundation — not the foundation.

---

## 3. Work packages (ordered)

### WP-A — Traffic fidelity (in progress)

- Apply `LifeSimMath.RushHourMultiplier` to lite O-D volume (morning/evening peaks).
- Raise Unity-native lite zone count via config (optional path toward 128–256 zones).
- Longer term: compile `TrafficSystem` into SimCore behind `SimHostInitOptions.UseFullTraffic` for desktop Unity (keep lite for WASM).

**Exit:** Congestion heat rises at rush on arterials between R and C/I; characterization test asserts peak > off-peak mean tile traffic.

### WP-B — Economy & trade observability

- Persist `EmploymentRate`, `UnemploymentRate`, `TradeBalance`, `MonthlyExportValue`, `MonthlyImportCost` on `WorldState` / `SimSnapshot`.
- Wire politics `ServiceScore` / `SafetyScore` from real `ServiceSystem` coverage (replace constants).
- Export top goods shortage/surplus indices for HUD/Herald.

**Exit:** Snapshot carries employment + trade; tests assert monthly trade changes city funds.

### WP-C — City-building closed loop

- Construction progress states surfaced in snapshot (`State`, condition).
- Service coverage scores feed zone growth / abandonment.
- Law toggles apply measurable budget/approval/traffic modifiers.

**Exit:** Paint R → buildings grow → jobs → traffic → approval moves without UI hacks.

### WP-D — Characterization & perf

- Golden tests: seed city → N months → assert RCI, employment, traffic, funds within bands.
- Tick budget doc at 10K HH / 5K buildings @ 8 Hz (native).

**Exit:** CI fails on economy/traffic regressions.

### WP-E — Spec stretch (cathedral, opt-in)

Only after A–D are green:

- Full Frank-Wolfe 500 zones + MNL in SimCore.
- Inter-zone trade friction matrix (`SIMULATION_ARCHITECTURE` §5).
- L0 utility grid balance (power/water partitions).
- Economic Control Spectrum slider ([SB-3729](https://linear.app/softblaze/issue/SB-3729)) — **v2**.

---

## 4. Non-goals (explicit)

- New Unity panels, Steam depot, achievement CI, agent orchestration docs.
- Photoreal traffic / agent-based cars (cosmetic vehicles stay snapshot-driven).
- Rewriting `EconomySystem` from scratch.
- Multiplayer / regional open world (v2/v3).

---

## 5. How agents work this program

| Lane | Owns |
|------|------|
| **SimCore** | `src/Forge.Game/Simulation/**`, `src/Forge.SimCore/**`, `src/Forge.SimWasm/WasmTrafficLite.cs`, `SimSnapshot`, tests |
| **Client** | Only consumes new snapshot fields — no sim math in Unity/R3F |

One worktree per WP. Merge gate: `dotnet test tests/Forge.Engine.Tests` + `./scripts/build-simcore-for-unity.sh`.

---

## 6. Progress log

| Date | Tip | Note |
|------|-----|------|
| 2026-07-16 | charter | Program opened; WP-A/B started (rush OD + employment/trade on snapshot) |
