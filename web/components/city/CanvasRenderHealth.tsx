"use client";

import { useFrame, useThree } from "@react-three/fiber";
import { useEffect, useRef } from "react";
import type { FpsStats } from "@/lib/types";

const BUILDING_WATCHDOG_MS = 5_000;
const BLACK_FRAME_STREAK_LIMIT = 4;
const PIXEL_PROBE_INTERVAL_FRAMES = 15;

type CanvasRenderHealthProps = {
  stats: Pick<FpsStats, "visibleBuildings" | "visibleChunks" | "totalBuildings">;
  postProcessingActive: boolean;
  onDisablePostProcessing: () => void;
  onRenderFallback: (reason: string) => void;
};

/**
 * Production guardrails for /play WebGL:
 * - Detects consecutive black default-framebuffer samples (EffectComposer symptom) and
 *   disables post-processing so readPixels / display recover.
 * - After 5s with zero visible buildings, triggers procedural city fallback.
 */
export function CanvasRenderHealth({
  stats,
  postProcessingActive,
  onDisablePostProcessing,
  onRenderFallback,
}: CanvasRenderHealthProps) {
  const { gl } = useThree();
  const mountMs = useRef(performance.now());
  const frame = useRef(0);
  const blackStreak = useRef(0);
  const buildingWatchdogFired = useRef(false);
  const postProcessingDisabled = useRef(false);
  const pixelBuf = useRef(new Uint8Array(4));

  useEffect(() => {
    mountMs.current = performance.now();
    frame.current = 0;
    blackStreak.current = 0;
    buildingWatchdogFired.current = false;
    postProcessingDisabled.current = false;
  }, []);

  useFrame(() => {
    frame.current += 1;

    const elapsed = performance.now() - mountMs.current;
    if (
      !buildingWatchdogFired.current &&
      elapsed >= BUILDING_WATCHDOG_MS &&
      stats.totalBuildings > 0 &&
      stats.visibleBuildings === 0
    ) {
      buildingWatchdogFired.current = true;
      console.warn(
        "[CityMajor] Render watchdog: no visible buildings after 5s — switching to procedural city",
        {
          visibleChunks: stats.visibleChunks,
          totalBuildings: stats.totalBuildings,
        },
      );
      onRenderFallback("no-visible-buildings");
    }

    if (!postProcessingActive || postProcessingDisabled.current) return;
    if (frame.current % PIXEL_PROBE_INTERVAL_FRAMES !== 0) return;

    const canvas = gl.domElement;
    if (canvas.width < 2 || canvas.height < 2) return;

    const x = Math.max(0, Math.floor(canvas.width / 2) - 1);
    const y = Math.max(0, Math.floor(canvas.height / 2) - 1);
    const context = gl.getContext();
    if (!context) return;
    context.readPixels(x, y, 1, 1, context.RGBA, context.UNSIGNED_BYTE, pixelBuf.current);

    const sum =
      pixelBuf.current[0] + pixelBuf.current[1] + pixelBuf.current[2];
    if (sum === 0) {
      blackStreak.current += 1;
      if (blackStreak.current >= BLACK_FRAME_STREAK_LIMIT) {
        postProcessingDisabled.current = true;
        console.warn(
          "[CityMajor] Black framebuffer streak — disabling bloom post-processing",
          { streak: blackStreak.current, elapsedMs: Math.round(elapsed) },
        );
        onDisablePostProcessing();
      }
    } else {
      blackStreak.current = 0;
    }
  }, 2);

  return null;
}
