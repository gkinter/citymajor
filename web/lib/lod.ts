import {
  BuildingCategory,
  PIXELS_PER_STORY,
  buildingVisualParams,
} from "@citymajor/sim-types";
import type { BuildingInstance } from "./types";
import { ZONE_COLORS } from "./constants";
import * as THREE from "three";

const _color = new THREE.Color();
const _matrix = new THREE.Matrix4();
const _position = new THREE.Vector3();
const _scale = new THREE.Vector3();
const _quat = new THREE.Quaternion();

/** World Y per isometric pixel (legacy storyH / PIXELS_PER_STORY). */
const STORY_WORLD_HEIGHT = 0.35;
const WORLD_HEIGHT_PER_PIXEL = STORY_WORLD_HEIGHT / PIXELS_PER_STORY;
const SCAFFOLDING_COLOR = "#8f8f8f";

export type LodVisual = {
  scale: [number, number, number];
  color: string;
  opacity: number;
  wireframe?: boolean;
};

function isCompactResidential(category: BuildingCategory): boolean {
  return category === BuildingCategory.ResidentialLow;
}

/**
 * LOD visual params per level:
 * L0/L1 use buildingVisualParams for height + wall colors; L2/L3 zone/heatmap.
 */
export function lodVisualForBuilding(
  building: BuildingInstance,
  lod: 0 | 1 | 2 | 3,
  dayNightFactor = 0,
): LodVisual {
  const compact = isCompactResidential(building.category);
  const baseW = compact ? 0.75 : 0.9;
  const baseD = compact ? 0.75 : 0.9;

  const params = buildingVisualParams(
    building.typeId,
    building.level,
    building.condition,
    dayNightFactor,
    building.state,
    building.tileX,
    building.tileZ,
  );

  const worldH = params.heightPx * WORLD_HEIGHT_PER_PIXEL;
  const wallColor = params.colors.base;

  switch (lod) {
    case 0: {
      if (params.showScaffolding) {
        return {
          scale: [baseW, worldH, baseD],
          color: SCAFFOLDING_COLOR,
          opacity: 0.88,
          wireframe: true,
        };
      }
      return {
        scale: [baseW, worldH, baseD],
        color: shadeColor(wallColor, 0.85 + building.heat * 0.15),
        opacity: 1,
      };
    }
    case 1:
      return {
        scale: [baseW * 0.92, worldH * 0.75, baseD * 0.92],
        color: shadeColor(wallColor, 0.7),
        opacity: 1,
      };
    case 2:
      return {
        scale: [0.95, Math.max(0.25, worldH * 0.35), 0.95],
        color: ZONE_COLORS[building.zone],
        opacity: 0.92,
      };
    case 3: {
      const heat = building.heat;
      const heatColor = heatColorRamp(heat);
      return {
        scale: [1, 0.2, 1],
        color: heatColor,
        opacity: 0.85,
      };
    }
  }
}

function shadeColor(hex: string, factor: number): string {
  _color.set(hex);
  _color.multiplyScalar(factor);
  return `#${_color.getHexString()}`;
}

function heatColorRamp(t: number): string {
  const r = Math.min(1, Math.max(0, (t - 0.5) * 2));
  const g = Math.min(1, Math.max(0, 1 - Math.abs(t - 0.5) * 2));
  const b = Math.min(1, Math.max(0, (0.5 - t) * 2));
  _color.setRGB(r, g, b);
  return `#${_color.getHexString()}`;
}

/** Deterministic 90° yaw per tile (matches sim window-seed mixing). */
export function tileYawRadians(
  tileX: number,
  tileZ: number,
  typeId: number,
): number {
  const hash =
    (tileX * 73856093) ^ (tileZ * 19349669) ^ (typeId * 83492791);
  return ((hash >>> 0) % 4) * (Math.PI / 2);
}

export function composeInstanceMatrix(
  tileX: number,
  tileZ: number,
  visual: LodVisual,
  hidden: boolean,
  rotationY = 0,
): THREE.Matrix4 {
  if (hidden) {
    return _matrix.compose(
      _position.set(tileX + 0.5, 0, tileZ + 0.5),
      _quat.identity(),
      _scale.set(0, 0, 0),
    );
  }

  const y = visual.scale[1] / 2;
  _quat.setFromAxisAngle(_position.set(0, 1, 0), rotationY);
  return _matrix.compose(
    _position.set(tileX + 0.5, y, tileZ + 0.5),
    _quat,
    _scale.set(visual.scale[0], visual.scale[1], visual.scale[2]),
  );
}

export type GltfFootprint = {
  width: number;
  height: number;
  depth: number;
  /** Local-space min Y before scaling (model footing). */
  minY: number;
};

/**
 * Fit a GLTF module to tile footprint with per-tile yaw.
 * Scales non-uniformly so authored meshes sit flush on the tile grid.
 */
export function composeGltfInstanceMatrix(
  tileX: number,
  tileZ: number,
  targetScale: [number, number, number],
  footprint: GltfFootprint,
  rotationY: number,
  hidden: boolean,
): THREE.Matrix4 {
  if (hidden) {
    return _matrix.compose(
      _position.set(tileX + 0.5, 0, tileZ + 0.5),
      _quat.identity(),
      _scale.set(0, 0, 0),
    );
  }

  const sx = targetScale[0] / Math.max(footprint.width, 0.001);
  const sy = targetScale[1] / Math.max(footprint.height, 0.001);
  const sz = targetScale[2] / Math.max(footprint.depth, 0.001);
  const y = -footprint.minY * sy;

  _quat.setFromAxisAngle(_position.set(0, 1, 0), rotationY);
  return _matrix.compose(
    _position.set(tileX + 0.5, y, tileZ + 0.5),
    _quat,
    _scale.set(sx, sy, sz),
  );
}

export function metalnessForCategory(category: BuildingCategory): number {
  return category === BuildingCategory.Industrial ||
    category === BuildingCategory.Commercial
    ? 0.15
    : 0.05;
}
