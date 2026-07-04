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

export function TrafficOverlayToggle({
  enabled,
  onToggle,
}: TrafficOverlayToggleProps) {
  return (
    <div
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
      <button
        type="button"
        style={{ ...hudButton(enabled), minWidth: 52 }}
        aria-pressed={enabled}
        title={
          enabled
            ? "Hide congestion heatmap on roads"
            : "Show congestion heatmap on roads"
        }
        onClick={() => onToggle(!enabled)}
      >
        {enabled ? "On" : "Off"}
      </button>
    </div>
  );
}
