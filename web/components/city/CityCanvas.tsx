"use client";

import { Canvas } from "@react-three/fiber";
import { Suspense, useEffect, useMemo, useRef, useState } from "react";
import type { FpsStats, PickResult } from "@/lib/types";
import { getCityData } from "@/lib/city-data";
import { createChunkStates } from "@/lib/chunks";
import { MAX_DPR } from "@/lib/constants";
import { CityScene } from "./CityScene";
import { AdaptiveDpr } from "./AdaptiveDpr";

type CityCanvasProps = {
  onStats: (stats: FpsStats) => void;
};

export function CityCanvas({ onStats }: CityCanvasProps) {
  const city = useMemo(() => getCityData(), []);
  const chunks = useMemo(() => createChunkStates(), []);
  const [dpr, setDpr] = useState(
    () => Math.min(MAX_DPR, typeof window !== "undefined" ? window.devicePixelRatio : 1),
  );
  const [pickedTile, setPickedTile] = useState<PickResult>(null);
  const latestStats = useRef<FpsStats>({
    fps: 0,
    dpr,
    visibleChunks: 0,
    visibleBuildings: 0,
    lodCounts: [0, 0, 0, 0],
    pickedTile: null,
  });

  useEffect(() => {
    latestStats.current = { ...latestStats.current, pickedTile, dpr };
    onStats(latestStats.current);
  }, [pickedTile, dpr, onStats]);

  const handleStats = (partial: FpsStats) => {
    latestStats.current = { ...partial, pickedTile, dpr };
    onStats(latestStats.current);
  };

  return (
    <Canvas
      dpr={dpr}
      camera={{ position: [140, 120, 140], fov: 50, near: 0.1, far: 800 }}
      gl={{ antialias: true, powerPreference: "high-performance" }}
      style={{ width: "100%", height: "100%" }}
    >
      <color attach="background" args={["#0b1020"]} />
      <Suspense fallback={null}>
        <CityScene
          city={city}
          chunks={chunks}
          pickedTile={pickedTile}
          onPick={setPickedTile}
          onStats={handleStats}
          dpr={dpr}
        />
        <AdaptiveDpr dpr={dpr} onDprChange={setDpr} />
      </Suspense>
    </Canvas>
  );
}
