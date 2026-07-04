export const GRID_SIZE = 256;
export const CHUNK_SIZE = 32;
export const CHUNKS_PER_AXIS = GRID_SIZE / CHUNK_SIZE;
export const CHUNK_COUNT = CHUNKS_PER_AXIS * CHUNKS_PER_AXIS;
export const TARGET_BUILDING_COUNT = 5000;

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

/** Graphics quality tier — controls post-processing and other GPU-heavy features. */
export type GraphicsQualityTier = "low" | "high";

export const GRAPHICS_QUALITY_STORAGE_KEY = "citymajor_graphics_quality";

/** Set when the /play guided onboarding tour is completed or dismissed. */
export const ONBOARDING_STORAGE_KEY = "citymajor_onboarding_done";

/** Session-scoped dismiss flags for GAMEPLAY_LOOP #9 crisis warnings. */
export const CRISIS_BANKRUPTCY_SESSION_KEY = "citymajor_crisis_bankruptcy_dismissed";
export const CRISIS_LOW_APPROVAL_SESSION_KEY = "citymajor_crisis_low_approval_dismissed";

/** Mayor approval below this percent triggers the low-approval warning (0–100). */
export const LOW_APPROVAL_WARNING_THRESHOLD = 30;

/** Duration of the era-transition fanfare modal (GAMEPLAY_LOOP #11). */
export const ERA_TRANSITION_FANFARE_MS = 3000;
