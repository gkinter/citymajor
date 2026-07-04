"use client";

import {
  HUD_COLORS,
  HUD_ZONE,
  hudButton,
  hudToolbar,
} from "@/lib/hud-theme";
import type { GraphicsQualityTier } from "@/lib/constants";

type QualityToolbarProps = {
  qualityTier: GraphicsQualityTier;
  onQualityChange: (tier: GraphicsQualityTier) => void;
};

const TIERS: { tier: GraphicsQualityTier; label: string }[] = [
  { tier: "low", label: "Low" },
  { tier: "high", label: "High" },
];

export function QualityToolbar({ qualityTier, onQualityChange }: QualityToolbarProps) {
  return (
    <div
      style={hudToolbar(HUD_ZONE.bottomRight)}
      role="toolbar"
      aria-label="Graphics quality"
    >
      <span style={{ opacity: 0.7, alignSelf: "center", marginRight: 4, color: HUD_COLORS.textMuted }}>
        Quality
      </span>
      {TIERS.map(({ tier, label }) => (
        <button
          key={tier}
          type="button"
          style={{ ...hudButton(qualityTier === tier), minWidth: 52 }}
          aria-pressed={qualityTier === tier}
          onClick={() => onQualityChange(tier)}
        >
          {label}
        </button>
      ))}
    </div>
  );
}
