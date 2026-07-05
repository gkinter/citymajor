"use client";

import {
  HUD_COLORS,
  HUD_ZONE,
  hudButton,
  hudToolbar,
} from "@/lib/hud-theme";

type TradeOverlayToggleProps = {
  enabled: boolean;
  onToggle: (enabled: boolean) => void;
};

const MODES: { value: boolean; label: string }[] = [
  { value: true, label: "On" },
  { value: false, label: "Off" },
];

/** On/Off toggle for the global-market trade summary HUD (ServicesToolbar cluster). */
export function TradeOverlayToggle({
  enabled,
  onToggle,
}: TradeOverlayToggleProps) {
  return (
    <div
      data-testid="trade-overlay-toggle"
      style={{
        ...hudToolbar(HUD_ZONE.bottomLeft),
        bottom: 52,
      }}
      role="toolbar"
      aria-label="Trade overlay"
    >
      <span
        style={{
          opacity: 0.7,
          alignSelf: "center",
          marginRight: 4,
          color: HUD_COLORS.textMuted,
        }}
      >
        Trade
      </span>
      {MODES.map(({ value, label }) => (
        <button
          key={label}
          type="button"
          style={{ ...hudButton(enabled === value), minWidth: 44 }}
          aria-pressed={enabled === value}
          title={
            value
              ? "Show global market trade summary (exports, imports, balance)"
              : "Hide trade summary overlay"
          }
          onClick={() => onToggle(value)}
        >
          {label}
        </button>
      ))}
    </div>
  );
}
