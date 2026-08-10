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
export type GraphicsQualityTier = "low" | "med" | "high" | "ultra";

export const GRAPHICS_QUALITY_STORAGE_KEY = "citymajor_graphics_quality";

export const GRAPHICS_QUALITY_TIERS: readonly {
  tier: GraphicsQualityTier;
  label: string;
  tooltip: string;
}[] = [
  {
    tier: "low",
    label: "Low",
    tooltip: "Fastest — no bloom, lower pixel density on weak GPUs",
  },
  {
    tier: "med",
    label: "Med",
    tooltip: "Balanced — no bloom, adaptive resolution for steady FPS",
  },
  {
    tier: "high",
    label: "High",
    tooltip: "Bloom and full adaptive pixel density",
  },
  {
    tier: "ultra",
    label: "Ultra",
    tooltip: "Maximum bloom and pixel density — best on discrete GPUs",
  },
] as const;

const GRAPHICS_QUALITY_TIER_SET = new Set<GraphicsQualityTier>(
  GRAPHICS_QUALITY_TIERS.map(({ tier }) => tier),
);

export function parseStoredQualityTier(
  stored: string | null,
): GraphicsQualityTier {
  if (stored && GRAPHICS_QUALITY_TIER_SET.has(stored as GraphicsQualityTier)) {
    return stored as GraphicsQualityTier;
  }
  return "med";
}

export function isPostProcessingEnabled(tier: GraphicsQualityTier): boolean {
  return tier === "high" || tier === "ultra";
}

/** Persist traffic heatmap overlay visibility on /play. */
export const TRAFFIC_OVERLAY_STORAGE_KEY = "citymajor_traffic_overlay";

/** Persist goods-transport friction corridor overlay visibility on /play. */
export const FRICTION_OVERLAY_STORAGE_KEY = "citymajor_friction_overlay";

/** Set when the /play guided onboarding tour is completed or dismissed. */
export const ONBOARDING_STORAGE_KEY = "citymajor_onboarding_done";

/** PlayClient "Empty start" toggle — skips WASM SeedStarterCity on init. */
export const EMPTY_CITY_STORAGE_KEY = "citymajor_empty";

/** Session-scoped dismiss flags for GAMEPLAY_LOOP #9 crisis warnings. */
export const CRISIS_BANKRUPTCY_SESSION_KEY = "citymajor_crisis_bankruptcy_dismissed";
export const CRISIS_LOW_APPROVAL_SESSION_KEY = "citymajor_crisis_low_approval_dismissed";

/** Mayor approval below this percent triggers the low-approval warning (0–100). */
export const LOW_APPROVAL_WARNING_THRESHOLD = 30;

/** Minimum treasury balance before the HUD cash-crisis strip warns (when expenses unknown). */
export const LOW_TREASURY_MIN_FUNDS = 25_000;

/** Treasury runway below this many months of expenses triggers a low-balance warning. */
export const LOW_TREASURY_RUNWAY_MONTHS = 3;

/** Duration of the era-transition fanfare modal (GAMEPLAY_LOOP #11). */
export const ERA_TRANSITION_FANFARE_MS = 3000;
