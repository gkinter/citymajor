import { CHUNK_COUNT, CHUNKS_PER_AXIS, CHUNK_SIZE, ZONE_COLORS } from "./constants";
import type { CityData } from "./types";
import { ZONE_OVERLAY_COLORS, type ZoneTile } from "./zoning";

const EMPTY_CHUNK_COLOR = "#141a28";

function chunkIndexForTile(tileX: number, tileZ: number): number {
  const cx = Math.floor(tileX / CHUNK_SIZE);
  const cz = Math.floor(tileZ / CHUNK_SIZE);
  return cz * CHUNKS_PER_AXIS + cx;
}

/** Dominant zone tint per 32×32 tile chunk (8×8 grid over the 256² world). */
export function summarizeChunkZoneColors(zones: ZoneTile[]): string[] {
  const counts = Array.from({ length: CHUNK_COUNT }, () => new Map<number, number>());

  for (const tile of zones) {
    const ci = chunkIndexForTile(tile.tileX, tile.tileZ);
    const map = counts[ci]!;
    map.set(tile.zoneType, (map.get(tile.zoneType) ?? 0) + 1);
  }

  return counts.map((map) => {
    if (map.size === 0) return EMPTY_CHUNK_COLOR;

    let bestType = 0;
    let bestCount = 0;
    for (const [zoneType, count] of map) {
      if (count > bestCount) {
        bestCount = count;
        bestType = zoneType;
      }
    }
    return ZONE_OVERLAY_COLORS[bestType] ?? EMPTY_CHUNK_COLOR;
  });
}

/** Building-zone histogram fallback when painted zones are not yet synced. */
export function summarizeChunkZoneColorsFromCity(city: CityData): string[] {
  const counts = Array.from({ length: CHUNK_COUNT }, () => new Map<string, number>());

  for (const building of city.buildings) {
    const map = counts[building.chunkIndex]!;
    map.set(building.zone, (map.get(building.zone) ?? 0) + 1);
  }

  return counts.map((map) => {
    if (map.size === 0) return EMPTY_CHUNK_COLOR;

    let bestZone: keyof typeof ZONE_COLORS = "residential";
    let bestCount = 0;
    for (const [zone, count] of map) {
      if (count > bestCount) {
        bestCount = count;
        bestZone = zone as keyof typeof ZONE_COLORS;
      }
    }
    return ZONE_COLORS[bestZone] ?? EMPTY_CHUNK_COLOR;
  });
}

export function resolveMinimapChunkColors(
  zones: ZoneTile[],
  city: CityData,
): string[] {
  if (zones.length > 0) return summarizeChunkZoneColors(zones);
  return summarizeChunkZoneColorsFromCity(city);
}
