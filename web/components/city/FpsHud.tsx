"use client";

import type { FpsStats } from "@/lib/types";
import { getCityData } from "@/lib/city-data";
import { useMemo } from "react";

type FpsHudProps = {
  stats: FpsStats;
};

export function FpsHud({ stats }: FpsHudProps) {
  const totalBuildings = useMemo(() => getCityData().buildings.length, []);

  return (
    <div
      style={{
        position: "absolute",
        top: 12,
        left: 12,
        padding: "10px 14px",
        fontFamily: "ui-monospace, SFMono-Regular, Menlo, monospace",
        fontSize: 12,
        lineHeight: 1.5,
        color: "#e8eef8",
        background: "rgba(8, 12, 24, 0.82)",
        border: "1px solid rgba(120, 160, 220, 0.25)",
        borderRadius: 8,
        pointerEvents: "none",
        minWidth: 220,
      }}
    >
      <div style={{ fontWeight: 700, marginBottom: 4 }}>CityMajor Web M0</div>
      <div>FPS: {stats.fps || "—"}</div>
      <div>DPR: {stats.dpr.toFixed(2)}</div>
      <div>
        Chunks: {stats.visibleChunks}/64 · Buildings: {stats.visibleBuildings}/
        {totalBuildings}
      </div>
      <div>
        LOD L0–L3: {stats.lodCounts.join(" / ")}
      </div>
      {stats.pickedTile ? (
        <div style={{ marginTop: 6, color: "#9fd4ff" }}>
          Picked tile ({stats.pickedTile.tileX}, {stats.pickedTile.tileZ}) · chunk{" "}
          {stats.pickedTile.chunkIndex}
        </div>
      ) : (
        <div style={{ marginTop: 6, opacity: 0.6 }}>Click terrain to pick tile</div>
      )}
    </div>
  );
}
