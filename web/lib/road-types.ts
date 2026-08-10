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

/** HUD toast when WASM rejects an illegal highway merge (P1.4 ramp rules). */
export const ILLEGAL_HIGHWAY_MERGE_TOAST =
  "Illegal highway merge — use a ramp";

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

export function roadTierLabel(tier: RoadTier): string {
  return ROAD_TIER_DEFINITIONS.find((d) => d.tier === tier)?.label ?? "Road";
}

export function roadTierOverlayColor(tier: RoadTier): string {
  return (
    ROAD_TIER_DEFINITIONS.find((d) => d.tier === tier)?.overlayColor ??
    "#6b7280"
  );
}
