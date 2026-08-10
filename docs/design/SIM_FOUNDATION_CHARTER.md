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

### WP-A — Traffic fidelity ✅ (v1 deepen)

- ✅ `LifeSimMath.RushHourMultiplier` on lite O-D
- ✅ Unity lite zones **128** (`TrafficLiteZoneCountUnity`); WASM stays 64
- ✅ `SimHostInitOptions.UseFullTraffic` compiles full `TrafficSystem` into SimCore (default off)
- Longer term: tune full-FW cost

### WP-B — Economy & trade observability ✅ (partial)

- ✅ Employment + trade balance on snapshot
- ✅ Goods shortage/surplus top-5 + indices on `WorldState`/`SimSnapshot` → Unity scalars
- ✅ Herald buckets consume shortage index (`EconomyShortage` / block prosperity)

### WP-C — City-building closed loop ✅

- ✅ Law aggregate effects → monthly budget + `LawTrafficCapacityMult`
- ✅ Multi-day construction (Condition progress) + law spawn/speed multipliers
- ✅ `ConstructingBuildingCount` on snapshot

### WP-D — Characterization & perf ✅

- ✅ `Forge.SimCore.Tests` — foundation + goods + laws + traffic + construction + tick budget
- ✅ Tick budget doc + CI gate @ 8 Hz — [`SIM_TICK_BUDGET.md`](./SIM_TICK_BUDGET.md); local target ≤10 ms (SB-3685), CI &lt;25 ms
- ✅ V1-scale tick characterization (8.2K HH / 3.2K buildings on 256×256) via `SimHost.SeedV1ScaleCity()` — `TickBudgetTests.V1Scale_256x256_NearFullPools_MedianFrameTick_UnderCiBudget`

### WP-E — Spec stretch (cathedral, opt-in)

Only after A–D are green. V1-scale pool characterization (WP-D) establishes baseline tick cost before opt-in cathedral traffic:

- ✅ Full Frank-Wolfe + MNL opt-in (`UseFullTraffic`, Unity `CITYMAJOR_FULL_TRAFFIC=1`, `FullTrafficTests`) — default remains lite 128.
- ✅ Inter-zone trade friction matrix (`SIMULATION_ARCHITECTURE` §5) — `TradeCoefficients` 16×16, price-aware greedy matching, `InterZoneTradeVolume` / `MeanInterZoneFriction` on snapshot.
- ✅ L0 utility grid balance (power/water partitions) — `UtilityPartitionBalance`, `ServiceSystem.L0Tick`, snapshot export.
- ✅ v1-scale tick characterization (10K HH / 5K buildings) — `SeedV1ScaleCity` + `TickBudgetTests`.
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
| 2026-07-16 | `bbffa11`+fix | Goods imbalance export · law→budget/traffic · full TrafficSystem option + Unity 128-zone lite · netstd2.1 Array.Clear fix |
| 2026-07-16 | tick-budget | `TickBudgetTests` + `SIM_TICK_BUDGET.md` — CI gate &lt;25 ms @ 8 Hz; starter-city characterization |
| 2026-07-16 | wave-2 | Herald←shortage · multi-day construction+law growth · tick budget — merged |
| 2026-07-18 | wave-3 | Inter-zone trade friction · L0 utility balance · v1-scale tick budget — merged |
| 2026-07-18 | wave-4 | CitySimState bridge · foundation HUD · desktop full traffic opt-in — merged |
| 2026-08-10 | wave-9 | Web BudgetPanel foundation · Economy E key · NewsTicker tests — merged |
| 2026-08-10 | wave-8 | EventTicker herald · building box LOD · CI vitest — merged |
| 2026-08-09 | wave-7 | Unity demand foundation · Herald LLM context · sim-metrics tests — merged |
