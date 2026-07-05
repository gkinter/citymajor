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

/** Hero GLB filenames for era-gate monuments (`nextEra` → target era). */
const ERA_LANDMARK_HERO_FILES: Record<number, string> = {
  1: "hero_industrial_train_station.glb",
  2: "hero_postwar_civic_hall.glb",
  3: "hero_modern_glass_tower.glb",
  4: "inf_future_fusion_reactor.glb",
};

const HERO_GLTF_BASE = "/assets/gltf/heroes";

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
 * True when all era-progress gates are satisfied (population AND tech count).
 * Matches ResearchSystem.CheckEraTransition / WasmEraDeriver.GetEraProgress.
 */
export function isEraGateReady(progress: EraProgress | undefined): boolean {
  if (!progress || progress.gates.length === 0) return false;
  return progress.gates.every((gate) => gate.met);
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

/** Public URL for the hero GLB tied to an era gate, or null when unmapped. */
export function eraLandmarkGltfPath(nextEra: number): string | null {
  const file = ERA_LANDMARK_HERO_FILES[nextEra];
  if (!file) return null;
  return `${HERO_GLTF_BASE}/${file}`;
}

/** HEAD probe — true when the hero GLB is present under `public/assets/gltf/heroes/`. */
export async function checkEraLandmarkGltfExists(path: string): Promise<boolean> {
  try {
    const res = await fetch(path, { method: "HEAD", cache: "no-store" });
    const type = res.headers.get("content-type") ?? "";
    return res.ok && (type.includes("model/gltf") || type.includes("octet-stream") || type.includes("application"));
  } catch {
    return false;
  }
}
