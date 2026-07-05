import allBuildings from "../../base/data/buildings/buildings.json";
import {
  BUILDING_SLUG_TO_TYPE_ID,
  TECH_V1_CATALOG,
  V1_UNLOCK_TO_BUILDING_SLUG,
  type WebV1PlayableEra,
} from "@/lib/tech-catalog";
import { isContentUnlocked } from "@/lib/tech-unlocks";

/** Build panel tabs for v1 civic ploppables (Frontier + Industrial). */
export const BUILD_CATEGORIES = [
  "Civic",
  "Education",
  "Safety",
  "Utilities",
  "Parks",
] as const;

export type BuildCategory = (typeof BUILD_CATEGORIES)[number];

export type BuildPloppable = {
  contentKey: string;
  typeId: number;
  label: string;
  era: WebV1PlayableEra;
  techRequirement: string;
  category: BuildCategory;
};

type BuildingNameRecord = { id: string; name: string };

const buildingNames = new Map(
  (allBuildings as BuildingNameRecord[]).map((b) => [b.id, b.name]),
);

/** contentKey → v1 tech catalog id (first unlocking tech in playable catalog). */
const TECH_REQUIREMENT_BY_CONTENT_KEY: Readonly<Record<string, string>> =
  Object.fromEntries(
    TECH_V1_CATALOG.flatMap((tech) =>
      (tech.unlocks ?? []).map((key) => [key, tech.id] as const),
    ),
  );

/** Ploppable content keys grouped by build-panel category. */
const CONTENT_KEYS_BY_CATEGORY: Readonly<
  Record<BuildCategory, readonly string[]>
> = {
  Civic: [
    "zoning_office",
    "central_bank",
    "stock_exchange",
    "insurance_office",
    "telegraph_office",
    "telephone_exchange",
    "radio_station",
    "fort",
    "heritage_site",
    "museum",
    "signal_tower",
    "city_wall",
  ],
  Education: ["elementary_school", "military_academy", "public_library"],
  Safety: [
    "fire_station",
    "police_station",
    "barracks",
    "watchtower",
    "vaccination_clinic",
    "hospital",
    "surgical_wing",
  ],
  Utilities: [
    "well",
    "outhouse",
    "watermill",
    "aqueduct",
    "dam",
    "reservoir",
    "filtration_plant",
    "chlorination_station",
    "treatment_plant",
    "coal_power_plant",
    "gas_power_plant",
    "transformer_station",
    "bridge_steel",
    "radio_tower",
    "oil_well",
  ],
  Parks: ["public_park", "botanical_garden", "amusement_park"],
};

function labelFromContentKey(contentKey: string): string {
  return contentKey
    .split("_")
    .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
    .join(" ");
}

function eraFromSlug(slug: string): WebV1PlayableEra {
  if (slug.includes("_frontier_")) return "frontier";
  return "industrial";
}

function resolvePloppable(
  contentKey: string,
  category: BuildCategory,
): BuildPloppable | null {
  const slug = V1_UNLOCK_TO_BUILDING_SLUG[contentKey];
  if (!slug) return null;
  const typeId = BUILDING_SLUG_TO_TYPE_ID[slug];
  if (typeof typeId !== "number") return null;
  const techRequirement = TECH_REQUIREMENT_BY_CONTENT_KEY[contentKey];
  if (!techRequirement) return null;
  const era = eraFromSlug(slug);
  const label = buildingNames.get(slug) ?? labelFromContentKey(contentKey);
  return { contentKey, typeId, label, era, techRequirement, category };
}

function buildCategoryCatalog(
  category: BuildCategory,
): readonly BuildPloppable[] {
  return CONTENT_KEYS_BY_CATEGORY[category]
    .map((contentKey) => resolvePloppable(contentKey, category))
    .filter((entry): entry is BuildPloppable => entry !== null);
}

/** Categorized v1 ploppables for the build panel. */
export const BUILD_CATALOG: Readonly<
  Record<BuildCategory, readonly BuildPloppable[]>
> = {
  Civic: buildCategoryCatalog("Civic"),
  Education: buildCategoryCatalog("Education"),
  Safety: buildCategoryCatalog("Safety"),
  Utilities: buildCategoryCatalog("Utilities"),
  Parks: buildCategoryCatalog("Parks"),
};

/** Flat list of all v1 build-panel ploppables. */
export const ALL_BUILD_PLOPPABLES: readonly BuildPloppable[] =
  BUILD_CATEGORIES.flatMap((category) => BUILD_CATALOG[category]);

const ploppableByContentKey = new Map(
  ALL_BUILD_PLOPPABLES.map((entry) => [entry.contentKey, entry]),
);

/** Lookup a catalog entry by tech unlock content key. */
export function findBuildPloppable(
  contentKey: string,
): BuildPloppable | undefined {
  return ploppableByContentKey.get(contentKey);
}

/** Whether a catalog ploppable is unlocked given researched WASM tech indices. */
export function isBuildUnlocked(
  contentKey: string,
  unlockedTechIds: ReadonlySet<number>,
): boolean {
  if (!ploppableByContentKey.has(contentKey)) return false;
  return isContentUnlocked(contentKey, unlockedTechIds);
}

export type BuildCatalogValidationIssue = {
  category: BuildCategory;
  contentKey: string;
  reason: string;
};

/** Validate every catalog entry resolves slug, TypeId, and tech requirement. */
export function validateBuildCatalog(): {
  ok: boolean;
  issues: BuildCatalogValidationIssue[];
} {
  const issues: BuildCatalogValidationIssue[] = [];
  for (const category of BUILD_CATEGORIES) {
    for (const contentKey of CONTENT_KEYS_BY_CATEGORY[category]) {
      const slug = V1_UNLOCK_TO_BUILDING_SLUG[contentKey];
      if (!slug) {
        issues.push({ category, contentKey, reason: "missing bridge slug" });
        continue;
      }
      const typeId = BUILDING_SLUG_TO_TYPE_ID[slug];
      if (typeof typeId !== "number") {
        issues.push({ category, contentKey, reason: "missing TypeId" });
      }
      if (!TECH_REQUIREMENT_BY_CONTENT_KEY[contentKey]) {
        issues.push({ category, contentKey, reason: "no v1 tech unlock" });
      }
    }
  }
  return { ok: issues.length === 0, issues };
}
