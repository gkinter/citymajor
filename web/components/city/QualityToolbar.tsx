"use client";

import {
  HUD_COLORS,
  HUD_ZONE,
  hudButton,
  hudToolbar,
} from "@/lib/hud-theme";
import {
  GRAPHICS_QUALITY_TIERS,
  type GraphicsQualityTier,
} from "@/lib/constants";

type QualityToolbarProps = {
  qualityTier: GraphicsQualityTier;
  onQualityChange: (tier: GraphicsQualityTier) => void;
};

export function QualityToolbar({ qualityTier, onQualityChange }: QualityToolbarProps) {
  return (
    <div
      data-testid="quality-toolbar"
      style={hudToolbar(HUD_ZONE.bottomRight)}
      role="toolbar"
      aria-label="Graphics quality"
      title="Graphics quality preset — affects bloom and pixel density"
    >
      <span
        style={{
          opacity: 0.7,
          alignSelf: "center",
          marginRight: 4,
          color: HUD_COLORS.textMuted,
        }}
      >
        Quality
      </span>
      {GRAPHICS_QUALITY_TIERS.map(({ tier, label, tooltip }) => (
        <button
          key={tier}
          type="button"
          style={{ ...hudButton(qualityTier === tier), minWidth: 44 }}
          aria-pressed={qualityTier === tier}
          aria-label={`${label} quality`}
          title={tooltip}
          onClick={() => onQualityChange(tier)}
        >
          {label}
        </button>
      ))}
    </div>
  );
}
