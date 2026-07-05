"use client";

import { useFrame } from "@react-three/fiber";
import { useEffect, useRef } from "react";
import {
  FPS_DEGRADE_DURATION_MS,
  FPS_DEGRADE_THRESHOLD,
  MAX_DPR,
  MIN_DPR,
} from "@/lib/constants";
import type { AdaptiveDprDebugInfo, AdaptiveDprStatus } from "@/lib/types";

type AdaptiveDprProps = {
  dpr: number;
  onDprChange: (dpr: number) => void;
  /** When true, emit HUD telemetry and console logs on DPR transitions. */
  perfDebug?: boolean;
  onDebugUpdate?: (info: AdaptiveDprDebugInfo) => void;
};

/**
 * Dynamic pixel-ratio downscale when FPS stays below 30 for 2 seconds.
 * Mirrors BuildCity dynamic resolution pattern (R3F PerformanceMonitor-style).
 */
export function AdaptiveDpr({
  dpr,
  onDprChange,
  perfDebug = false,
  onDebugUpdate,
}: AdaptiveDprProps) {
  const lowFpsSince = useRef<number | null>(null);
  const frameTimes = useRef<number[]>([]);
  const lastEvent = useRef<"degrade" | "recover" | undefined>(undefined);
  const baseDpr = useRef(
    typeof window !== "undefined"
      ? Math.min(MAX_DPR, window.devicePixelRatio)
      : 1,
  );

  useEffect(() => {
    baseDpr.current = Math.min(MAX_DPR, window.devicePixelRatio);
  }, []);

  useFrame((_, delta) => {
    frameTimes.current.push(delta);
    if (frameTimes.current.length > 120) frameTimes.current.shift();

    const avgDelta =
      frameTimes.current.reduce((a, b) => a + b, 0) / frameTimes.current.length;
    const fps = 1 / avgDelta;
    const now = performance.now();
    const lowFpsElapsedMs =
      lowFpsSince.current === null ? 0 : now - lowFpsSince.current;

    let status: AdaptiveDprStatus = "stable";
    if (fps < FPS_DEGRADE_THRESHOLD) {
      status = lowFpsElapsedMs >= FPS_DEGRADE_DURATION_MS ? "degraded" : "low-fps";
    } else if (fps > FPS_DEGRADE_THRESHOLD + 8 && dpr < baseDpr.current) {
      status = "recovering";
    } else if (dpr < baseDpr.current - 0.01) {
      status = "degraded";
    }

    if (fps < FPS_DEGRADE_THRESHOLD) {
      if (lowFpsSince.current === null) lowFpsSince.current = now;
      else if (
        now - lowFpsSince.current >= FPS_DEGRADE_DURATION_MS &&
        dpr > MIN_DPR
      ) {
        const next = Math.max(MIN_DPR, Math.round(dpr * 0.75 * 100) / 100);
        if (next < dpr) {
          lastEvent.current = "degrade";
          if (perfDebug) {
            console.info(
              `[AdaptiveDpr] degrade ${dpr.toFixed(2)} → ${next.toFixed(2)} (${Math.round(fps)} FPS for ${(lowFpsElapsedMs / 1000).toFixed(1)}s)`,
            );
          }
          onDprChange(next);
          lowFpsSince.current = now;
        }
      }
    } else if (fps > FPS_DEGRADE_THRESHOLD + 8) {
      lowFpsSince.current = null;
      const target = Math.min(baseDpr.current, MAX_DPR);
      if (dpr < target) {
        const next = Math.min(target, Math.round((dpr + 0.1) * 100) / 100);
        if (next > dpr) {
          lastEvent.current = "recover";
          if (perfDebug) {
            console.info(
              `[AdaptiveDpr] recover ${dpr.toFixed(2)} → ${next.toFixed(2)} (${Math.round(fps)} FPS)`,
            );
          }
          onDprChange(next);
        }
      }
    }

    if (perfDebug && onDebugUpdate) {
      onDebugUpdate({
        status,
        sampledFps: Math.round(fps),
        baseDpr: baseDpr.current,
        lowFpsElapsedMs,
        lastEvent: lastEvent.current,
      });
    }
  });

  return null;
}
