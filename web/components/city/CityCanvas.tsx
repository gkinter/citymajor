"use client";

import { Canvas } from "@react-three/fiber";
import { Suspense, useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { FpsStats, PickResult, CityData } from "@/lib/types";
import { cityDataFromSnapshot, getCityData } from "@/lib/city-data";
import { createChunkStates } from "@/lib/chunks";
import { MAX_DPR } from "@/lib/constants";
import { createSimBridge, type SimBridge } from "@/lib/sim-bridge";
import type { ZoningTool, ZoneTile } from "@/lib/zoning";
import { ENGINE_ZONE_TYPE } from "@/lib/zoning";
import { CityScene } from "./CityScene";
import { AdaptiveDpr } from "./AdaptiveDpr";

type CityCanvasProps = {
  activeTool: ZoningTool;
  onStats: (stats: FpsStats) => void;
};

export function CityCanvas({ activeTool, onStats }: CityCanvasProps) {
  const [city, setCity] = useState<CityData>(() => getCityData());
  const [zones, setZones] = useState<ZoneTile[]>([]);
  const [simSource, setSimSource] = useState<"wasm" | "procedural">(
    "procedural",
  );
  const chunks = useMemo(() => createChunkStates(), []);
  const [dpr, setDpr] = useState(
    () => Math.min(MAX_DPR, typeof window !== "undefined" ? window.devicePixelRatio : 1),
  );
  const [pickedTile, setPickedTile] = useState<PickResult>(null);
  const bridgeRef = useRef<SimBridge | null>(null);
  const latestStats = useRef<FpsStats>({
    fps: 0,
    dpr,
    visibleChunks: 0,
    visibleBuildings: 0,
    totalBuildings: city.buildings.length,
    lodCounts: [0, 0, 0, 0],
    pickedTile: null,
  });

  useEffect(() => {
    const bridge = createSimBridge();
    bridgeRef.current = bridge;
    let cancelled = false;
    let raf = 0;
    let last = performance.now();

    const startTickLoop = () => {
      const loop = (now: number) => {
        if (cancelled) return;
        const delta = now - last;
        last = now;
        bridge.send({ type: "tick", deltaMs: delta });
        raf = requestAnimationFrame(loop);
      };
      raf = requestAnimationFrame(loop);
    };

    (async () => {
      try {
        await bridge.init("/dotnet", 256);
        if (cancelled) return;

        setSimSource("wasm");
        bridge.onSnapshot((snapshot) => {
          setCity(cityDataFromSnapshot(snapshot));
          if (snapshot.zones) setZones(snapshot.zones);
        });
        startTickLoop();
      } catch (err) {
        console.warn(
          "[CityMajor] WASM sim unavailable — using procedural city fallback",
          err,
        );
        if (!cancelled) {
          setSimSource("procedural");
          setCity(getCityData());
        }
      }
    })();

    return () => {
      cancelled = true;
      cancelAnimationFrame(raf);
      bridge.dispose();
      bridgeRef.current = null;
    };
  }, []);

  const handlePick = useCallback(
    (pick: PickResult) => {
      setPickedTile(pick);
      if (!pick || activeTool === "road") return;

      const bridge = bridgeRef.current;
      if (!bridge) return;

      if (activeTool === "bulldoze") {
        bridge.send({
          type: "bulldoze",
          tileX: pick.tileX,
          tileZ: pick.tileZ,
        });
        return;
      }

      bridge.send({
        type: "zone_paint",
        tileX: pick.tileX,
        tileZ: pick.tileZ,
        zoneType: ENGINE_ZONE_TYPE[activeTool],
      });
    },
    [activeTool],
  );

  useEffect(() => {
    latestStats.current = { ...latestStats.current, pickedTile, dpr, simSource };
    onStats(latestStats.current);
  }, [pickedTile, dpr, simSource, onStats]);

  const handleStats = (partial: FpsStats) => {
    latestStats.current = {
      ...partial,
      pickedTile,
      dpr,
      simSource,
      totalBuildings: city.buildings.length,
    };
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
          zones={zones}
          pickedTile={pickedTile}
          onPick={handlePick}
          onStats={handleStats}
          dpr={dpr}
        />
        <AdaptiveDpr dpr={dpr} onDprChange={setDpr} />
      </Suspense>
    </Canvas>
  );
}
