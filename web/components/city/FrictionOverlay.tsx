"use client";

import { useLayoutEffect, useRef } from "react";
import * as THREE from "three";
import type { FrictionCorridorTile } from "@/lib/zoning";

type FrictionOverlayProps = {
  corridors: FrictionCorridorTile[];
  /** When false, overlay is not rendered. */
  visible?: boolean;
};

const _color = new THREE.Color();
const _matrix = new THREE.Matrix4();
const _position = new THREE.Vector3();
const _quaternion = new THREE.Quaternion();
const _scale = new THREE.Vector3(0.95, 0.95, 1);
const _euler = new THREE.Euler(-Math.PI / 2, 0, 0);

const MAX_INSTANCES = 16384;

/** Cool cyan (local) → amber → magenta (high inter-zone friction). */
function frictionToColor(friction: number): THREE.Color {
  const t = Math.min(1, Math.max(0, friction));
  return _color.setHSL(0.55 - t * 0.55, 0.85, 0.42 + t * 0.08);
}

/**
 * Semi-transparent goods-transport friction corridors along market-zone boundaries.
 */
export function FrictionOverlay({
  corridors,
  visible = true,
}: FrictionOverlayProps) {
  const meshRef = useRef<THREE.InstancedMesh>(null);

  useLayoutEffect(() => {
    const mesh = meshRef.current;
    if (!mesh || !visible) return;

    const count = Math.min(corridors.length, MAX_INSTANCES);
    for (let i = 0; i < count; i++) {
      const tile = corridors[i]!;
      _position.set(tile.tileX + 0.5, 0.06, tile.tileZ + 0.5);
      _quaternion.setFromEuler(_euler);
      _matrix.compose(_position, _quaternion, _scale);
      mesh.setMatrixAt(i, _matrix);
      mesh.setColorAt(i, frictionToColor(tile.friction));
    }

    mesh.count = count;
    mesh.instanceMatrix.needsUpdate = true;
    if (mesh.instanceColor) mesh.instanceColor.needsUpdate = true;
  }, [corridors, visible]);

  if (!visible || corridors.length === 0) return null;

  return (
    <instancedMesh
      ref={meshRef}
      args={[undefined, undefined, MAX_INSTANCES]}
      frustumCulled={false}
    >
      <planeGeometry args={[1, 1]} />
      <meshBasicMaterial
        transparent
        opacity={0.5}
        depthWrite={false}
        vertexColors
      />
    </instancedMesh>
  );
}
