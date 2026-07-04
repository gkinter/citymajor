# AGENT 10: GAME FEEL & UX RESEARCH

## Role
Research what makes the best city builders *feel* satisfying. Study successful games (SimCity 4, Cities Skylines, Anno 1800, Transport Tycoon, Factorio, Stardew Valley, Frostpunk). Distill findings into a GAME_FEEL_BIBLE.md that every other agent references for UX decisions.

**This agent produces NO code. It produces reference documents that guide all other agents.**

---

## Phase 1: Placement Feel Research (Day 1-3)

### Research Questions
1. **Why does road-drawing feel good in Cities Skylines?**
   - What happens on mouse-down vs mouse-drag vs mouse-up?
   - How does the preview/ghost system work?
   - What audio feedback plays?
   - How are invalid placements communicated?
   - What's the snap behavior?

2. **Why does building placement feel satisfying in SimCity 4?**
   - How does the grid-snap work?
   - What's the "thunk" sound design?
   - How is cost feedback shown during placement?
   - How are demolition consequences previewed?

3. **Why does Factorio's belt placement feel perfect?**
   - Automatic direction detection
   - Underground belt auto-pairing
   - Blueprint system for mass placement
   - How does it handle invalid placements without frustrating?

4. **Zone painting: SimCity 4 vs Cities Skylines**
   - Brush sizes, fill modes
   - Visual feedback during paint
   - How quickly do zones populate after painting?
   - Is there a satisfaction moment when buildings grow?

### AI Tool Usage
- **Claude**: Research each game's UX mechanics via web search + documentation
- **YouTube analysis**: Watch gameplay videos, note specific UX moments
- **Web search**: Find GDC talks about city builder UX
- **Kimi**: Screenshot moments of great UX -> annotate what makes them work

### Output
- `GAME_FEEL_BIBLE.md` Section 1: Placement Feel
  - Best practices for ghost/preview systems
  - Audio feedback timing guidelines
  - Invalid placement communication patterns
  - Snap behavior rules

---

## Phase 2: Information Architecture Research (Day 3-5)

### Research Questions
1. **How does Anno 1800 display complex supply chains without overwhelming?**
   - Progressive disclosure pattern
   - What's hidden by default vs. shown on hover?
   - How are production bottlenecks visualized?
   - Color coding system for chain status

2. **How does Cities Skylines handle 15+ overlays?**
   - Toggle UX, hotkey mapping
   - Color scale consistency across overlays
   - Information density at different zoom levels
   - Which overlays are used most? (community data)

3. **How does SimCity 4's budget screen make finance fun?**
   - Visual design of the budget panel
   - Feedback loops (change tax -> see immediate revenue preview)
   - Historical data presentation
   - Audio cues for positive/negative budget

4. **How does Transport Tycoon present route profitability?**
   - Per-route P&L display
   - Vehicle utilization metrics
   - Route map overlay design
   - When does it show warnings vs. require player to check?

5. **Tooltip design across city builders**
   - How much info is too much?
   - Tooltip vs. info panel threshold
   - Best tooltip visual designs
   - Contextual tooltip variations

### AI Tool Usage
- **Claude**: Analyze information architecture patterns across games
- **Web search**: Find UI/UX case studies for city builder games
- **Kimi**: Screenshot UI panels from each game -> annotate design patterns
- **Kimi**: Screenshot tooltip designs -> document best practices

### Output
- `GAME_FEEL_BIBLE.md` Section 2: Information Architecture
  - Progressive disclosure guidelines
  - Tooltip design rules
  - Overlay color standardization
  - Budget/statistics panel layout principles
  - Data density by zoom level

---

## Phase 3: Growth & Satisfaction Research (Day 5-7)

### Research Questions
1. **What makes watching a city grow satisfying?**
   - Building growth animations (SimCity: pop-up, Cities Skylines: construction crane)
   - Skyline evolution over time
   - Population milestone celebrations
   - Visual density progression (empty -> suburb -> city -> metropolis)

2. **Stardew Valley's "one more day" loop -- why does it work?**
   - Day length tuning (not too short, not too long)
   - End-of-day summary (what happened today?)
   - Tomorrow's promise (crops growing, events coming)
   - Seasonal anticipation
   - How to adapt this for a city builder's time scale

3. **Frostpunk's pressure and consequence communication**
   - How does it make cold *feel* dangerous through UI alone?
   - Citizen desperation quotes (how to adapt for city builders)
   - The "hope" and "discontent" meters
   - Sound design for crisis escalation

4. **Factorio's "factory must grow" compulsion**
   - Why is expansion so satisfying?
   - How do bottlenecks create engagement (not frustration)?
   - Production graph as satisfaction metric
   - Optimization as endgame content

5. **Era transition moments**
   - Civilization's era-change cinematics
   - How to make technology unlocks feel momentous
   - Architecture style change as reward (your city visually evolves)
   - Audio shift when entering new era

### AI Tool Usage
- **Claude**: Research game design theory on satisfaction loops
- **Web search**: GDC talks on game feel, "juice" in games
- **Web search**: Community discussions on what makes city builders addictive
- **Kimi**: Screenshot before/after city growth -> document visual progression

### Output
- `GAME_FEEL_BIBLE.md` Section 3: Growth & Satisfaction
  - Milestone celebration design guidelines
  - "One more turn" loop structure
  - Crisis communication patterns
  - Era transition ceremony design
  - Growth visualization best practices

---

## Phase 4: Tutorial & Onboarding Research (Day 7-8)

### Research Questions
1. **How does Factorio teach 200+ mechanics without tutorials?**
   - Recipe unlocking as natural pacing
   - Achievement-based guidance
   - Freeform vs. guided objectives
   - Community wiki as design philosophy

2. **SimCity 4's advisor system**
   - When do advisors speak? (threshold triggers)
   - How helpful vs. annoying is the advice?
   - How to make advisors feel like characters, not error messages

3. **Cities Skylines' milestone unlocks**
   - Population gates for feature unlocks
   - Natural complexity curve
   - What's available at start vs. what unlocks?
   - Does it feel restrictive or does it feel rewarding?

4. **Stardew Valley's first-hour experience**
   - How much is explained vs. discovered?
   - Visual cues that guide without text
   - The Parsnip tutorial: minimal text, maximum learning
   - How to adapt gentle onboarding for a complex simulation

### AI Tool Usage
- **Claude**: Research tutorial design patterns in strategy/simulation games
- **Web search**: "Best tutorial design in city builders" community discussions
- **Web search**: GDC talks on onboarding in complex games

### Output
- `GAME_FEEL_BIBLE.md` Section 4: Tutorial & Onboarding
  - First 30 minutes script
  - Feature unlock pacing table
  - Advisor trigger guidelines
  - "Show don't tell" implementation patterns

---

## Phase 5: Audio & Feedback Research (Day 8-9)

### Research Questions
1. **Sound design in city builders**
   - What ambient sounds play at different zoom levels?
   - Construction sound timing (satisfying "placed!" confirmation)
   - Cash register sound for income -- when/how?
   - Music that matches game state (peaceful city vs. crisis)

2. **Screen shake and visual punch**
   - When is screen shake appropriate? (demolition, disaster, milestone)
   - Magnitude guidelines (subtle vs. dramatic)
   - Number pop-ups ("+$500" floating text)
   - Particle bursts on important moments

3. **Notification design**
   - Priority levels: info, warning, critical
   - Audio cues per priority
   - Visual design: toast vs. banner vs. modal
   - Dismissal behavior
   - Notification queue (don't spam the player)

### AI Tool Usage
- **Claude**: Research audio design patterns in simulation games
- **Web search**: Audio design in city builders, game sound design theory
- **YouTube**: Listen to SimCity 4, Cities Skylines, Anno 1800 soundtracks for analysis

### Output
- `GAME_FEEL_BIBLE.md` Section 5: Audio & Feedback
  - Sound trigger timing chart
  - Screen shake magnitude guidelines
  - Notification priority system
  - Music state machine design
  - Number pop-up animation specs

---

## Phase 6: Competitive UX Audit (Day 9-10)

### Tasks
1. **Play/watch 30 minutes of each reference game**:
   - SimCity 4 (depth, budget pressure)
   - Cities Skylines (ease of use, modding)
   - Anno 1800 (production chains, beauty)
   - Transport Tycoon / OpenTTD (transport depth)
   - Factorio (optimization loop)
   - Stardew Valley (pixel art warmth, one-more-day)
   - Frostpunk (consequence weight, atmosphere)
   - Workers & Resources: Soviet Republic (deep simulation)
   - Tropico 6 (politics, humor)

2. **Per-game audit template**:
   - 3 things this game does BETTER than any other
   - 3 things this game does POORLY
   - 1 unique mechanic worth stealing
   - UX screenshots with annotations

3. **Synthesis**: What should Iron & Oak steal from each?

### AI Tool Usage
- **Claude**: Research each game's strengths/weaknesses from reviews + community
- **Web search**: "Best mechanics in [game name]" community analysis
- **Kimi**: Screenshots from gameplay -> annotate what to copy vs. avoid

### Output
- `GAME_FEEL_BIBLE.md` Section 6: Competitive Audit
  - Per-game steal list
  - Anti-patterns to avoid
  - Iron & Oak's unique differentiation opportunities

---

## Final Deliverable: GAME_FEEL_BIBLE.md

### Structure
```
# GAME FEEL BIBLE - Iron & Oak

## 1. Placement Feel
   - Ghost/preview system spec
   - Snap behavior rules
   - Audio trigger timing
   - Invalid placement patterns

## 2. Information Architecture
   - Progressive disclosure rules
   - Tooltip design spec
   - Overlay standards
   - Panel layout principles

## 3. Growth & Satisfaction
   - Milestone celebration design
   - "One more turn" loop
   - Crisis communication
   - Era transition ceremony

## 4. Tutorial & Onboarding
   - First 30 minutes script
   - Feature unlock table
   - Advisor guidelines
   - Show-don't-tell patterns

## 5. Audio & Feedback
   - Sound trigger chart
   - Screen shake specs
   - Notification system
   - Music state machine

## 6. Competitive Audit
   - Per-game steal list
   - Anti-patterns
   - Differentiation plan

## 7. Pixel Art UX Specific
   - Readability at each zoom level
   - Color coding consistency
   - Animation timing for game speed
   - Icon design rules (16x16 icons)
```

---

## Dependencies

### This Agent Needs
| From | What | When |
|------|------|------|
| None | This agent works independently | Starts Day 1 |

### This Agent Provides
| To | What | Format |
|----|------|--------|
| ALL agents | GAME_FEEL_BIBLE.md | Reference document |
| Agent 08 (UI/UX) | Specific UI patterns to implement | Design specs |
| Agent 09 (Art) | Art style references, animation timing | Guidelines |
| Agent 01 (Core) | Camera feel, time speed guidelines | Design specs |

---

## Estimated Duration
- **With AI**: 8-10 days (research-heavy, no code)
- **Can start**: Day 1 (no dependencies)
- **Key output**: GAME_FEEL_BIBLE.md available by Day 5 (first draft), Day 10 (complete)
- **Note**: This agent's output improves every other agent's quality
