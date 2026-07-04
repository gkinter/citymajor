# Research: 08 Game Feel Audit

The file at `/Users/fredericbeeg/citymajor/citymajor/docs/design/GAME_FEEL_BIBLE.md` is currently a placeholder with no actual content. There is nothing to audit against -- the document hasn't been written yet.

However, based on your description of the 7 sections it *will* cover, here is the complete gap analysis. Every item below is a UX/game-feel topic that a city builder of this scope needs and that is not listed among your 7 planned sections.

---

## Gap Analysis: Missing Topics

### 1. Camera Controls Feel
No section on pan acceleration curves, zoom smoothing (lerp vs. spring), zoom-to-cursor behavior, rotation snapping (if rotation is supported), edge-scrolling speed ramps, middle-mouse-drag feel, or camera momentum/inertia on release. Camera is the single most-used input in a city builder -- it needs its own subsection with exact easing curves and dead-zone values.

### 2. Map Edge Behavior
What happens when the player pans to the world boundary? Hard stop, elastic bounce, fade-to-fog, or infinite scroll with content culling? This affects feel significantly and interacts with minimap design.

### 3. Minimap Design
No specification for minimap rendering (full color vs. simplified), click-to-navigate, drag-to-pan, viewport rectangle style, zoom-level indicator, alert pips (fire, crime, traffic), or minimap toggle/resize behavior. Critical for a 5-era game where the city grows large.

### 4. Hotkey System Design
No keybind map, modifier conventions (Shift for multi-select, Ctrl for append), conflict resolution, or rebinding UI. Needs a default keymap, a "cheat sheet" overlay toggle, and categorized binding groups (camera, tools, overlays, speed, menus).

### 5. Undo/Redo System
No spec for what actions are undoable (placement, demolition, zone painting, road deletion), undo stack depth, visual feedback on undo (ghost reappears, cost refunded), redo behavior after a new action, and whether undo works during pause vs. live play. Undo is expected in modern builders.

### 6. Controller / Gamepad Support
No input mapping for dual-stick camera (left stick pan, right stick cursor), radial menus for tool selection, trigger-based placement/demolition, bumper-based overlay cycling, or D-pad menu navigation. Needs cursor-snap-to-grid behavior, aim assist for small buildings, and a "controller mode" HUD that replaces hover tooltips with persistent info panels.

### 7. Touch / Steam Deck Support
No pinch-to-zoom, two-finger-pan, tap-to-select, long-press-for-context-menu, or virtual trackpad specs. Steam Deck's 1280x800 resolution and 7" screen need larger touch targets (minimum 48px), simplified toolbar layouts, and a "deck mode" toggle.

### 8. Keyboard-Only Play
No spec for tab-order through UI panels, arrow-key grid cursor movement, Enter-to-confirm, Escape-to-cancel chains, or spatial focus navigation in the game world without a mouse. Required for accessibility compliance.

### 9. Accessibility Beyond Colorblind
The document mentions colorblind palette but is missing: screen reader support (ARIA-equivalent for game UI, announcement of state changes), motor disability accommodations (one-switch play, sticky keys, adjustable click-hold thresholds, auto-repeat for road drawing), cognitive load options (simplified UI mode, reduced notification frequency, optional pause-before-crisis), text scaling/UI scaling independent of resolution, high-contrast mode, and reduced-motion mode (disable screen shake, slow animations).

### 10. Save/Load UX
No spec for autosave frequency and feedback (subtle icon vs. toast), manual save flow (name, screenshot thumbnail, metadata display), load screen design (city preview, stats summary, era indicator, play time), save file management (rename, delete, duplicate, export), save corruption handling, and cloud save conflict resolution.

### 11. Settings Menu UX
No specification for settings categories (Graphics, Audio, Gameplay, Controls, Accessibility), slider vs. toggle vs. dropdown conventions, live preview of changes, "Apply" vs. instant-apply behavior, "Reset to Default" per-category, graphics preset system (Low/Medium/High/Ultra with auto-detect), and in-game vs. main-menu settings parity.

### 12. Performance Degradation UX
What happens when FPS drops below target? Needs specs for: automatic LOD reduction, simulation speed throttling, visual quality dropdown notifications, "your city is getting large" warnings, optional auto-pause at critical FPS thresholds, and a performance overlay/diagnostics mode for players to self-diagnose.

### 13. Screenshot Mode
No spec for HUD-hide toggle, free camera (unlock from grid constraints), depth-of-field/tilt-shift filter, time-of-day override, resolution multiplier for super-resolution captures, watermark/branding options, and Steam screenshot integration.

### 14. Replay / Timelapse System
No spec for recording city growth over time, playback speed controls, export to video/GIF, camera path keyframing, or "city history" timeline scrubber showing era transitions and major events.

### 15. Achievement Popup Design
No spec for popup position, duration, animation style (slide-in, fade), sound effect, queue behavior when multiple achievements fire, rare achievement emphasis (special particle effect, different sound), and Steam achievement overlay coordination (avoiding double-popup).

### 16. Steam Overlay Integration
No spec for how the game behaves when the Steam overlay opens (auto-pause?), shift-tab responsiveness, Steam screenshot key handling, and Steam Input API integration for controller remapping.

### 17. Mod Browser UX
No spec for in-game mod discovery, install/uninstall flow, mod load order management, mod conflict warnings, mod-induced save incompatibility handling, Workshop integration, and visual feedback for modded content (subtle badge on modded buildings).

### 18. Multiplayer Lobby / Chat UX
If multiplayer is planned: no spec for lobby creation/joining, player list, ready-check, chat UI (position, opacity, fade behavior), spectator mode, pause negotiation (who can pause?), desync detection and recovery, and latency indicator.

### 19. City Comparison / Statistics Export
No spec for cross-save city comparison (population curves, revenue graphs), statistics export format (CSV, JSON, image), shareable city cards (auto-generated infographic with key stats), or leaderboard integration.

### 20. Multi-Monitor Support
No spec for UI panel detachment to second monitor (stats dashboard, overlay controls), extended viewport rendering, or cursor confinement behavior.

### 21. Pause Menu / Game Speed UX
No detailed spec for pause/play/fast-forward/ultra-fast controls, visual treatment when paused (desaturation, "PAUSED" indicator, frozen particle effects), whether building is allowed while paused, and speed indicator positioning.

### 22. Notification Queue / Priority System
Section 5 mentions notifications but lacks a priority/queue system spec: how many notifications can stack, priority ordering (disaster > milestone > advisor tip), notification history log, "do not disturb" mode during focused building, and per-category mute toggles.

### 23. Context Menu / Right-Click Design
No spec for right-click behavior on buildings (inspect, upgrade, demolish, relocate), on roads (split, upgrade, one-way toggle), on zones (rezone, clear), or on empty land (quick-place, query terrain). Context menus need pixel-art-consistent styling.

### 24. Drag Selection / Multi-Select
No spec for box-select multiple buildings, shift-click to add to selection, selection outline rendering, batch operations on selected items (demolish all, upgrade all, move group), and selection count indicator.

### 25. LLM Integration UX
Your game features LLM integration but there's no spec for: LLM response latency handling (typing indicator, skeleton text), fallback when LLM is unavailable (offline mode, cached responses), prompt-in-progress cancellation, response quality indicators, and conversation history UI for advisor interactions.

### 26. Loading Screen Design
No spec for initial load, era-transition loads, or chunk-streaming loads. Needs: progress bar style, loading tips/lore, animated pixel art vignettes, estimated time remaining, and graceful handling of slow loads.

### 27. First-Time Experience / FTUE Flow
Section 4 covers tutorial philosophy but lacks the specific FTUE flow: splash screen sequence, main menu first-visit state, "new city" wizard (map selection, difficulty, era start, city name), and the exact first 5 minutes of guided play.

### 28. Error / Crash Recovery UX
No spec for crash detection, "your game crashed" recovery dialog, crash report submission flow, auto-recovery from last autosave, and graceful handling of mod-induced crashes.

### 29. Tooltip Delay and Behavior
Section 2 mentions tooltips but lacks timing specs: hover delay before show (typically 300-500ms), fade-in duration, tooltip anchor behavior (follow cursor vs. fixed position), tooltip dismissal on scroll/zoom, nested tooltip prevention, and tooltip content hierarchy (title, value, trend, explanation).

### 30. Demolition Feel
No dedicated spec for demolition feedback: destruction animation (dust cloud, debris particles), sound design (crunch, crash scaled to building size), cost display, "are you sure" threshold (single building = instant, block demolish = confirm), and undo window after demolition.

### 31. Time/Date Display and Calendar
No spec for in-game calendar UI, season indicators, day/night cycle feedback (if present), year counter prominence, and how the 1850-2050+ timeline maps to real play time at each speed setting.

### 32. Budget / Financial Management UX
Section 2 mentions financial display but lacks: budget allocation sliders, tax rate adjustment UI, loan management interface, financial forecast graphs, deficit warnings, and bankruptcy cascade UX.

### 33. Map Generation / Selection UX
No spec for map preview, seed input, map parameter sliders (water level, terrain roughness, resource density), map size selection, and pre-made scenario maps.

---

**Summary**: The planned 7 sections cover core placement, information display, satisfaction loops, tutorial philosophy, audio/feedback, competitive audit, and pixel art constraints. The major structural gaps fall into four categories:

1. **Input methods** (gaps 1, 6, 7, 8, 24) -- the document assumes mouse+keyboard and doesn't spec alternative inputs
2. **System-level UX** (gaps 5, 10, 11, 12, 16, 20, 28) -- save/load, settings, performance, crash recovery
3. **Accessibility** (gap 9) -- only colorblind is covered; motor, cognitive, and visual impairments are unaddressed
4. **Meta-game UX** (gaps 13, 14, 15, 17, 18, 19) -- screenshot, replay, achievements, mods, multiplayer, stats