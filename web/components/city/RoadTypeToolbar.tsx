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
  isRampToolUnlocked,
  isRoadTierUnlocked,
  RAMP_OVERLAY_COLOR,
  type RoadElevationMode,
  type RoadToolId,
  type RoadTier,
} from "@/lib/road-types";

type RoadTypeToolbarProps = {
  activeRoadTool: RoadToolId;
  onSelect: (tool: RoadToolId) => void;
  elevationMode?: RoadElevationMode;
  onElevationChange?: (mode: RoadElevationMode) => void;
  unlockedTechIds?: number[];
};

const ELEVATION_OPTIONS: ReadonlyArray<{
  mode: Exclude<RoadElevationMode, "none">;
  label: string;
  shortLabel: string;
  color: string;
}> = [
  {
    mode: "bridge",
    label: "Bridge",
    shortLabel: "Bridge",
    color: "#5b8def",
  },
  {
    mode: "tunnel",
    label: "Tunnel",
    shortLabel: "Tunnel",
    color: "#8b6b4a",
  },
];

export function RoadTypeToolbar({
  activeRoadTool,
  onSelect,
  elevationMode = "none",
  onElevationChange,
  unlockedTechIds,
}: RoadTypeToolbarProps) {
  const tiers = useMemo(() => getPlayableRoadTiers(), []);
  const rampUnlocked = isRampToolUnlocked(unlockedTechIds);
  const rampActive = activeRoadTool === "ramp";

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
        const active = activeRoadTool === tier;

        return (
          <button
            key={tier}
            type="button"
            data-testid={`road-tool-${tier}`}
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
              if (unlocked) onSelect(tier as RoadTier);
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
      <button
        type="button"
        data-testid="road-tool-ramp"
        style={{
          ...hudButton(rampActive, !rampUnlocked),
          minWidth: 72,
          display: "flex",
          alignItems: "center",
          gap: 6,
          opacity: rampUnlocked ? 1 : 0.45,
        }}
        aria-pressed={rampActive}
        disabled={!rampUnlocked}
        title={
          rampUnlocked
            ? "Place highway ramp connector (collector/local)"
            : "Research highway to unlock ramp connectors"
        }
        onClick={() => {
          if (rampUnlocked) onSelect("ramp");
        }}
      >
        <span
          style={{
            width: 8,
            height: 8,
            borderRadius: 2,
            background: RAMP_OVERLAY_COLOR,
            flexShrink: 0,
          }}
          aria-hidden
        />
        Ramp
        {!rampUnlocked ? " 🔒" : null}
      </button>
      {onElevationChange ? (
        <>
          <span
            style={{
              width: 1,
              alignSelf: "stretch",
              background: HUD_COLORS.border,
              opacity: 0.5,
              margin: "0 4px",
            }}
            aria-hidden
          />
          {ELEVATION_OPTIONS.map(({ mode, label, shortLabel, color }) => {
            const active = !rampActive && elevationMode === mode;
            return (
              <button
                key={mode}
                type="button"
                data-testid={`road-elevation-${mode}`}
                style={{
                  ...hudButton(active, rampActive),
                  minWidth: 72,
                  display: "flex",
                  alignItems: "center",
                  gap: 6,
                  opacity: rampActive ? 0.45 : 1,
                }}
                aria-pressed={active}
                disabled={rampActive}
                title={
                  rampActive
                    ? "Exit ramp tool to paint bridge/tunnel"
                    : active
                      ? `Clear ${label.toLowerCase()} mode`
                      : `Paint ${label.toLowerCase()} roads`
                }
                onClick={() => {
                  onElevationChange(active ? "none" : mode);
                }}
              >
                <span
                  style={{
                    width: 8,
                    height: 8,
                    borderRadius: 2,
                    background: color,
                    flexShrink: 0,
                  }}
                  aria-hidden
                />
                {shortLabel}
              </button>
            );
          })}
        </>
      ) : null}
    </div>
  );
}
