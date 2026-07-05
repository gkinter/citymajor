/**
 * CI guard: every Frontier + Industrial tech unlock must resolve via
 * base/data/tech/v1-unlock-bridge.json to a capability or buildings.json TypeId.
 *
 * Run: pnpm verify:tech-unlocks
 */
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import {
  buildSlugToTypeIdMap,
  isKnownBuildingTypeId,
} from "../lib/building-type-id-map.ts";

const root = join(dirname(fileURLToPath(import.meta.url)), "../..");
const readJson = (rel: string) => JSON.parse(readFileSync(join(root, rel), "utf8"));

const technologies = readJson("base/data/tech/technologies.json");
const buildings = readJson("base/data/buildings/buildings.json");
const bridge = readJson("base/data/tech/v1-unlock-bridge.json");

const V1_ERAS = new Set(["frontier", "industrial"]);
const v1Techs = technologies.filter((t: { era: string }) => V1_ERAS.has(t.era));
const capabilities = new Set<string>(bridge.capabilities);
const unlockToSlug: Record<string, string> = bridge.buildingSlugs;

const slugToTypeId = buildSlugToTypeIdMap(buildings);

function isValidUnlock(unlock: string): boolean {
  if (capabilities.has(unlock)) return true;
  const slug = unlockToSlug[unlock];
  if (!slug) return false;
  const typeId = slugToTypeId[slug];
  return typeof typeId === "number" && isKnownBuildingTypeId(typeId);
}

const issues: Array<{ techId: string; techName: string; unlock: string }> = [];
const seenUnlocks = new Set<string>();

for (const tech of v1Techs) {
  for (const unlock of tech.unlocks ?? []) {
    seenUnlocks.add(unlock);
    if (!isValidUnlock(unlock)) {
      issues.push({ techId: tech.id, techName: tech.name, unlock });
    }
  }
}

const bridgeKeys = new Set([...capabilities, ...Object.keys(unlockToSlug)]);

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
