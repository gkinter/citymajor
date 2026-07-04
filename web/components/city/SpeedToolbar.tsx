"use client";

import type { CSSProperties } from "react";
import type { GameSpeedLevel } from "@/lib/sim-bridge";

type SpeedToolbarProps = {
  speedLevel: GameSpeedLevel;
  onSpeedChange: (level: GameSpeedLevel) => void;
};

const SPEEDS: { level: GameSpeedLevel; label: string }[] = [
  { level: 0, label: "Pause" },
  { level: 1, label: "1x" },
  { level: 2, label: "2x" },
  { level: 3, label: "3x" },
];

const toolbarStyle: CSSProperties = {
  position: "absolute",
  top: 12,
  left: 12,
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

export function SpeedToolbar({ speedLevel, onSpeedChange }: SpeedToolbarProps) {
  return (
    <div style={toolbarStyle} role="toolbar" aria-label="Game speed">
      {SPEEDS.map(({ level, label }) => (
        <button
          key={level}
          type="button"
          style={buttonStyle(speedLevel === level)}
          aria-pressed={speedLevel === level}
          onClick={() => onSpeedChange(level)}
        >
          {label}
        </button>
      ))}
    </div>
  );
}
