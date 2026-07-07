"use client";

import { useFrame } from "@react-three/fiber";
import { useCallback, useEffect, useRef } from "react";
import type { BuildingInstance } from "./types";

const SPAWN_DURATION_MS = 480;

/** Slight overshoot for a satisfying pop-in. */
function easeOutBack(t: number): number {
  const c1 = 1.70158;
  const c3 = c1 + 1;
  return 1 + c3 * (t - 1) ** 3 + c1 * (t - 1) ** 2;
}

/**
 * Tracks newly appeared buildings and returns per-id Y-scale multipliers
 * that animate from 0 → 1 over SPAWN_DURATION_MS.
 */
export function useBuildingSpawnScales(buildings: BuildingInstance[]) {
  const spawnStarts = useRef(new Map<number, number>());
  const prevIds = useRef<Set<number>>(new Set());
  const scales = useRef(new Map<number, number>());
  const seeded = useRef(false);

  useEffect(() => {
    const ids = new Set(buildings.map((b) => b.id));
    if (!seeded.current) {
      prevIds.current = ids;
      seeded.current = true;
      return;
    }
    for (const id of ids) {
      if (!prevIds.current.has(id)) {
        spawnStarts.current.set(id, performance.now());
        scales.current.set(id, 0);
      }
    }
    prevIds.current = ids;

    for (const id of spawnStarts.current.keys()) {
      if (!ids.has(id)) spawnStarts.current.delete(id);
    }
  }, [buildings]);

  useFrame(() => {
    const now = performance.now();
    for (const [id, start] of spawnStarts.current) {
      const t = Math.min(1, (now - start) / SPAWN_DURATION_MS);
      scales.current.set(id, easeOutBack(t));
      if (t >= 1) spawnStarts.current.delete(id);
    }
  });

  return useCallback((buildingId: number) => scales.current.get(buildingId) ?? 1, []);
}

export function scaleVisualForSpawn(
  scale: [number, number, number],
  spawnScale: number,
): [number, number, number] {
  if (spawnScale >= 0.999) return scale;
  return [scale[0] * spawnScale, scale[1] * spawnScale, scale[2] * spawnScale];
}
