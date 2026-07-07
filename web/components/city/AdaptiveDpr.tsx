"use client";

import { useFrame } from "@react-three/fiber";
import { useEffect, useRef } from "react";
import {
  FPS_DEGRADE_DURATION_MS,
  FPS_DEGRADE_THRESHOLD,
  MAX_DPR,
  MIN_DPR,
} from "@/lib/constants";

type AdaptiveDprProps = {
  dpr: number;
  onDprChange: (dpr: number) => void;
};

/**
 * Dynamic pixel-ratio downscale when FPS stays below 30 for 2 seconds.
 * Mirrors BuildCity dynamic resolution pattern (R3F PerformanceMonitor-style).
 */
export function AdaptiveDpr({ dpr, onDprChange }: AdaptiveDprProps) {
  const lowFpsSince = useRef<number | null>(null);
  const frameTimes = useRef<number[]>([]);
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

    if (fps < FPS_DEGRADE_THRESHOLD) {
      if (lowFpsSince.current === null) lowFpsSince.current = now;
      else if (
        now - lowFpsSince.current >= FPS_DEGRADE_DURATION_MS &&
        dpr > MIN_DPR
      ) {
        const next = Math.max(MIN_DPR, Math.round(dpr * 0.75 * 100) / 100);
        if (next < dpr) {
          onDprChange(next);
          lowFpsSince.current = now;
        }
      }
    } else if (fps > FPS_DEGRADE_THRESHOLD + 8) {
      lowFpsSince.current = null;
      const target = Math.min(baseDpr.current, MAX_DPR);
      if (dpr < target) {
        onDprChange(Math.min(target, Math.round((dpr + 0.1) * 100) / 100));
      }
    }
  });

  return null;
}
