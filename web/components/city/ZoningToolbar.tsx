"use client";

import {
  HUD_ZONE,
  hudButton,
  hudToolbar,
} from "@/lib/hud-theme";
import type { ZoningTool } from "@/lib/zoning";
import { ZONING_TOOLS } from "@/lib/zoning";

type ZoningToolbarProps = {
  activeTool: ZoningTool;
  onToolChange: (tool: ZoningTool) => void;
};

export function ZoningToolbar({ activeTool, onToolChange }: ZoningToolbarProps) {
  return (
    <div
      style={hudToolbar(HUD_ZONE.bottomCenter)}
      role="toolbar"
      aria-label="Zoning tools"
    >
      {ZONING_TOOLS.map(({ id, label, stub }) => (
        <button
          key={id}
          type="button"
          style={hudButton(activeTool === id, stub)}
          disabled={stub}
          title={stub ? "Road tool — coming soon" : undefined}
          aria-pressed={activeTool === id}
          onClick={() => {
            if (!stub) onToolChange(id);
          }}
        >
          {label}
          {stub ? " ⏳" : ""}
        </button>
      ))}
    </div>
  );
}
