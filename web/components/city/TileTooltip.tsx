"use client";

import { HUD_COLORS, hudLabel, hudPanel } from "@/lib/hud-theme";
import type { PickResult } from "@/lib/types";

type TileTooltipProps = {
  pick: PickResult;
  zoneLabel: string;
  buildingId: number | null;
  x: number;
  y: number;
};

/** Cursor-following HUD tooltip for hovered terrain tiles. */
export function TileTooltip({
  pick,
  zoneLabel,
  buildingId,
  x,
  y,
}: TileTooltipProps) {
  if (!pick) return null;

  return (
    <div
      data-testid="tile-tooltip"
      style={hudPanel({
        position: "absolute",
        left: x + 12,
        top: y + 12,
        padding: "5px 9px",
        fontSize: 11,
        lineHeight: 1.4,
        pointerEvents: "none",
        zIndex: 30,
        whiteSpace: "nowrap",
      })}
    >
      <div style={{ color: HUD_COLORS.accentHighlight }}>
        ({pick.tileX}, {pick.tileZ})
      </div>
      <div>
        <span style={hudLabel()}>Zone</span>
        {zoneLabel}
      </div>
      {buildingId != null ? (
        <div>
          <span style={hudLabel()}>Building</span>
          {buildingId}
        </div>
      ) : null}
    </div>
  );
}
