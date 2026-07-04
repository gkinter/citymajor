"use client";

import { useEffect, useRef, useState } from "react";
import type { Entitlements } from "@/lib/entitlements";
import type { FpsStats } from "@/lib/types";
import { FpsHud } from "@/components/city/FpsHud";
import { CityCanvas } from "@/components/city/CityCanvas";

export function PlayClient() {
  const [stats, setStats] = useState<FpsStats>({
    fps: 0,
    dpr: 1,
    visibleChunks: 0,
    visibleBuildings: 0,
    totalBuildings: 0,
    lodCounts: [0, 0, 0, 0],
    pickedTile: null,
  });
  const [entitlements, setEntitlements] = useState<Entitlements | null>(null);
  const [entitlementsError, setEntitlementsError] = useState<string | null>(null);

  const statsRef = useRef(stats);
  statsRef.current = stats;

  useEffect(() => {
    let cancelled = false;

    async function loadEntitlements() {
      try {
        const res = await fetch("/api/me/entitlements", { credentials: "include" });
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const data = (await res.json()) as Entitlements;
        if (!cancelled) {
          setEntitlements(data);
          setEntitlementsError(null);
        }
      } catch (err) {
        if (!cancelled) {
          setEntitlementsError(err instanceof Error ? err.message : "Failed to load entitlements");
        }
      }
    }

    void loadEntitlements();
    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <div style={{ width: "100vw", height: "100vh", position: "relative" }}>
      <CityCanvas onStats={setStats} />
      <FpsHud stats={stats} totalBuildings={stats.totalBuildings} />
      <div
        style={{
          position: "absolute",
          top: 12,
          right: 12,
          padding: "8px 12px",
          background: "rgba(0,0,0,0.65)",
          color: "#e8e8e8",
          fontFamily: "monospace",
          fontSize: 12,
          borderRadius: 6,
          pointerEvents: "none",
          lineHeight: 1.4,
        }}
      >
        {entitlements ? (
          <>
            <div>tier: {entitlements.tier}</div>
            <div>saves: {entitlements.maxSaveSlots} max</div>
            <div>
              narrative/day:{" "}
              {entitlements.maxNarrativeEventsPerDay === Number.MAX_SAFE_INTEGER
                ? "∞"
                : entitlements.maxNarrativeEventsPerDay}
            </div>
          </>
        ) : entitlementsError ? (
          <div>entitlements: {entitlementsError}</div>
        ) : (
          <div>entitlements: loading…</div>
        )}
      </div>
    </div>
  );
}
