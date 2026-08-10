import allTechnologies from "../../base/data/tech/technologies.json";
import {
  BUILDING_SLUG_TO_TYPE_ID,
  V1_UNLOCK_TO_BUILDING_SLUG,
  techIdFromCatalogId,
  type TechPreview,
} from "@/lib/tech-catalog";

const fullCatalog = allTechnologies as TechPreview[];

/**
 * Content keys unlocked by researched technologies (buildings, roads, policies).
 * Keys match `unlocks` entries in technologies.json.
 */
export function collectUnlockedContentKeys(
  unlockedTechIds: ReadonlySet<number>,
): Set<string> {
  const keys = new Set<string>();
  for (const techId of unlockedTechIds) {
    const tech = fullCatalog[techId];
    if (!tech?.unlocks) continue;
    for (const key of tech.unlocks) keys.add(key);
  }
  return keys;
}

/** Whether a building/content key is available from current research state. */
export function isContentUnlocked(
  contentKey: string,
  unlockedTechIds: ReadonlySet<number>,
): boolean {
  return collectUnlockedContentKeys(unlockedTechIds).has(contentKey);
}

/** Starter content available before any research (low-tier zones, dirt roads). */
export const BASELINE_CONTENT_KEYS = new Set([
  "dirt_road",
  "residential_zone",
  "commercial_zone",
  "industrial_zone",
  "agricultural_zone",
]);

/** True when content is playable with no tech research required. */
export function isBaselineContent(contentKey: string): boolean {
  return BASELINE_CONTENT_KEYS.has(contentKey);
}

export type NewlyUnlockedResearch = {
  techIds: number[];
  contentKeys: string[];
};

/** Content keys that became available after research completes. */
export function detectNewlyUnlockedContent(
  prevUnlockedTechIds: ReadonlySet<number>,
  nextUnlockedTechIds: ReadonlyArray<number>,
): NewlyUnlockedResearch | null {
  const techIds = nextUnlockedTechIds.filter((id) => !prevUnlockedTechIds.has(id));
  if (techIds.length === 0) return null;

  const prevKeys = collectUnlockedContentKeys(prevUnlockedTechIds);
  const nextKeys = collectUnlockedContentKeys(new Set(nextUnlockedTechIds));
  const contentKeys = [...nextKeys].filter((key) => !prevKeys.has(key));

  return { techIds, contentKeys };
}

/** Map WASM building typeId ranges to content keys when a catalog entry exists. */
export function buildingTypeRequiresTech(typeId: number): string | null {
  for (const [contentKey, slug] of Object.entries(V1_UNLOCK_TO_BUILDING_SLUG)) {
    if (BUILDING_SLUG_TO_TYPE_ID[slug] === typeId) return contentKey;
  }
  return null;
}
