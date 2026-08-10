"use client";

import {
  HUD_COLORS,
  HUD_ZONE,
  hudButton,
  hudToolbar,
} from "@/lib/hud-theme";

type FrictionOverlayToggleProps = {
  enabled: boolean;
  onToggle: (enabled: boolean) => void;
};

const MODES: { value: boolean; label: string }[] = [
  { value: true, label: "On" },
  { value: false, label: "Off" },
];

export function FrictionOverlayToggle({
  enabled,
  onToggle,
}: FrictionOverlayToggleProps) {
  return (
    <div
      data-testid="friction-overlay-toggle"
      style={{
        ...hudToolbar(HUD_ZONE.bottomRight),
        bottom: 92,
        right: 12,
      }}
      role="toolbar"
      aria-label="Trade friction overlay"
    >
      <span
        style={{
          opacity: 0.7,
          alignSelf: "center",
          marginRight: 4,
          color: HUD_COLORS.textMuted,
        }}
      >
        Friction
      </span>
      {MODES.map(({ value, label }) => (
        <button
          key={label}
          type="button"
          style={{ ...hudButton(enabled === value), minWidth: 44 }}
          aria-pressed={enabled === value}
          title={
            value
              ? "Show goods-transport friction corridors"
              : "Hide goods-transport friction corridors"
          }
          onClick={() => onToggle(value)}
        >
          {label}
        </button>
      ))}
    </div>
  );
}
