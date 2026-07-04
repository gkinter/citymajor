/**
 * Procedural building assembly rules for 3D / R3F consumers.
 *
 * Ports visual assembly logic from Forge.Engine.Rendering.BuildingRenderer.cs.
 * Pure data layer — no Three.js or GLTF.
 *
 * Linear: SB-3679 (partial)
 */

import {
  BuildingCategory,
  BuildingState,
  CONSTRUCTION_CONDITION_THRESHOLD,
  NIGHT_WINDOW_COLOR,
  PIXELS_PER_STORY,
  applyAbandonedTint,
  classifyBuilding,
  computeStories,
  deriveEra,
  resolveWallColor,
  roofArchetype,
} from "./buildingArchetypes";
import type { Era, RoofArchetype } from "./buildingArchetypes";

export { computeStories };

// ---------------------------------------------------------------------------
// Constants (BuildingRenderer.cs)
// ---------------------------------------------------------------------------

export const NIGHT_WINDOW_THRESHOLD = 0.3;
export const MAX_WINDOW_COLS = 4;

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface BuildingFootprint {
  /** Footprint width in isometric tile units */
  widthTiles: number;
  /** Footprint depth in isometric tile units */
  heightTiles: number;
}

export interface RgbColor {
  r: number;
  g: number;
  b: number;
}

export interface BuildingFaceColors {
  /** Original unmixed base wall color (hex) */
  base: string;
  roof: RgbColor;
  left: RgbColor;
  right: RgbColor;
}

export interface BuildingVisualParams {
  typeId: number;
  category: BuildingCategory;
  era: Era;
  stories: number;
  roofType: RoofArchetype;
  /** Per-window lit flag in row-major order (both faces, left then right) */
  windowLit: boolean[];
  /** Simulation state passed through */
  state: BuildingState;
  /** Construction progress 0.1–1.0 (height scale factor) */
  progress: number;
  /** Whether window meshes should render (operational + progress ≥ 0.9) */
  showWindows: boolean;
  /** True when dayNightFactor exceeds night window glow threshold */
  isNight: boolean;
  /** Scaffolding overlay when under construction or progress < 1 */
  showScaffolding: boolean;
  /** Height in world pixels at zoom=1 (stories × PIXELS_PER_STORY × progress) */
  heightPx: number;
  footprint: BuildingFootprint;
  colors: BuildingFaceColors;
  /** Night window glow color when dayNightFactor exceeds threshold */
  nightWindowColor: string;
}

// ---------------------------------------------------------------------------
// Footprint (3D heuristic — sim currently spawns 1×1; scales with category/level)
// ---------------------------------------------------------------------------

/**
 * Tile footprint for procedural mesh placement.
 * Not present in BuildingRenderer.cs (2D uses single-tile diamond); derived from
 * category density expectations in docs/design/AGENT_05_ZONING_BUILDINGS.md.
 */
export function computeFootprint(
  category: BuildingCategory,
  level: number,
): BuildingFootprint {
  const lvl = Math.max(level, 1);

  switch (category) {
    case BuildingCategory.ResidentialLow:
      return { widthTiles: 1, heightTiles: 1 };

    case BuildingCategory.ResidentialHigh:
      if (lvl >= 4) return { widthTiles: 2, heightTiles: 2 };
      if (lvl >= 2) return { widthTiles: 2, heightTiles: 1 };
      return { widthTiles: 1, heightTiles: 1 };

    case BuildingCategory.Commercial:
      if (lvl >= 4) return { widthTiles: 3, heightTiles: 2 };
      if (lvl >= 2) return { widthTiles: 2, heightTiles: 1 };
      return { widthTiles: 1, heightTiles: 1 };

    case BuildingCategory.Industrial:
      if (lvl >= 3) return { widthTiles: 2, heightTiles: 2 };
      if (lvl >= 2) return { widthTiles: 2, heightTiles: 1 };
      return { widthTiles: 1, heightTiles: 1 };

    case BuildingCategory.Service:
      if (lvl >= 4) return { widthTiles: 3, heightTiles: 3 };
      if (lvl >= 2) return { widthTiles: 2, heightTiles: 2 };
      return { widthTiles: 2, heightTiles: 1 };

    default:
      return { widthTiles: 1, heightTiles: 1 };
  }
}

// ---------------------------------------------------------------------------
// Construction progress (BuildingRenderer.Update)
// ---------------------------------------------------------------------------

export function computeConstructionProgress(
  state: BuildingState,
  condition: number,
): number {
  if (
    state !== BuildingState.Constructing &&
    condition >= CONSTRUCTION_CONDITION_THRESHOLD
  ) {
    return 1;
  }

  if (state === BuildingState.Constructing) {
    return clamp(condition / 255, 0.1, 1);
  }

  return clamp(condition / CONSTRUCTION_CONDITION_THRESHOLD, 0.1, 1);
}

// ---------------------------------------------------------------------------
// Window grid + deterministic lit flags (BuildingRenderer.RenderWindows)
// ---------------------------------------------------------------------------

export function computeWindowCols(footprintWidthTiles: number): number {
  const cols = Math.floor(footprintWidthTiles * 3);
  return Math.max(1, Math.min(cols, MAX_WINDOW_COLS));
}

export function computeWindowSeed(
  gridX: number,
  gridY: number,
  typeId: number,
): number {
  return (
    (gridX * 73856093) ^ (gridY * 19349669) ^ (typeId * 83492791)
  ) >>> 0;
}

export function hashStep(x: number): number {
  let v = x >>> 0;
  v ^= v >>> 16;
  v = Math.imul(v, 0x45d9f3b);
  v ^= v >>> 16;
  return v >>> 0;
}

export function computeWindowLitFlags(
  stories: number,
  windowCols: number,
  windowSeed: number,
  state: BuildingState,
): boolean[] {
  const windowRows = stories;
  const total = windowRows * windowCols * 2;
  const flags: boolean[] = [];
  let seed = windowSeed >>> 0;

  for (let i = 0; i < total; i++) {
    seed = hashStep(seed);
    let windowDark = (seed & 3) === 0;

    if (state === BuildingState.Abandoned) {
      seed = hashStep(seed);
      if ((seed & 1) === 0) windowDark = true;
    }

    flags.push(!windowDark);
  }

  return flags;
}

// ---------------------------------------------------------------------------
// Face colors (BuildingRenderer.Update + GetBuildingColor)
// ---------------------------------------------------------------------------

function hexToRgb01(hex: string): RgbColor {
  const n = parseInt(hex.replace("#", ""), 16);
  return {
    r: ((n >> 16) & 0xff) / 255,
    g: ((n >> 8) & 0xff) / 255,
    b: (n & 0xff) / 255,
  };
}

function scaleRgb(c: RgbColor, factor: number): RgbColor {
  return {
    r: Math.min(c.r * factor, 1),
    g: Math.min(c.g * factor, 1),
    b: Math.min(c.b * factor, 1),
  };
}

export function computeFaceColors(
  typeId: number,
  state: BuildingState,
): BuildingFaceColors {
  let baseHex = resolveWallColor(typeId);

  if (state === BuildingState.Abandoned) {
    baseHex = applyAbandonedTint(baseHex);
  }

  const base = hexToRgb01(baseHex);

  return {
    base: baseHex,
    roof: scaleRgb(base, 1.2),
    left: scaleRgb(base, 0.75),
    right: scaleRgb(base, 0.55),
  };
}

// ---------------------------------------------------------------------------
// Main assembly entry point
// ---------------------------------------------------------------------------

export function buildingVisualParams(
  typeId: number,
  level: number,
  condition: number,
  dayNightFactor: number,
  state: BuildingState = BuildingState.Operational,
  gridX = 0,
  gridY = 0,
): BuildingVisualParams {
  const category = classifyBuilding(typeId);
  const era = deriveEra(typeId);
  const stories = computeStories(category, level);
  const footprint = computeFootprint(category, level);
  const progress = computeConstructionProgress(state, condition);
  const heightPx = stories * PIXELS_PER_STORY * progress;
  const roofType = roofArchetype(category);
  const colors = computeFaceColors(typeId, state);

  const isNight = dayNightFactor > NIGHT_WINDOW_THRESHOLD;
  const showWindows =
    state === BuildingState.Operational && progress >= 0.9;

  const windowCols = computeWindowCols(footprint.widthTiles);
  const windowSeed = computeWindowSeed(gridX, gridY, typeId);
  const windowLit = computeWindowLitFlags(
    stories,
    windowCols,
    windowSeed,
    state,
  );

  const showScaffolding =
    state === BuildingState.Constructing || progress < 0.95;

  return {
    typeId,
    category,
    era,
    stories,
    roofType,
    windowLit,
    state,
    progress,
    showWindows,
    isNight,
    showScaffolding,
    heightPx,
    footprint,
    colors,
    nightWindowColor: NIGHT_WINDOW_COLOR,
  };
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}
