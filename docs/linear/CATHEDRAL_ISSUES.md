# CityMajor Sim Cathedral — Linear Issue Templates

> **Project:** CityMajor Sim Cathedral (create in Linear)  
> **Initiative:** CityMajor — Simulation Cathedral (sibling to SB-3708)  
> **Charter:** [`CATHEDRAL_PROGRAM.md`](../design/CATHEDRAL_PROGRAM.md)  
> **Usage:** Create epics manually or via Linear API; map SB-4xxx placeholders below.

---

## Epic P1 — Infrastructure Truth (SB-4200)

**Description:** Road types, graph v2, intersections, full traffic on typed edges, congestion export. Blocks P3/P4 traffic coupling.

**Labels:** `pillar:infrastructure`, `sim-core`, `unity`, `web`

| ID | Title | Description | Acceptance criteria | Labels | Depends |
|----|-------|-------------|---------------------|--------|---------|
| SB-4201 | P1.1 Road tier in PlaceRoad API | Thread tier through SimHost, RoadFlags, WASM, web toolbar | Tier 0 vs 2 changes edge level after rebuild; `RoadTierGraphTests` pass | `pillar:infrastructure`, `sim-core` | — |
| SB-4202 | P1.2 Graph builder v2 (intersection segments) | Collapse degree-2 chains; type 4-way/T-junction nodes | Cross pattern has 1 intersection node; capacity from tier table | `pillar:infrastructure`, `sim-core` | SB-4201 |
| SB-4203 | P1.3 Bridge/tunnel/one-way flags | Bits 6–7 affect cost/capacity; one-way enforcement | Bridge over water tile works; illegal reverse blocked | `pillar:infrastructure`, `sim-core` | SB-4202 |
| SB-4204 | P1.4 Highway on/off ramps | `RoadNode.NodeType.Ramp`; ramp-only highway access | No illegal highway merges from surface streets | `pillar:infrastructure`, `sim-core` | SB-4202 |
| SB-4205 | P1.5 Full TrafficSystem default (Unity) | Opt-in full FW becomes default on desktop; lite on WASM | Home→work O-D on typed edges; BPR on capacity | `pillar:infrastructure`, `sim-core`, `unity` | SB-4202 |

---

## Epic P2 — Land & Housing (SB-4210)

**Description:** Zone types beyond R/C/I, density brush, housing market, rent burden, decline UX.

**Labels:** `pillar:housing`, `sim-core`, `unity`, `web`

| ID | Title | Description | Acceptance criteria | Labels | Depends |
|----|-------|-------------|---------------------|--------|---------|
| SB-4211 | P2.1 Extended zone types (modern subset) | Office, mixed, park, ag zones paintable | Each type affects RCI + spawn rules per AGENT_05 | `pillar:housing`, `sim-core` | — |
| SB-4212 | P2.2 Density brush | Low/med/high affects spawn tier + HH cap | Same zone type, different density → different building tier | `pillar:housing`, `sim-core`, `unity` | SB-4211 |
| SB-4213 | P2.3 Housing market + rent burden | rent = f(land_value, supply, shortage); per-HH `rent_burden` | Shortage increases mean rent; exported on snapshot | `pillar:housing`, `sim-core` | SB-4211 |
| SB-4214 | P2.4 Affordability → migration + events | rent_burden threshold triggers out-migration + `housing_crisis` | Herald fires only when snapshot predicate true | `pillar:housing`, `pillar:governance`, `sim-core` | SB-4213 |
| SB-4215 | P2.6 Decline/abandonment UX | Negative demand + pollution/crime → abandonment | Buildings enter abandoned state visible in UI | `pillar:housing`, `unity`, `web` | SB-4211 |

---

## Epic P3 — Economy & Trade (SB-4220)

**Description:** Expose 45-goods economy, market zones, friction matrix, traffic→goods coupling.

**Labels:** `pillar:economy`, `sim-core`, `unity`, `web`

| ID | Title | Description | Acceptance criteria | Labels | Depends |
|----|-------|-------------|---------------------|--------|---------|
| SB-4221 | P3.1 Goods panel (shortage/surplus) | Top 5 goods + prices in HUD | Panel matches `EconomySystem` indices | `pillar:economy`, `unity`, `web` | — |
| SB-4222 | P3.2 Production chain visibility | Building → inputs/outputs drill-down | Click building shows chain edges | `pillar:economy`, `unity` | SB-4221 |
| SB-4223 | P3.3 Market zone partition pricing | 8–16 zones with distinct prices on snapshot | Two zones show price delta after trade shock | `pillar:economy`, `sim-core` | SB-4221 |
| SB-4224 | P3.4 Inter-zone friction UI | Visualize 16×16 friction matrix (read-only v1) | Overlay shows high-friction corridors | `pillar:economy`, `web` | SB-4223 |
| SB-4225 | P3.6 Traffic delay → goods delivery | Congestion increases industrial input latency | Highway upgrade reduces goods delay metric | `pillar:economy`, `pillar:infrastructure`, `sim-core` | SB-4205, SB-4223 |

---

## Epic P4 — Population Life (SB-4230)

**Description:** Home/work O-D truth, commute satisfaction, employment HUD, L2 scale export.

**Labels:** `pillar:population`, `sim-core`, `unity`, `web`

| ID | Title | Description | Acceptance criteria | Labels | Depends |
|----|-------|-------------|---------------------|--------|---------|
| SB-4231 | P4.1 Home/work building IDs on commuters | Replace gravity O-D with building-pair assignment | 90%+ commuters have valid home/work IDs | `pillar:population`, `sim-core` | SB-4205 |
| SB-4232 | P4.2 Commute time → satisfaction | Graph travel time feeds satisfaction formula | Longer commute lowers satisfaction ceteris paribus | `pillar:population`, `sim-core` | SB-4231 |
| SB-4233 | P4.3 Unemployment by zone HUD | Export + display zone-level unemployment | HUD updates after industrial zone bulldoze | `pillar:population`, `unity`, `web` | SB-4231 |
| SB-4234 | P4.4 L2 export at scale (50–100 HH) | Snapshot + map pick for citizen stories | Click citizen shows job, commute, rent burden | `pillar:population`, `unity` | SB-4232, SB-4213 |

---

## Epic P5 — Services (SB-4240)

**Description:** Utilities L0, emergency response, phased fire/EMS depth.

**Labels:** `pillar:services`, `sim-core`, `unity`, `web`

| ID | Title | Description | Acceptance criteria | Labels | Depends |
|----|-------|-------------|---------------------|--------|---------|
| SB-4241 | P5.1 Utilities L0 balance HUD | Power/water partition stress visible | Blackout tile shows when demand > supply | `pillar:services`, `unity`, `web` | — |
| SB-4242 | P5.2 Emergency response time model | Distance + traffic → fire/EMS arrival | Response time increases under congestion | `pillar:services`, `sim-core` | SB-4205 |
| SB-4243 | P5.3 Fire spread v1 | Basic fire propagation + hydrant coverage | Uncovered zone fire spreads; hydrant stops | `pillar:services`, `sim-core` | SB-4242 |

---

## Epic P6 — Governance (SB-4250)

**Description:** Law effects, event→state bridge, Herald grounded in sim.

**Labels:** `pillar:governance`, `sim-core`, `unity`, `web`

| ID | Title | Description | Acceptance criteria | Labels | Depends |
|----|-------|-------------|---------------------|--------|---------|
| SB-4251 | P6.1 Law effects on budget/traffic/spawn | Law toggles apply documented multipliers | Enable law changes budget line + traffic cap | `pillar:governance`, `sim-core` | — |
| SB-4252 | P6.2 ApplyEventEffectsToState bridge | Wire desktop event bridge in WASM + Unity | Active event modifies world state same as desktop | `pillar:governance`, `sim-core` | — |
| SB-4253 | P6.3 Herald snapshot predicates | Herald buckets require snapshot fields true | False-positive Herald headlines eliminated in test city | `pillar:governance`, `unity`, `web` | SB-4252, SB-4221 |

---

## Epic P7 — Client Truth Layer (SB-4260)

**Description:** Snapshot v2 contract, characterization tests, acceptance suite, gap matrix CI.

**Labels:** `pillar:client`, `sim-core`, `unity`, `web`, `acceptance-test`

| ID | Title | Description | Acceptance criteria | Labels | Depends |
|----|-------|-------------|---------------------|--------|---------|
| SB-4261 | P7.1 SIM_SNAPSHOT_V2.md | Document fields, cadence, ownership per pillar | Doc reviewed; linked from WASM_SIM_BRIDGE | `pillar:client` | — |
| SB-4262 | P7.2 Per-pillar characterization tests | Test suite pins each pillar's core behavior | CI runs Cathedral test filter; skipped tests tracked | `pillar:client`, `acceptance-test`, `sim-core` | — |
| SB-4263 | P7.3 Acceptance Playwright + Unity menu | End-to-end checks for road tier + goods panel | Smoke passes on preview deploy | `pillar:client`, `acceptance-test`, `web`, `unity` | SB-4201, SB-4221 |
| SB-4264 | P7.4 Gap matrix CI regeneration | Script diffs spec exports vs SIM_V1_GAP_MATRIX | PR fails if gap matrix stale | `pillar:client`, `acceptance-test` | SB-4261 |

---

## Suggested Linear setup

1. Create project **CityMajor Sim Cathedral** under CityMajor initiative.
2. Create 7 epics (SB-4200–4260) with descriptions from this file.
3. Create child issues per table; set dependencies in Linear.
4. Link each epic to OpenSpec domain spec under `openspec/specs/`.
5. First sprint: SB-4201 + SB-4202 (OpenSpec change `001-road-tier-and-graph-v2`).

## Label taxonomy

| Label | Use |
|-------|-----|
| `pillar:infrastructure` | P1 |
| `pillar:housing` | P2 |
| `pillar:economy` | P3 |
| `pillar:population` | P4 |
| `pillar:services` | P5 |
| `pillar:governance` | P6 |
| `pillar:client` | P7 |
| `sim-core` | C# sim changes |
| `unity` | Unity client |
| `web` | Next.js / WASM web |
| `acceptance-test` | Playwright / CI gates |
