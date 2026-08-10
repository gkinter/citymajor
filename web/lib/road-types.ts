import {
  WEB_V1_PLAYABLE_ERAS,
  type WebV1PlayableEra,
} from "@/lib/tech-catalog";
import {
  isBaselineContent,
  isContentUnlocked,
} from "@/lib/tech-unlocks";

/** Active road paint tier for /play (local → collector → highway). */
export type RoadTier = 0 | 1 | 2;

/** Toolbar selection — tiers plus dedicated ramp connector tool (P1.5). */
export type RoadToolId = RoadTier | "ramp";

/**
 * Elevation paint mode for PlaceRoad bridge/tunnel flags (RoadFlags bits 6–7).
 * Mutually exclusive with ramp (0xC0) — bridge and tunnel cannot both be set.
 */
export type RoadElevationMode = "none" | "bridge" | "tunnel";

/** HUD toast when WASM rejects an illegal highway merge (P1.4 ramp rules). */
export const ILLEGAL_HIGHWAY_MERGE_TOAST =
  "Illegal highway merge — use a ramp";

export const DEFAULT_ROAD_ELEVATION: RoadElevationMode = "none";

/** RoadFlags structure encoding (bits 6–7, mutually exclusive). */
export const ROAD_FLAG_BRIDGE = 0x40;
export const ROAD_FLAG_TUNNEL = 0x80;
export const ROAD_FLAG_RAMP = ROAD_FLAG_BRIDGE | ROAD_FLAG_TUNNEL; // 0xC0

export const RAMP_OVERLAY_COLOR = "#b45309";

export type RoadTierDefinition = {
  tier: RoadTier;
  /** Content key from technologies.json / v1-unlock-bridge capabilities. */
  contentKey: string;
  label: string;
  shortLabel: string;
  overlayColor: string;
  era: WebV1PlayableEra;
};

/**
 * v1 road tiers — Frontier + Industrial eras only.
 * Display: Local / Collector / Highway (content keys unchanged for tech unlocks).
 */
export const ROAD_TIER_DEFINITIONS: readonly RoadTierDefinition[] = [
  {
    tier: 0,
    contentKey: "dirt_road",
    label: "Local",
    shortLabel: "Local",
    overlayColor: "#8b6914",
    era: "frontier",
  },
  {
    tier: 1,
    contentKey: "cobblestone_road",
    label: "Collector",
    shortLabel: "Coll",
    overlayColor: "#6b6560",
    era: "frontier",
  },
  {
    tier: 2,
    contentKey: "asphalt_road",
    label: "Highway",
    shortLabel: "Hwy",
    overlayColor: "#4a4f57",
    era: "industrial",
  },
] as const;

export const DEFAULT_ROAD_TIER: RoadTier = 0;
export const DEFAULT_ROAD_TOOL: RoadToolId = DEFAULT_ROAD_TIER;

/** Map elevation mode → PlaceRoad / place_road bridge+tunnel bools. */
export function roadElevationFlags(mode: RoadElevationMode): {
  bridge: boolean;
  tunnel: boolean;
} {
  return {
    bridge: mode === "bridge",
    tunnel: mode === "tunnel",
  };
}

/**
 * Encode tier (bits 4–5) + optional bridge/tunnel/ramp (bits 6–7) into roadFlags.
 * Matches Forge.Engine.Data.RoadFlags / SimHost.ComputeRoadFlags.
 * Ramp takes precedence over elevation (structure bits 0xC0).
 */
export function encodeRoadFlags(
  tier: number | undefined,
  mode: RoadElevationMode = DEFAULT_ROAD_ELEVATION,
  ramp = false,
): number {
  const t = (tier ?? 0) & 0x03;
  let flags = (t << 4) | 0x01;
  if (ramp) flags |= ROAD_FLAG_RAMP;
  else if (mode === "bridge") flags |= ROAD_FLAG_BRIDGE;
  else if (mode === "tunnel") flags |= ROAD_FLAG_TUNNEL;
  return flags;
}

export function roadElevationLabel(mode: RoadElevationMode): string {
  switch (mode) {
    case "bridge":
      return "Bridge";
    case "tunnel":
      return "Tunnel";
    default:
      return "Grade";
  }
}

const PLAYABLE_ERA_SET = new Set<string>(WEB_V1_PLAYABLE_ERAS);

export function toUnlockedTechSet(
  unlockedTechIds?: readonly number[],
): ReadonlySet<number> {
  return new Set(unlockedTechIds ?? []);
}

/** Road tiers exposed in web v1 (Frontier + Industrial only). */
export function getPlayableRoadTiers(): readonly RoadTierDefinition[] {
  return ROAD_TIER_DEFINITIONS.filter((def) => PLAYABLE_ERA_SET.has(def.era));
}

/** Whether a road tier is available from baseline content or researched tech. */
export function isRoadTierUnlocked(
  tier: RoadTier,
  unlockedTechIds?: readonly number[],
): boolean {
  const def = ROAD_TIER_DEFINITIONS.find((d) => d.tier === tier);
  if (!def) return false;
  if (isBaselineContent(def.contentKey)) return true;
  return isContentUnlocked(def.contentKey, toUnlockedTechSet(unlockedTechIds));
}

/**
 * Ramp tool unlocks with highway — connectors only make sense once highways exist.
 */
export function isRampToolUnlocked(
  unlockedTechIds?: readonly number[],
): boolean {
  return isRoadTierUnlocked(2, unlockedTechIds);
}

/**
 * Tier for ramp paint: prefer collector when unlocked, else local.
 */
export function resolveRampPaintTier(
  unlockedTechIds?: readonly number[],
): RoadTier {
  if (isRoadTierUnlocked(1, unlockedTechIds)) return 1;
  return 0;
}

export function isRoadToolId(value: unknown): value is RoadToolId {
  return value === "ramp" || value === 0 || value === 1 || value === 2;
}

export function roadTierLabel(tier: RoadTier): string {
  return ROAD_TIER_DEFINITIONS.find((d) => d.tier === tier)?.label ?? "Road";
}

export function roadTierOverlayColor(tier: RoadTier): string {
  return (
    ROAD_TIER_DEFINITIONS.find((d) => d.tier === tier)?.overlayColor ??
    "#6b7280"
  );
}

export function roadToolOverlayColor(tool: RoadToolId): string {
  if (tool === "ramp") return RAMP_OVERLAY_COLOR;
  return roadTierOverlayColor(tool);
}
