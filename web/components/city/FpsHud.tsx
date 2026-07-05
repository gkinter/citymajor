"use client";

import type { CSSProperties } from "react";
import { FPS_DEGRADE_THRESHOLD } from "@/lib/constants";
import {
  HUD_COLORS,
  HUD_ZONE,
  hudInfoPanel,
  hudLabel,
} from "@/lib/hud-theme";
import type { FpsStats } from "@/lib/types";
import type { ZoningTool } from "@/lib/zoning";

type FpsTone = "good" | "warn" | "bad";

const FPS_GOOD_THRESHOLD = 55;

const FPS_TONE_COLOR: Record<FpsTone, string> = {
  good: "#7dffb2",
  warn: "#e8d44a",
  bad: HUD_COLORS.error,
};

function fpsTone(fps: number): FpsTone | null {
  if (fps <= 0) return null;
  if (fps >= FPS_GOOD_THRESHOLD) return "good";
  if (fps >= FPS_DEGRADE_THRESHOLD) return "warn";
  return "bad";
}

type FpsHudProps = {
  stats: FpsStats;
  totalBuildings: number;
  activeTool?: ZoningTool;
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

export function FpsHud({ stats, totalBuildings, activeTool }: FpsHudProps) {
  const simLabel = stats.simSource === "wasm" ? "WASM sim" : "procedural";
  const fpsToneKey = fpsTone(stats.fps);
  const fpsDisplay = stats.fps > 0 ? String(stats.fps) : "—";
  const showCoverage =
    stats.healthcareCoverage !== undefined ||
    stats.policeCoverage !== undefined ||
    stats.fireCoverage !== undefined;

  return (
    <div style={{ ...HUD_ZONE.bottomLeft, ...hudInfoPanel() }}>
      <div style={sectionTitle}>Diagnostics</div>
      <div>Data: {simLabel}</div>
      <div>
        FPS:{" "}
        <span
          style={{
            fontWeight: 600,
            fontVariantNumeric: "tabular-nums",
            color: fpsToneKey ? FPS_TONE_COLOR[fpsToneKey] : undefined,
          }}
          title={
            fpsToneKey === "good"
              ? "≥55 FPS — smooth"
              : fpsToneKey === "warn"
                ? `30–54 FPS — integrated GPU range (degrades below ${FPS_DEGRADE_THRESHOLD})`
                : fpsToneKey === "bad"
                  ? `<${FPS_DEGRADE_THRESHOLD} FPS — AdaptiveDpr may reduce resolution`
                  : "Frame rate unavailable"
          }
        >
          {fpsDisplay}
        </span>
      </div>
      <div>DPR: {stats.dpr.toFixed(2)}</div>
      <div>
        Chunks: {stats.visibleChunks}/64 · Buildings: {stats.visibleBuildings}/
        {totalBuildings}
      </div>
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
