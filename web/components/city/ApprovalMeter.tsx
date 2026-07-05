"use client";

import type { CSSProperties } from "react";
import { LOW_APPROVAL_WARNING_THRESHOLD } from "@/lib/constants";
import {
  HUD_COLORS,
  HUD_Z,
  HUD_ZONE,
  hudInfoPanel,
  hudLabel,
} from "@/lib/hud-theme";
import { LOW_HAPPINESS_APPROVAL } from "@/lib/sim-metrics";
import type { SimResources } from "@/lib/sim-bridge";

type ApprovalMeterProps = {
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

function approvalFillColor(approval: number): string {
  if (approval >= 70) return "#7dffb2";
  if (approval >= 50) return "#b8e86a";
  if (approval >= 35) return "#f0e060";
  if (approval >= 20) return "#ffb070";
  return "#ff7070";
}

function approvalStatusLabel(approval: number): string {
  if (approval >= 70) return "Beloved";
  if (approval >= 50) return "Stable";
  if (approval >= 35) return "Uneasy";
  if (approval >= 20) return "Restless";
  return "Critical";
}

function progressBarStyle(): CSSProperties {
  return {
    height: 6,
    marginTop: 6,
    borderRadius: 3,
    background: HUD_COLORS.rowBg,
    overflow: "hidden",
  };
}

function progressFillStyle(approval: number): CSSProperties {
  return {
    height: "100%",
    width: `${Math.min(100, Math.max(0, approval))}%`,
    background: approvalFillColor(approval),
    transition: "width 0.25s ease, background 0.25s ease",
  };
}

/** Vertical offset below BudgetPanel in the top-right stack. */
function topOffset(resources: SimResources | null): number {
  const budgetTop = resources?.eraProgress ? 240 : 50;
  return budgetTop + 132;
}

export function ApprovalMeter({ resources }: ApprovalMeterProps) {
  const approval = resources?.approval;
  if (approval === undefined) return null;

  const clamped = Math.min(100, Math.max(0, approval));
  const status = approvalStatusLabel(clamped);

  return (
    <aside
      aria-label="Mayor approval"
      data-onboarding-target="approval"
      title={`Mayor approval drives Herald unrest below ${LOW_HAPPINESS_APPROVAL}% and crisis warnings below ${LOW_APPROVAL_WARNING_THRESHOLD}%.`}
      style={{
        ...HUD_ZONE.topRight,
        top: topOffset(resources),
        zIndex: HUD_Z.panel,
        ...hudInfoPanel({ minWidth: 200, maxWidth: 240 }),
      }}
    >
      <div style={sectionTitle}>Approval</div>
      <div style={{ display: "flex", alignItems: "baseline", gap: 8 }}>
        <span
          style={{
            fontSize: 20,
            fontWeight: 700,
            fontVariantNumeric: "tabular-nums",
            color: approvalFillColor(clamped),
          }}
        >
          {clamped.toFixed(0)}%
        </span>
        <span style={{ fontSize: 11, opacity: 0.85, color: HUD_COLORS.textMuted }}>
          {status}
        </span>
      </div>
      <div
        style={progressBarStyle()}
        role="progressbar"
        aria-valuenow={Math.round(clamped)}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-label={`Mayor approval ${clamped.toFixed(0)} percent`}
      >
        <div style={progressFillStyle(clamped)} />
      </div>
      {resources?.happiness !== undefined ? (
        <div style={{ marginTop: 6, fontSize: 11, opacity: 0.75 }}>
          <span style={hudLabel()}>Happiness</span>
          {(resources.happiness * 100).toFixed(0)}%
        </div>
      ) : null}
    </aside>
  );
}
