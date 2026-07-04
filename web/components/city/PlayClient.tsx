"use client";

import { useRef, useState } from "react";
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

  const statsRef = useRef(stats);
  statsRef.current = stats;

  return (
    <div style={{ width: "100vw", height: "100vh", position: "relative" }}>
      <CityCanvas onStats={setStats} />
      <FpsHud stats={stats} totalBuildings={stats.totalBuildings} />
    </div>
  );
}
