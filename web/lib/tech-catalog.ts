import allTechnologies from "../../base/data/tech/technologies.json";

export type TechPreview = {
  id: string;
  name: string;
  era: string;
  category: string;
  cost_rp: number;
  prerequisites: string[];
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
