"use client";

import {
  HUD_COLORS,
  HUD_ZONE,
  hudButton,
  hudToolbar,
} from "@/lib/hud-theme";

type TrafficOverlayToggleProps = {
  enabled: boolean;
  onToggle: (enabled: boolean) => void;
};

const MODES: { value: boolean; label: string }[] = [
  { value: true, label: "On" },
  { value: false, label: "Off" },
];

export function TrafficOverlayToggle({
  enabled,
  onToggle,
}: TrafficOverlayToggleProps) {
  return (
    <div
      data-testid="traffic-overlay-toggle"
      style={{
        ...hudToolbar(HUD_ZONE.bottomRight),
        bottom: 52,
        right: 12,
      }}
      role="toolbar"
      aria-label="Traffic overlay"
    >
      <span
        style={{
          opacity: 0.7,
          alignSelf: "center",
          marginRight: 4,
          color: HUD_COLORS.textMuted,
        }}
      >
        Traffic
      </span>
      {MODES.map(({ value, label }) => (
        <button
          key={label}
          type="button"
          style={{ ...hudButton(enabled === value), minWidth: 44 }}
          aria-pressed={enabled === value}
          title={
            value
              ? "Show congestion heatmap on roads"
              : "Hide congestion heatmap on roads"
          }
          onClick={() => onToggle(value)}
        >
          {label}
        </button>
      ))}
    </div>
  );
}
