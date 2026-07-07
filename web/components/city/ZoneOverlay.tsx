"use client";

import { useLayoutEffect, useRef } from "react";
import * as THREE from "three";
import type { ZoneTile } from "@/lib/zoning";
import { ZONE_OVERLAY_COLORS } from "@/lib/zoning";

type ZoneOverlayProps = {
  zones: ZoneTile[];
};

const _color = new THREE.Color();
const _matrix = new THREE.Matrix4();
const _position = new THREE.Vector3();
const _quaternion = new THREE.Quaternion();
const _scale = new THREE.Vector3(0.92, 0.92, 1);
const _euler = new THREE.Euler(-Math.PI / 2, 0, 0);

const MAX_INSTANCES = 65536;

/**
 * Semi-transparent zone tint planes slightly above terrain.
 */
export function ZoneOverlay({ zones }: ZoneOverlayProps) {
  const meshRef = useRef<THREE.InstancedMesh>(null);

  useLayoutEffect(() => {
    const mesh = meshRef.current;
    if (!mesh) return;

    // Sparse zone list — instance index follows array order, not grid coords.
    const count = Math.min(zones.length, MAX_INSTANCES);
    for (let i = 0; i < count; i++) {
      const tile = zones[i]!;
      _position.set(tile.tileX + 0.5, 0.03, tile.tileZ + 0.5);
      _quaternion.setFromEuler(_euler);
      _matrix.compose(_position, _quaternion, _scale);
      mesh.setMatrixAt(i, _matrix);
      mesh.setColorAt(
        i,
        _color.set(ZONE_OVERLAY_COLORS[tile.zoneType] ?? "#888888"),
      );
    }

    mesh.count = count;
    mesh.instanceMatrix.needsUpdate = true;
    if (mesh.instanceColor) mesh.instanceColor.needsUpdate = true;
  }, [zones]);

  if (zones.length === 0) return null;

  return (
    <instancedMesh
      ref={meshRef}
      args={[undefined, undefined, MAX_INSTANCES]}
      frustumCulled={false}
    >
      <planeGeometry args={[1, 1]} />
      <meshBasicMaterial
        transparent
        opacity={0.45}
        depthWrite={false}
        vertexColors
      />
    </instancedMesh>
  );
}
