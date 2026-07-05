"use client";

import { useEffect, useRef } from "react";
import { CHUNKS_PER_AXIS } from "@/lib/constants";
import {
  HUD_COLORS,
  HUD_MINIMAP,
  hudMinimapShell,
} from "@/lib/hud-theme";

const PLACEHOLDER_FILL = "#0c1018";

/**
 * Bottom-left minimap shell — static chunk grid until live data + camera sync land.
 *
 * TODO(camera-sync): Subscribe to CityCanvas OrbitControls (target + distance) and
 *   draw a viewport rectangle on this canvas that tracks pan/zoom in world space.
 */
export function MinimapPanel() {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const dpr = Math.min(2, window.devicePixelRatio || 1);
    const px = Math.round(HUD_MINIMAP.sizePx * dpr);
    canvas.width = px;
    canvas.height = px;

    const ctx = canvas.getContext("2d");
    if (!ctx) return;

    ctx.fillStyle = PLACEHOLDER_FILL;
    ctx.fillRect(0, 0, px, px);

    const cell = px / CHUNKS_PER_AXIS;
    ctx.strokeStyle = HUD_COLORS.borderSubtle;
    ctx.lineWidth = Math.max(1, dpr * 0.5);

    for (let i = 0; i <= CHUNKS_PER_AXIS; i++) {
      const offset = i * cell;
      ctx.beginPath();
      ctx.moveTo(offset, 0);
      ctx.lineTo(offset, px);
      ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(0, offset);
      ctx.lineTo(px, offset);
      ctx.stroke();
    }

    // Static viewport hint — replaced once camera-sync TODO is implemented.
    ctx.strokeStyle = HUD_COLORS.accentBorder;
    ctx.lineWidth = Math.max(1.5, dpr);
    const inset = cell * 2;
    ctx.strokeRect(inset, inset, px - inset * 2, px - inset * 2);
  }, []);

  return (
    <div aria-label="City minimap" style={hudMinimapShell()}>
      <canvas
        ref={canvasRef}
        aria-hidden
        style={{ width: "100%", height: "100%", display: "block" }}
      />
    </div>
  );
}
