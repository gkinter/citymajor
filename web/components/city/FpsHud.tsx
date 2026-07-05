"use client";

import type { CSSProperties } from "react";
import { FPS_DEGRADE_DURATION_MS, FPS_DEGRADE_THRESHOLD } from "@/lib/constants";
import {
  HUD_COLORS,
  HUD_ZONE,
  hudInfoPanel,
  hudLabel,
} from "@/lib/hud-theme";
import type { AdaptiveDprDebugInfo, FpsStats } from "@/lib/types";
import type { ZoningTool } from "@/lib/zoning";

type FpsHudProps = {
  stats: FpsStats;
  totalBuildings: number;
  activeTool?: ZoningTool;
  /** Show AdaptiveDpr telemetry lines (/play?debug=perf). */
  perfDebug?: boolean;
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

function formatAdaptiveDprLine(info: AdaptiveDprDebugInfo): string {
  const fpsPart = `${info.sampledFps} FPS`;
  switch (info.status) {
    case "low-fps":
      return `low FPS ${(info.lowFpsElapsedMs / 1000).toFixed(1)}s/${(FPS_DEGRADE_DURATION_MS / 1000).toFixed(0)}s @ ${fpsPart}`;
    case "degraded":
      return `degraded @ ${fpsPart}${info.lastEvent === "degrade" ? " (step-down)" : ""}`;
    case "recovering":
      return `recovering @ ${fpsPart} (target >${FPS_DEGRADE_THRESHOLD + 8} FPS)`;
    default:
      return `stable @ ${fpsPart}`;
  }
}

export function FpsHud({
  stats,
  totalBuildings,
  activeTool,
  perfDebug = false,
}: FpsHudProps) {
  const simLabel = stats.simSource === "wasm" ? "WASM sim" : "procedural";
  const showCoverage =
    stats.healthcareCoverage !== undefined ||
    stats.policeCoverage !== undefined ||
    stats.fireCoverage !== undefined;
  const dprAtBase =
    stats.adaptiveDpr !== undefined &&
    Math.abs(stats.dpr - stats.adaptiveDpr.baseDpr) < 0.01;

  return (
    <div style={{ ...HUD_ZONE.bottomLeft, ...hudInfoPanel() }}>
      <div style={sectionTitle}>Diagnostics</div>
      <div>Data: {simLabel}</div>
      <div>FPS: {stats.fps || "—"}</div>
      <div>
        DPR: {stats.dpr.toFixed(2)}
        {perfDebug && stats.adaptiveDpr ? (
          <span style={{ opacity: 0.75 }}>
            {" "}
            / {stats.adaptiveDpr.baseDpr.toFixed(2)} base
            {dprAtBase ? "" : " (adapted)"}
          </span>
        ) : null}
      </div>
      {perfDebug && stats.adaptiveDpr ? (
        <div
          style={{
            fontFamily: "ui-monospace, monospace",
            fontSize: 11,
            color:
              stats.adaptiveDpr.status === "degraded" ||
              stats.adaptiveDpr.status === "low-fps"
                ? HUD_COLORS.error
                : stats.adaptiveDpr.status === "recovering"
                  ? "#e8d44a"
                  : HUD_COLORS.textDim,
          }}
          title="AdaptiveDpr controller — steps down after 2s below 30 FPS"
        >
          AdaptiveDpr: {formatAdaptiveDprLine(stats.adaptiveDpr)}
        </div>
      ) : null}
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
