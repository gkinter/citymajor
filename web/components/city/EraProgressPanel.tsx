"use client";

import type { CSSProperties } from "react";
import { hudEraName } from "@/lib/era";
import {
  HUD_COLORS,
  HUD_ZONE,
  hudInfoPanel,
  hudLabel,
} from "@/lib/hud-theme";
import type { EraProgress, SimResources } from "@/lib/sim-bridge";

type EraProgressPanelProps = {
  resources: SimResources | null;
};

const sectionTitle: CSSProperties = {
  fontWeight: 700,
  marginBottom: 6,
  fontSize: 11,
  letterSpacing: "0.06em",
  textTransform: "uppercase",
  color: HUD_COLORS.textMuted,
};

function formatGateValue(id: string, value: number): string {
  if (id === "researchPoints") {
    return value >= 10 ? value.toFixed(0) : value.toFixed(1);
  }
  return Math.floor(value).toLocaleString();
}

function gateRowStyle(met: boolean): CSSProperties {
  return {
    display: "flex",
    alignItems: "center",
    gap: 6,
    marginTop: 4,
    color: met ? HUD_COLORS.accentHighlight : HUD_COLORS.text,
    opacity: met ? 1 : 0.9,
  };
}

function checklistMark(met: boolean): CSSProperties {
  return {
    width: 14,
    flexShrink: 0,
    textAlign: "center",
    color: met ? "#7dffb2" : HUD_COLORS.textDim,
    fontWeight: 700,
  };
}

function progressBar(percent: number): CSSProperties {
  return {
    height: 4,
    marginTop: 8,
    borderRadius: 2,
    background: HUD_COLORS.rowBg,
    overflow: "hidden",
  };
}

function progressFill(percent: number): CSSProperties {
  return {
    height: "100%",
    width: `${Math.min(100, Math.max(0, percent))}%`,
    background: `linear-gradient(90deg, ${HUD_COLORS.accent}, ${HUD_COLORS.accentHighlight})`,
    transition: "width 0.25s ease",
  };
}

function EraProgressContent({ progress, currentEra }: { progress: EraProgress; currentEra: number }) {
  if (progress.gates.length === 0) {
    return (
      <div style={{ opacity: 0.75 }}>
        {hudEraName(currentEra)} — max era reached
      </div>
    );
  }

  const targetName = progress.nextEraName || hudEraName(progress.nextEra);

  return (
    <>
      <div style={{ marginBottom: 2 }}>
        <span style={hudLabel()}>Next</span>
        {targetName}
        <span style={{ marginLeft: 8, opacity: 0.7 }}>
          {Math.round(progress.percent)}%
        </span>
      </div>
      <div style={progressBar(progress.percent)}>
        <div style={progressFill(progress.percent)} />
      </div>
      <div style={{ marginTop: 8 }}>
        {progress.gates.map((gate) => (
          <div key={gate.id} style={gateRowStyle(gate.met)}>
            <span style={checklistMark(gate.met)}>{gate.met ? "✓" : "○"}</span>
            <span style={{ flex: 1 }}>{gate.label}</span>
            <span style={{ opacity: 0.8, fontVariantNumeric: "tabular-nums" }}>
              {formatGateValue(gate.id, gate.current)}
              {" / "}
              {formatGateValue(gate.id, gate.required)}
            </span>
          </div>
        ))}
      </div>
    </>
  );
}

export function EraProgressPanel({ resources }: EraProgressPanelProps) {
  const progress = resources?.eraProgress;
  const currentEra = resources?.era ?? 0;

  if (!progress) return null;

  return (
    <div
      style={{
        ...HUD_ZONE.topRight,
        top: 50,
        ...hudInfoPanel({ minWidth: 220, maxWidth: 280 }),
      }}
    >
      <div style={sectionTitle}>Era Progress</div>
      <EraProgressContent progress={progress} currentEra={currentEra} />
    </div>
  );
}
