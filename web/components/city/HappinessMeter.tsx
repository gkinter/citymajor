"use client";

import type { CSSProperties } from "react";
import {
  HUD_COLORS,
  HUD_ZONE,
  hudInfoPanel,
} from "@/lib/hud-theme";
import type { SimResources } from "@/lib/sim-bridge";

type HappinessMeterProps = {
  resources: SimResources | null;
};

const sectionTitle: CSSProperties = {
  fontWeight: 700,
  marginBottom: 6,
  fontSize: 11,
  letterSpacing: "0.06em",
  textTransform: "uppercase",
  color: HUD_COLORS.textMuted,
};

const valueStyle: CSSProperties = {
  fontSize: 20,
  fontWeight: 700,
  fontVariantNumeric: "tabular-nums",
  color: HUD_COLORS.text,
};

const stubNote: CSSProperties = {
  marginTop: 2,
  fontSize: 11,
  lineHeight: 1.4,
  color: HUD_COLORS.textMuted,
};

const trackStyle: CSSProperties = {
  marginTop: 8,
  height: 6,
  borderRadius: 3,
  background: "rgba(8, 12, 24, 0.55)",
  overflow: "hidden",
};

function happinessFillColor(happiness: number): string {
  const pct = happiness * 100;
  if (pct > 70) return "#7dffb2";
  if (pct >= 40) return "#e8d44a";
  return HUD_COLORS.error;
}

export function HappinessMeter({ resources }: HappinessMeterProps) {
  const happiness = resources?.happiness;
  const hasHappiness = happiness !== undefined;

  return (
    <aside
      aria-label="Citizen happiness"
      data-onboarding-target="happiness"
      style={{
        ...HUD_ZONE.leftStack,
        top: "var(--hud-toast-top)",
        ...hudInfoPanel({ minWidth: 140, maxWidth: 180 }),
      }}
    >
      <div style={sectionTitle}>Happiness</div>

      {hasHappiness ? (
        <>
          <div style={valueStyle}>{(happiness * 100).toFixed(0)}%</div>
          <div style={trackStyle} aria-hidden>
            <div
              style={{
                width: `${Math.max(0, Math.min(100, happiness * 100))}%`,
                height: "100%",
                borderRadius: "inherit",
                background: happinessFillColor(happiness),
                transition: "width 0.25s ease",
              }}
            />
          </div>
        </>
      ) : (
        <p style={stubNote}>
          TODO: Happiness unavailable — read{" "}
          <code style={{ fontSize: 10 }}>Happiness</code> from WASM status JSON
          (PoliticsSystem / WorldState).
        </p>
      )}
    </aside>
  );
}
