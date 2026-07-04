import { eraQuestTitle } from "@/lib/era-narrative";
import { HUD_ERA_BADGE_COLORS } from "@/lib/era";
import type { EraProgress } from "@/lib/sim-bridge";

/** Monument names per next era — ERA_ARC_DESIGN_V2 §6.B + MESHY_HERO_LANDMARKS. */
const ERA_LANDMARK_NAMES: Record<number, string> = {
  1: "The Grand Terminus",
  2: "The Interstate Hub",
  3: "International Airport",
  4: "Fusion Reactor",
};

export type EraLandmarkSpec = {
  landmarkName: string;
  questTitle: string | null;
  accentColor: string;
  /** Monument height in world units (tile = 1). */
  height: number;
  /** Footprint width/depth in tiles. */
  footprint: number;
};

/**
 * True when WASM era-progress gates are satisfied for the next era.
 * Uses `percent >= 100` so Frontier→Industrial OR-path logic matches WasmEraDeriver.
 */
export function isEraGateReady(progress: EraProgress | undefined): boolean {
  if (!progress || progress.gates.length === 0) return false;
  return progress.percent >= 100;
}

export function eraLandmarkSpec(nextEra: number): EraLandmarkSpec | null {
  const landmarkName = ERA_LANDMARK_NAMES[nextEra];
  if (!landmarkName) return null;

  const palette = HUD_ERA_BADGE_COLORS[nextEra];

  const heights: Record<number, number> = {
    1: 6,
    2: 8,
    3: 14,
    4: 18,
  };
  const footprints: Record<number, number> = {
    1: 3,
    2: 4,
    3: 4,
    4: 5,
  };

  return {
    landmarkName,
    questTitle: eraQuestTitle(nextEra),
    accentColor: palette?.text ?? "#e8eef8",
    height: heights[nextEra] ?? 6,
    footprint: footprints[nextEra] ?? 3,
  };
}

/** City-center tile for era landmark placement (v1). */
export const ERA_LANDMARK_TILE = {
  tileX: 128,
  tileZ: 128,
} as const;
