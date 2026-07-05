"use client";

import type { CSSProperties } from "react";
import { eraQuestTitle } from "@/lib/era-narrative";
import { hudEraBadgeStyle, hudEraName } from "@/lib/era";
import {
  HUD_COLORS,
  HUD_Z,
  HUD_ZONE,
  hudEraBadgeChrome,
  hudEraHeaderRow,
  hudInfoPanel,
  hudLabel,
  hudProgressFill,
  hudProgressTrack,
} from "@/lib/hud-theme";
import type { EraProgress, SimResources } from "@/lib/sim-bridge";

type EraProgressPanelProps = {
  resources: SimResources | null;
};

const sectionTitle: CSSProperties = {
  fontWeight: 700,
  fontSize: 11,
  letterSpacing: "0.06em",
  textTransform: "uppercase",
  color: HUD_COLORS.textMuted,
};

const questSubtitle: CSSProperties = {
  marginBottom: 8,
  fontSize: 11,
  lineHeight: 1.35,
  color: HUD_COLORS.text,
  opacity: 0.9,
};

function formatGateValue(_id: string, value: number): string {
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

function CurrentEraHeader({ era }: { era: number }) {
  const name = hudEraName(era);
  return (
    <div style={hudEraHeaderRow()}>
      <span style={sectionTitle}>Current Era</span>
      <span
        style={{
          ...hudEraBadgeChrome(),
          ...hudEraBadgeStyle(era),
        }}
        aria-label={`Current era: ${name}`}
      >
        {name}
      </span>
    </div>
  );
}

function EraQuestChecklist({ progress }: { progress: EraProgress }) {
  return (
    <div
      role="list"
      aria-label="Era quest objectives"
      style={{ marginTop: 8 }}
    >
      {progress.gates.map((gate) => (
        <div
          key={gate.id}
          role="listitem"
          style={gateRowStyle(gate.met)}
          aria-label={`${gate.label}: ${formatGateValue(gate.id, gate.current)} of ${formatGateValue(gate.id, gate.required)}${gate.met ? ", complete" : ""}`}
        >
          <span style={checklistMark(gate.met)} aria-hidden>
            {gate.met ? "✓" : "○"}
          </span>
          <span style={{ flex: 1 }}>{gate.label}</span>
          <span style={{ opacity: 0.8, fontVariantNumeric: "tabular-nums" }}>
            {formatGateValue(gate.id, gate.current)}
            {" / "}
            {formatGateValue(gate.id, gate.required)}
          </span>
        </div>
      ))}
    </div>
  );
}

function EraProgressContent({ progress }: { progress: EraProgress }) {
  if (progress.gates.length === 0) {
    return (
      <div style={{ fontSize: 11, color: HUD_COLORS.textMuted }}>
        Maximum era reached
      </div>
    );
  }

  const targetName = progress.nextEraName || hudEraName(progress.nextEra);
  const questTitle = eraQuestTitle(progress.nextEra);
  const percent = Math.round(progress.percent);

  return (
    <>
      <div
        style={hudProgressTrack({ marginBottom: 6 })}
        role="progressbar"
        aria-valuenow={percent}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-label={`Progress toward ${targetName}`}
      >
        <div style={hudProgressFill(progress.percent)} />
      </div>
      <div style={{ marginBottom: 2, fontSize: 11 }}>
        <span style={hudLabel()}>Next</span>
        {targetName}
        <span style={{ marginLeft: 8, opacity: 0.7, fontVariantNumeric: "tabular-nums" }}>
          {percent}%
        </span>
      </div>
      {questTitle ? (
        <div style={questSubtitle}>
          <span style={hudLabel()}>Quest</span>
          {questTitle}
        </div>
      ) : null}
      <EraQuestChecklist progress={progress} />
    </>
  );
}

export function EraProgressPanel({ resources }: EraProgressPanelProps) {
  if (!resources) return null;

  const currentEra = resources.era ?? 0;
  const progress = resources.eraProgress;

  return (
    <aside
      aria-label="Era quest progress"
      data-onboarding-target="era-quest"
      style={{
        ...HUD_ZONE.topRight,
        top: 50,
        zIndex: HUD_Z.panel,
        ...hudInfoPanel({ minWidth: 220, maxWidth: 280 }),
      }}
    >
      <CurrentEraHeader era={currentEra} />
      {progress ? <EraProgressContent progress={progress} /> : null}
    </aside>
  );
}
