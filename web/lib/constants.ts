export const GRID_SIZE = 256;
export const CHUNK_SIZE = 32;
export const CHUNKS_PER_AXIS = GRID_SIZE / CHUNK_SIZE;
export const CHUNK_COUNT = CHUNKS_PER_AXIS * CHUNKS_PER_AXIS;
export const TARGET_BUILDING_COUNT = 5000;
export const ARCHETYPE_COUNT = 5;

/** World units per tile (1 tile = 1 unit). */
export const TILE_SIZE = 1;

export const ZONE_COLORS = {
  residential: "#4a90d9",
  commercial: "#e8b84a",
  industrial: "#8b7355",
  office: "#9b7ed9",
  mixed: "#5cb88a",
} as const;

export type ZoneType = keyof typeof ZONE_COLORS;

export const ARCHETYPE_LABELS = [
  "res-low",
  "res-high",
  "commercial",
  "industrial",
  "office",
] as const;

export type ArchetypeId = 0 | 1 | 2 | 3 | 4;

export const LOD_THRESHOLDS = {
  /** Distance to chunk center — enter thresholds (camera moves away). */
  enter: [40, 90, 160] as const,
  /** Exit thresholds (camera moves closer) — hysteresis band. */
  exit: [32, 78, 145] as const,
} as const;

export const FPS_DEGRADE_THRESHOLD = 30;
export const FPS_DEGRADE_DURATION_MS = 2000;
export const MIN_DPR = 0.5;
export const MAX_DPR = 2;
