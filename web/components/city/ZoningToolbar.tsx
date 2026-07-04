"use client";

import {
  HUD_ZONE,
  hudButton,
  hudLabel,
  hudToolbar,
} from "@/lib/hud-theme";
import {
  isPaintFeedbackEnabled,
  setPaintFeedbackEnabled,
} from "@/lib/paint-feedback";
import type { RciDemand } from "@/lib/sim-bridge";
import {
  isZoningDemandHigh,
  zoningDemandHint,
} from "@/lib/zoning-economy";
import type { PaintBrushSize, ZoningTool } from "@/lib/zoning";
import {
  nextBrushSize,
  ZONING_TOOLS,
  zoningToolColor,
  zoningToolShortLabel,
} from "@/lib/zoning";
import { useCallback, useState } from "react";

type ZoningToolbarProps = {
  activeTool: ZoningTool;
  brushSize: PaintBrushSize;
  onToolChange: (tool: ZoningTool) => void;
  onBrushSizeChange: (size: PaintBrushSize) => void;
  /** Live RCI demand — drives tooltips and high-demand highlights. */
  rci?: RciDemand | null;
};

export function ZoningToolbar({
  activeTool,
  brushSize,
  onToolChange,
  onBrushSizeChange,
  rci = null,
}: ZoningToolbarProps) {
  const [feedbackOn, setFeedbackOn] = useState(() => isPaintFeedbackEnabled());
  const activeColor = zoningToolColor(activeTool);
  const showBrush = true;

  const toggleFeedback = useCallback(() => {
    const next = !feedbackOn;
    setPaintFeedbackEnabled(next);
    setFeedbackOn(next);
  }, [feedbackOn]);

  const cycleBrush = useCallback(() => {
    onBrushSizeChange(nextBrushSize(brushSize));
  }, [brushSize, onBrushSizeChange]);

  return (
    <div style={{ ...HUD_ZONE.bottomCenter, display: "flex", flexDirection: "column", alignItems: "center", gap: 6, pointerEvents: "auto" }}>
      <div
        style={{
          ...hudToolbar(),
          alignItems: "center",
          padding: "4px 10px",
          fontSize: 11,
          gap: 10,
        }}
        aria-live="polite"
      >
        <span
          style={{
            width: 10,
            height: 10,
            borderRadius: 2,
            background: activeColor,
            boxShadow: `0 0 8px ${activeColor}88`,
            flexShrink: 0,
          }}
          aria-hidden
        />
        <span>
          <span style={hudLabel()}>Zone</span>
          <strong>{zoningToolShortLabel(activeTool)}</strong>
        </span>
        {showBrush ? (
          <button
            type="button"
            style={{
              ...hudButton(false),
              padding: "2px 8px",
              fontSize: 11,
            }}
            title="Cycle brush size"
            onClick={cycleBrush}
          >
            Brush {brushSize}×{brushSize}
          </button>
        ) : null}
        <button
          type="button"
          style={{
            ...hudButton(false),
            padding: "2px 8px",
            fontSize: 11,
            opacity: feedbackOn ? 1 : 0.55,
          }}
          title={
            feedbackOn
              ? "Mute paint sound & haptics"
              : "Enable paint sound & haptics"
          }
          aria-pressed={feedbackOn}
          onClick={toggleFeedback}
        >
          {feedbackOn ? "🔊" : "🔇"}
        </button>
      </div>

      <div
        style={hudToolbar()}
        role="toolbar"
        aria-label="Zoning tools"
        data-onboarding-target="zoning"
      >
        {ZONING_TOOLS.map(({ id, label, stub }) => {
          const demandHint = zoningDemandHint(id, rci);
          const title = stub
            ? "Road tool — coming soon"
            : demandHint
              ? `${label}: ${demandHint}`
              : label;
          const highDemand = !stub && isZoningDemandHigh(id, rci);

          return (
          <button
            key={id}
            type="button"
            className={highDemand ? "hud-zoning-btn--demand" : undefined}
            style={hudButton(activeTool === id, stub)}
            disabled={stub}
            title={title}
            aria-pressed={activeTool === id}
            onClick={() => {
              if (!stub) onToolChange(id);
            }}
          >
            {label}
            {highDemand ? " ↑" : null}
            {stub ? " ⏳" : ""}
          </button>
          );
        })}
      </div>
    </div>
  );
}
