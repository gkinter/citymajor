# Tech Tree Gap Analysis

This document analyzes the gap between the CityMajor technology/research system design and its current implementation in the WASM web spike (`feat/wasm-r3f-integration-2026-07-04`).

## 1. Design vs Implementation

**Design Intent:**
- **Scale:** 154 technologies spanning 5 eras (Frontier, Industrial, Postwar, Modern, Future).
- **Mechanics:** Techs have RP costs, prerequisites, unlocks (buildings/policies), effects, and eureka conditions.
- **Branching:** Mutually exclusive branching decisions (e.g., `agriculture_philosophy`, `industrial_philosophy`) force players to choose a specific path.
- **Era Transitions:** Eras advance when specific population and unlocked tech count thresholds are met.

**Implementation State (WASM Spike):**
- `technologies.json` is successfully bundled and loaded in `WasmSimHost.cs` via `WasmEmbeddedData`.
- `ResearchSystem.cs` is fully implemented in C# (RP generation, queueing, eureka bonuses, branching, era transitions).
- **Gap:** `WasmSimHost.cs` calls `_research.MonthlyTick()` but immediately overrides the era using `WasmEraDeriver.UpdateEra()`. This derives the era from tick count and RP/population proxies instead of the actual unlocked tech count, bypassing the `ResearchSystem`'s era transition logic.

## 2. Web vs Desktop (WASM boundaries)

**Desktop (SimCore):**
- Full `ResearchSystem` logic.
- Full building catalog to match the 154 tech unlocks.

**Web (WASM Spike):**
- `ResearchSystem` is running and accumulating RP.
- `GetStatus()` in `WasmExports.cs` exports `researchPoints`, `researchRate`, and `techCount`.
- **Gap (sim-worker):** `web/workers/sim-worker.ts` drops these fields in `WasmStatus` and `readStatus()`.
- **Gap (sim-bridge):** `web/lib/sim-bridge.ts` `SimResources` does not include research fields.
- **Gap (Dead Techs):** The 154 techs unlock buildings (e.g., `train_depot`, `hospital`, `factory`) that do not exist in the WASM spike's limited building pool. Most techs are functionally "dead" in the web version because their unlocks map to unimplemented assets.

## 3. UI Gaps

- **Missing HUD Elements:** `ResourcesHud.tsx` shows Population, Funds, Tick, and Era, but does not display Research Points or Research Rate.
- **No Research Panel:** There is no UI in `/play` to view the tech tree, see available techs, or enqueue research.
- **Empty Queue:** The `ResearchSystem` queue remains empty (`-1`) because no commands are sent from the client to enqueue tech.

## 4. Recommendations

### 4.1. Research UI v1
- **HUD Update:** Add `researchPoints`, `researchRate`, and `techCount` to `SimResources` and display them in `ResourcesHud.tsx`.
- **Basic Tech Panel:** Create a simple modal or drawer in `/play` that lists available technologies (filtered by unmet prerequisites).
- **Enqueue Command:** Add an `enqueue_research` command to `SimCommand` and wire it through `sim-worker.ts` to a new `EnqueueResearch(int techId)` export in `WasmExports.cs`.

### 4.2. Tech Categories & Branching
- **Categories:** Group techs by category (`transport`, `energy`, `construction`, `social`, etc.) in the UI to make the 154-tech list manageable.
- **Civic vs Industrial Branches:** Highlight mutually exclusive branches (e.g., `T061 Industrial Robotics` vs `T062 Lean Manufacturing`) visually so players understand the permanent tradeoffs.

### 4.3. Skill-Tree UX Ideas (Not Full Implementation)
- **Node Graph:** A visual node-based tree (like *Civilization* or *Path of Exile*) where lines connect prerequisites to unlocks.
- **Era Columns:** Organize the tree horizontally by Era (Frontier on the left, Future on the right) to show progression.
- **Eureka Badges:** Add a small icon or tooltip on tech nodes showing the `eureka_condition` to encourage organic gameplay discovery.
- **Branching Forks:** Use distinct visual forks (e.g., a split path with a lock icon) for `branch_exclusive` choices to emphasize the decision weight.
