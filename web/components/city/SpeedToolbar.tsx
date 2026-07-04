"use client";

import {
  HUD_ZONE,
  hudButton,
  hudToolbar,
} from "@/lib/hud-theme";
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

export function SpeedToolbar({ speedLevel, onSpeedChange }: SpeedToolbarProps) {
  return (
    <div
      style={hudToolbar(HUD_ZONE.topLeft)}
      role="toolbar"
      aria-label="Game speed"
    >
      {SPEEDS.map(({ level, label }) => (
        <button
          key={level}
          type="button"
          style={{ ...hudButton(speedLevel === level), minWidth: 52 }}
          aria-pressed={speedLevel === level}
          onClick={() => onSpeedChange(level)}
        >
          {label}
        </button>
      ))}
    </div>
  );
}
