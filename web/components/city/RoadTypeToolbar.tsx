"use client";

import { useMemo } from "react";
import {
  HUD_COLORS,
  HUD_ZONE,
  hudButton,
  hudToolbar,
} from "@/lib/hud-theme";
import {
  getPlayableRoadTiers,
  isRoadTierUnlocked,
  type RoadTier,
} from "@/lib/road-types";

type RoadTypeToolbarProps = {
  activeRoadTier: RoadTier;
  onSelect: (tier: RoadTier) => void;
  unlockedTechIds?: number[];
};

export function RoadTypeToolbar({
  activeRoadTier,
  onSelect,
  unlockedTechIds,
}: RoadTypeToolbarProps) {
  const tiers = useMemo(() => getPlayableRoadTiers(), []);

  return (
    <div
      data-testid="road-type-toolbar"
      style={{
        ...hudToolbar(HUD_ZONE.bottomCenter),
        bottom: 108,
      }}
      role="toolbar"
      aria-label="Road type"
    >
      <span
        style={{
          opacity: 0.7,
          alignSelf: "center",
          marginRight: 4,
          color: HUD_COLORS.textMuted,
        }}
      >
        Road
      </span>
      {tiers.map(({ tier, label, shortLabel, overlayColor }) => {
        const unlocked = isRoadTierUnlocked(tier, unlockedTechIds);
        const active = activeRoadTier === tier;

        return (
          <button
            key={tier}
            type="button"
            style={{
              ...hudButton(active, !unlocked),
              minWidth: 72,
              display: "flex",
              alignItems: "center",
              gap: 6,
              opacity: unlocked ? 1 : 0.45,
            }}
            aria-pressed={active}
            disabled={!unlocked}
            title={
              unlocked
                ? `Place ${label.toLowerCase()} roads`
                : `Research ${label.toLowerCase()} to unlock`
            }
            onClick={() => {
              if (unlocked) onSelect(tier);
            }}
          >
            <span
              style={{
                width: 8,
                height: 8,
                borderRadius: 2,
                background: overlayColor,
                flexShrink: 0,
              }}
              aria-hidden
            />
            {shortLabel}
            {!unlocked ? " 🔒" : null}
          </button>
        );
      })}
    </div>
  );
}
