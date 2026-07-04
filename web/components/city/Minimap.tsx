"use client";

import { useEffect, useMemo, useRef } from "react";
import { CHUNKS_PER_AXIS } from "@/lib/constants";
import { resolveMinimapChunkColors } from "@/lib/minimap";
import type { CityData } from "@/lib/types";
import type { ZoneTile } from "@/lib/zoning";

const MINIMAP_PX = 160;
const MINIMAP_MARGIN = 12;

type MinimapProps = {
  zones: ZoneTile[];
  city: CityData;
};

/**
 * Bottom-left 2D canvas minimap — one swatch per 32×32 world chunk.
 * Uses Canvas2D (not WebGL) so the main play canvas keeps a single GL context.
 */
export function Minimap({ zones, city }: MinimapProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const chunkColors = useMemo(
    () => resolveMinimapChunkColors(zones, city),
    [zones, city],
  );

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const dpr = Math.min(2, window.devicePixelRatio || 1);
    const px = Math.round(MINIMAP_PX * dpr);
    canvas.width = px;
    canvas.height = px;

    const ctx = canvas.getContext("2d");
    if (!ctx) return;

    const cell = px / CHUNKS_PER_AXIS;
    ctx.clearRect(0, 0, px, px);

    for (let index = 0; index < chunkColors.length; index++) {
      const cx = index % CHUNKS_PER_AXIS;
      const cz = Math.floor(index / CHUNKS_PER_AXIS);
      ctx.fillStyle = chunkColors[index]!;
      ctx.fillRect(cx * cell, cz * cell, cell, cell);
    }
  }, [chunkColors]);

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
      <canvas
        ref={canvasRef}
        aria-hidden
        style={{ width: "100%", height: "100%", display: "block" }}
      />
    </div>
  );
}
