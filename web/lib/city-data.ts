import {
  ARCHETYPE_COUNT,
  CHUNK_SIZE,
  CHUNKS_PER_AXIS,
  GRID_SIZE,
  TARGET_BUILDING_COUNT,
  type ArchetypeId,
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

const ZONE_BY_ARCHETYPE: ZoneType[] = [
  "residential",
  "residential",
  "commercial",
  "industrial",
  "office",
];

function chunkIndexForTile(tileX: number, tileZ: number): number {
  const cx = Math.floor(tileX / CHUNK_SIZE);
  const cz = Math.floor(tileZ / CHUNK_SIZE);
  return cz * CHUNKS_PER_AXIS + cx;
}

function archetypeForNoise(n: number): ArchetypeId {
  if (n < 0.35) return 0;
  if (n < 0.55) return 1;
  if (n < 0.72) return 2;
  if (n < 0.88) return 3;
  return 4;
}

/**
 * Procedural 256×256 city mock — ~5000 building instances across 5 archetypes.
 * No WASM; patterns mirror Forge spatial grid + instanced placement.
 */
export function generateCityData(seed = 0x63697479): CityData {
  const rand = mulberry32(seed);
  const occupied = new Uint8Array(GRID_SIZE * GRID_SIZE);
  const buildings: BuildingInstance[] = [];
  const buildingsByArchetype: BuildingInstance[][] = Array.from(
    { length: ARCHETYPE_COUNT },
    () => [],
  );
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

    // Cluster bias — denser near center (simulated downtown).
    const nx = (tileX / GRID_SIZE - 0.5) * 2;
    const nz = (tileZ / GRID_SIZE - 0.5) * 2;
    const dist = Math.sqrt(nx * nx + nz * nz);
    if (rand() > 0.55 + dist * 0.35) continue;

    occupied[idx] = 1;
    const archetype = archetypeForNoise(rand());
    const zone = ZONE_BY_ARCHETYPE[archetype];
    const stories = 1 + Math.floor(rand() * (archetype <= 1 ? 6 : 10));
    const chunkIndex = chunkIndexForTile(tileX, tileZ);
    const building: BuildingInstance = {
      id: buildings.length,
      tileX,
      tileZ,
      archetype,
      zone,
      stories,
      chunkIndex,
      heat: rand(),
    };
    buildings.push(building);
    buildingsByArchetype[archetype].push(building);
    chunkBuildingIndices[chunkIndex].push(building.id);
  }

  return { buildings, buildingsByArchetype, chunkBuildingIndices };
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
