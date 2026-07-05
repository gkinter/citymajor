"use client";

import type { CSSProperties } from "react";
import {
  HUD_COLORS,
  HUD_ZONE,
  hudInfoPanel,
  hudLabel,
} from "@/lib/hud-theme";
import { CHUNK_COUNT } from "@/lib/constants";
import type { FpsStats } from "@/lib/types";
import type { ZoningTool } from "@/lib/zoning";

type FpsHudProps = {
  stats: FpsStats;
  totalBuildings: number;
  activeTool?: ZoningTool;
  /** When true (`?debug=chunks`), show cumulative loaded chunk count. */
  showChunkDebug?: boolean;
};

const sectionTitle: CSSProperties = {
  fontWeight: 700,
  marginBottom: 4,
  fontSize: 11,
  letterSpacing: "0.06em",
  textTransform: "uppercase",
  color: HUD_COLORS.textMuted,
};

function formatCoveragePct(value: number | undefined): string {
  if (value === undefined) return "—";
  return `${Math.round(value * 100)}%`;
}

export function FpsHud({
  stats,
  totalBuildings,
  activeTool,
  showChunkDebug = false,
}: FpsHudProps) {
  const simLabel = stats.simSource === "wasm" ? "WASM sim" : "procedural";
  const showCoverage =
    stats.healthcareCoverage !== undefined ||
    stats.policeCoverage !== undefined ||
    stats.fireCoverage !== undefined;

  return (
    <div style={{ ...HUD_ZONE.bottomLeft, ...hudInfoPanel() }}>
      <div style={sectionTitle}>Diagnostics</div>
      <div>Data: {simLabel}</div>
      <div>FPS: {stats.fps || "—"}</div>
      <div>DPR: {stats.dpr.toFixed(2)}</div>
      <div>
        Chunks: {stats.visibleChunks}/{CHUNK_COUNT} · Buildings:{" "}
        {stats.visibleBuildings}/{totalBuildings}
      </div>
      {showChunkDebug ? (
        <div style={{ color: HUD_COLORS.accentHighlight }}>
          Loaded chunks: {stats.loadedChunks ?? 0}/{CHUNK_COUNT}
        </div>
      ) : null}
      <div>LOD L0–L3: {stats.lodCounts.join(" / ")}</div>
      {showCoverage ? (
        <div
          style={{
            marginTop: 4,
            fontFamily: "ui-monospace, monospace",
            fontSize: 11,
            color: HUD_COLORS.textDim,
          }}
          title="City-wide mean coverage over zoned tiles"
        >
          Coverage: H {formatCoveragePct(stats.healthcareCoverage)} · P{" "}
          {formatCoveragePct(stats.policeCoverage)} · F{" "}
          {formatCoveragePct(stats.fireCoverage)}
        </div>
      ) : null}
      {activeTool ? (
        <div style={{ marginTop: 4, opacity: 0.85 }}>
          <span style={hudLabel()}>Tool</span>
          {activeTool}
        </div>
      ) : null}
      {stats.pickedTile ? (
        <div style={{ marginTop: 6, color: HUD_COLORS.accentHighlight }}>
          Picked tile ({stats.pickedTile.tileX}, {stats.pickedTile.tileZ}) · chunk{" "}
          {stats.pickedTile.chunkIndex}
        </div>
      ) : (
        <div style={{ marginTop: 6, opacity: 0.6 }}>Click terrain to pick tile</div>
      )}
    </div>
  );
}
