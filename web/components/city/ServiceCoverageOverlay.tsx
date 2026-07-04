"use client";

import { useLayoutEffect, useRef } from "react";
import * as THREE from "three";
import type { ServiceCoverageSnapshot, ServiceViewMode } from "@/lib/sim-bridge";

type ServiceCoverageOverlayProps = {
  tiles: ServiceCoverageSnapshot[];
  mode: ServiceViewMode;
};

const _color = new THREE.Color();
const _matrix = new THREE.Matrix4();
const _position = new THREE.Vector3();
const _quaternion = new THREE.Quaternion();
const _scale = new THREE.Vector3(0.92, 0.92, 1);
const _euler = new THREE.Euler(-Math.PI / 2, 0, 0);

const MAX_INSTANCES = 65536;

/** SimCity-style data heat: red (none) → yellow → green (full). */
function coverageHeatColor(value: number, hue: number): THREE.Color {
  const t = Math.min(1, Math.max(0, value));
  const lightness = 0.22 + t * 0.38;
  const saturation = 0.55 + t * 0.35;
  return _color.setHSL((hue * (1 - t * 0.65)) / 360, saturation, lightness);
}

function valueForMode(tile: ServiceCoverageSnapshot, mode: ServiceViewMode): number {
  switch (mode) {
    case "health":
      return tile.health;
    case "police":
      return tile.police;
    case "fire":
      return tile.fire;
    default:
      return 0;
  }
}

/** Hue anchor per service type (health=green, police=blue, fire=orange). */
function hueForMode(mode: ServiceViewMode): number {
  switch (mode) {
    case "health":
      return 130;
    case "police":
      return 220;
    case "fire":
      return 28;
    default:
      return 0;
  }
}

/**
 * Semi-transparent coverage heat planes above zoned tiles.
 */
export function ServiceCoverageOverlay({
  tiles,
  mode,
}: ServiceCoverageOverlayProps) {
  const meshRef = useRef<THREE.InstancedMesh>(null);

  useLayoutEffect(() => {
    const mesh = meshRef.current;
    if (!mesh || mode === "off") return;

    const hue = hueForMode(mode);
    const count = Math.min(tiles.length, MAX_INSTANCES);
    for (let i = 0; i < count; i++) {
      const tile = tiles[i]!;
      _position.set(tile.tileX + 0.5, 0.05, tile.tileZ + 0.5);
      _quaternion.setFromEuler(_euler);
      _matrix.compose(_position, _quaternion, _scale);
      mesh.setMatrixAt(i, _matrix);
      mesh.setColorAt(i, coverageHeatColor(valueForMode(tile, mode), hue));
    }

    mesh.count = count;
    mesh.instanceMatrix.needsUpdate = true;
    if (mesh.instanceColor) mesh.instanceColor.needsUpdate = true;
  }, [tiles, mode]);

  if (mode === "off" || tiles.length === 0) return null;

  return (
    <instancedMesh
      ref={meshRef}
      args={[undefined, undefined, MAX_INSTANCES]}
      frustumCulled={false}
    >
      <planeGeometry args={[1, 1]} />
      <meshBasicMaterial
        transparent
        opacity={0.55}
        depthWrite={false}
        vertexColors
      />
    </instancedMesh>
  );
}
