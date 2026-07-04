"use client";

import { Canvas } from "@react-three/fiber";
import { useMemo } from "react";
import { CHUNKS_PER_AXIS } from "@/lib/constants";
import { resolveMinimapChunkColors } from "@/lib/minimap";
import type { CityData } from "@/lib/types";
import type { ZoneTile } from "@/lib/zoning";
import { MinimapScene } from "./MinimapScene";

const MINIMAP_PX = 160;
const MINIMAP_MARGIN = 12;

type MinimapProps = {
  zones: ZoneTile[];
  city: CityData;
};

/**
 * Bottom-left orthographic R3F minimap — one swatch per 32×32 world chunk.
 */
export function Minimap({ zones, city }: MinimapProps) {
  const chunkColors = useMemo(
    () => resolveMinimapChunkColors(zones, city),
    [zones, city],
  );

  return (
    <div
      aria-label="City zone minimap"
      style={{
        position: "absolute",
        left: MINIMAP_MARGIN,
        bottom: MINIMAP_MARGIN,
        width: MINIMAP_PX,
        height: MINIMAP_PX,
        borderRadius: 8,
        overflow: "hidden",
        border: "1px solid rgba(120, 160, 220, 0.35)",
        background: "rgba(8, 12, 24, 0.88)",
        boxShadow: "0 4px 16px rgba(0, 0, 0, 0.35)",
        pointerEvents: "none",
        zIndex: 9,
      }}
    >
      <Canvas
        orthographic
        dpr={[1, 2]}
        gl={{ antialias: false, alpha: true }}
        style={{ width: "100%", height: "100%" }}
        camera={{
          position: [CHUNKS_PER_AXIS / 2, 10, CHUNKS_PER_AXIS / 2],
          rotation: [-Math.PI / 2, 0, 0],
          zoom: 18,
          near: 0.1,
          far: 50,
        }}
      >
        <MinimapScene chunkColors={chunkColors} />
      </Canvas>
    </div>
  );
}
