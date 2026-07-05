"use client";

import { useGLTF, Billboard, Html } from "@react-three/drei";
import { useFrame } from "@react-three/fiber";
import {
  Component,
  Suspense,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import type { CSSProperties } from "react";
import * as THREE from "three";
import {
  ERA_LANDMARK_TILE,
  checkEraLandmarkGltfExists,
  eraLandmarkGltfPath,
  eraLandmarkSpec,
  isEraGateReady,
  type EraLandmarkSpec,
} from "@/lib/era-landmarks";
import type { EraProgress } from "@/lib/sim-bridge";

type EraLandmarkPlaceholderProps = {
  era: number;
  eraProgress?: EraProgress;
};

/**
 * Era-gate monument — Meshy hero GLB when shipped, procedural box fallback (SB-3719 / SB-3680).
 */
export function EraLandmarkPlaceholder({
  era,
  eraProgress,
}: EraLandmarkPlaceholderProps) {
  const ready = isEraGateReady(eraProgress);
  const spec = useMemo(
    () => (ready && eraProgress ? eraLandmarkSpec(eraProgress.nextEra) : null),
    [ready, eraProgress],
  );

  if (!ready || !spec || !eraProgress) return null;

  const { tileX, tileZ } = ERA_LANDMARK_TILE;
  const half = spec.footprint / 2;
  const centerX = tileX + half;
  const centerZ = tileZ + half;

  return (
    <EraLandmarkMonument
      centerX={centerX}
      centerZ={centerZ}
      spec={spec}
      nextEra={eraProgress.nextEra}
      questTitle={spec.questTitle}
      nextEraName={eraProgress.nextEraName}
      currentEra={era}
    />
  );
}

type MonumentProps = {
  centerX: number;
  centerZ: number;
  spec: EraLandmarkSpec;
  nextEra: number;
  questTitle: string | null;
  nextEraName: string;
  currentEra: number;
};

function EraLandmarkMonument({
  centerX,
  centerZ,
  spec,
  nextEra,
  questTitle,
  nextEraName,
  currentEra,
}: MonumentProps) {
  const glowRef = useRef<THREE.Mesh>(null);
  const baseColor = spec.accentColor;
  const gltfPath = eraLandmarkGltfPath(nextEra);
  const [gltfAvailable, setGltfAvailable] = useState(false);

  useEffect(() => {
    if (!gltfPath) {
      setGltfAvailable(false);
      return;
    }

    let cancelled = false;
    void checkEraLandmarkGltfExists(gltfPath).then((exists) => {
      if (!cancelled) setGltfAvailable(exists);
    });

    return () => {
      cancelled = true;
    };
  }, [gltfPath]);

  useFrame(({ clock }) => {
    const glow = glowRef.current;
    if (!glow) return;
    const pulse = 1 + 0.06 * Math.sin(clock.elapsedTime * 2.4);
    glow.scale.set(pulse, 1, pulse);
    const material = glow.material as THREE.MeshStandardMaterial;
    material.emissiveIntensity = 0.15 + 0.1 * Math.sin(clock.elapsedTime * 2.4);
  });

  const pedestalH = 0.4;
  const shaftH = spec.height * 0.72;
  const capH = spec.height * 0.18;
  const fp = spec.footprint * 0.85;
  const labelY = pedestalH + shaftH + capH + 1.2;

  const labelStyle = {
    "--era-landmark-color": baseColor,
  } as CSSProperties;

  const boxFallback = (
    <EraLandmarkBoxMesh pedestalH={pedestalH} shaftH={shaftH} capH={capH} fp={fp} baseColor={baseColor} />
  );

  return (
    <group
      name="era-landmark-placeholder"
      position={[centerX, 0, centerZ]}
      userData={{ era: currentEra, landmark: spec.landmarkName }}
    >
      {gltfAvailable && gltfPath ? (
        <LandmarkGltfBoundary fallback={boxFallback}>
          <Suspense fallback={boxFallback}>
            <EraLandmarkGltfModel path={gltfPath} spec={spec} />
          </Suspense>
        </LandmarkGltfBoundary>
      ) : (
        boxFallback
      )}

      <mesh
        ref={glowRef}
        rotation={[-Math.PI / 2, 0, 0]}
        position={[0, 0.05, 0]}
      >
        <ringGeometry args={[fp * 0.55, fp * 0.85, 48]} />
        <meshStandardMaterial
          color={baseColor}
          emissive={baseColor}
          emissiveIntensity={0.2}
          transparent
          opacity={0.55}
          side={THREE.DoubleSide}
        />
      </mesh>

      <Billboard position={[0, labelY, 0]}>
        <Html center distanceFactor={90} zIndexRange={[35, 0]}>
          <div className="era-landmark" style={labelStyle}>
            <p className="era-landmark__eyebrow">Landmark site</p>
            <p className="era-landmark__title">{spec.landmarkName}</p>
            {questTitle ? (
              <p className="era-landmark__quest">{questTitle}</p>
            ) : null}
            <p className="era-landmark__era">{nextEraName} era</p>
          </div>
        </Html>
      </Billboard>
    </group>
  );
}

type BoxMeshProps = {
  pedestalH: number;
  shaftH: number;
  capH: number;
  fp: number;
  baseColor: string;
};

function EraLandmarkBoxMesh({ pedestalH, shaftH, capH, fp, baseColor }: BoxMeshProps) {
  return (
    <>
      <mesh position={[0, pedestalH / 2, 0]} castShadow receiveShadow>
        <boxGeometry args={[fp, pedestalH, fp]} />
        <meshStandardMaterial color="#4a5568" roughness={0.75} metalness={0.1} />
      </mesh>

      <mesh position={[0, pedestalH + shaftH / 2, 0]} castShadow receiveShadow>
        <boxGeometry args={[fp * 0.55, shaftH, fp * 0.55]} />
        <meshStandardMaterial
          color={baseColor}
          roughness={0.45}
          metalness={0.35}
          emissive={baseColor}
          emissiveIntensity={0.08}
        />
      </mesh>

      <mesh position={[0, pedestalH + shaftH + capH / 2, 0]} castShadow>
        <boxGeometry args={[fp * 0.7, capH, fp * 0.7]} />
        <meshStandardMaterial color="#e8eef8" roughness={0.35} metalness={0.5} />
      </mesh>
    </>
  );
}

type GltfModelProps = {
  path: string;
  spec: EraLandmarkSpec;
};

function EraLandmarkGltfModel({ path, spec }: GltfModelProps) {
  const { scene } = useGLTF(path);

  const { clone, scale, yOffset } = useMemo(() => {
    const cloned = scene.clone(true);
    cloned.updateMatrixWorld(true);

    const box = new THREE.Box3().setFromObject(cloned);
    const size = new THREE.Vector3();
    box.getSize(size);

    const targetFootprint = spec.footprint * 0.85;
    const sx = targetFootprint / Math.max(size.x, 0.001);
    const sz = targetFootprint / Math.max(size.z, 0.001);
    const sy = spec.height / Math.max(size.y, 0.001);
    const minY = box.min.y;

    return {
      clone: cloned,
      scale: [sx, sy, sz] as [number, number, number],
      yOffset: -minY * sy,
    };
  }, [scene, spec.footprint, spec.height]);

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
