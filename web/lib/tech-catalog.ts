import allTechnologies from "../../base/data/tech/technologies.json";

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
