# Research: 09 Ai Sim Audit

Both documents are placeholders. I have enough context from your description to do a thorough gap analysis. Here is the complete audit.

---

## Complete Gap Audit: "Building Iron & Oak"

Based on your description of the document's coverage (3 pillars, 18-month roadmap, tech stack), here are all identified gaps organized by category.

---

### SIMULATION GAPS

**Infrastructure Physics (not covered)**

1. **Noise propagation model** -- Sound attenuates with distance, reflects off buildings, is absorbed by vegetation. Needs a simplified wave propagation or zone-of-influence model per source type (roads by traffic volume, industry by type, airports by flight path). Affects land value and residential desirability.

2. **Power grid load balancing** -- Beyond "has power / no power", a city builder at this depth needs: peak vs baseload demand curves, grid frequency stability, renewable intermittency (solar/wind output varies by weather/season), transformer capacity bottlenecks, and brownout/blackout cascading failure. Victoria 3's goods market model could inspire a power market.

3. **Water pressure calculation** -- Elevation-dependent pressure, pump station placement requirements, pipe diameter constraints, pressure loss over distance. Determines which buildings get adequate water and which suffer low pressure on upper floors.

4. **Sewage flow modeling** -- Gravity-driven flow (downhill to treatment plants), pump station requirements for uphill routing, capacity overflow during storms (combined sewer overflow), and treatment plant processing rates. Ties into the pollution/water quality system.

5. **Flood simulation** -- Water level rise from rainfall intensity exceeding drainage capacity, river flooding from upstream events, coastal storm surge. Needs at minimum a heightmap-based flow accumulation model. Ties into climate model but needs specific hydraulic mechanics.

6. **Fire spread dynamics** -- Building material flammability, wind direction influence on spread rate, firebreak effectiveness (roads, parks), fire department response time based on station coverage. Simplified cellular automata or influence-map approach rather than full fluid dynamics.

7. **Wind simulation for pollution dispersal** -- Prevailing wind direction/speed affects where industrial pollution settles. Needs at minimum a vector field overlay that shifts pollution concentration maps. Directly affects health outcomes and land value.

8. **Soil contamination modeling** -- Industrial zones and landfills create contamination plumes that persist after demolition (brownfield sites). Remediation cost/time. Affects land value and health for decades after source is removed.

9. **Snow/ice mechanics** -- Road surface conditions affecting traffic speed, snow accumulation rates, plowing priority routing (arterials first, then collectors, then local streets), salt/sand supply logistics, budget impact of severe winters.

10. **Earthquake/disaster structural simulation** -- Building age, construction material, and code compliance determining damage probability. Liquefaction risk by soil type. Not full structural engineering, but a damage probability model per building class.

**Transportation (not covered or underspecified)**

11. **Pathfinding algorithm choice** -- For 20K households generating trips, naive A* will not scale. Needs hierarchical pathfinding (e.g., Contraction Hierarchies or HPA*) with precomputed highway-level routes and local last-mile resolution. This is a critical performance decision.

12. **Multi-modal trip planning** -- Citizens choosing between walk, bike, bus, metro, car based on time, cost, and comfort. Needs a mode-choice model (simplified logit model) feeding into route assignment. Determines transit ridership organically rather than by fiat.

13. **Freight routing optimization** -- Commercial/industrial goods movement on separate logic from commuter traffic. Truck routes, rail freight, port throughput. Heavy vehicles cause road damage (maintenance cost scaling). Freight corridors affect noise and pollution differently than passenger traffic.

14. **Public transit schedule optimization** -- Headway calculation based on demand, transfer penalty modeling, express vs local service tradeoffs, depot placement, vehicle fleet sizing. Players set policy, simulation determines ridership and financials.

15. **Parking demand modeling** -- Parking requirements per building type, on-street vs structured parking, spillover parking in residential areas near commercial zones. Affects traffic (cruising for parking adds congestion), land use efficiency, and revenue (parking fees/fines).

16. **Pedestrian flow simulation** -- Sidewalk capacity, crosswalk wait times, pedestrian desire lines that cut through parks/lots. Important for downtown density, transit station access, and commercial district vitality.

17. **Traffic accident simulation** -- Intersection design (signalized vs roundabout vs uncontrolled) affecting accident rates, speed limits, sight lines. Feeds into healthcare demand, insurance costs, and political pressure for traffic calming.

18. **Garbage collection route optimization** -- Collection frequency by zone density, transfer station placement, landfill capacity/lifespan, recycling diversion rates. A logistics problem that scales with city size and affects both budget and environmental outcomes.

**Social/Economic (potentially underspecified)**

19. **Immigration cultural compatibility scoring** -- If the pop system models cultural groups, immigration rates should factor in: diaspora network effects (existing community attracts more), language barriers affecting employment, cultural friction generating political tension. Needs a compatibility/friction matrix without being reductive.

20. **Construction material supply chain** -- Building construction requiring materials (concrete, steel, lumber) that have supply chains, price fluctuations, and import dependencies. A construction boom can cause material shortages and price spikes, naturally throttling growth.

21. **Criminal justice system** -- Crime rates by neighborhood (driven by poverty, unemployment, inequality), policing models (community vs aggressive), court capacity, incarceration costs, rehabilitation effectiveness. Feeds back into labor pool and political factions.

22. **Healthcare capacity modeling** -- Hospital bed counts, specialist availability, ambulance response times, epidemic spread modeling (connects to pop density and sanitation). Different from the climate/disease mention in pillars -- this is the service delivery side.

23. **Education quality feedback loops** -- School capacity and quality affecting property values, educated workforce attracting industries, brain drain if higher education is absent. Multi-generational effects on pop skill levels.

24. **Real estate market dynamics** -- Land value as an emergent property of accessibility, amenities, and neighborhood quality. Speculation, bubbles, gentrification displacement. Not just "land value overlay" but actual market transactions with bid/ask dynamics.

25. **Labor market friction** -- Skills mismatch (factory workers cannot immediately become programmers), commute tolerance affecting job market radius, wage negotiation between employers and pop groups. Victoria 3 handles this well -- the document should specify how deeply to mirror it.

---

### AI GAPS

**Content Generation Pipeline**

26. **Fine-tuning data generation strategy** -- If using local models (Pillar 1 mentions fallback), what training data? Synthetic data generation from larger models, human-written exemplars, or gameplay logs? Format, volume, and quality benchmarks needed.

27. **Prompt engineering best practices** -- Structured prompt templates for each content type (newspaper articles, citizen complaints, trade proposals). Few-shot examples, system prompts with world state injection, output format enforcement (JSON schema validation).

28. **Content quality filtering/safety** -- Generated text needs filtering for: anachronisms, tone-breaking modern references, offensive content, factual contradictions with game state. Automated quality gates before content reaches the player.

29. **Hallucination prevention for game-specific facts** -- LLM might reference buildings that don't exist, cite wrong population numbers, or invent historical events. Needs grounding: inject verified game state into context, validate output claims against simulation data.

30. **Content caching and deduplication** -- Players will trigger similar events repeatedly. Cache generated content by event-type + key-parameters hash. Detect and prevent near-duplicate newspaper headlines or citizen quotes across sessions.

31. **A/B testing generated content quality** -- How to measure if generated content is good? Player engagement metrics (do they read it, do they click through), explicit thumbs up/down, or implicit signals (time spent reading). Needed to iterate on prompts.

32. **Player feedback loop on AI content quality** -- Mechanism for players to flag bad content, which feeds back into prompt refinement or fine-tuning data. Also community-level quality signals.

33. **Fallback content quality assurance** -- When the local model fallback activates (Pillar 1), its output quality will be lower. Needs: quality threshold detection, graceful degradation (shorter/simpler content rather than bad content), pre-generated fallback pools for critical moments.

**Conversational AI**

34. **Multi-turn conversation memory for trade negotiations** -- If the advisor chatbot or AI mayors negotiate trade deals, they need to track: prior offers, concessions made, stated priorities, and red lines across multiple conversation turns. RAG or structured state tracking.

35. **Personality persistence for AI mayors across sessions** -- Neighboring AI mayors should have consistent personalities, negotiation styles, and relationship memory. Needs: personality embeddings or trait vectors, relationship state serialization, behavioral consistency validation.

**AI Architecture**

36. **Token budget management** -- With game state injection into prompts, context windows fill fast. Needs: prioritized state summarization (most relevant facts first), sliding window for conversation history, cost estimation per API call, budget caps per game session.

37. **Latency hiding for LLM calls** -- API calls take 1-5 seconds. Needs: pre-generation of likely-needed content during idle moments, background generation queues, placeholder UI while content loads, speculative generation for probable player actions.

38. **Offline mode content strategy** -- Beyond local model fallback: what happens with no internet and no local model capability? Pre-generated content pools, template-based generation, or degraded feature set? Needs explicit design.

---

### TECHNICAL GAPS

**Persistence and Replay**

39. **Save file format versioning strategy** -- As the game evolves over 18+ months, save format will change. Needs: version header in save files, migration functions per version step, forward-compatibility policy (can old saves load in new versions?), save file size budget.

40. **Deterministic replay system** -- For debugging simulation bugs: ability to replay from a save with identical random seeds producing identical outcomes. Requires: deterministic RNG seeding, fixed-order entity processing, no floating-point non-determinism across platforms.

41. **Autosave and crash recovery** -- Periodic autosave strategy, atomic write (write to temp then rename to prevent corruption), recovery from mid-save crashes, save file integrity validation.

**Multiplayer/Network**

42. **Network sync for multiplayer** -- If multiplayer is ever considered (even async like trading with AI mayors becoming real players): lockstep vs state-sync architecture, latency compensation, conflict resolution for simultaneous actions, anti-cheat for economic exploits.

**Modding**

43. **Mod sandboxing and security** -- If mods can add buildings, change simulation parameters, or inject scripts: sandboxed execution environment, API surface definition, mod load order and conflict resolution, preventing mods from accessing filesystem or network.

44. **Mod API surface design** -- What can mods change? Building definitions, simulation parameters, UI elements, AI prompts? Needs explicit API boundaries, versioning, and documentation strategy.

**Performance**

45. **Memory management for long play sessions** -- City builders are played for hours. Entity pooling, texture streaming, garbage collection pauses (if using a GC language), memory leak detection strategy, memory budget per subsystem.

46. **Thread safety for simulation + rendering** -- Simulation tick on background thread(s) while rendering continues. Needs: double-buffered state, lock-free data structures or clear mutex boundaries, job system design for parallelizing simulation subsystems (economy tick, traffic tick, pop tick independently).

47. **Profiling and optimization strategy** -- Performance budget per simulation tick (target ms), profiling toolchain selection, regression detection (automated perf benchmarks in CI), LOD strategy for simulation (distant city areas tick at lower fidelity).

48. **Simulation LOD / spatial partitioning** -- At 20K households, not everything needs per-tick updates. Spatial partitioning (quadtree/grid) for locality-sensitive systems (traffic, noise, fire), temporal LOD (economy ticks every second, traffic every frame, politics every game-day).

49. **ECS vs OOP architecture decision** -- For 20K households plus buildings plus vehicles: entity-component-system gives cache-friendly iteration and easy parallelism. This is a foundational architecture choice that affects everything downstream. The tech stack section should address it.

**Data and Analytics**

50. **Telemetry and playtesting analytics** -- What data to collect during playtesting to validate that emergence actually works? Balancing data (are players always bankrupt at year 5?), engagement data (which systems do players interact with?), emergent event frequency.

51. **Simulation validation framework** -- How to verify that interconnected systems produce sensible outcomes? Automated scenario tests ("build 100 houses with no jobs, verify unemployment rises"), regression tests for economy balance, statistical validation of distributions.

---

### DESIGN/GAMEPLAY GAPS (not in your list but relevant)

52. **Difficulty scaling and scenario design** -- How do the 8-10 interconnected systems adjust for different difficulty levels? Starting conditions, event frequency, economic headwinds? Scenarios (industrial revolution, post-war reconstruction, climate crisis) that exercise different subsystems.

53. **Tutorial and progressive disclosure** -- Victoria 3's biggest criticism is impenetrable complexity. How does the game teach its systems? Which systems unlock when? What's the "first 30 minutes" experience?

54. **Balancing methodology** -- With emergent systems, traditional number-tweaking doesn't work. Needs: automated balancing runs (simulate 1000 cities overnight), genetic algorithm parameter search, or ML-based balance testing.

55. **Time scale and pacing** -- What's one game-tick in real time? How fast can the player speed up? At 5x speed with 20K households, can the simulation keep up? Pacing affects which systems feel responsive vs sluggish.

56. **Map generation algorithm** -- Terrain, rivers, coastlines, resource distribution. Needs to produce maps that create interesting constraints (not flat plains every time). Perlin noise + erosion simulation + resource placement heuristics.

---

### SUMMARY BY PRIORITY

**Critical (blocks core gameplay):** Pathfinding algorithm (#11), thread safety (#46), simulation LOD (#48), ECS architecture (#49), save versioning (#39), memory management (#45), time scale/pacing (#55)

**High (needed for Victoria 3-level depth):** Power grid (#2), multi-modal transport (#12), real estate market (#24), labor friction (#25), construction supply chain (#20), transit scheduling (#14), prompt engineering (#27), hallucination prevention (#29)

**Medium (differentiators and polish):** Flood sim (#5), fire spread (#6), wind/pollution (#7), noise (#1), crime system (#21), content caching (#30), deterministic replay (#40), profiling strategy (#47), balancing methodology (#54)

**Lower (can be added post-launch):** Earthquake resistance (#10), snow logistics (#9), soil contamination (#8), parking (#15), pedestrian flow (#16), mod sandboxing (#43), multiplayer sync (#42), A/B testing content (#31)

The document covers the "what to simulate" well but has significant gaps in "how to simulate it efficiently" (performance architecture) and "how to make AI content reliable" (quality pipeline). The 7 critical items should be addressed before the 18-month roadmap is finalized, as they affect technology choices at the foundation level.