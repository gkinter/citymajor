/**
 * CI guard: every Frontier + Industrial tech unlock must resolve via
 * base/data/tech/v1-unlock-bridge.json to a capability or buildings.json TypeId.
 *
 * Run: pnpm verify:tech-unlocks
 */
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "../..");
const readJson = (rel) => JSON.parse(readFileSync(join(root, rel), "utf8"));

const TYPE_ID = {
  RES_LOW_START: 100,
  RES_HIGH_START: 200,
  COM_START: 300,
  IND_START: 400,
  IND_END: 499,
};

const technologies = readJson("base/data/tech/technologies.json");
const buildings = readJson("base/data/buildings/buildings.json");
const bridge = readJson("base/data/tech/v1-unlock-bridge.json");

const V1_ERAS = new Set(["frontier", "industrial"]);
const v1Techs = technologies.filter((t) => V1_ERAS.has(t.era));
const capabilities = new Set(bridge.capabilities);
const unlockToSlug = bridge.buildingSlugs;

function categoryBase(category, density = 0) {
  switch (category) {
    case "residential":
      return density >= 2 ? TYPE_ID.RES_HIGH_START : TYPE_ID.RES_LOW_START;
    case "commercial":
      return TYPE_ID.COM_START;
    case "industrial":
      return TYPE_ID.IND_START;
    default:
      return null;
  }
}

function buildSlugToTypeIdMap() {
  const map = {};
  const zoneBands = new Map();
  const serviceBands = new Map();
  const heroBuildings = [];

  for (const building of buildings) {
    const base = categoryBase(building.category, building.density ?? 0);
    if (base !== null) {
      const key = `${base}:${building.era}`;
      const band = zoneBands.get(key) ?? [];
      band.push(building);
      zoneBands.set(key, band);
      continue;
    }
    if (building.category === "service") {
      const band = serviceBands.get(building.era) ?? [];
      band.push(building);
      serviceBands.set(building.era, band);
      continue;
    }
    if (building.category === "infrastructure" || building.category === "special") {
      heroBuildings.push(building);
    }
  }

  for (const [key, band] of zoneBands) {
    const base = Number(key.split(":")[0]);
    band.sort((a, b) => a.id.localeCompare(b.id));
    band.forEach((building, index) => {
      if (index < 20) map[building.id] = base + building.era * 20 + index;
    });
  }

  for (const [era, band] of serviceBands) {
    band.sort((a, b) => a.id.localeCompare(b.id));
    band.forEach((building, index) => {
      if (index < 20) map[building.id] = 500 + era * 20 + index;
    });
  }

  heroBuildings.sort((a, b) => a.id.localeCompare(b.id));
  heroBuildings.forEach((building, index) => {
    map[building.id] = 600 + index;
  });

  return map;
}

const slugToTypeId = buildSlugToTypeIdMap();

function isKnownTypeId(typeId) {
  return (
    (typeId >= TYPE_ID.RES_LOW_START && typeId <= TYPE_ID.IND_END) ||
    (typeId >= 500 && typeId < 600) ||
    (typeId >= 600 && typeId < 700)
  );
}

function isValidUnlock(unlock) {
  if (capabilities.has(unlock)) return true;
  const slug = unlockToSlug[unlock];
  if (!slug) return false;
  const typeId = slugToTypeId[slug];
  return typeof typeId === "number" && isKnownTypeId(typeId);
}

const issues = [];
const seenUnlocks = new Set();

for (const tech of v1Techs) {
  for (const unlock of tech.unlocks ?? []) {
    seenUnlocks.add(unlock);
    if (!isValidUnlock(unlock)) {
      issues.push({ techId: tech.id, techName: tech.name, unlock });
    }
  }
}

const bridgeKeys = new Set([
  ...capabilities,
  ...Object.keys(unlockToSlug),
]);

const orphanBridgeKeys = [...bridgeKeys].filter((k) => !seenUnlocks.has(k));

console.log(`v1 techs: ${v1Techs.length}`);
console.log(`unique unlock keys: ${seenUnlocks.size}`);
console.log(`capability unlocks: ${capabilities.size}`);
console.log(`building slug mappings: ${Object.keys(unlockToSlug).length}`);

if (issues.length > 0) {
  console.error("\nDead v1 unlocks (no capability or building TypeId):");
  for (const issue of issues) {
    console.error(`  ${issue.techId} ${issue.techName}: ${issue.unlock}`);
  }
  process.exit(1);
}

if (orphanBridgeKeys.length > 0) {
  console.warn("\nBridge entries not referenced by any v1 tech unlock:");
  for (const key of orphanBridgeKeys.sort()) {
    console.warn(`  ${key}`);
  }
}

console.log("\nOK: all v1 tech unlocks map to valid capabilities or building TypeIds");
