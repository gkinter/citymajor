"use client";

import { useLayoutEffect, useRef } from "react";
import * as THREE from "three";
import type { TrafficTile } from "@/lib/zoning";

type TrafficOverlayProps = {
  traffic: TrafficTile[];
  /** When false, overlay is not rendered. */
  visible?: boolean;
};

const _color = new THREE.Color();
const _matrix = new THREE.Matrix4();
const _position = new THREE.Vector3();
const _quaternion = new THREE.Quaternion();
const _scale = new THREE.Vector3(0.9, 0.9, 1);
const _euler = new THREE.Euler(-Math.PI / 2, 0, 0);

const MAX_INSTANCES = 65536;

/** Green (free flow) → amber → red (gridlock) by congestion density. */
function densityToColor(density: number): THREE.Color {
  const t = Math.min(1, Math.max(0, density));
  return _color.setHSL(0.33 * (1 - t), 0.9, 0.48);
}

/**
 * Semi-transparent congestion heatmap on road tiles — sits above RoadOverlay.
 */
export function TrafficOverlay({ traffic, visible = true }: TrafficOverlayProps) {
  const meshRef = useRef<THREE.InstancedMesh>(null);

  useLayoutEffect(() => {
    const mesh = meshRef.current;
    if (!mesh || !visible) return;

    const count = Math.min(traffic.length, MAX_INSTANCES);
    for (let i = 0; i < count; i++) {
      const tile = traffic[i]!;
      _position.set(tile.tileX + 0.5, 0.05, tile.tileZ + 0.5);
      _quaternion.setFromEuler(_euler);
      _matrix.compose(_position, _quaternion, _scale);
      mesh.setMatrixAt(i, _matrix);
      mesh.setColorAt(i, densityToColor(tile.density));
    }

    mesh.count = count;
    mesh.instanceMatrix.needsUpdate = true;
    if (mesh.instanceColor) mesh.instanceColor.needsUpdate = true;
  }, [traffic, visible]);

  if (!visible || traffic.length === 0) return null;

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
