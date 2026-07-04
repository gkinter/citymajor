# Research: 19 Simulation Scalability

The complete specification is at `/Users/fredericbeeg/citymajor/citymajor/docs/design/SIMULATION_ARCHITECTURE.md` -- 2,738 lines covering all 17 systems plus 4 appendices.

**Summary of what was produced:**

**17 fully specified systems:**

1. **Hierarchical Simulation** -- 4-level LOD (L0 every tick through L3 yearly) with exact tick budgets per level and complete orchestrator logic
2. **Core Data Structures** -- byte-exact layouts: Tile (SoA, 18MB), Household (64B AoS, 6.1MB), Citizen (32B, 15.3MB), Building (128B, 6.1MB), RoadNode/RoadEdge (CSR format)
3. **Spatial Partitioning** -- 1024 uniform 32x32 partitions with 256B aggregate struct, dirty flag bitmask system, O(1) tile-to-partition lookup
4. **Traffic System** -- BPR macroscopic flow on road graph, 512-zone O-D gravity model, Contraction Hierarchies (10us queries vs 2ms Dijkstra), Frank-Wolfe assignment (3-5 iterations)
5. **Economic Simulation** -- 45 goods, 20 production chains, 8-16 market zones each with Leontief I-O solver (45x45 matrix, 20us), inter-zone trade, global import/export
6. **Population System** -- Staggered 1/30th household updates per tick (3,333 fit in L2 cache), 12-factor satisfaction model, gravity-based migration, spatial+skill employment matching
7. **Service Coverage** -- Event-driven influence maps (flood-fill on build/demolish), 4MB per service type, zero per-tick cost in steady state
8. **Overlay Systems** -- 15+ overlays with pollution advection-diffusion, land value compositing, desirability calculation
9. **Threading Model** -- Main/Sim/Workers(6)/IO threads, lock-free double-buffer snapshots (3.2MB), SPSC command queue for player input, fork-join job system
10. **Memory Budget** -- 120.5MB simulation, ~490MB total including rendering/audio/runtime. Well under 2GB
11. **Scaling Benchmarks** -- Detailed per-component timing at each population level. 500K pop = 2.5ms average tick, 60ms worst case (year tick), both under 66.7ms budget
12. **Event System** -- 80+ events as state machines (Brewing/Active/Waning/Aftermath), spatial spread, chain triggers, player response options
13. **Law & Compliance** -- 70+ laws in 10 categories, slider/toggle controls, building-type affinity matrix for efficient compliance checks, budget impact
14. **Technology** -- 154 techs across 7 eras, prerequisite graph, effects on production/buildings/laws/events
15. **Sports** -- 11 leagues simulated yearly, stadium revenue, championship events
16. **Vehicle Routing** -- 10K cosmetic vehicles following pre-assigned paths, spawned proportional to edge volume, ring-buffer path pool
17. **Serialization** -- Tagged binary chunks, Brotli compression (95MB raw to 25-40MB), async I/O, <2s save/<3s load

**4 appendices:** System interaction matrix, tick budget summary, SIMD opportunities, design decision rationale.