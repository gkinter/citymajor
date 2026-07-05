import allTechnologies from "../../base/data/tech/technologies.json";
import allBuildings from "../../base/data/buildings/buildings.json";
import v1UnlockBridge from "../../base/data/tech/v1-unlock-bridge.json";
import {
  buildSlugToTypeIdMap,
  isKnownBuildingTypeId,
  type BuildingRecord,
} from "@/lib/building-type-id-map";

export type TechPreview = {
  id: string;
  name: string;
  era: string;
  category: string;
  cost_rp: number;
  prerequisites: string[];
  unlocks?: string[];
  description: string;
};

/** Playable era tags for web v1 — see docs/design/WEB_V1_SCOPE.md §2–3. */
export const WEB_V1_PLAYABLE_ERAS = ["frontier", "industrial"] as const;

export type WebV1PlayableEra = (typeof WEB_V1_PLAYABLE_ERAS)[number];

const fullCatalog = allTechnologies as TechPreview[];

/** Full design catalog size (156 technologies across 5 eras). */
export const TECH_FULL_CATALOG_TOTAL = fullCatalog.length;

function isWebV1PlayableEra(era: string): era is WebV1PlayableEra {
  return (WEB_V1_PLAYABLE_ERAS as readonly string[]).includes(era);
}

/** Frontier + Industrial technologies only — v1 playable subset. */
export const TECH_V1_CATALOG: TechPreview[] = fullCatalog.filter((tech) =>
  isWebV1PlayableEra(tech.era),
);

/** Playable tech count for Research UI and API (60 in current data). */
export const TECH_CATALOG_TOTAL = TECH_V1_CATALOG.length;

/** Default research panel list — v1 playable subset. */
export const TECH_PREVIEW_LIST = TECH_V1_CATALOG;

/** Map catalog string id (e.g. T001) to WASM ResearchSystem tech index. */
export function techIdFromCatalogId(catalogId: string): number {
  return fullCatalog.findIndex((tech) => tech.id === catalogId);
}

/** Resolve tech display name from WASM index (-1 if unknown). */
export function techNameFromIndex(techIndex: number): string | undefined {
  if (techIndex < 0 || techIndex >= fullCatalog.length) return undefined;
  return fullCatalog[techIndex]?.name;
}

/** Whether prerequisites are satisfied given unlocked WASM indices. */
export function arePrerequisitesMet(
  tech: TechPreview,
  unlockedIds: ReadonlySet<number>,
): boolean {
  return tech.prerequisites.every((prereq) => {
    const idx = techIdFromCatalogId(prereq);
    return idx >= 0 && unlockedIds.has(idx);
  });
}

export type TechAvailability = "unlocked" | "researching" | "available" | "locked";

/** Classify a catalog tech for Research panel styling. */
export function classifyTechAvailability(
  tech: TechPreview,
  unlockedIds: ReadonlySet<number>,
  currentResearchId: number | undefined,
): TechAvailability {
  const techId = techIdFromCatalogId(tech.id);
  if (techId < 0) return "locked";
  if (unlockedIds.has(techId)) return "unlocked";
  if (currentResearchId === techId) return "researching";
  if (arePrerequisitesMet(tech, unlockedIds)) return "available";
  return "locked";
}

// ---------------------------------------------------------------------------
// v1 unlock → building TypeId bridge (see DATA_BRIDGE.md + v1-unlock-bridge.json)
// ---------------------------------------------------------------------------

const buildingRecords = allBuildings as BuildingRecord[];

/** Non-placeable capability keys (roads, networks, transit modes). */
export const V1_CAPABILITY_UNLOCKS = new Set<string>(v1UnlockBridge.capabilities);

/** Tech unlock content key → buildings.json slug for v1 Frontier/Industrial pool. */
export const V1_UNLOCK_TO_BUILDING_SLUG: Readonly<Record<string, string>> =
  v1UnlockBridge.buildingSlugs;

/** buildings.json slug → sim TypeId (DATA_BRIDGE rules). */
export const BUILDING_SLUG_TO_TYPE_ID: Readonly<Record<string, number>> =
  buildSlugToTypeIdMap(buildingRecords);

/** TypeIds reachable from v1 tech unlocks (Frontier + Industrial content). */
export const V1_UNLOCK_TYPE_IDS: ReadonlySet<number> = new Set(
  Object.values(V1_UNLOCK_TO_BUILDING_SLUG)
    .map((slug) => BUILDING_SLUG_TO_TYPE_ID[slug])
    .filter((typeId): typeId is number => typeof typeId === "number"),
);

export { isKnownBuildingTypeId };

/** Resolve a tech unlock key to a placeable TypeId, or null for capabilities / unknown. */
export function resolveUnlockTypeId(contentKey: string): number | null {
  if (V1_CAPABILITY_UNLOCKS.has(contentKey)) return null;
  const slug = V1_UNLOCK_TO_BUILDING_SLUG[contentKey];
  if (!slug) return null;
  const typeId = BUILDING_SLUG_TO_TYPE_ID[slug];
  return typeof typeId === "number" ? typeId : null;
}

/** Human-readable unlock label for Research UI (slug + TypeId, or raw capability key). */
export function formatUnlockLabel(contentKey: string): string {
  const typeId = resolveUnlockTypeId(contentKey);
  if (typeId === null) return contentKey;
  const slug = V1_UNLOCK_TO_BUILDING_SLUG[contentKey];
  return slug ? `${slug} (#${typeId})` : `#${typeId}`;
}

/** True when unlock is a v1 capability or maps to a known building TypeId. */
export function isValidV1Unlock(contentKey: string): boolean {
  if (V1_CAPABILITY_UNLOCKS.has(contentKey)) return true;
  const typeId = resolveUnlockTypeId(contentKey);
  return typeId !== null && isKnownBuildingTypeId(typeId);
}

export type V1UnlockValidationIssue = {
  techId: string;
  techName: string;
  unlock: string;
};

/** Validate every unlock on the v1 playable tech catalog. */
export function validateV1TechUnlocks(): {
  ok: boolean;
  issues: V1UnlockValidationIssue[];
} {
  const issues: V1UnlockValidationIssue[] = [];
  for (const tech of TECH_V1_CATALOG) {
    for (const unlock of tech.unlocks ?? []) {
      if (!isValidV1Unlock(unlock)) {
        issues.push({ techId: tech.id, techName: tech.name, unlock });
      }
    }
  }
  return { ok: issues.length === 0, issues };
}
