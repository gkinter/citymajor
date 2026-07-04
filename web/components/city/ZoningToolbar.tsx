"use client";

import type { CSSProperties } from "react";
import type { ZoningTool } from "@/lib/zoning";
import { ZONING_TOOLS } from "@/lib/zoning";

type ZoningToolbarProps = {
  activeTool: ZoningTool;
  onToolChange: (tool: ZoningTool) => void;
};

const toolbarStyle: CSSProperties = {
  position: "absolute",
  bottom: 16,
  left: "50%",
  transform: "translateX(-50%)",
  display: "flex",
  gap: 6,
  padding: "8px 10px",
  fontFamily: "ui-monospace, SFMono-Regular, Menlo, monospace",
  fontSize: 12,
  color: "#e8eef8",
  background: "rgba(8, 12, 24, 0.82)",
  border: "1px solid rgba(120, 160, 220, 0.25)",
  borderRadius: 8,
  pointerEvents: "auto",
  zIndex: 10,
};

function buttonStyle(active: boolean, disabled?: boolean): CSSProperties {
  return {
    padding: "6px 12px",
    border: active
      ? "1px solid rgba(94, 200, 255, 0.7)"
      : "1px solid rgba(120, 160, 220, 0.2)",
    borderRadius: 6,
    background: active ? "rgba(94, 200, 255, 0.15)" : "rgba(16, 22, 38, 0.6)",
    color: disabled ? "rgba(232, 238, 248, 0.35)" : "#e8eef8",
    cursor: disabled ? "not-allowed" : "pointer",
    fontWeight: active ? 700 : 500,
    opacity: disabled ? 0.55 : 1,
  };
}

export function ZoningToolbar({ activeTool, onToolChange }: ZoningToolbarProps) {
  return (
    <div style={toolbarStyle} role="toolbar" aria-label="Zoning tools">
      {ZONING_TOOLS.map(({ id, label, stub }) => (
        <button
          key={id}
          type="button"
          style={buttonStyle(activeTool === id, stub)}
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
