/**
 * CI guard: build-catalog ploppables resolve slug, TypeId, and v1 tech unlock.
 *
 * Run: pnpm verify:build-catalog
 */
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { buildSlugToTypeIdMap } from "../lib/building-type-id-map.ts";

const root = join(dirname(fileURLToPath(import.meta.url)), "../..");
const readJson = (rel: string) => JSON.parse(readFileSync(join(root, rel), "utf8"));

const technologies = readJson("base/data/tech/technologies.json");
const buildings = readJson("base/data/buildings/buildings.json");
const bridge = readJson("base/data/tech/v1-unlock-bridge.json");

const V1_ERAS = new Set(["frontier", "industrial"]);
const v1Techs = technologies.filter((t: { era: string }) => V1_ERAS.has(t.era));
const unlockToSlug: Record<string, string> = bridge.buildingSlugs;
const slugToTypeId = buildSlugToTypeIdMap(buildings);

const techByUnlock = new Map<string, string>();
for (const tech of v1Techs) {
  for (const unlock of tech.unlocks ?? []) {
    if (!techByUnlock.has(unlock)) techByUnlock.set(unlock, tech.id);
  }
}

const CONTENT_KEYS_BY_CATEGORY: Record<string, readonly string[]> = {
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

const issues: Array<{ category: string; contentKey: string; reason: string }> =
  [];
let total = 0;

for (const [category, keys] of Object.entries(CONTENT_KEYS_BY_CATEGORY)) {
  console.log(`${category}: ${keys.length}`);
  for (const contentKey of keys) {
    total += 1;
    const slug = unlockToSlug[contentKey];
    if (!slug) {
      issues.push({ category, contentKey, reason: "missing bridge slug" });
      continue;
    }
    const typeId = slugToTypeId[slug];
    if (typeof typeId !== "number") {
      issues.push({ category, contentKey, reason: "missing TypeId" });
    }
    if (!techByUnlock.has(contentKey)) {
      issues.push({ category, contentKey, reason: "no v1 tech unlock" });
    }
  }
}

console.log(`\nploppables: ${total}`);

if (issues.length > 0) {
  console.error("\nBuild catalog validation failed:");
  for (const issue of issues) {
    console.error(`  [${issue.category}] ${issue.contentKey}: ${issue.reason}`);
  }
  process.exit(1);
}

console.log("\nOK: all build-catalog ploppables resolve slug, TypeId, and tech unlock");
