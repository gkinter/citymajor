# Research: 14 Ux Systems



# Iron & Oak — Complete Design Specifications

## 1. Camera Controls

### Pan System

```
Input Methods (all active simultaneously):
  WASD / Arrow Keys  → digital pan (8-directional)
  Middle Mouse Drag   → analog pan (free direction)
  Edge Scroll         → directional pan (4 cardinal + 4 diagonal)

Acceleration Curve (WASD/Arrow Keys):
  t=0ms:   0 px/sec
  t=50ms:  60 px/sec   (15% of max)
  t=100ms: 180 px/sec  (45% of max)
  t=150ms: 320 px/sec  (80% of max)
  t=200ms: 400 px/sec  (100% of max)
  Easing:  ease-in-quad → velocity = max_speed * clamp((elapsed / 200.0)^2, 0, 1)
  Deceleration: instant (velocity = 0 on key release, no momentum)

Speed Scaling by Zoom Level:
  Zoom 1x: 400 px/sec max (base)
  Zoom 2x: 200 px/sec max (÷2)
  Zoom 3x: 133 px/sec max (÷3)
  Zoom 4x: 100 px/sec max (÷4)
  Formula:  effective_speed = base_speed / zoom_level

Middle Mouse Drag:
  Activation: middle mouse button down
  Behavior:   1:1 pixel mapping — camera moves opposite to drag direction
  Speed:      uncapped, matches mouse delta directly
  Release:    instant stop, no inertia

Edge Scroll:
  Trigger zone:    20px inset from each viewport edge
  Activation delay: 150ms (cursor must remain in zone for 150ms before scroll begins)
  Speed:           constant 300 px/sec (no acceleration curve)
  Speed scaling:   same zoom divisor as WASD
  Corner behavior: diagonal movement at 300/sqrt(2) ≈ 212 px/sec per axis
  Deactivation:    instant when cursor leaves trigger zone
  Toggle:          can be disabled in Settings > Gameplay
```

### Zoom System

```
Input:          mouse scroll wheel (discrete steps)
Levels:         1x, 2x, 3x, 4x (integer only, no fractional)
Default:        2x on new game
Transition:     instant (no lerp, no animation — pixel art requires clean integer scaling)

Zoom-to-Cursor:
  On scroll up (zoom in):
    1. Record mouse position in world space: world_pos = screen_to_world(mouse_pos)
    2. Increase zoom level by 1 (clamp at 4)
    3. Calculate new screen position of world_pos at new zoom
    4. Offset camera so world_pos remains under mouse cursor
    Formula:
      new_camera_offset = world_pos - (mouse_pos / new_zoom)

  On scroll down (zoom out):
    Same logic, decrease zoom level by 1 (clamp at 1)

Viewport Math (320x180 native):
  Zoom 1x: viewport shows 320x180 world pixels → display at 1:1
  Zoom 2x: viewport shows 160x90 world pixels  → each pixel = 2x2 display pixels
  Zoom 3x: viewport shows 106x60 world pixels  → each pixel = 3x3 display pixels
  Zoom 4x: viewport shows 80x45 world pixels   → each pixel = 4x4 display pixels
```

### Rotation System

```
Input:        Q (rotate left/CCW), E (rotate right/CW)
Snap:         90° increments only
Orientations: 4 total (North, East, South, West — or 0°, 90°, 180°, 270°)
Transition:   instant (no animation — pixel art sprites swap, not rotate)
Default:      North (0°) on new game

Implementation:
  Each tile has 4 pre-rendered sprite variants (one per orientation)
  On rotation:
    1. Record world position at screen center
    2. Swap all tile sprites to new orientation variant
    3. Recalculate isometric projection matrix
    4. Reposition camera so the same world tile remains at screen center

  Isometric transform per orientation:
    North (0°):   screen_x = (tile_x - tile_y) * 16,  screen_y = (tile_x + tile_y) * 8
    East  (90°):  screen_x = (tile_y + tile_x) * 16,  screen_y = (tile_y - tile_x) * 8
    South (180°): screen_x = (tile_y - tile_x) * 16,  screen_y = -(tile_x + tile_y) * 8
    West  (270°): screen_x = -(tile_x + tile_y) * 16, screen_y = (tile_x - tile_y) * 8
```

### Map Edge Behavior

```
Hard boundary:     camera cannot pan beyond map bounds
Elastic bounce:    32px overshoot allowed, then spring back

On reaching edge:
  1. Allow camera to travel 32px past the boundary
  2. Apply spring-back force immediately
  3. Spring-back duration: 100ms
  4. Easing: ease-out-cubic
  5. Final position: exactly at boundary (0px overshoot)

Spring-back curve (pixels past boundary over time):
  t=0ms:   32px overshoot
  t=25ms:  18px
  t=50ms:  8px
  t=75ms:  2px
  t=100ms: 0px (at boundary)

  Formula: overshoot = 32 * (1 - (t/100))^3

Edge detection:
  world_min_x, world_min_y = 0, 0
  world_max_x = map_width_tiles * 32
  world_max_y = map_height_tiles * 16
  Camera clamped to: [world_min - 32, world_max + 32] during pan
  Spring-back triggers when camera is outside [world_min, world_max]
```

---

## 2. Undo/Redo System

### Action Stack Architecture

```
Data Structure:
  undo_stack: Array[UndoAction], max size 50
  redo_stack: Array[UndoAction], cleared on any new action

UndoAction {
    action_type:   enum (ROAD_PLACE, ZONE_PAINT, BUILDING_PLACE, DEMOLISH, ROAD_UPGRADE)
    timestamp:     game time when action occurred
    tiles:         Array[TileCoord]          — all tiles affected
    previous_state: Array[TileState]         — state before action (for undo)
    new_state:     Array[TileState]          — state after action (for redo)
    cost:          int                       — currency spent
    refund:        int                       — currency returned (always equals cost)
    ghost_sprites: Array[SpriteData]         — for visual feedback on undo
  }

TileState {
    tile_pos:      Vector2i
    terrain_type:  enum
    zone_type:     enum or null
    building_id:   int or null
    road_type:     enum or null
    road_level:    int (0=none, 1=dirt, 2=paved, 3=avenue)
    metadata:      Dictionary               — building rotation, upgrade level, etc.
  }
```

### Undoable vs Non-Undoable Actions

```
UNDOABLE (mutate the physical map):
  ┌─────────────────────┬───────────────────────────────────────────┐
  │ Action              │ Undo Behavior                             │
  ├─────────────────────┼───────────────────────────────────────────┤
  │ Road placement      │ Remove road, restore previous terrain     │
  │ Zone painting       │ Remove zone designation from tiles        │
  │ Building placement  │ Remove building, restore terrain          │
  │ Demolition          │ Restore demolished structure exactly       │
  │ Road upgrade        │ Downgrade road to previous level          │
  └─────────────────────┴───────────────────────────────────────────┘

NOT UNDOABLE (simulation state, policy, irreversible game logic):
  - Time advancement (cannot reverse simulation ticks)
  - Tax rate changes (economic effects already propagated)
  - Law enactment (citizen behavior already modified)
  - Research selection (research points already allocated)
  - Budget allocation changes
  - Speed changes

  Attempting Ctrl+Z when only non-undoable actions have occurred:
    → play "denied" sound (soft thud, 80ms)
    → show toast: "Nothing to undo" (Info priority, 3s duration)
```

### Undo/Redo Flow

```
On New Action:
  1. Capture TileState for all affected tiles BEFORE mutation
  2. Execute the action (mutate world state)
  3. Capture TileState for all affected tiles AFTER mutation
  4. Push UndoAction to undo_stack
  5. Clear redo_stack entirely
  6. If undo_stack.size > 50: remove oldest (index 0)

On Undo (Ctrl+Z):
  1. If undo_stack is empty → play denied sound, show toast, return
  2. Pop action from undo_stack
  3. Restore all tiles to previous_state
  4. Refund action.cost to player treasury (full refund, no depreciation)
  5. Push action to redo_stack
  6. Trigger ghost visual feedback (see below)
  7. Play undo sound: reverse "place" sound, 120ms

On Redo (Ctrl+Y):
  1. If redo_stack is empty → play denied sound, show toast, return
  2. Pop action from redo_stack
  3. Apply all tiles to new_state
  4. Deduct action.cost from player treasury
  5. Push action to undo_stack
  6. Play redo sound: normal "place" sound, 100ms
```

### Ghost Visual Feedback

```
On undo of a placement action (road, zone, building):
  1. At each affected tile, spawn a ghost sprite:
     - Sprite: the removed element's sprite
     - Modulate: white with alpha 0.5
     - Shader: outline glow, 1px, color #FFD700 (gold)
  2. Ghost lifetime: 2000ms
  3. Ghost fade-out:
     t=0ms:      alpha 0.5 (appears instantly)
     t=1500ms:   alpha 0.5 (holds)
     t=1500-2000ms: alpha 0.5 → 0.0 (linear fade)
     t=2000ms:   ghost removed from scene tree

On undo of a demolition action:
  1. Restored structure appears with a brief "materialize" effect:
     t=0ms:   alpha 0.0, scale_y 0.5
     t=150ms: alpha 1.0, scale_y 1.0 (ease-out-back)
  2. No ghost — the building is fully restored
```

### Batch Undo for Drag Operations

```
When player drag-paints a zone or drag-places a road:
  All tiles in the drag are recorded as ONE UndoAction
  Undo reverses the entire drag in one Ctrl+Z press
  
  Drag grouping rule:
    Actions within the same mouse-down → mouse-up are one UndoAction
    Multiple discrete clicks are separate UndoActions
```

---

## 3. Controller/Gamepad Support

### Input Mapping (Xbox layout, remappable)

```
┌─────────────────┬────────────────────────────────────────────┐
│ Input           │ Action                                      │
├─────────────────┼────────────────────────────────────────────┤
│ Left Stick      │ Camera pan (analog, with acceleration)      │
│ Right Stick     │ Grid cursor movement (grid-snapped)         │
│ A               │ Confirm / Place                             │
│ B               │ Cancel / Back / Deselect tool                │
│ X               │ Toggle demolish mode                        │
│ Y               │ Toggle info panel for hovered tile          │
│ LB              │ Cycle overlay mode backward (or hold for radial) │
│ RB              │ Cycle overlay mode forward                  │
│ LT (analog)     │ Zoom out (on full press, discrete step)    │
│ RT (analog)     │ Zoom in (on full press, discrete step)     │
│ D-pad Up/Down   │ Navigate tool categories                   │
│ D-pad Left/Right│ Navigate tools within category             │
│ Start           │ Pause menu                                 │
│ Select (Back)   │ Cycle game speed (1x → 2x → 3x → paused → 1x) │
│ L3 (stick click)│ Center camera on cursor                    │
│ R3 (stick click)│ Quick-rotate camera (same as E key)        │
└─────────────────┴────────────────────────────────────────────┘
```

### Left Stick — Camera Pan

```
Dead zone:        0.15 (15% of full deflection ignored)
Acceleration:     same curve as WASD (ease-in-quad over 200ms)
Speed mapping:
  Stick deflection 0.15–0.5:  0–50% of max speed (precision zone)
  Stick deflection 0.5–1.0:   50–100% of max speed (speed zone)
  Formula: speed_factor = smoothstep(0.15, 1.0, stick_magnitude)
  effective_speed = base_speed * speed_factor / zoom_level

Direction:        continuous (not 8-directional), based on stick angle
Release:          instant stop (no momentum)
```

### Right Stick — Grid Cursor

```
Dead zone:        0.25 (higher than pan — precision matters for tile selection)
Movement:         grid-snapped, cursor moves tile-by-tile
Repeat rate:
  Initial move:     instant on crossing dead zone threshold
  Repeat delay:     300ms before auto-repeat begins
  Repeat rate:      150ms per tile (6.67 tiles/sec)
  Acceleration:     after 1.5s of continuous hold, repeat rate → 80ms (12.5 tiles/sec)

Cursor rendering:
  Position:         always at tile center (isometric diamond center)
  Visual:           pulsing highlight on tile, 2px border, color matches current tool
  Pulse:            alpha oscillates 0.6–1.0, period 800ms, sine wave

Diagonal handling:
  Stick angle 0°±22.5°:    move East
  Stick angle 45°±22.5°:   move Southeast
  Stick angle 90°±22.5°:   move South
  (etc. for all 8 directions, mapped to isometric grid)
```

### Trigger Zoom

```
LT/RT are analog but zoom is discrete:
  Activation threshold: 0.8 (80% press)
  On crossing threshold: one zoom step
  Repeat prevention:     must release below 0.3 before next zoom step allowed
  This prevents accidental multi-zoom from a single press
```

### Radial Menu (Tool Wheel)

```
Activation:      hold LB for 300ms
Display:         circular menu, 8 segments, centered on screen
                 radius: 80px (at native 320x180 resolution)
Segments:        one per tool category (Roads, Zones, Buildings, Services, etc.)
Selection:       left stick direction selects segment
                 highlighted segment: scale 1.1x, brighter color
Confirm:         release LB to select highlighted category
Cancel:          press B while wheel is open
Sub-tools:       after selecting a category, D-pad left/right cycles tools within it

Visual:
  Background:     semi-transparent black circle, alpha 0.7
  Segments:       colored icons per category
  Label:          tool name text below icon, 6px font
  Animation:      scale from 0 to 1 over 150ms (ease-out-back)
  Dismiss:        scale from 1 to 0 over 100ms (ease-in-quad)
```

### Aim Assist

```
When placing buildings/roads with gamepad:
  If cursor is on an invalid tile:
    Search radius:   2 tiles in all directions (diamond-shaped search)
    Priority:        closest valid tile wins (Manhattan distance on iso grid)
    Tie-breaking:    prefer tile closest to stick direction
    Visual:          dotted line from cursor to snapped tile, 1px, white, alpha 0.4
    
  Snap behavior:
    Cursor visual stays at actual position
    Placement preview (ghost building) snaps to valid tile
    A-button places at the snapped position, not the cursor position
    
  Disable:         aim assist can be toggled off in Settings > Controls
```

---

## 4. Steam Deck Support

### Display Configuration

```
Steam Deck native:    1280x800
Game native viewport: 320x180

Integer scaling options:
  4x: 320×4=1280, 180×4=720 → 1280x720 display area
  Letterbox:  (800-720)/2 = 40px black bars top and bottom

  3x: 320×3=960, 180×3=540 → 960x540 centered
  Letterbox:  160px horizontal + 130px vertical (not recommended)

Default on Steam Deck: 4x with 40px letterbox (top 40px black, bottom 40px black)
Letterbox color:       pure black #000000

Alternative: fill mode
  Scale to fill 1280x800: factor 7.111x vertical → not integer → pixel shimmer
  NOT recommended, but available in Settings > Display > "Stretch to Fill (non-integer)"
```

### Touch Input (Steam Deck touchscreen)

```
Touch targets:    minimum 48px at display resolution (= 12px at native 320x180)
All UI buttons:   minimum 48x48 display pixels clickable area
                  Visual size can be smaller, but hit area must be 48x48

Touch mapping:
  Tap:            same as left mouse click (select / place)
  Tap-and-hold:   same as right click (context menu / info panel) — 400ms threshold
  Two-finger tap: cancel / back (same as B button)
  Pinch:          zoom in/out (mapped to discrete integer steps)
                  Pinch thresholds: 20% spread change per zoom step
  Two-finger drag: camera pan (1:1 mapping like middle mouse drag)
  Swipe from left edge: open tool palette
  Swipe from right edge: open notification history
```

### Trackpad Configuration

```
Right trackpad:   mouse emulation (cursor control)
  Sensitivity:    default 1.0, adjustable 0.5–2.0 in settings
  Haptic feedback: 
    On tile boundary crossing: light pulse (intensity 2/10, 15ms)
    On placement confirm:      medium pulse (intensity 5/10, 30ms)
    On invalid placement:      double pulse (intensity 3/10, 15ms gap 50ms 15ms)
    On menu item hover:        micro pulse (intensity 1/10, 8ms)

Left trackpad:    D-pad emulation (tool category navigation)
  Mode:           4-directional click zones
  Haptic:         click feedback on zone activation (intensity 4/10, 20ms)
```

### Back Grip Buttons

```
L4 (rear left):   Undo (Ctrl+Z equivalent)
L5 (rear left 2): Redo (Ctrl+Y equivalent) — if available on Deck model
R4 (rear right):  Screenshot (F12 equivalent, triggers screenshot mode quick capture)
R5 (rear right 2): Toggle HUD visibility — if available on Deck model
```

### Performance Targets

```
Target frame rate:
  Plugged in:     60 fps, uncapped simulation
  On battery:     30 fps target, simulation optimizations active

Battery optimization (when unplugged):
  Simulation tick rate:  reduce from 60/sec to 30/sec
  Particle effects:      reduce particle count by 50%
  Shadow updates:        every 2nd frame instead of every frame
  Background workers:    pathfinding batches halved
  Audio:                 reduce reverb quality (no audible difference on Deck speakers)
  
  Detection: check OS power status every 5 seconds
  Transition: gradual over 500ms when switching power states (no visible hitch)

GPU budget (Steam Deck APU):
  Draw calls:     target < 500 per frame
  Sprite batching: mandatory — combine same-atlas sprites into single draw
  Shader complexity: pixel art shaders only (palette swap, outline, simple lighting)
  VRAM budget:    < 1GB for all loaded textures
```

---

## 5. Keyboard-Only Play

### Complete Keyboard Map

```
┌──────────────────────┬──────────────────────────────────────────┐
│ Key                  │ Action                                    │
├──────────────────────┼──────────────────────────────────────────┤
│ Arrow Keys           │ Move grid cursor (1 tile per press)       │
│ Shift + Arrow Keys   │ Fast cursor movement (5 tiles per press)  │
│ WASD                 │ Camera pan                                │
│ Enter                │ Confirm / Place                           │
│ Escape               │ Cancel / Close panel / Back to game       │
│ Space                │ Pause / Unpause                           │
│ Tab                  │ Cycle focus: Game → Toolbar → Sidebar → Info Panel → Game │
│ Shift + Tab          │ Cycle focus in reverse order              │
│ 1                    │ Select tool: Roads                        │
│ 2                    │ Select tool: Zones                        │
│ 3                    │ Select tool: Residential                  │
│ 4                    │ Select tool: Commercial                   │
│ 5                    │ Select tool: Industrial                   │
│ 6                    │ Select tool: Services                     │
│ 7                    │ Select tool: Parks & Decorations          │
│ 8                    │ Select tool: Utilities                    │
│ 9                    │ Select tool: Special Buildings            │
│ 0                    │ Deselect tool (pointer mode)              │
│ Q                    │ Rotate camera CCW                         │
│ E                    │ Rotate camera CW                          │
│ R                    │ Rotate placement preview (building)       │
│ Delete               │ Demolish selected tile                    │
│ F1                   │ Toggle overlay: Population density        │
│ F2                   │ Toggle overlay: Land value                │
│ F3                   │ Toggle overlay: Happiness                 │
│ F4                   │ Toggle overlay: Traffic                   │
│ F5                   │ Toggle overlay: Pollution                 │
│ F6                   │ Toggle overlay: Crime                     │
│ F7                   │ Toggle overlay: Fire risk                 │
│ F8                   │ Toggle overlay: Services coverage         │
│ N                    │ Open notification history log             │
│ M                    │ Toggle minimap                            │
│ P                    │ Toggle budget panel                       │
│ Ctrl+Z               │ Undo                                      │
│ Ctrl+Y               │ Redo                                      │
│ Ctrl+S               │ Save game                                 │
│ F12                  │ Screenshot mode                           │
│ Ctrl+Shift+S         │ Screenshot mode (alternate)               │
│ + / =                │ Zoom in                                   │
│ - / _                │ Zoom out                                  │
│ [ / ]                │ Game speed decrease / increase             │
└──────────────────────┴──────────────────────────────────────────┘
```

### Focus & Navigation System

```
Focus ring:       2px solid #FFD700 (gold), visible on all focused elements
                  Animated: pulse alpha 0.7–1.0, period 1200ms, sine wave

Tab order:
  1. Game viewport (cursor active, arrow keys move cursor)
  2. Bottom toolbar (left/right arrows cycle tools, Enter selects)
  3. Right sidebar panels (up/down arrows navigate items)
  4. Info panel (if open — up/down scrolls, Escape closes)
  → Wraps back to 1

Within toolbar (when focused):
  Left/Right:     move tool selection highlight
  Up/Down:        switch tool category (same as D-pad on gamepad)
  Enter:          activate selected tool
  Escape:         return focus to game viewport

Grid cursor (when viewport is focused):
  Visible:        always, even without mouse movement
  Appearance:     isometric diamond outline, 2px, white, pulsing
  Arrow keys:     move 1 tile per press
  Shift+arrows:   move 5 tiles per press (hold shift)
  Key repeat:     OS key repeat rate applies (typically 30ms after 500ms delay)
                  No custom acceleration — OS repeat is sufficient
```

### Escape Key Stack

```
Escape behavior (checked in order, first match wins):
  1. Modal dialog open?       → close modal
  2. Screenshot mode active?  → exit screenshot mode
  3. Info panel open?         → close info panel
  4. Overlay active?          → turn off overlay
  5. Tool selected?           → deselect tool (return to pointer)
  6. Submenu open?            → close submenu
  7. Nothing to close?        → open pause menu
```

---

## 6. Save/Load UX

### Autosave System

```
Trigger:          every 5 game-months (not real-time — tied to simulation calendar)
Slot rotation:    3 slots: autosave_1, autosave_2, autosave_3
                  Newest overwrites oldest (circular buffer)
                  
File naming:      autosave_{slot}_{city_name}_{game_date}.sav
                  Example: autosave_1_ironhaven_y12m3.sav

Autosave indicator:
  Position:       top-right corner, 12px from top edge, 12px from right edge (native res)
  Icon:           8x8 pixel floppy disk sprite
  Animation:
    t=0ms:        icon appears, alpha 0.0
    t=0-500ms:    fade in, alpha 0.0 → 1.0 (linear)
    t=500ms:      icon fully visible, hold during save operation
    Save completes:
    t=complete:   hold for 200ms additional
    t+200-700ms:  fade out, alpha 1.0 → 0.0 (linear)
  
  If save takes longer than expected:
    Icon remains visible with a subtle rotation animation (±5°, 400ms period)
    until save completes

Performance during autosave:
  Save runs on a background thread
  Game does NOT pause or stutter
  If frame budget exceeded: defer save to next frame boundary
```

### Manual Save Dialog

```
Trigger:          Ctrl+S or menu > Save Game
Behavior:         opens save dialog overlay (game pauses automatically)

Dialog layout (at native 320x180):
  Background:     full-screen semi-transparent overlay, #000000 alpha 0.6
  Panel:          centered, 240x140px, dark panel with 1px border
  
  Panel contents:
  ┌────────────────────────────────────────────┐
  │  SAVE GAME                          [X]    │  ← title bar, 16px height
  ├────────────────────────────────────────────┤
  │  ┌──────┐                                  │
  │  │128x72│  City: [text input_________]     │  ← auto-populated with city name
  │  │thumb │  Pop:  12,450                    │  ← auto-filled stats
  │  │ nail │  Era:  Industrial                │
  │  │      │  Date: Year 12, Month 3          │
  │  │      │  Time: 2h 34m played             │
  │  └──────┘                                  │
  │                                            │
  │  ┌─ Existing Saves ──────────────────────┐ │
  │  │ ▸ ironhaven_y10m1  Pop:8200  1h20m    │ │  ← scrollable list
  │  │   ironhaven_y8m6   Pop:6100  0h55m    │ │
  │  │   newcity_y3m2     Pop:1200  0h22m    │ │
  │  └───────────────────────────────────────┘ │
  │                                            │
  │         [New Save]    [Overwrite]    [Cancel]│  ← 3 action buttons
  └────────────────────────────────────────────┘

Thumbnail generation:
  Size:           128x72 pixels (native viewport ÷ 2.5, maintains 16:9)
  Capture:        screenshot of current viewport without HUD
  Timing:         captured at moment dialog opens (before overlay renders)
  Format:         embedded in save file as PNG bytes

Save file format:
  Header (64 bytes):
    magic:          "IOAK" (4 bytes)
    version:        uint16 (save format version)
    checksum:       uint32 (CRC32 of data section)
    timestamp:      uint64 (Unix epoch, real-world save time)
    city_name:      char[32] (UTF-8, null-padded)
    game_date:      uint16 year + uint8 month
    population:     uint32
    play_time_sec:  uint32
    thumbnail_size: uint32 (byte length of embedded PNG)
  
  Thumbnail section:
    PNG bytes (variable length, indicated by header)
  
  Data section:
    MessagePack or binary serialized game state
    Compressed with LZ4
```

### Load Screen

```
Layout:
  Grid of save cards, 3 columns, scrollable vertically
  Each card: 72x48px (native resolution)
  
  Card contents:
  ┌──────────────────────┐
  │  [thumbnail 64x36]   │
  │  City Name           │  ← 5px font, truncated with "..." if > 16 chars
  │  Pop: 12,450         │  ← 4px font
  │  Y12 M3 · 2h34m     │  ← 4px font, dimmer color
  └──────────────────────┘

Sort options (top bar):
  [Date ▼] [Name] [Population]
  Default: Date (newest first)

Selection:
  Mouse:    click to select (highlight border), double-click to load
  Keyboard: arrow keys to navigate grid, Enter to load
  Gamepad:  D-pad / left stick to navigate, A to load

Load confirmation:
  "Load [city name]? Unsaved progress will be lost."
  [Load] [Cancel]
  If current game has unsaved changes (any action since last save)

Autosave indicator:
  Autosave cards have a small "AUTO" badge, top-left corner, red background
```

### Save Corruption Handling

```
On load:
  1. Read header, extract checksum
  2. Compute CRC32 of data section
  3. Compare:

  Match → load normally

  Mismatch → corruption dialog:
  ┌──────────────────────────────────────────┐
  │  ⚠ Save File May Be Corrupted           │
  │                                          │
  │  The save file "[name]" failed           │
  │  integrity checks. Loading may cause     │
  │  unexpected behavior.                    │
  │                                          │
  │  [Load Anyway]  [Restore Autosave]  [Cancel] │
  └──────────────────────────────────────────┘

  "Load Anyway":      attempt to deserialize, catch errors gracefully
                      if deserialization fails → "Save file is unreadable. 
                      Please restore from autosave or start a new city."
  
  "Restore Autosave": show only autosave slots in load screen
```

### Steam Cloud Integration

```
Sync behavior:
  Save files stored in: [Steam userdata path]/[app_id]/remote/saves/
  Sync trigger: after every manual save and autosave
  
Conflict resolution (when local and cloud differ):
  Dialog:
  ┌──────────────────────────────────────────────┐
  │  ☁ Save Conflict Detected                    │
  │                                              │
  │  Local save and cloud save differ for:       │
  │  "[city name]"                               │
  │                                              │
  │  Local:  Year 14, Month 2 · Pop 15,200       │
  │          Saved 2 hours ago                    │
  │                                              │
  │  Cloud:  Year 12, Month 8 · Pop 11,400       │
  │          Saved yesterday                     │
  │                                              │
  │  [Keep Local]  [Keep Cloud]  [Keep Newest]   │
  └──────────────────────────────────────────────┘

  "Keep Newest":  compare real-world timestamps, keep more recent
                  upload the winner to cloud
```

---

## 7. Settings Menu

### Menu Structure

```
Settings Panel (overlay, game pauses):
  Position:     centered, 280x160px (native resolution)
  Background:   dark panel #1a1a2e, 1px border #333355
  
  Left column (60px wide): category tabs, vertical
    ▸ Display
      Audio
      Gameplay
      Controls
      Accessibility

  Right area (220px wide): settings for selected category
  
  Bottom bar:  [Apply]  [Reset to Default]  [Back]
```

### Display Settings

```
┌─────────────────────────────────┬──────────────────────────────┐
│ Setting                         │ Options                       │
├─────────────────────────────────┼──────────────────────────────┤
│ Resolution                      │ Dropdown: detected resolutions │
│                                 │ Default: desktop native       │
│ Window Mode                     │ Fullscreen / Windowed /       │
│                                 │ Borderless Windowed           │
│ VSync                           │ On / Off (default: On)        │
│ Integer Scaling                 │ On / Off (default: On)        │
│                                 │ On: nearest-neighbor, no      │
│                                 │ sub-pixel rendering           │
│                                 │ Off: bilinear filter allowed  │
│ Pixel Filter                    │ None / CRT Scanlines /        │
│                                 │ LCD Grid / Softened           │
│                                 │ Preview: live on game behind  │
│                                 │ settings panel                │
│ Max FPS                         │ 30 / 60 / 120 / Unlimited    │
│                                 │ Default: 60                   │
└─────────────────────────────────┴──────────────────────────────┘

Resolution change behavior:
  1. User selects new resolution
  2. User clicks "Apply"
  3. Resolution changes immediately
  4. Confirmation dialog:
     "Keep this resolution? Reverting in 15s..."
     [Keep] [Revert]
     Countdown timer visible: "14... 13... 12..."
  5. If no response in 15s: revert to previous resolution
  6. This prevents being locked out by unsupported resolutions
```

### Audio Settings

```
┌─────────────────────────────────┬──────────────────────────────┐
│ Setting                         │ Control                       │
├─────────────────────────────────┼──────────────────────────────┤
│ Master Volume                   │ Slider 0–100%, default 80%   │
│ Music Volume                    │ Slider 0–100%, default 70%   │
│ SFX Volume                      │ Slider 0–100%, default 80%   │
│ Ambient Volume                  │ Slider 0–100%, default 60%   │
│ Mute Master                     │ Toggle (default: off)        │
│ Mute Music                      │ Toggle (default: off)        │
│ Mute SFX                        │ Toggle (default: off)        │
│ Mute Ambient                    │ Toggle (default: off)        │
└─────────────────────────────────┴──────────────────────────────┘

Slider behavior:
  Visual: horizontal bar, 80px wide, filled portion colored #4488ff
  Interaction: drag handle or click anywhere on bar
  Step: 1% per scroll tick, 5% per arrow key press
  Audio preview: play sample sound on slider release (SFX: hammer tap, Ambient: wind, Music: 2s loop)
  Mute toggle: dims slider to 30% opacity, shows 🔇 icon equivalent (speaker-off pixel sprite)
```

### Gameplay Settings

```
┌─────────────────────────────────┬──────────────────────────────┐
│ Setting                         │ Control                       │
├─────────────────────────────────┼──────────────────────────────┤
│ Autosave Frequency              │ Dropdown: Every 3 / 5 / 10   │
│                                 │ game-months / Off             │
│                                 │ Default: 5 months             │
│ Default Game Speed              │ 1x / 2x / 3x (default: 1x)  │
│ Edge Scroll                     │ On / Off (default: On)        │
│ Edge Scroll Speed               │ Slider 50–150% (default 100%)│
│ Tooltip Delay                   │ Slider 0–1000ms, step 100ms  │
│                                 │ Default: 400ms                │
│ Confirm Demolition              │ On / Off (default: On)        │
│ Auto-Pause on Focus Loss        │ On / Off (default: On)        │
│ Show Tutorial Tips               │ On / Off (default: On)        │
└─────────────────────────────────┴──────────────────────────────┘
```

### Controls Settings

```
Keybinding Editor:
  Layout: two-column table
    Column 1: Action name (e.g., "Pan Camera Up")
    Column 2: Current binding (e.g., "W" / "Up Arrow")
    
  Rebinding flow:
    1. Click on binding cell → cell highlights, text changes to "Press key..."
    2. User presses desired key
    3. If key already bound to another action:
       "Key [K] is bound to [Action]. Swap?" [Swap] [Cancel]
       Swap: old action gets the key that was on the new action
    4. Binding updates immediately
    5. Escape during "Press key..." cancels rebinding

  Forbidden keys (cannot be rebound):
    None — all keys are rebindable except:
    - System keys (Alt+F4, Ctrl+Alt+Del)
    - The "Rebind Cancel" key is always Escape (meta-binding)

Mouse sensitivity:
  Slider 0.5–2.0, step 0.1, default 1.0
  Affects middle-mouse drag speed multiplier

Scroll direction:
  Normal / Inverted (default: Normal)
  "Normal": scroll up = zoom in
  "Inverted": scroll up = zoom out

Controller section (visible when controller detected):
  Left stick dead zone:   slider 0.05–0.30, default 0.15
  Right stick dead zone:  slider 0.10–0.40, default 0.25
  Stick sensitivity:      slider 0.5–2.0, default 1.0
  Vibration:              On / Off (default: On)
  Vibration intensity:    slider 0–100%, default 70%
```

### Accessibility Settings

```
┌─────────────────────────────────┬──────────────────────────────┐
│ Setting                         │ Control                       │
├─────────────────────────────────┼──────────────────────────────┤
│ Colorblind Mode                 │ Off / Deuteranopia /          │
│                                 │ Protanopia / Tritanopia       │
│                                 │ Default: Off                  │
│ UI Scale                        │ Slider 100–200%, step 25%    │
│                                 │ Default: 100%                 │
│ Text Size                       │ Small (5px) / Medium (6px) / │
│                                 │ Large (7px) / XL (8px)        │
│                                 │ Default: Medium               │
│ Reduced Motion                  │ On / Off (default: Off)       │
│                                 │ Disables: particle effects,   │
│                                 │ screen shake, popup animations│
│                                 │ (popups appear/disappear      │
│                                 │ instantly instead of sliding)  │
│ High Contrast UI                │ On / Off (default: Off)       │
│                                 │ Increases border contrast,    │
│                                 │ panel opacity to 95%,         │
│                                 │ text becomes pure white       │
│ Screen Reader Hints             │ On / Off (default: Off)       │
│                                 │ Adds text descriptions to     │
│                                 │ icon-only buttons             │
│ Hold-to-Confirm Duration        │ Slider 200–1000ms, default    │
│                                 │ 0ms (instant click)           │
│                                 │ Prevents accidental placements│
│ Flash/Strobe Reduction          │ On / Off (default: Off)       │
│                                 │ Removes lightning flashes,    │
│                                 │ fire flicker rate halved      │
└─────────────────────────────────┴──────────────────────────────┘

Colorblind mode implementation:
  Deuteranopia: red-green → shift reds to orange, greens to blue-green
  Protanopia:   red-green → shift reds to yellow, greens to cyan
  Tritanopia:   blue-yellow → shift blues to cyan, yellows to pink
  
  Applied as a full-screen shader post-process
  All overlay colors, zone colors, and UI indicators affected
  Color palette remapping table:
    Original          Deutan          Protan          Tritan
    #FF0000 (red)     #FF8800 (org)   #FFCC00 (yel)   #FF0066 (pink)
    #00FF00 (green)   #0088FF (teal)  #00CCCC (cyan)   #00FF00 (keep)
    #0000FF (blue)    #0000FF (keep)  #0000FF (keep)   #00CCCC (cyan)
    #FFFF00 (yellow)  #FFFF00 (keep)  #FFFF00 (keep)   #FF8888 (pink)
```

### Settings Persistence

```
File:     user://settings.cfg (Godot's user data directory)
Format:   INI-style (Godot ConfigFile)
Loaded:   on game launch, before main menu renders
Saved:    on "Apply" button press and on game exit
Default:  if file missing or corrupt, all defaults applied silently
```

---

## 8. Screenshot Mode

### Activation & State

```
Toggle:     F12 or Ctrl+Shift+S
On enter:
  1. Game pauses (simulation frozen, clock stops)
  2. All HUD elements fade out over 200ms (alpha 1.0 → 0.0)
  3. Screenshot mode toolbar appears at bottom: 12px height strip
  4. Camera unlocked from grid (free float, sub-pixel positioning allowed)
  5. Input mode switches to screenshot controls

On exit (Escape or F12):
  1. Camera snaps back to nearest valid grid position
  2. HUD fades in over 200ms
  3. Game unpauses
  4. Screenshot toolbar removed
  5. Input mode returns to normal
```

### Screenshot Mode Controls

```
Camera movement:
  WASD / Arrow keys:  smooth pan, 200px/sec, no grid snap
  Mouse drag:         free pan (left mouse button, since no placement in this mode)
  Scroll wheel:       zoom (same integer levels: 1x, 2x, 3x, 4x)
  Q/E:                rotate (same 90° snap)

Screenshot toolbar (bottom strip, 320px wide × 12px tall at native res):
  ┌──────────────────────────────────────────────────────────────┐
  │ Time:◀[slider]▸  Season:[▼]  Weather:[▼]  Res:[1x 2x 4x]  [📷] │
  └──────────────────────────────────────────────────────────────┘
```

### Time-of-Day Slider

```
Range:        0:00 – 23:59 (24-hour cycle)
Step:         15 minutes per tick
Visual:       horizontal slider, 60px wide
               Sun/moon icon follows slider position
Lighting:     real-time update as slider moves
               0:00-5:00:   night (dark blue ambient, warm window lights)
               5:00-7:00:   dawn (orange-pink gradient)
               7:00-17:00:  day (neutral white-yellow)
               17:00-19:00: dusk (orange-red gradient)
               19:00-24:00: night
Default:      current game time of day when entering screenshot mode
```

### Season & Weather Overrides

```
Season dropdown:
  Options: Spring / Summer / Autumn / Winter
  Default: current game season
  Effect:  swaps terrain tileset palette and tree sprites immediately
           Spring: green, flowers
           Summer: deep green, full canopy
           Autumn: orange/red/yellow leaves
           Winter: snow coverage, bare trees

Weather dropdown:
  Options: Clear / Cloudy / Rain / Snow / Fog
  Default: current game weather
  Effect:
    Clear:   no particles, full brightness
    Cloudy:  ambient light reduced 20%, cloud shadows on ground
    Rain:    rain particle overlay (diagonal lines, alpha 0.3), puddle reflections
    Snow:    snow particle overlay (slow fall), ground whitening +30%
    Fog:     distance fade shader, visibility reduced to 80% of screen
```

### Super-Resolution Capture

```
Resolution multiplier options:
  1x: 320×180 pixels (native)
  2x: 640×360 pixels
  4x: 1280×720 pixels

Implementation:
  1. Create off-screen viewport at target resolution
  2. Render scene to viewport (sprites scaled appropriately with nearest-neighbor)
  3. Capture viewport as Image
  4. Save as PNG

Capture button (📷 icon or Enter key):
  1. Hide screenshot toolbar for the capture
  2. Render at selected resolution
  3. Save to: Steam screenshots folder (if Steam overlay active)
             OR user://screenshots/ with filename:
             ironoak_[cityname]_[date]_[time].png
  4. Flash effect: screen goes white alpha 0.8 for 50ms, then fades to normal over 150ms
  5. Camera shutter sound: 80ms click
  6. Toast notification (outside screenshot mode): "Screenshot saved" (3s, Info priority)
```

---

## 9. Achievement Popup

### Layout & Position

```
Position:     bottom-center of screen
              horizontally centered, 20px from bottom edge (native resolution)

Popup dimensions (native resolution):
  Width:      160px
  Height:     28px
  
  Layout:
  ┌────────────────────────────────────────┐
  │  [16x16    ]  Achievement Name         │  ← 6px font, bold
  │  [ icon    ]  Brief description text   │  ← 5px font, regular, dimmer
  └────────────────────────────────────────┘
  
  Background:   #1a1a2e, alpha 0.95, 1px border #FFD700 (gold)
  Icon:         16x16 achievement-specific pixel art
  Padding:      4px all sides
  Icon-to-text: 4px gap
```

### Animation Sequence

```
Appear:
  t=0ms:      popup at y = bottom_edge + 28px (fully below screen)
  t=0-300ms:  slide up to final position (20px from bottom)
              easing: ease-out-back (slight overshoot, bouncy feel)
              overshoot amount: 4px above final position at t=220ms
              settles to exact position at t=300ms

Hold:
  t=300ms-5300ms: popup stationary at final position (5000ms hold)

Dismiss:
  t=5300ms-5500ms: slide down, 28px + 20px = below screen
                    easing: ease-in-quad (accelerates downward)
  t=5500ms:        popup removed from scene tree

Total lifecycle: 5500ms
```

### Sound Design

```
Achievement chime:
  Distinct from all other game sounds
  Characteristics: bright, celebratory, 3-note ascending arpeggio
  Duration: 600ms
  Notes: C5 → E5 → G5 (major chord arpeggio)
  Instrument: chiptune bell/synth (matching pixel art aesthetic)
  Volume: 80% of SFX volume (prominent but not jarring)
  
  Plays at t=0ms (when popup begins appearing)
```

### Rare Achievement Effect

```
Detection:     achievement metadata includes rarity flag (common/uncommon/rare/legendary)
               Gold particle effect triggers for "rare" and "legendary" only

Particle effect:
  Type:         8 gold sparkle particles
  Spawn:        around popup border
  Color:        #FFD700 to #FFA500 gradient
  Size:         2x2 pixels each
  Behavior:     float upward and outward, alpha fade
  Lifetime:     1200ms per particle
  Spawn rate:   staggered, 2 particles every 150ms (4 bursts)
  
  For "legendary" achievements:
    Double particle count (16)
    Add screen-edge gold vignette, alpha 0.15, fading over 2000ms
    Chime uses 5-note arpeggio (C5→E5→G5→C6→E6) instead of 3-note
```

### Queue Management

```
Max visible simultaneously: 2 popups
  
  Layout when 2 visible:
    First popup:   20px from bottom edge
    Second popup:  20px + 28px + 4px gap = 52px from bottom edge

  Queue behavior:
    If 3rd achievement unlocked while 2 visible:
      3rd queued in FIFO order
      When slot opens (a visible popup finishes its dismiss animation):
        Wait 1000ms
        Then animate queued popup in

  Multiple simultaneous unlocks:
    Each separated by 1000ms gap minimum
    Queue processes in unlock order

  Maximum queue depth: 10 (discard oldest if exceeded — unlikely edge case)
```

### Steam Overlay Coordination

```
Steam achievement notification:
  Steam shows its own overlay popup (top-right, typically)
  
Game popup timing:
  On achievement unlock:
    1. Trigger Steam achievement API call
    2. Wait 500ms (let Steam overlay appear first)
    3. Show game's bottom-center popup
    
  This prevents both popups appearing simultaneously and competing for attention
  The 500ms delay is constant regardless of Steam overlay speed
```

---

## 10. Notification Queue System

### Priority Levels

```
┌───────────┬──────────────┬──────────┬────────────┬───────────────────────────┐
│ Priority  │ Auto-dismiss │ Position │ Sound      │ Example                   │
├───────────┼──────────────┼──────────┼────────────┼───────────────────────────┤
│ Info      │ 5000ms       │ Toast    │ Soft chime │ "New building completed"  │
│ Warning   │ 12000ms      │ Toast    │ Alert tone │ "Power grid at 90%"       │
│ Critical  │ Never        │ Toast    │ Alarm beep │ "No water supply"         │
│ Emergency │ Never        │ Modal    │ Siren loop │ "Treasury empty — bankrupt│
│           │              │ dialog   │ (until ack)│  in 3 months"             │
└───────────┴──────────────┴──────────┴────────────┴───────────────────────────┘
```

### Toast Layout

```
Position:     top-right corner, 8px from top edge, 8px from right edge (native res)

Single toast dimensions:
  Width:      120px
  Height:     20px minimum, expands for longer text (max 32px, 2 lines)
  
  Layout:
  ┌────────────────────────────────────┐
  │ [icon 8x8] Notification text here  │  ← 5px font
  │            Second line if needed   │
  └────────────────────────────────────┘
  
  Background colors by priority:
    Info:      #2a2a4e, border #444477
    Warning:   #3e3a1e, border #887722
    Critical:  #3e1e1e, border #882222
  
  Icon by priority:
    Info:      blue circle-i (ℹ), 8x8
    Warning:   yellow triangle-!, 8x8
    Critical:  red octagon-!, 8x8

Stacking (when 2 visible):
  First toast:   8px from top
  Second toast:  8px + 20px + 4px gap = 32px from top (below first)
```

### Toast Animation

```
Appear:
  t=0ms:      toast at x = right_edge + 120px (fully off-screen right)
  t=0-250ms:  slide left to final position
              easing: ease-out-cubic
  
Auto-dismiss (Info/Warning):
  t=dismiss-200ms to t=dismiss:  fade out alpha 1.0 → 0.0 (linear)
  Then remove from scene tree

Manual dismiss:
  Critical toasts: show small "X" button (8x8, top-right of toast)
  Click X: toast slides right over 150ms (ease-in-quad), then removed
  Keyboard: when toast is focused (Tab navigation), Enter dismisses

Emergency modal:
  Full-screen overlay, #000000 alpha 0.7
  Centered panel, 200x80px
  Title + description + action buttons
  Cannot be dismissed without choosing an action
  Game pauses automatically
```

### Same-Type Cooldown & Aggregation

```
Cooldown:
  After dismissing (or auto-dismissing) a notification of type X:
    Suppress next notification of type X for 20000ms
    "Type" = specific notification category (e.g., "building_complete", "power_warning")
    Different types are independent — building_complete and power_warning can coexist
  
  Cooldown bypass:
    Critical and Emergency always show immediately (no cooldown)
    If suppressed notification upgrades priority (Info→Warning), show it

Aggregation:
  When multiple notifications of the same type arrive within a cooldown window:
    Instead of queueing each one, aggregate into a single notification
    
  Examples:
    3 buildings complete → "3 buildings completed" (single toast)
    5 citizens moved in  → "5 new residents arrived"
    2 fires started      → "2 fires reported" (Warning, not 2 separate)
    
  Counter updates:
    If aggregated toast is still visible and count increases:
      Update number in-place with a brief scale pulse (1.0 → 1.2 → 1.0 over 200ms)
      Reset auto-dismiss timer
```

### Notification History Log

```
Hotkey:       N (toggle)
Position:     right side panel, 100px wide, full height (180px at native res)
Background:   #1a1a2e, alpha 0.95, 1px left border #333355

Content:
  Scrollable list of all notifications from current session
  Newest at top
  
  Entry format:
  ┌──────────────────────────────┐
  │ [icon] [timestamp] message   │  ← 4px font
  └──────────────────────────────┘
  
  Timestamp: game time (Y12 M3 D15)
  Color coding: same as toast priority colors but at 50% saturation
  
  Scroll: mouse wheel or arrow keys when focused
  Max entries: 200 (oldest trimmed when exceeded)
  
  Filter buttons (top of panel):
    [All] [Info] [Warn] [Crit]
    Active filter highlighted, others dimmed
```

### Per-Category Mute

```
Mutable categories (independent toggles):
  ┌──────────────────────┬──────────────────────────────────┐
  │ Category             │ What it silences                  │
  ├──────────────────────┼──────────────────────────────────┤
  │ Advisor Tips         │ Gameplay suggestions, tutorials   │
  │ Milestone Alerts     │ Population thresholds, era changes│
  │ Building Complete    │ Construction finished notices     │
  │ Resource Warnings    │ Power/water/budget low warnings   │
  │ Citizen Feedback     │ Happiness/complaint notifications │
  │ Disaster Alerts      │ Fire, crime, pollution events     │
  └──────────────────────┴──────────────────────────────────┘

  Muted categories:
    - Still logged to notification history
    - No toast displayed
    - No sound played
    - Critical/Emergency of muted category still show (safety override)
    
  Access: Settings > Gameplay > Notification Categories
          OR right-click any toast → "Mute this category"
```

### Do Not Disturb Mode

```
Activation:   Settings > Gameplay > Do Not Disturb: On/Off
              OR hotkey: Ctrl+D (toggle)
              
When active:
  Indicator:  small "DND" text, top-right corner, 4px font, alpha 0.5
  Suppressed: Info and Warning priority notifications
  Still shown: Critical and Emergency (cannot be suppressed)
  
  Notifications are logged to history even when suppressed
  
  Auto-disable: DND turns off automatically when:
    - Game is paused
    - A Critical notification fires (DND stays on, but the critical shows)
    - Player opens notification history (H key)
```

### Notification Sound Design

```
Info chime:
  Single soft bell note, C4, 150ms, low velocity
  Volume: 60% of SFX slider

Warning alert:
  Two-note rising tone, C4→E4, 100ms each, 50ms gap
  Volume: 80% of SFX slider

Critical alarm:
  Three rapid beeps, 80ms each, 40ms gaps, higher pitch (G4)
  Volume: 100% of SFX slider

Emergency siren:
  Alternating two-tone, C4↔E4, 200ms each, loops until acknowledged
  Volume: 100% of SFX slider, cannot be muted (hardcoded safety)

Achievement chime (separate from notifications):
  See Section 9 — distinct sound, never confused with notifications
```

---

## Implementation Priority Order

```
Phase 1 (Core Loop):
  1. Camera Controls (pan, zoom, rotation)         — blocks everything
  2. Keyboard-Only Play (cursor, focus system)      — core input
  3. Undo/Redo System                               — player trust

Phase 2 (Polish):
  4. Save/Load UX                                   — retention critical
  5. Settings Menu                                   — player control
  6. Notification Queue System                       — game feedback

Phase 3 (Platform):
  7. Controller/Gamepad Support                      — expanded audience
  8. Steam Deck Support                              — platform requirement

Phase 4 (Delight):
  9. Achievement Popup                               — reward loop
  10. Screenshot Mode                                — community/marketing
```

---

All pixel values are in native 320x180 resolution unless specified as "display resolution." All timing values are in milliseconds. All easing functions reference standard CSS/animation easing curves (ease-in-quad = t^2, ease-out-cubic = 1-(1-t)^3, ease-out-back = overshoot cubic).