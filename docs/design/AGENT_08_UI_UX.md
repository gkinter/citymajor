# AGENT 08: UI/UX SYSTEM

## Role
Build all user interface: HUD, panels, menus, overlays, tooltips, data visualizations, statistics, advisors, news ticker, and tutorial system. This is the player's window into every other system.

---

## Prerequisites
- Agent 01: EventBus, all data schemas
- Agent 10 (reference): GAME_FEEL_BIBLE.md for UX patterns

---

## Phase 1: HUD & Core UI Framework (Day 5-7)

### Tasks
1. **UI Framework**:
   - Godot Control nodes for all UI
   - Theme system: wood/metal texture panels matching current era
   - Retro pixel font for headers ("Press Start 2P" style)
   - Clean pixel font for data/numbers
   - Consistent color coding: green=good, yellow=warning, red=bad
   - All UI scales with resolution (responsive)

2. **Main HUD** (always visible):
```
┌─────────────────────────────────────────────────────┐
│ [Date: Mar 1850]  [Speed: >>]  [$12,450]  [Pop: 247]│
│ [Era: Frontier]   [Approval: 72%]                    │
├─────────────────────────────────────────────────────┤
│                                                      │
│              [GAME VIEWPORT]                         │
│                                                      │
├─────────────────────────────────────────────────────┤
│ [Road][Zone][Build][Demolish][Transit][Overlay][Stats]│
│ [Tool Options Panel - context sensitive]             │
└─────────────────────────────────────────────────────┘
│ [News ticker scrolling: "New families moving to...]  │
└─────────────────────────────────────────────────────┘
```

3. **Toolbar System**:
   - Road tools: draw, upgrade, one-way, demolish
   - Zone tools: R/C/I/A/O/P paint, density selector
   - Build menu: categorized (services, utilities, transit, special)
   - Demolish tool: click or drag
   - Overlay toggle: 15 overlay options
   - Statistics button: open stats panel

4. **Context Panel**:
   - Appears at bottom when tool selected
   - Shows: cost, requirements, current tool options
   - Preview: ghost placement with validity highlighting

### AI Tool Usage
- **Claude**: Generate GDScript UI controllers, theme system
- **Kimi**: Screenshot of SimCity 4 HUD -> generate Godot .tscn layout
- **Kimi**: Screenshot of Cities Skylines toolbar -> adapt for pixel art style
- **Kimi**: Screenshot of Transport Tycoon info bar -> generate equivalent
- **Art AI**: Generate pixel art UI panel textures (wood grain, metal, paper)

### Output Files
- `scenes/ui/HUD.tscn`
- `scripts/ui/hud/HUDController.gd`
- `scripts/ui/hud/Toolbar.gd`
- `scripts/ui/hud/ContextPanel.gd`
- `scripts/ui/hud/NewsTicker.gd`
- `assets/ui/theme_frontier.tres` (+ themes per era)

---

## Phase 2: Information Panels (Day 7-10)

### Tasks
1. **Budget Panel**:
   - Revenue breakdown (11 sources, bar chart)
   - Expense breakdown (14 categories, bar chart)
   - Balance: surplus/deficit with trend arrow
   - Tax sliders with real-time preview
   - Loan status, interest rate
   - Historical graph (toggle revenue/expense/balance over time)

2. **Building Info Panel** (click on any building):
   - Building name, type, era
   - Occupants/capacity
   - Condition (with aging bar)
   - Services coverage
   - Land value
   - Issues (if any: no power, overcrowded, etc.)
   - Actions: upgrade, renovate, demolish

3. **Transit Panel**:
   - Route list with ridership/profit per route
   - Create/edit/delete routes
   - Vehicle assignment
   - Frequency slider
   - Route map overlay

4. **Demographics Panel**:
   - Population pyramid (age x gender)
   - Wealth distribution bar chart
   - Education level breakdown
   - Employment by sector
   - Cultural DNA radar chart (8 dimensions)
   - Migration trends (in/out per month)

5. **Research Panel**:
   - Tech tree visualization (zoomable, scrollable)
   - Current research progress bar
   - Funding slider
   - Eureka bonus indicators
   - Branching decision highlights

6. **Politics Panel**:
   - Council composition (9 seats, faction colors)
   - Approval gauge
   - Active laws list
   - Propose law button -> law browser
   - Election countdown
   - Lobbying offers

7. **Trade Panel**:
   - Export/import table (resource, quantity, price)
   - Trade balance chart
   - Trade partner cities
   - Market price trends

### AI Tool Usage
- **Claude**: Generate all panel GDScript controllers with data binding
- **Kimi**: Screenshot of Anno 1800 production panel -> adapt layout
- **Kimi**: Screenshot of Civilization tech tree UI -> adapt for research panel
- **Kimi**: Screenshot of SimCity budget screen -> adapt with pixel art style
- **Kimi**: Screenshot of Transport Fever route management -> adapt
- **Art AI**: Generate panel background textures per era

### Output Files
- `scenes/ui/panels/BudgetPanel.tscn`
- `scenes/ui/panels/BuildingInfoPanel.tscn`
- `scenes/ui/panels/TransitPanel.tscn`
- `scenes/ui/panels/DemographicsPanel.tscn`
- `scenes/ui/panels/ResearchPanel.tscn`
- `scenes/ui/panels/PoliticsPanel.tscn`
- `scenes/ui/panels/TradePanel.tscn`
- `scripts/ui/panels/` (matching .gd files for each)

---

## Phase 3: Overlay System (Day 10-11)

### Tasks
1. **15 Overlay Types**:
   - Traffic: green/yellow/red per road segment
   - Land Value: blue-green-yellow-red heatmap
   - Crime: intensity heatmap with police circles
   - Pollution: air/noise/ground layers
   - Services: coverage circles for each service type
   - Zoning: zone color overlay
   - Transit: route lines with ridership thickness
   - Power/Water: grid connectivity, pressure/voltage
   - Happiness: per-tile satisfaction heatmap
   - Commute: average commute time heatmap
   - Demographics: age/wealth distribution
   - Industry: production chain flows
   - Resources: deposit locations + extraction rates
   - Education: coverage + literacy rates
   - Health: coverage + disease risk

2. **Overlay Rendering**:
   - Dedicated TileMap layer for overlay heatmaps
   - Semi-transparent color mapping
   - Legend panel showing color scale
   - Toggle with hotkeys (1-9 for common overlays)
   - Multiple overlays can layer (e.g., traffic + transit)

3. **Overlay Data Pipeline**:
   - Each simulation system exposes overlay data as 2D array
   - UI system samples this data per chunk
   - Updates on dirty-flag basis (not every frame)
   - Smooth color interpolation for transitions

### AI Tool Usage
- **Claude**: Generate overlay renderer with heatmap color mapping
- **Claude**: Generate hotkey system for overlay toggling
- **Kimi**: Screenshot of Cities Skylines traffic overlay -> adapt color scheme
- **Kimi**: Screenshot of SimCity pollution overlay -> adapt visual style

### Output Files
- `scripts/render/overlays/OverlayManager.gd`
- `scripts/render/overlays/HeatmapRenderer.gd`
- `scripts/render/overlays/CoverageCircleRenderer.gd`
- `scripts/render/overlays/TransitRouteRenderer.gd`

---

## Phase 4: Statistics & Charts (Day 11-12)

### Tasks
1. **Statistics Panel**:
   - Historical graphs for ANY metric over time
   - Line charts, bar charts, pie charts, area charts
   - Zoom: last month / year / decade / all time
   - Compare two metrics on same chart
   - Export data (clipboard copy for player analysis)

2. **Chart Rendering** (custom Godot drawing):
   - Line chart: `_draw()` with anti-aliased lines
   - Bar chart: filled rectangles with value labels
   - Pie chart: arc segments with percentage labels
   - Area chart: filled polygon under line
   - All charts: pixel art styling (chunky, retro feel)

3. **Advisor System**:
   - 6 advisors: Transport, Economy, Safety, Environment, Education, Health
   - Each advisor monitors their domain
   - Priority alerts: "Traffic congestion critical on Main Street!"
   - Recommendations: "Build a fire station in the south district"
   - Advisor portraits (pixel art, era-appropriate clothing)

### AI Tool Usage
- **Claude**: Generate chart rendering system in GDScript
- **Claude**: Generate advisor AI logic (trigger conditions, message templates)
- **Art AI**: Generate 6 advisor portraits in pixel art style
- **Kimi**: Screenshot of SimCity advisor panel -> adapt layout

### Output Files
- `scripts/ui/stats/StatisticsPanel.gd`
- `scripts/ui/stats/ChartRenderer.gd`
- `scripts/ui/stats/AdvisorSystem.gd`
- `scenes/ui/stats/StatisticsPanel.tscn`

---

## Phase 5: Menus & Tutorial (Day 12-13)

### Tasks
1. **Main Menu**:
   - New Game (map selection, difficulty, cultural preset)
   - Load Game (save browser with thumbnails)
   - Settings (video, audio, gameplay, controls)
   - Scenarios (8 curated challenges)
   - Quit

2. **Settings**:
   - Resolution, fullscreen, VSync
   - Master/Music/SFX volume
   - Game speed default
   - Auto-save frequency
   - Keybinding customization
   - Accessibility: colorblind mode, text size, screen reader support

3. **Tutorial System**:
   - Progressive disclosure (don't dump everything at once)
   - Guided first 30 minutes: place roads, zone, build fire station, connect power
   - Contextual tooltips: hover over anything for explanation
   - "Learn by doing" missions (mini objectives)
   - Can skip/disable tutorial

4. **Tooltips**:
   - Every UI element has tooltip
   - Building tooltips: name, cost, effect, requirements
   - Tile tooltips: zone type, land value, services, issues
   - Rich tooltips with icons and mini-charts

### AI Tool Usage
- **Claude**: Generate menu system, settings, tutorial sequence
- **Kimi**: Screenshot of Stardew Valley main menu -> adapt pixel art style
- **Kimi**: Screenshot of Factorio tooltip system -> adapt information density
- **Art AI**: Generate main menu background (city panorama, pixel art)

### Output Files
- `scenes/ui/menus/MainMenu.tscn`
- `scenes/ui/menus/SettingsMenu.tscn`
- `scenes/ui/menus/NewGameMenu.tscn`
- `scenes/ui/menus/SaveBrowser.tscn`
- `scripts/ui/tutorial/TutorialManager.gd`
- `scripts/ui/tooltip/TooltipSystem.gd`

---

## Dependencies

### This Agent Needs
| From | What | When |
|------|------|------|
| Agent 01 | EventBus, data schemas | Phase 1 |
| ALL agents | Data APIs for displaying information | Phase 2+ |
| Agent 09 | UI sprites, panel textures, fonts | Phase 1 (placeholder ok) |
| Agent 10 | GAME_FEEL_BIBLE.md for UX patterns | Phase 1 |

### This Agent Provides
| To | What | Format |
|----|------|--------|
| ALL agents | Visual feedback for their systems | .tscn scenes |
| Agent 10 | UI for game feel evaluation | Playable UI |

---

## Estimated Duration
- **With AI**: 7-8 days (HEAVY Kimi usage for layout)
- **Without AI**: 5-6 weeks
- **Can start**: After Agent 01 Phase 6 (Day 4)
- **Note**: This agent has the MOST Kimi image-to-code usage
