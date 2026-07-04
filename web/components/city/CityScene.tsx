"use client";

import { OrbitControls } from "@react-three/drei";
import { useFrame, useThree } from "@react-three/fiber";
import { useMemo, useRef } from "react";
import * as THREE from "three";
import type { ChunkState, CityData, FpsStats, PickResult } from "@/lib/types";
import {
  updateChunkLod,
  updateChunkVisibility,
  visibleBuildingCount,
} from "@/lib/chunks";
import { BuildingInstances } from "./BuildingInstances";
import { TerrainChunks } from "./TerrainChunks";
import { TilePicker } from "./TilePicker";

type CitySceneProps = {
  city: CityData;
  chunks: ChunkState[];
  pickedTile: PickResult;
  onPick: (pick: PickResult) => void;
  onStats: (stats: FpsStats) => void;
  dpr: number;
};

export function CityScene({
  city,
  chunks,
  pickedTile,
  onPick,
  onStats,
  dpr,
}: CitySceneProps) {
  const { camera } = useThree();
  const fpsAccum = useRef({ frames: 0, last: performance.now(), fps: 0 });
  const statsSnapshot = useRef<Partial<FpsStats>>({});

  const dirLight = useMemo(() => new THREE.DirectionalLight("#fff5e6", 1.1), []);
  dirLight.position.set(80, 120, 40);

  useFrame(() => {
    const now = performance.now();
    fpsAccum.current.frames += 1;
    const elapsed = now - fpsAccum.current.last;
    if (elapsed >= 500) {
      fpsAccum.current.fps = (fpsAccum.current.frames * 1000) / elapsed;
      fpsAccum.current.frames = 0;
      fpsAccum.current.last = now;

      const visibleChunks = updateChunkVisibility(chunks, camera);
      const lodCounts = updateChunkLod(chunks, camera.position);
      const visibleBuildings = visibleBuildingCount(
        chunks,
        city.chunkBuildingIndices,
      );

      statsSnapshot.current = {
        fps: Math.round(fpsAccum.current.fps),
        dpr,
        visibleChunks,
        visibleBuildings,
        lodCounts,
        pickedTile,
      };
      onStats(statsSnapshot.current as FpsStats);
    }
  });

  return (
    <>
      <ambientLight intensity={0.35} />
      <primitive object={dirLight} />
      <hemisphereLight args={["#8ec5ff", "#1a2030", 0.4]} />
      <OrbitControls
        makeDefault
        enableDamping
        dampingFactor={0.08}
        maxPolarAngle={Math.PI / 2.1}
        minDistance={20}
        maxDistance={400}
        target={[GRID_CENTER, 0, GRID_CENTER]}
      />
      <TerrainChunks chunks={chunks} pickedTile={pickedTile} />
      <BuildingInstances city={city} chunks={chunks} />
      <TilePicker onPick={onPick} />
    </>
  );
}

const GRID_CENTER = 128;
