import {
  BuildingCategory,
  BuildingState,
  TYPE_ID,
  archetypeKey,
  classifyBuilding,
  computeStories,
  deriveEra,
} from "@citymajor/sim-types";
import type { SimSnapshot } from "./sim-bridge";
import {
  CHUNK_SIZE,
  CHUNKS_PER_AXIS,
  GRID_SIZE,
  TARGET_BUILDING_COUNT,
  type ZoneType,
} from "./constants";
import type { BuildingInstance, CityData } from "./types";

/** Mulberry32 seeded PRNG for reproducible procedural city. */
function mulberry32(seed: number) {
  return () => {
    seed |= 0;
    seed = (seed + 0x6d2b79f5) | 0;
    let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

const ZONE_BY_CATEGORY: Record<BuildingCategory, ZoneType> = {
  [BuildingCategory.ResidentialLow]: "residential",
  [BuildingCategory.ResidentialHigh]: "residential",
  [BuildingCategory.Commercial]: "commercial",
  [BuildingCategory.Industrial]: "industrial",
  [BuildingCategory.Service]: "office",
};

const CATEGORY_BASE: Record<
  Exclude<BuildingCategory, BuildingCategory.Service>,
  number
> = {
  [BuildingCategory.ResidentialLow]: TYPE_ID.RES_LOW_START,
  [BuildingCategory.ResidentialHigh]: TYPE_ID.RES_HIGH_START,
  [BuildingCategory.Commercial]: TYPE_ID.COM_START,
  [BuildingCategory.Industrial]: TYPE_ID.IND_START,
};

function categoryForNoise(n: number, dist: number): BuildingCategory {
  if (dist < 0.35 && n > 0.55) return BuildingCategory.Commercial;
  if (dist < 0.5 && n > 0.7) return BuildingCategory.ResidentialHigh;
  if (n < 0.3) return BuildingCategory.Industrial;
  if (n < 0.55) return BuildingCategory.ResidentialLow;
  if (n < 0.75) return BuildingCategory.ResidentialHigh;
  if (n < 0.9) return BuildingCategory.Commercial;
  return BuildingCategory.Industrial;
}

/** Pick a TypeId within category era bands (mirrors sim TypeId layout). */
function typeIdForCategory(
  category: BuildingCategory,
  rand: () => number,
): number {
  if (category === BuildingCategory.Service) {
    return Math.floor(rand() * 20);
  }
  const base = CATEGORY_BASE[category];
  const eraBand = Math.floor(rand() * 5);
  const variant = Math.floor(rand() * 20);
  return base + eraBand * 20 + variant;
}

function chunkIndexForTile(tileX: number, tileZ: number): number {
  const cx = Math.floor(tileX / CHUNK_SIZE);
  const cz = Math.floor(tileZ / CHUNK_SIZE);
  return cz * CHUNKS_PER_AXIS + cx;
}

/**
 * Procedural 256×256 city mock — ~5000 buildings classified via sim-types.
 * TypeIds follow Forge BuildingRenderer ranges; keys match BUILDING_ARCHETYPE_3D ADR.
 */
export function generateCityData(seed = 0x63697479): CityData {
  const rand = mulberry32(seed);
  const occupied = new Uint8Array(GRID_SIZE * GRID_SIZE);
  const buildings: BuildingInstance[] = [];
  const buildingsByArchetypeKey: Record<string, BuildingInstance[]> = {};
  const chunkBuildingIndices: number[][] = Array.from(
    { length: CHUNKS_PER_AXIS * CHUNKS_PER_AXIS },
    () => [],
  );

  let attempts = 0;
  const maxAttempts = TARGET_BUILDING_COUNT * 12;

  while (buildings.length < TARGET_BUILDING_COUNT && attempts < maxAttempts) {
    attempts += 1;
    const tileX = Math.floor(rand() * GRID_SIZE);
    const tileZ = Math.floor(rand() * GRID_SIZE);
    const idx = tileZ * GRID_SIZE + tileX;
    if (occupied[idx]) continue;

    const nx = (tileX / GRID_SIZE - 0.5) * 2;
    const nz = (tileZ / GRID_SIZE - 0.5) * 2;
    const dist = Math.sqrt(nx * nx + nz * nz);
    if (rand() > 0.55 + dist * 0.35) continue;

    occupied[idx] = 1;

    const category = categoryForNoise(rand(), dist);
    const typeId = typeIdForCategory(category, rand);
    const key = archetypeKey(typeId);
    const era = deriveEra(typeId);
    const zone = ZONE_BY_CATEGORY[classifyBuilding(typeId)];
    const level = 1 + Math.floor(rand() * (category === BuildingCategory.ResidentialLow ? 3 : 5));
    const stories = computeStories(classifyBuilding(typeId), level);
    const chunkIndex = chunkIndexForTile(tileX, tileZ);

    const building: BuildingInstance = {
      id: buildings.length,
      tileX,
      tileZ,
      typeId,
      archetypeKey: key,
      category: classifyBuilding(typeId),
      era,
      zone,
      level,
      stories,
      condition: 255,
      state: BuildingState.Operational,
      chunkIndex,
      heat: rand(),
    };

    buildings.push(building);
    if (!buildingsByArchetypeKey[key]) buildingsByArchetypeKey[key] = [];
    buildingsByArchetypeKey[key].push(building);
    chunkBuildingIndices[chunkIndex].push(building.id);
  }

  const archetypeKeys = Object.keys(buildingsByArchetypeKey).sort();

  return { buildings, buildingsByArchetypeKey, archetypeKeys, chunkBuildingIndices };
}

/**
 * Build CityData from a WASM sim render snapshot (GetRenderSnapshot JSON).
 * Maps sim building pool slots into R3F instancing buckets.
 */
export function cityDataFromSnapshot(snapshot: SimSnapshot): CityData {
  const buildings: BuildingInstance[] = [];
  const buildingsByArchetypeKey: Record<string, BuildingInstance[]> = {};
  const chunkBuildingIndices: number[][] = Array.from(
    { length: CHUNKS_PER_AXIS * CHUNKS_PER_AXIS },
    () => [],
  );

  for (let i = 0; i < snapshot.buildings.length; i++) {
    const b = snapshot.buildings[i];
    const typeId = b.typeId;
    const category = classifyBuilding(typeId);
    const key = archetypeKey(typeId);
    const era = deriveEra(typeId);
    const zone = ZONE_BY_CATEGORY[category];
    const stories = computeStories(category, b.level);
    const chunkIndex = chunkIndexForTile(b.tileX, b.tileZ);
    const heat = (b.condition / 255) * 0.5 + b.level * 0.1;

    const state =
      b.state >= BuildingState.Constructing &&
      b.state <= BuildingState.Demolishing
        ? (b.state as BuildingState)
        : BuildingState.Operational;

    const building: BuildingInstance = {
      id: b.id,
      tileX: b.tileX,
      tileZ: b.tileZ,
      typeId,
      archetypeKey: key,
      category,
      era,
      zone,
      level: b.level,
      stories,
      condition: b.condition,
      state,
      chunkIndex,
      heat,
    };

    buildings.push(building);
    if (!buildingsByArchetypeKey[key]) buildingsByArchetypeKey[key] = [];
    buildingsByArchetypeKey[key].push(building);
    chunkBuildingIndices[chunkIndex].push(building.id);
  }

  const archetypeKeys = Object.keys(buildingsByArchetypeKey).sort();

  return { buildings, buildingsByArchetypeKey, archetypeKeys, chunkBuildingIndices };
}

/** Singleton city data for the spike (generated once per session). */
let cachedCity: CityData | null = null;

export function getCityData(): CityData {
  if (!cachedCity) cachedCity = generateCityData();
  return cachedCity;
}

export function tileToWorld(tile: number): number {
  return tile + 0.5;
}

export function worldToTile(world: number): number {
  return Math.floor(world);
}

export function chunkOrigin(chunkIndex: number): { x: number; z: number } {
  const cx = chunkIndex % CHUNKS_PER_AXIS;
  const cz = Math.floor(chunkIndex / CHUNKS_PER_AXIS);
  return { x: cx * CHUNK_SIZE, z: cz * CHUNK_SIZE };
}

export function chunkCenter(chunkIndex: number): { x: number; z: number } {
  const { x, z } = chunkOrigin(chunkIndex);
  return { x: x + CHUNK_SIZE / 2, z: z + CHUNK_SIZE / 2 };
}
