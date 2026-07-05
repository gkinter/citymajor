"use client";

import { useGLTF } from "@react-three/drei";
import {
  Component,
  Suspense,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import * as THREE from "three";
import {
  checkGltfAssetExists,
  resolveHeroGltfPath,
} from "@/lib/gltf-catalog";

const HERO_KEY = "hero_frontier_church";

/** Civic plaza tile — offset from era-gate monument at (128, 128). */
export const FRONTIER_CHURCH_TILE = {
  tileX: 132,
  tileZ: 124,
} as const;

const FOOTPRINT = 2;
const HEIGHT = 2.2;

type FrontierChurchLandmarkProps = {
  era: number;
};

/**
 * Optional frontier church landmark — renders Meshy hero GLB when shipped.
 * Stub path lives in `gltf-catalog`; nothing draws until the file exists.
 */
export function FrontierChurchLandmark({ era }: FrontierChurchLandmarkProps) {
  const gltfPath = resolveHeroGltfPath(HERO_KEY);
  const [gltfAvailable, setGltfAvailable] = useState(false);

  useEffect(() => {
    if (era !== 0 || !gltfPath) {
      setGltfAvailable(false);
      return;
    }

    let cancelled = false;
    void checkGltfAssetExists(gltfPath).then((exists) => {
      if (!cancelled) setGltfAvailable(exists);
    });

    return () => {
      cancelled = true;
    };
  }, [era, gltfPath]);

  if (era !== 0 || !gltfAvailable || !gltfPath) return null;

  const { tileX, tileZ } = FRONTIER_CHURCH_TILE;
  const centerX = tileX + FOOTPRINT / 2;
  const centerZ = tileZ + FOOTPRINT / 2;

  return (
    <group
      name="frontier-church-landmark"
      position={[centerX, 0, centerZ]}
      userData={{ era: 0, landmark: "Frontier Church" }}
    >
      <LandmarkGltfBoundary fallback={null}>
        <Suspense fallback={null}>
          <HeroGltfModel path={gltfPath} footprint={FOOTPRINT} height={HEIGHT} />
        </Suspense>
      </LandmarkGltfBoundary>
    </group>
  );
}

type HeroGltfModelProps = {
  path: string;
  footprint: number;
  height: number;
};

function HeroGltfModel({ path, footprint, height }: HeroGltfModelProps) {
  const { scene } = useGLTF(path);

  const { clone, scale, yOffset } = useMemo(() => {
    const cloned = scene.clone(true);
    cloned.updateMatrixWorld(true);

    const box = new THREE.Box3().setFromObject(cloned);
    const size = new THREE.Vector3();
    box.getSize(size);

    const targetFootprint = footprint * 0.95;
    const sx = targetFootprint / Math.max(size.x, 0.001);
    const sz = targetFootprint / Math.max(size.z, 0.001);
    const sy = height / Math.max(size.y, 0.001);
    const minY = box.min.y;

    return {
      clone: cloned,
      scale: [sx, sy, sz] as [number, number, number],
      yOffset: -minY * sy,
    };
  }, [scene, footprint, height]);

  return (
    <group position={[0, yOffset, 0]} scale={scale}>
      <primitive object={clone} castShadow receiveShadow />
    </group>
  );
}

type BoundaryProps = {
  children: ReactNode;
  fallback: ReactNode;
};

type BoundaryState = { hasError: boolean };

class LandmarkGltfBoundary extends Component<BoundaryProps, BoundaryState> {
  state: BoundaryState = { hasError: false };

  static getDerivedStateFromError(): BoundaryState {
    return { hasError: true };
  }

  render() {
    return this.state.hasError ? this.props.fallback : this.props.children;
  }
}
