# MASTER DEVELOPMENT PLAN: Iron & Oak

## 10-Agent Parallel Development Strategy

**Target**: Playable prototype in 3-4 weeks. Early Access in 10-12 weeks.

---

## 1. AGENT DEPENDENCY GRAPH

```
                    ┌──────────────────┐
                    │  AGENT 10        │
                    │  Game Feel       │  ← Starts Day 1, no deps
                    │  Research        │
                    └────────┬─────────┘
                             │ GAME_FEEL_BIBLE.md (Day 5)
                             ▼
┌──────────────────────────────────────────────────────────────────┐
│                                                                  │
│  ┌──────────────┐                                                │
│  │  AGENT 01    │ ← Starts Day 1, no deps                       │
│  │  Core Engine │ ← ALL agents wait for this (Day 1-2 output)   │
│  └──────┬───────┘                                                │
│         │                                                        │
│    Day 2│ GridData, EventBus, TileMap ready                      │
│         │                                                        │
│         ├────────────────────────────────────────────┐            │
│         │              │              │              │            │
│         ▼              ▼              ▼              ▼            │
│  ┌──────────┐   ┌──────────┐   ┌──────────┐   ┌──────────┐     │
│  │ AGENT 02 │   │ AGENT 03 │   │ AGENT 04 │   │ AGENT 05 │     │
│  │ Simulate │   │ Economy  │   │Transport │   │ Zoning   │     │
│  │ Pop/Demo │   │ Budget   │   │ Traffic  │   │ Building │     │
│  └────┬─────┘   └────┬─────┘   └────┬─────┘   └────┬─────┘     │
│       │              │              │              │             │
│       │    Day 4-6   │              │              │             │
│       ├──────────────┤              │              │             │
│       │              │              │              │             │
│       ▼              ▼              ▼              ▼             │
│  ┌──────────┐   ┌──────────┐                                    │
│  │ AGENT 06 │   │ AGENT 07 │                                    │
│  │ Services │   │ Politics │   ← Need pop + economy data       │
│  └──────────┘   └──────────┘                                    │
│                                                                  │
│  ┌──────────┐   ┌──────────┐                                    │
│  │ AGENT 08 │   │ AGENT 09 │   ← Can start with stubs/placeholders│
│  │ UI/UX    │   │ Art/Audio│                                    │
│  └──────────┘   └──────────┘                                    │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

---

## 2. TIMELINE: WEEK-BY-WEEK

### WEEK 1: Foundation Sprint

| Day | Agent 01 | Agent 09 | Agent 10 | Others |
|-----|----------|----------|----------|--------|
| 1 | Project scaffold, folder structure | Terrain tiles (AI gen) | Placement feel research | BLOCKED (waiting for A01) |
| 2 | Grid system, TileMap, coordinates | Road/rail tiles | Info architecture research | Start with stubs |
| 3 | Camera, time/era system | Building sprites (Frontier era) | Growth satisfaction research | A02-A05 begin Phase 1 |
| 4 | Save/load, event bus | Building sprites (Industrial) | Tutorial research | A02-A05 continue |
| 5 | Integration testing, handoff doc | Building sprites (Postwar) | GAME_FEEL_BIBLE v1 draft | A06-A07 begin |

**Week 1 Milestone**: Grid renders, camera works, terrain visible, 5 placeholder buildings growable.

### WEEK 2: Core Systems Sprint

| Day | A02 | A03 | A04 | A05 | A06 | A07 | A08 | A09 |
|-----|-----|-----|-----|-----|-----|-----|-----|-----|
| 6 | Households | Budget | Road graph | Zone painter | Coverage model | Approval | HUD layout | Modern buildings |
| 7 | Lifecycle | RCI demand | Traffic model | Growth logic | Fire dept | Council | Toolbar | Future buildings |
| 8 | Migration | Prod chains | Transit modes | Building DB | Police | Laws (16/32) | Budget panel | Vehicle sprites |
| 9 | Cultural DNA | Trade | Rail network | Placement | Healthcare | Laws (32/32) | Building info | Citizen sprites |
| 10 | Satisfaction | Financial | Vehicle render | Land value | Education | Lobbying | Transit panel | Shaders (4/8) |

**Week 2 Milestone**: Basic city loop works -- zone, build, watch buildings grow, see traffic, collect taxes.

### WEEK 3: Integration Sprint

| Day | Focus |
|-----|-------|
| 11 | Cross-system integration: economy drives demand, demand drives zoning, zoning creates traffic |
| 12 | Service coverage affects satisfaction, satisfaction affects migration, migration affects demand |
| 13 | Politics: laws affect simulation parameters, approval reacts to player decisions |
| 14 | UI: all panels wired to real data, overlays showing real simulation output |
| 15 | Bug fixing, balance tuning, performance profiling |

**Week 3 Milestone**: All 10 systems talking to each other. Playable 30-minute loop.

### WEEK 4: Polish Sprint

| Day | Focus |
|-----|-------|
| 16 | Tutorial implementation, first-30-minutes flow |
| 17 | All 8 shaders complete, day/night + seasons working |
| 18 | Audio: all 10 music tracks + 79 SFX integrated |
| 19 | Events system: disasters, economic events, social events |
| 20 | Era progression test: play Frontier through Modern |

**Week 4 Milestone**: Playable prototype. One full era playable with all systems.

### WEEKS 5-8: Content Sprint (All 5 Eras)

- Complete all building sprites for all eras (260+)
- Complete all vehicle sprites for all eras (120)
- Implement all 102 technologies
- Implement all 32 laws with effects
- Implement all 20 production chains
- Implement all 8 scenarios
- Cultural DNA: all 12 presets, all 20 archetypes

### WEEKS 9-10: Advanced Features

- Multi-city regional play
- Global trade between cities
- Cultural variant building skins
- Advanced transit modes (metro, airport, seaport)
- Statistics panel with full historical charts

### WEEKS 11-12: Ship Preparation

- Performance optimization (60fps at max city size)
- Balance pass (play each era for 2+ hours)
- Steam integration (achievements, cloud saves, workshop prep)
- Trailer capture, store page, marketing materials
- Early Access release

---

## 3. AI TOOL USAGE GUIDE

### When to Use Each AI Tool

#### Claude (Code/Architecture Agent)
**USE FOR**:
- All C# simulation code
- All GDScript game logic
- Data structures, algorithms, formulas
- JSON data file generation (buildings, techs, laws)
- Shader code (GLSL)
- Project configuration files
- Bug fixing and refactoring
- Documentation

**DON'T USE FOR**:
- Generating pixel art images
- Producing audio assets
- Final game balance decisions (needs playtesting)

#### Kimi (Image-to-Code)
**USE FOR**:
- **UI Layout Generation**: Screenshot a reference game's panel -> generate Godot .tscn
  ```
  Input:  Screenshot of SimCity 4 budget screen
  Output: BudgetPanel.tscn with matching layout adapted to pixel art style
  ```
- **Sprite-to-SpriteFrames**: Take AI-generated sprite -> generate Godot SpriteFrames .tres
- **Shader Reference**: Screenshot a visual effect -> analyze technique -> generate equivalent shader
- **Tileset Generation**: Screenshot existing isometric tileset -> generate matching Godot TileSet
- **Animation Reference**: Screenshot animation frames -> generate AnimationPlayer keyframes

**DON'T USE FOR**:
- Complex simulation logic
- Data processing
- File I/O operations
- Anything that doesn't involve visual -> code conversion

**KIMI WORKFLOW**:
```
Step 1: Find reference screenshot (Google Images, gameplay video frame)
Step 2: Feed to Kimi with prompt: "Convert this [UI panel / tileset / effect]
        to Godot 4 [.tscn / .tres / .gdshader]. Use isometric pixel art style
        at 32x16 tile base. Use GDScript for logic."
Step 3: Review output, adjust for project conventions
Step 4: Integrate into project
```

#### Art AI (Midjourney / DALL-E / Stable Diffusion)
**USE FOR**:
- Building sprite concepts (generate at 4x, downscale to pixel art)
- Vehicle sprite concepts
- UI texture backgrounds (wood grain, metal, paper)
- Main menu background art
- Advisor portraits
- Concept art for art direction

**PROMPT TEMPLATE**:
```
"Isometric pixel art [SUBJECT], [ERA] architectural style,
32x16 pixel tile base, clean pixel art style, dark 1px outline,
[ERA_PALETTE] color palette, white background, top-down 45-degree angle,
Stardew Valley quality, no anti-aliasing"
```

**BATCH PROCESSING PIPELINE**:
```
1. Generate 4 variations per building concept (Art AI)
2. Human selects best variation (10 sec per building)
3. Claude script: downscale, palette-map, outline, shadow (automated)
4. Export to assets/sprites/ with correct naming
5. Register in buildings.json metadata
```

**DON'T USE FOR**:
- UI elements (use Kimi instead)
- Shader effects (use Claude)
- Animation frames (generate base, animate in Aseprite)

#### ElevenLabs (Audio Generation)
**USE FOR**:
- All 79 sound effects (construction, traffic, weather, UI, events)
- 4 seasonal ambient loops

**SFX PROMPT TEMPLATE**:
```
"[SOUND DESCRIPTION], 8-bit retro style, warm and satisfying,
suitable for pixel art city builder game"
```

**DON'T USE FOR**:
- Music (use Suno/Udio)
- Voice acting (none in this game)

#### Suno / Udio (Music Generation)
**USE FOR**:
- 10 background music tracks (2 per era)
- 10 event stings (disaster, milestone, era change)

**MUSIC PROMPT TEMPLATE**:
```
"Instrumental background music for [ERA] city builder game.
[ERA_INSTRUMENTS]. Warm, nostalgic, relaxing but engaging.
Loopable, 3-4 minutes. Pixel art game aesthetic."

Era instruments:
Frontier: acoustic guitar, harmonica, banjo, fiddle
Industrial: piano, strings quartet, brass, pipe organ
Postwar: jazz ensemble, big band, saxophone, double bass
Modern: electronic ambient, soft synth, clean guitar
Future: ethereal synth, glitch percussion, AI-harmonic pads
```

---

## 4. INTER-AGENT COMMUNICATION PROTOCOL

### Stub Strategy
Every agent can start work using stubs for missing dependencies:

```csharp
// Example: Agent 02 needs commute data from Agent 04
// Before Agent 04 delivers:
public float GetCommuteTime(uint householdId) {
    return 15.0f; // STUB: assume 15 min commute for all
}

// After Agent 04 delivers:
public float GetCommuteTime(uint householdId) {
    return _transportSystem.CalculateCommute(
        _households[householdId].HomeBuilding,
        _households[householdId].Workplace1
    );
}
```

### Interface Contracts
Each agent defines its public API on Day 1 as a C# interface:

```csharp
// Agent 02 publishes this interface immediately
public interface IPopulationSystem {
    int TotalPopulation { get; }
    int HouseholdCount { get; }
    float AverageHappiness { get; }
    float[] WealthDistribution { get; }  // 6 classes
    float[] AgeDistribution { get; }     // 8 brackets
    float[] CulturalDNA { get; }         // 8 dimensions
    void ProcessMonthlyTick();
}
```

Other agents code against the interface from Day 1, even before the implementation exists.

### Handoff Documents
Each agent produces `AGENT_XX_HANDOFF.md` upon completion:
- Every public class + method signature
- Every signal emitted + parameters
- Every data file created + schema
- Known limitations
- Performance characteristics
- Test results

### Integration Points (Critical Path)
These connections are where bugs will live:
```
1. Economy demand  →  Zone growth    (A03 → A05): Does demand correctly trigger building growth?
2. Population O-D  →  Traffic model  (A02 → A04): Do commute origins match household locations?
3. Service coverage → Satisfaction    (A06 → A02): Does fire coverage correctly reduce fire anxiety?
4. Law effects     →  Simulation     (A07 → ALL): Does "emission standards" actually reduce pollution?
5. Budget expenses →  Service quality (A03 → A06): Does cutting fire budget reduce response time?
```

**Integration testing**: Week 3 dedicated to verifying all 5 critical paths work.

---

## 5. QUALITY GATES

### Per-Agent Quality Gate (must pass before handoff)
- [ ] All C# code compiles without warnings
- [ ] All GDScript runs without errors
- [ ] Performance: target tick rate achieved under load
- [ ] Data: JSON files validate against schema
- [ ] Integration: signals fire correctly, data flows to downstream agents

### Prototype Quality Gate (Week 4)
- [ ] New game starts without crash
- [ ] Can place roads, zones, buildings
- [ ] Buildings grow from zones
- [ ] Traffic visible on roads
- [ ] Budget panel shows real numbers
- [ ] Can save and load game
- [ ] 60fps at 10,000 population
- [ ] Day/night cycle works
- [ ] At least 1 season change works
- [ ] At least 1 era transition works

### Early Access Quality Gate (Week 12)
- [ ] 5 eras playable (Frontier through Future)
- [ ] All 15 transit modes functional
- [ ] All 20 production chains working
- [ ] All 32 laws implemented
- [ ] 102 technologies researchable
- [ ] 8 scenarios playable
- [ ] 200k population without crash
- [ ] 930+ sprites complete
- [ ] 89 audio assets complete
- [ ] Save/load reliable
- [ ] Tutorial functional
- [ ] No game-breaking bugs

---

## 6. RISK REGISTER

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| Integration breaks between agents | HIGH | HIGH | Interface contracts on Day 1, integration week |
| Performance degrades at scale | MEDIUM | HIGH | Profile weekly, SOA layout, dirty flags |
| Art style inconsistency across AI tools | MEDIUM | MEDIUM | Art direction doc, palette enforcement script |
| Scope creep (always adding features) | HIGH | MEDIUM | Feature freeze at Week 8, polish only after |
| AI-generated code has subtle bugs | HIGH | MEDIUM | Unit tests per agent, integration tests weekly |
| Save format changes break old saves | LOW | HIGH | Version migration system from Day 1 |
| Game feel is technically correct but unsatisfying | MEDIUM | HIGH | Agent 10 GAME_FEEL_BIBLE, playtest weekly |

---

## 7. DAILY STANDUP FORMAT

Each agent reports daily (in its handoff log):
```
AGENT [XX] - Day [N] Status
COMPLETED: [what was finished]
IN PROGRESS: [current task]
BLOCKED BY: [dependency on other agent, if any]
DECISIONS NEEDED: [questions for project lead]
OUTPUT FILES: [new files created today]
```

---

## 8. TOKEN BUDGET ESTIMATE

### Per-Agent Daily Cost (Opus for architecture, Sonnet for routine)

| Agent | Model Mix | Est. Tokens/Day | Est. Cost/Day |
|-------|-----------|-----------------|---------------|
| 01 Core | 70% Opus, 30% Sonnet | 400k | $12 |
| 02 Simulation | 80% Opus, 20% Sonnet | 500k | $16 |
| 03 Economy | 80% Opus, 20% Sonnet | 500k | $16 |
| 04 Transport | 90% Opus, 10% Sonnet | 600k | $20 |
| 05 Zoning | 60% Opus, 40% Sonnet | 350k | $10 |
| 06 Services | 70% Opus, 30% Sonnet | 450k | $14 |
| 07 Politics | 60% Opus, 40% Sonnet | 350k | $10 |
| 08 UI/UX | 40% Opus, 60% Sonnet | 400k | $10 |
| 09 Art | 20% Opus, 80% Sonnet | 300k | $6 |
| 10 Research | 50% Opus, 50% Sonnet | 400k | $10 |
| **TOTAL** | | **~4.3M tokens/day** | **~$124/day** |

### Additional AI Tool Costs
| Tool | Usage | Est. Cost/Day |
|------|-------|---------------|
| Midjourney / DALL-E | ~50 image generations | $5-10 |
| Kimi | ~20 image-to-code conversions | $0-5 |
| ElevenLabs SFX | ~10 sound effects | $2-5 |
| Suno/Udio | ~2 music tracks | $1-3 |
| **TOTAL additional** | | **~$10-23/day** |

### Grand Total
- **Daily**: ~$135-150/day
- **4-week prototype**: ~$2,800-4,200
- **12-week Early Access**: ~$8,400-12,600

**Compare to**: Hiring 3 developers for 3 months = $45,000-90,000+

---

## 9. FILE STRUCTURE AFTER ALL AGENTS COMPLETE

```
iron_and_oak/
├── project.godot
├── scripts/
│   ├── simulation/ (C#)           ← Agents 02, 03, 04, 06, 07
│   │   ├── core/                  ← Agent 01
│   │   │   ├── GridData.cs
│   │   │   ├── ChunkManager.cs
│   │   │   ├── SimulationLoop.cs
│   │   │   ├── DoubleBuffer.cs
│   │   │   ├── SimulationBridge.cs
│   │   │   ├── BuildingRegistry.cs
│   │   │   ├── LandValueCalculator.cs
│   │   │   ├── BuildingCondition.cs
│   │   │   └── Serializer.cs
│   │   ├── population/            ← Agent 02
│   │   │   ├── PopulationPool.cs
│   │   │   ├── LifecycleSimulator.cs
│   │   │   ├── MigrationSimulator.cs
│   │   │   ├── CulturalDNA.cs
│   │   │   ├── SatisfactionCalculator.cs
│   │   │   └── ...
│   │   ├── economy/               ← Agent 03
│   │   │   ├── BudgetSystem.cs
│   │   │   ├── DemandCalculator.cs
│   │   │   ├── ProductionChain.cs
│   │   │   ├── TradeSystem.cs
│   │   │   └── ...
│   │   ├── transport/             ← Agent 04
│   │   │   ├── RoadNetwork.cs
│   │   │   ├── TrafficSimulator.cs
│   │   │   ├── TransitSystem.cs
│   │   │   ├── RailNetwork.cs
│   │   │   └── ...
│   │   ├── services/              ← Agent 06
│   │   │   ├── CoverageCalculator.cs
│   │   │   ├── fire/
│   │   │   ├── police/
│   │   │   ├── health/
│   │   │   ├── education/
│   │   │   └── utilities/
│   │   └── politics/              ← Agent 07
│   │       ├── ApprovalSystem.cs
│   │       ├── CityCouncil.cs
│   │       ├── LawSystem.cs
│   │       └── ...
│   ├── game/ (GDScript)           ← Agents 01, 05
│   │   ├── build_system/
│   │   ├── zone_system/
│   │   ├── time_system/
│   │   ├── event_system/
│   │   ├── save_system/
│   │   ├── EventBus.gd
│   │   └── AudioManager.gd
│   ├── render/ (GDScript)         ← Agents 04, 05
│   │   ├── CameraController.gd
│   │   ├── tile_renderer/
│   │   ├── vehicle_renderer/
│   │   ├── citizen_renderer/
│   │   ├── weather/
│   │   └── overlays/
│   └── ui/ (GDScript)            ← Agent 08
│       ├── hud/
│       ├── panels/
│       ├── menus/
│       ├── stats/
│       ├── tutorial/
│       └── tooltip/
├── scenes/                        ← Agents 01, 05, 08
│   ├── main/
│   ├── ui/
│   ├── buildings/
│   ├── vehicles/
│   └── effects/
├── assets/                        ← Agent 09
│   ├── sprites/ (~930 .png)
│   ├── shaders/ (8 .gdshader)
│   ├── audio/ (89 assets)
│   └── fonts/
├── data/                          ← Agents 02-07
│   ├── buildings.json (~260 entries)
│   ├── technologies.json (102 entries)
│   ├── laws.json (32 entries)
│   ├── production_chains.json (20 chains)
│   ├── vehicles.json (~120 entries)
│   ├── cultural_presets.json (12 presets)
│   ├── factions.json (5 factions)
│   ├── events.json (~80 events)
│   └── schemas/
└── docs/
    ├── GAME_FEEL_BIBLE.md         ← Agent 10
    ├── AGENT_01_HANDOFF.md
    ├── AGENT_02_HANDOFF.md
    └── ... (10 handoff docs)
```

---

## 10. GETTING STARTED

### Day 1 Morning Checklist
1. Start Agent 01 (Core Engine) -- PRIORITY, blocks everything
2. Start Agent 09 (Art) -- terrain tiles can begin immediately
3. Start Agent 10 (Game Feel Research) -- no dependencies

### Day 1 Afternoon
4. Agents 02-08 prepare their interface contracts (C# interfaces)
5. Agents 02-08 set up their folder structure and stub implementations

### Day 2 Morning (after Agent 01 delivers grid + EventBus)
6. Launch all remaining agents in parallel
7. Each agent works against stubs for cross-agent dependencies

### Weekly Integration Check
- Every Friday: merge all agent outputs into single project
- Run build, fix compilation errors
- Test basic gameplay loop
- Profile performance
- Update task priorities for next week
