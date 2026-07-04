"use client";

import type { CSSProperties } from "react";
import type { GraphicsQualityTier } from "@/lib/constants";

type QualityToolbarProps = {
  qualityTier: GraphicsQualityTier;
  onQualityChange: (tier: GraphicsQualityTier) => void;
};

const TIERS: { tier: GraphicsQualityTier; label: string }[] = [
  { tier: "low", label: "Low" },
  { tier: "high", label: "High" },
];

const toolbarStyle: CSSProperties = {
  position: "absolute",
  bottom: 12,
  right: 12,
  display: "flex",
  gap: 6,
  padding: "8px 10px",
  fontFamily: "ui-monospace, SFMono-Regular, Menlo, monospace",
  fontSize: 12,
  color: "#e8eef8",
  background: "rgba(8, 12, 24, 0.82)",
  border: "1px solid rgba(120, 160, 220, 0.25)",
  borderRadius: 8,
  pointerEvents: "auto",
  zIndex: 10,
};

function buttonStyle(active: boolean): CSSProperties {
  return {
    padding: "6px 12px",
    border: active
      ? "1px solid rgba(94, 200, 255, 0.7)"
      : "1px solid rgba(120, 160, 220, 0.2)",
    borderRadius: 6,
    background: active ? "rgba(94, 200, 255, 0.15)" : "rgba(16, 22, 38, 0.6)",
    color: "#e8eef8",
    cursor: "pointer",
    fontWeight: active ? 700 : 500,
    minWidth: 52,
  };
}

export function QualityToolbar({ qualityTier, onQualityChange }: QualityToolbarProps) {
  return (
    <div style={toolbarStyle} role="toolbar" aria-label="Graphics quality">
      <span style={{ opacity: 0.7, alignSelf: "center", marginRight: 4 }}>Quality</span>
      {TIERS.map(({ tier, label }) => (
        <button
          key={tier}
          type="button"
          style={buttonStyle(qualityTier === tier)}
          aria-pressed={qualityTier === tier}
          onClick={() => onQualityChange(tier)}
        >
          {label}
        </button>
      ))}
    </div>
  );
}
