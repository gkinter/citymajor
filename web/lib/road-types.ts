import {
  WEB_V1_PLAYABLE_ERAS,
  type WebV1PlayableEra,
} from "@/lib/tech-catalog";
import {
  isBaselineContent,
  isContentUnlocked,
} from "@/lib/tech-unlocks";

/** Active road paint tier for /play (dirt → cobblestone → asphalt). */
export type RoadTier = 0 | 1 | 2;

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
 * Keys: dirt_road (baseline), cobblestone_road, asphalt_road.
 */
export const ROAD_TIER_DEFINITIONS: readonly RoadTierDefinition[] = [
  {
    tier: 0,
    contentKey: "dirt_road",
    label: "Dirt",
    shortLabel: "Dirt",
    overlayColor: "#8b6914",
    era: "frontier",
  },
  {
    tier: 1,
    contentKey: "cobblestone_road",
    label: "Cobblestone",
    shortLabel: "Cobb",
    overlayColor: "#6b6560",
    era: "frontier",
  },
  {
    tier: 2,
    contentKey: "asphalt_road",
    label: "Asphalt",
    shortLabel: "Asph",
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
