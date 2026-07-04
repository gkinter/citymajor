# CityMajor Gameplay Loop Audit — v1.0 vs Design Intent

**Scope:** `docs/design/MASTER_GAME_CONCEPT.md`, web `/play` (`PlayClient`, `ZoningToolbar`, `HeraldPanel`, `ResourcesHud`, `SaveLoadControls`), WASM sim (`Forge.SimWasm`), `base/data/events/events.json`, era system (`WasmEraDeriver`, `ERA_ARC_DESIGN_V2.md`).

**Verdict:** The simulation backbone is substantially ahead of the player-facing loop. WASM runs economy, zone growth, events (80 definitions), politics, budget, and era derivation — but the web play experience is still a **3D zoning sandbox** with optional, disconnected Herald stories. The mayor fantasy from the GDD is largely missing.

---

## 1. Mayor Fantasy — What's Missing

### Design intent (`MASTER_GAME_CONCEPT` §3)

A casual session opens with **pulse check**: budget, approval, RCI bars, morning Herald headline → respond to demand → build services → **handle events with choices** → tweak policy → watch growth → save and plan research.

### Current web reality

| GDD pillar | Implemented? | Evidence |
|---|---|---|
| RCI demand bars | **No** | `EconomySystem` + `ZoneGrowthSystem` compute demand; `SimSnapshotDto` exports only tick/pop/funds/era/buildings/zones/roads |
| Approval / happiness HUD | **No** | Desktop `HudPanel.cs` shows both; web `ResourcesHud` shows Pop/Funds/Tick/Era only |
| Budget panel / tax levers | **No** | `BudgetSystem` runs in WASM; no web panel |
| Advisors | **No** | Only a line in `narrative-templates.ts` default story |
| Goals / milestones | **No** | `ERA_ARC_DESIGN_V2.md` §5–6 documents this gap explicitly |
| Failure states | **Sim only** | `BudgetSystem.IsBankrupt` logs a console warning in desktop; no player-facing game-over or recovery UX |
| Policy / council choices | **Display only** | `HeraldPanel` renders options but has **no click handlers** — choices don't affect sim |
| Elections / factions | **Sim stub** | `PoliticsSystem` in WASM; zero web UI |

**Mayor fantasy gap:** The player paints zones and watches buildings appear. They are not a mayor making tradeoffs under pressure — they're a city painter with a newspaper button.

---

## 2. Herald AI Integration Depth

### Design intent

- LLM reads structured sim state → grounded stories (factory owner letters, faction debates, Daily Herald editorials).
- 3–5 stories per game-day from real events.
- Player responds to crises via council options that change simulation.

### Current implementation

```
PlayClient.fetchHeraldStory()
  → deriveNarrativeBucket(healthcareCoverage)  // M0 heuristic only
  → POST /api/narrative/event
  → narrativeFromBucket()  // static templates, source: "template"
  → HeraldPanel (read-only)
```

**Critical gaps:**

1. **`events.json` is not connected to Herald.** WASM loads 80 event definitions (`WasmSimHost` → `EventSystem.LoadDefinitionsFromJson`) and runs `DailyTick` / `UpdateEvents`, but:
   - Active events are **not exported** in `GetStatus()` or `SimSnapshotDto`
   - Herald never receives event type, phase, severity, or location
   - Narrative buckets (`healthcare_low`, `pollution_spike`, etc.) are **hardcoded templates**, not mapped from `events.json` ids (`fire`, `flood`, `strike`, etc.)

2. **No LLM.** `NarrativeEventResponseSchema` requires `source: "template"`. Quota system (`narrative-quota.ts`) gates daily pulls — appropriate for SaaS, but there's no path to sim-grounded generation.

3. **Manual, not ambient.** Herald opens via `HeraldButton` on demand. GDD describes opening the game to a **morning headline** — not a quota-gated side panel.

4. **Choices are inert.** Council options show `label` + `tradeoff` but never dispatch sim commands or call an API.

5. **Weak sim coupling.** `deriveNarrativeBucket` only checks `healthcareCoverage < 0.3`; seven other buckets in templates are unreachable from play state.

---

## 3. Era Arc: Frontier → Industrial — Player-Visible Progression?

### Design intent

- Earned through research, population, industry — not a menu toggle.
- 2-year transition period, special Herald edition, palette shift, new tensions.
- Industrial era: RCI bars appear, zoning essential, pollution/strikes/cholera.

### WASM implementation (`WasmEraDeriver` + `WasmConfig`)

- Era 0→1 (Frontier→Industrial) via **tick threshold** (1,200 ticks) **OR** research proxies (25 RP / 3 heavy industry / pop 400 + educated 80).
- `ResourcesHud` shows era badge with palette from `hudEraBadgeStyle`.

### Player-visible gaps

| Expected signal | Status |
|---|---|
| Era badge in HUD | ✅ Minimal — text label only |
| Progress toward next era | ❌ No meter, no checklist |
| Transition fanfare | ❌ Silent flip when `state.Era` increments |
| Visual palette shift | ⚠️ Building `TypeId` era ranges exist in renderer; player may not notice |
| Industrial gameplay shift (RCI, zoning scale) | ❌ RCI not shown; zoning works but growth feedback is invisible |
| Herald era beats | ❌ Templates are era-agnostic; `ERA_ARC_DESIGN_V2` §4 beats unused |
| Landmark / quest gates | ❌ Proposed in era design doc, not built |

**Frontier→Industrial feels like a debug counter**, not a narrative milestone. Tick-based era advance can fire before the player understands zoning or demand.

---

## 4. RCI Demand / Zone Growth Feedback Loop

### Sim (works)

- `EconomySystem.CalculateRCIDemand()` → `ResidentialDemand`, `CommercialDemand`, `IndustrialDemand`
- `ZoneGrowthSystem.Tick()` uses demand + desirability + road access + services to spawn/abandon buildings on zoned tiles
- Player zones via `ZoningToolbar` → `zone_paint` → WASM `PaintZone`

### UX (broken loop)

```
Player zones tile → ??? → building appears (maybe)
```

**Missing feedback:**

- No R/C/I demand bars (GDD's primary city-builder readout)
- No explanation why a zone grows or stagnates (no road access? negative demand? no services?)
- No land-value or desirability overlay
- No abandoned-building signal when demand turns negative
- `ZoneOverlay` shows color tint only — no growth pulse, no "high demand" highlight
- `FpsHud` is dev diagnostics (FPS, LOD, picked tile) — not player-facing economy

**Result:** The core SimCity loop ("R bar maxed → zone residential east of downtown → extend utilities") is **unplayable by design** because the player cannot read demand.

---

## 5. Onboarding — First 15 Minutes

### Design intent (`AGENT_08_UI_UX`, `ERA_ARC_DESIGN_V2`)

- Guided first 30 minutes: roads → zone → fire station → power.
- Progressive disclosure; contextual tooltips; skippable tutorial.
- Frontier slice: intimate placement, bootstrap scarcity, first health crisis.

### Current `/play` experience

1. Land on full-screen 3D city (WASM seeds starter city with roads + zones + buildings).
2. No main menu wizard, no city name, no difficulty, no scenario.
3. No tutorial overlay, no objectives, no tooltips on zoning tools.
4. `SaveLoadControls` available immediately; Herald quota may confuse new users.
5. `FpsHud` exposes WASM/procedural source and tile coords — developer chrome.

**First 15 minutes:** Player experiments with zoning paint and speed controls. No guided arc from "frontier settlement" to "first industrial milestone." Starter city pre-solves bootstrap — undermining Frontier intimacy from the GDD.

---

## 6. Save / Load — Loop Position

**Works:** `SaveLoadControls` snapshots `SimSnapshot` (tick, pop, funds, era, buildings, zones, roads) to `/api/saves` with tier slot limits.

**Gaps:**

- Save slots show name + timestamp only — no era, pop, or thumbnail preview (GDD `08-game-feel-audit` gap).
- No autosave indicator.
- Load doesn't restore Herald narrative context or active events (events not in snapshot).
- No "continue mayor's term" framing — feels like file I/O, not campaign persistence.

---

## 7. Fifteen Concrete v1.1 Improvements (Ranked Impact / Effort)

| Rank | Improvement | Impact | Effort | Notes |
|:---:|---|:---:|:---:|---|
| **1** | **RCI demand bars in HUD** — export `residentialDemand` / `commercialDemand` / `industrialDemand` from WASM `GetStatus`, render classic R/C/I meters | ★★★★★ | S | Unblocks core city-builder loop; sim already computes values |
| **2** | **Wire Herald to active sim events** — export `activeEvents[]` from WASM; map `TypeId` → headline template from `events.json` descriptions | ★★★★★ | M | Makes 80 events player-visible without LLM |
| **3** | **Clickable Herald council options** — dispatch sim commands (budget line items, law toggles, or event response stubs) | ★★★★☆ | M | Closes "choices don't matter" gap |
| **4** | **Auto-Herald on event spawn** — push toast/slide-in when `EventSystem` enters `Active` phase; optional morning digest on session start | ★★★★☆ | M | Matches GDD "read the headline first" |
| **5** | **Era progress meter (Frontier→Industrial)** — show pop/RP/industry checklist + % toward gates from `WasmConfig` | ★★★★☆ | S | Uses existing `WasmEraDeriver` thresholds |
| **6** | **Approval + happiness + monthly budget in `ResourcesHud`** — extend snapshot DTO | ★★★★☆ | S | Mirrors desktop `HudPanel` essentials |
| **7** | **Guided onboarding: 5-step overlay** — "Zone residential → watch growth → open Herald → save city → reach 500 pop" | ★★★★☆ | M | No full tutorial system needed |
| **8** | **Zone growth feedback** — tooltip on picked tile: demand, road access, desirability, growth chance | ★★★☆☆ | M | Teaches why zones stall |
| **9** | **Bankruptcy / low-approval warnings** — modal when `IsBankrupt` or approval &lt; 30% for 3 months | ★★★☆☆ | M | Introduces failure state without full game-over |
| **10** | **News ticker** — scroll recent event names from sim (even without LLM) | ★★★☆☆ | S | Ambient mayor feel, low cost |
| **11** | **Era transition fanfare** — 3s modal + Herald special edition when era increments | ★★★☆☆ | S | Addresses `ERA_ARC_DESIGN_V2` "no triumphant moment" |
| **12** | **Expand `deriveNarrativeBucket`** — use WASM exports: pollution, crime, budget deficit, RCI extremes | ★★★☆☆ | M | Makes template Herald reactive to sim |
| **13** | **Service coverage overlay toggle** — fire/health/education radius from `ServiceSystem` | ★★★☆☆ | L | GDD session step 3 ("check coverage overlay") |
| **14** | **Frontier starter: empty map option** — disable pre-seeded city; player builds from scratch | ★★☆☆☆ | M | Aligns with Era 1 intimate bootstrap |
| **15** | **LLM Herald path (Founder tier)** — replace templates when quota allows; pass structured event payload | ★★★★★ | L | Full GDD vision; depends on #2 |

**Recommended v1.1 sprint:** #1, #5, #6, #7, #2, #4 (two weeks of focused UX wiring on existing sim).

---

## 8. Architecture Notes for Implementers

**Snapshot extension target** (`SimSnapshotDto` / `sim-bridge.ts`):

```typescript
// Proposed additions
rci: { residential: number; commercial: number; industrial: number };
approval: number;
happiness: number;
monthlyIncome: number;
monthlyExpenses: number;
activeEvents: Array<{ typeId: string; phase: string; severity: number; tileX: number; tileY: number }>;
eraProgress: { nextEra: number; gates: Array<{ id: string; current: number; required: number; met: boolean }> };
```

**Herald data flow target:**

```
EventSystem.SpawnEvent → WASM export → PlayClient auto-fetch OR push
  → /api/narrative/event { eventTypeId, simContext }
  → template from events.json OR LLM
  → HeraldPanel with onSelect → sim command queue
```

---

## 9. Summary Matrix

| Loop stage | GDD | Web v1.0 |
|---|---|---|
| Check pulse | Budget, approval, RCI, Herald | Pop, funds, era badge |
| Respond to demand | RCI-driven zoning | Blind zoning |
| Build services | Coverage overlays, placement | Not in web UI |
| Handle events | Choices affect sim | Events run silently; Herald manual + inert |
| Tweak policy | Budget, laws, taxes | Not exposed |
| Watch & enjoy | Growth, seasons, festivals | 3D growth (good); no narrative context |
| Save & plan | Research queue, projections | Save/load works; no research UI |

**Bottom line:** CityMajor's WASM sim is a credible L1 city builder. The web play page is an R3F viewer with zoning paint. v1.1 should prioritize **exporting what the sim already knows** before adding new systems.

---

*Audit date: 2026-07-04 · Branch: `feat/wasm-r3f-integration-2026-07-04` · Worktree: `citymajor-web-r3f-spike`*
