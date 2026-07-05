"use client";

import type { CSSProperties } from "react";
import { HUD_COLORS, hudSlidePanel } from "@/lib/hud-theme";
import type { SimResources } from "@/lib/sim-bridge";

const headerStyle: CSSProperties = {
  display: "flex",
  alignItems: "center",
  justifyContent: "space-between",
  padding: "14px 16px",
  borderBottom: `1px solid ${HUD_COLORS.borderSubtle}`,
};

const bodyStyle: CSSProperties = {
  flex: 1,
  overflowY: "auto",
  padding: "16px 18px 24px",
};

const sectionTitle: CSSProperties = {
  fontWeight: 700,
  marginBottom: 8,
  fontSize: 11,
  letterSpacing: "0.06em",
  textTransform: "uppercase",
  color: HUD_COLORS.textMuted,
};

type LawPanelProps = {
  open: boolean;
  onClose: () => void;
  resources: SimResources | null;
};

function formatCount(value: number | undefined): string {
  if (value === undefined) return "—";
  return value.toLocaleString();
}

function StatRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="hud-law-stat">
      <span className="hud-law-stat__label">{label}</span>
      <span className="hud-law-stat__value">{value}</span>
    </div>
  );
}

export function LawPanel({ open, onClose, resources }: LawPanelProps) {
  if (!open) return null;

  const definitionCount = resources?.lawDefinitionCount;
  const activeCount = resources?.activeLawCount;
  const hasWasmData =
    definitionCount !== undefined || activeCount !== undefined;

  return (
    <aside
      className="hud-law-panel"
      aria-label="City laws"
      style={hudSlidePanel()}
    >
      <header style={headerStyle}>
        <div>
          <div style={{ fontWeight: 700, fontSize: 14 }}>Laws</div>
          <div style={{ fontSize: 11, opacity: 0.65, marginTop: 2 }}>
            Policy catalog and active ordinances
          </div>
        </div>
        <button
          type="button"
          className="hud-law-close"
          style={{
            padding: "4px 10px",
            fontFamily: "inherit",
            fontSize: 12,
            color: HUD_COLORS.text,
            background: "transparent",
            border: `1px solid ${HUD_COLORS.borderSubtle}`,
            borderRadius: 6,
            cursor: "pointer",
          }}
          onClick={onClose}
        >
          Close
        </button>
      </header>

      <div style={bodyStyle}>
        {!hasWasmData ? (
          <p className="hud-law-empty">
            Law counts appear once the WASM sim exports LawSystem status.
          </p>
        ) : null}

        <section aria-label="Law counts">
          <div style={sectionTitle}>Catalog</div>
          <StatRow
            label="Definitions loaded"
            value={formatCount(definitionCount)}
          />
        </section>

        <section aria-label="Active laws" style={{ marginTop: 18 }}>
          <div style={sectionTitle}>Active</div>
          <StatRow
            label="Ordinances in effect"
            value={formatCount(activeCount)}
          />
        </section>
      </div>
    </aside>
  );
}
