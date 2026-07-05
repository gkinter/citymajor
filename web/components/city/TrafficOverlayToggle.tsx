"use client";

import {
  HUD_COLORS,
  HUD_ZONE,
  hudButton,
  hudLabel,
  hudToolbar,
} from "@/lib/hud-theme";

type TrafficOverlayToggleProps = {
  enabled: boolean;
  onToggle: (enabled: boolean) => void;
};

const MODES: { value: boolean; label: string; ariaLabel: string; tooltip: string }[] = [
  {
    value: true,
    label: "On",
    ariaLabel: "Show traffic overlay",
    tooltip: "Show congestion heatmap on roads",
  },
  {
    value: false,
    label: "Off",
    ariaLabel: "Hide traffic overlay",
    tooltip: "Hide congestion heatmap on roads",
  },
];

/** Matches TrafficOverlay densityToColor — green (free) → red (gridlock). */
const CONGESTION_LEGEND = [
  { label: "Free", color: "hsl(118, 90%, 48%)" },
  { label: "Gridlock", color: "hsl(0, 90%, 48%)" },
] as const;

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
      title="Road congestion heatmap — green is free flow, red is gridlock"
    >
      <span style={{ ...hudLabel(), alignSelf: "center", color: HUD_COLORS.textMuted }}>
        Traffic
      </span>
      {MODES.map(({ value, label, ariaLabel, tooltip }) => (
        <button
          key={label}
          type="button"
          style={{ ...hudButton(enabled === value), minWidth: 44 }}
          aria-pressed={enabled === value}
          aria-label={ariaLabel}
          title={tooltip}
          onClick={() => onToggle(value)}
        >
          {label}
        </button>
      ))}
      {enabled ? (
        <span
          style={{
            display: "inline-flex",
            alignItems: "center",
            gap: 6,
            marginLeft: 4,
            paddingLeft: 8,
            borderLeft: `1px solid ${HUD_COLORS.borderSubtle}`,
            color: HUD_COLORS.textDim,
            fontSize: 11,
          }}
          aria-hidden
        >
          {CONGESTION_LEGEND.map(({ label, color }) => (
            <span
              key={label}
              style={{ display: "inline-flex", alignItems: "center", gap: 3 }}
            >
              <span
                style={{
                  width: 8,
                  height: 8,
                  borderRadius: 2,
                  background: color,
                  flexShrink: 0,
                }}
              />
              {label}
            </span>
          ))}
        </span>
      ) : null}
    </div>
  );
}
