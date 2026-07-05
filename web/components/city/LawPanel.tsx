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
  onSetLawActive?: (lawId: string, active: boolean) => void;
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

function LawToggleRow({
  law,
  disabled = false,
  comingSoon = false,
  onToggle,
}: {
  law: { id: string; name: string; active: boolean };
  disabled?: boolean;
  comingSoon?: boolean;
  onToggle?: (active: boolean) => void;
}) {
  const toggleId = `law-toggle-${law.id}`;

  return (
    <div
      className={`hud-law-row${comingSoon ? " hud-law-row--soon" : ""}`}
      aria-label={law.name}
    >
      <div className="hud-law-row__copy">
        <span className="hud-law-row__name">{law.name}</span>
        {comingSoon ? (
          <span className="hud-law-row__badge">Coming soon</span>
        ) : (
          <span className="hud-law-row__hint">
            {law.active ? "In effect" : "Not active"}
          </span>
        )}
      </div>
      <label className="hud-law-toggle" htmlFor={toggleId}>
        <input
          id={toggleId}
          type="checkbox"
          className="hud-law-toggle__input"
          checked={law.active}
          disabled={disabled || comingSoon}
          onChange={(event) => onToggle?.(event.target.checked)}
        />
        <span className="hud-law-toggle__track" aria-hidden />
      </label>
    </div>
  );
}

export function LawPanel({
  open,
  onClose,
  resources,
  onSetLawActive,
}: LawPanelProps) {
  if (!open) return null;

  const definitionCount = resources?.lawDefinitionCount;
  const activeCount = resources?.activeLawCount;
  const sampleLaw = resources?.sampleLaw;
  const hasWasmData =
    definitionCount !== undefined || activeCount !== undefined;
  const canToggleSample =
    sampleLaw !== undefined &&
    onSetLawActive !== undefined &&
    !sampleLaw.id.startsWith("placeholder");

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

        <section aria-label="Ordinance toggles" style={{ marginTop: 18 }}>
          <div style={sectionTitle}>Ordinances</div>
          {sampleLaw ? (
            <LawToggleRow
              law={sampleLaw}
              disabled={!canToggleSample}
              onToggle={
                canToggleSample
                  ? (active) => onSetLawActive?.(sampleLaw.id, active)
                  : undefined
              }
            />
          ) : (
            <LawToggleRow
              law={{
                id: "placeholder",
                name: "Speed Limits",
                active: false,
              }}
              comingSoon
              disabled
            />
          )}
          <LawToggleRow
            law={{
              id: "placeholder-catalog",
              name: "Full ordinance catalog",
              active: false,
            }}
            comingSoon
            disabled
          />
        </section>
      </div>
    </aside>
  );
}
