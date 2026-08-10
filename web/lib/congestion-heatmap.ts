import type { RoadGraphSnapshot } from "@/lib/sim-bridge";
import type { TrafficTile } from "@/lib/zoning";

function clamp01(n: number): number {
  return Math.min(1, Math.max(0, n));
}

/** Max absolute volume used when normalizing sparse/low traffic samples. */
const VOLUME_FLOOR = 1e-6;

/**
 * Density 0–1 from P4.2 edgeVolumes (preferred) or travelTimes (fallback).
 * Volumes are relative to the snapshot max; travel times use (tt − min) / (max − min).
 */
export function edgeCongestionDensity(
  edgeIndex: number,
  volumes: number[] | undefined,
  travelTimes: number[] | undefined,
  maxVolume: number,
  minTravelTime: number,
  maxTravelTime: number,
): number {
  const volume = volumes?.[edgeIndex];
  if (volume != null && maxVolume > VOLUME_FLOOR) {
    return clamp01(volume / maxVolume);
  }

  const tt = travelTimes?.[edgeIndex];
  if (
    tt != null &&
    Number.isFinite(tt) &&
    maxTravelTime > minTravelTime + VOLUME_FLOOR
  ) {
    return clamp01((tt - minTravelTime) / (maxTravelTime - minTravelTime));
  }

  return 0;
}

/** Bresenham-style inclusive line between two tile centers (axis-aligned or diagonal). */
export function tilesAlongSegment(
  x0: number,
  z0: number,
  x1: number,
  z1: number,
): Array<{ tileX: number; tileZ: number }> {
  const tiles: Array<{ tileX: number; tileZ: number }> = [];
  let x = Math.trunc(x0);
  let z = Math.trunc(z0);
  const xEnd = Math.trunc(x1);
  const zEnd = Math.trunc(z1);
  const dx = Math.abs(xEnd - x);
  const dz = Math.abs(zEnd - z);
  const sx = x < xEnd ? 1 : -1;
  const sz = z < zEnd ? 1 : -1;
  let err = dx - dz;

  for (;;) {
    tiles.push({ tileX: x, tileZ: z });
    if (x === xEnd && z === zEnd) break;
    const e2 = 2 * err;
    if (e2 > -dz) {
      err -= dz;
      x += sx;
    }
    if (e2 < dx) {
      err += dx;
      z += sz;
    }
  }
  return tiles;
}

function arrayMax(values: number[] | undefined, count: number): number {
  if (!values || values.length === 0 || count <= 0) return 0;
  let max = 0;
  const n = Math.min(count, values.length);
  for (let i = 0; i < n; i++) {
    const v = values[i]!;
    if (v > max) max = v;
  }
  return max;
}

function arrayMinPositive(
  values: number[] | undefined,
  count: number,
): number {
  if (!values || values.length === 0 || count <= 0) return 0;
  let min = Number.POSITIVE_INFINITY;
  const n = Math.min(count, values.length);
  for (let i = 0; i < n; i++) {
    const v = values[i]!;
    if (Number.isFinite(v) && v < min) min = v;
  }
  return Number.isFinite(min) ? min : 0;
}

/**
 * Build sparse congestion tiles for TrafficOverlay from roadGraph edge metrics.
 * Prefers edgeVolumes; falls back to travelTimes. Returns [] when topology or
 * metrics are missing so callers can keep snapshot.traffic.
 */
export function trafficTilesFromRoadGraph(
  graph: RoadGraphSnapshot | undefined | null,
): TrafficTile[] {
  if (!graph || graph.nodeCount <= 0) return [];

  const edgeCount = graph.edgeCount ?? 0;
  const volumes = graph.edgeVolumes;
  const travelTimes = graph.travelTimes;
  const edgeFrom = graph.edgeFrom;
  const edgeTo = graph.edgeTo;

  const hasVolumes = !!volumes && volumes.length > 0;
  const hasTimes = !!travelTimes && travelTimes.length > 0;
  if (edgeCount <= 0 || (!hasVolumes && !hasTimes)) return [];
  if (!edgeFrom || !edgeTo || edgeFrom.length === 0 || edgeTo.length === 0) {
    return [];
  }

  const xs = graph.nodeTileX;
  const zs = graph.nodeTileZ;
  if (!xs || !zs || xs.length < graph.nodeCount || zs.length < graph.nodeCount) {
    return [];
  }

  const nEdges = Math.min(edgeCount, edgeFrom.length, edgeTo.length);
  const maxVolume = arrayMax(volumes, nEdges);
  const maxTravelTime = arrayMax(travelTimes, nEdges);
  const minTravelTime = arrayMinPositive(travelTimes, nEdges);

  const densityByKey = new Map<string, number>();
  const bump = (tileX: number, tileZ: number, density: number) => {
    if (density < 0.01) return;
    const key = `${tileX},${tileZ}`;
    const prev = densityByKey.get(key) ?? 0;
    if (density > prev) densityByKey.set(key, density);
  };

  for (let e = 0; e < nEdges; e++) {
    const from = edgeFrom[e]!;
    const to = edgeTo[e]!;
    if (from < 0 || to < 0 || from >= graph.nodeCount || to >= graph.nodeCount) {
      continue;
    }

    const density = edgeCongestionDensity(
      e,
      volumes,
      travelTimes,
      maxVolume,
      minTravelTime,
      maxTravelTime,
    );
    if (density < 0.01) continue;

    const x0 = xs[from]!;
    const z0 = zs[from]!;
    const x1 = xs[to]!;
    const z1 = zs[to]!;
    for (const tile of tilesAlongSegment(x0, z0, x1, z1)) {
      bump(tile.tileX, tile.tileZ, density);
    }
  }

  const out: TrafficTile[] = [];
  for (const [key, density] of densityByKey) {
    const [tileX, tileZ] = key.split(",").map(Number) as [number, number];
    out.push({ tileX, tileZ, density });
  }
  return out;
}

/**
 * Prefer P4.2 roadGraph edge metrics; fall back to sparse WasmTrafficLite tiles.
 */
export function resolveTrafficOverlayTiles(
  graph: RoadGraphSnapshot | undefined | null,
  fallback: TrafficTile[] | undefined | null,
): TrafficTile[] {
  const fromGraph = trafficTilesFromRoadGraph(graph);
  if (fromGraph.length > 0) return fromGraph;
  return fallback ?? [];
}
