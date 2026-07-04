"use client";

import type { CSSProperties } from "react";
import {
  HUD_COLORS,
  hudActionButton,
  hudSlidePanel,
} from "@/lib/hud-theme";
import {
  TECH_CATALOG_TOTAL,
  TECH_V1_CATALOG,
  techIdFromCatalogId,
  type TechPreview,
} from "@/lib/tech-catalog";

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

const statRowStyle: CSSProperties = {
  display: "flex",
  gap: 16,
  marginBottom: 16,
  fontSize: 12,
};

function formatRp(value: number | undefined): string {
  if (value === undefined) return "—";
  return value.toFixed(1);
}

type ResearchPanelProps = {
  open: boolean;
  onClose: () => void;
  techCount?: number;
  researchPoints?: number;
  researchRate?: number;
  /** Override catalog (defaults to WEB v1 Frontier + Industrial subset). */
  technologies?: TechPreview[];
  /** Called when the player clicks a tech to enqueue research. */
  onEnqueueResearch?: (techId: number) => void;
};

export function ResearchPanel({
  open,
  onClose,
  techCount,
  researchPoints,
  researchRate,
  technologies = TECH_V1_CATALOG,
  onEnqueueResearch,
}: ResearchPanelProps) {
  if (!open) return null;

  const unlocked = techCount ?? 0;
  const catalogTotal = TECH_CATALOG_TOTAL;

  return (
    <aside
      className="hud-research-panel"
      style={hudSlidePanel()}
      role="dialog"
      aria-label="Research catalog"
      aria-modal="true"
    >
      <header style={headerStyle}>
        <div>
          <div style={{ fontWeight: 700, fontSize: 15, letterSpacing: "0.04em" }}>
            Research
          </div>
          <div style={{ opacity: 0.65, fontSize: 11, marginTop: 2 }}>
            {unlocked} / {catalogTotal} technologies unlocked
          </div>
        </div>
        <button
          type="button"
          style={{ ...hudActionButton(), padding: "4px 10px", fontSize: 12 }}
          onClick={onClose}
          aria-label="Close"
        >
          ✕
        </button>
      </header>

      <div style={bodyStyle}>
        <div style={statRowStyle}>
          <div>
            <span className="hud-research-stat__label">RP</span>
            <span className="hud-research-stat__value">{formatRp(researchPoints)}</span>
          </div>
          {researchRate !== undefined ? (
            <div>
              <span className="hud-research-stat__label">Rate</span>
              <span className="hud-research-stat__value">
                {formatRp(researchRate)}/mo
              </span>
            </div>
          ) : null}
        </div>

        <div className="hud-research-list__heading">
          Technologies ({technologies.length})
        </div>

        <ul className="hud-research-list">
          {technologies.map((tech) => {
            const techId = techIdFromCatalogId(tech.id);
            const canEnqueue = onEnqueueResearch !== undefined && techId >= 0;

            return (
              <li key={tech.id} className="hud-research-list__item">
                <button
                  type="button"
                  className="hud-research-list__enqueue"
                  disabled={!canEnqueue}
                  onClick={() => {
                    if (canEnqueue) onEnqueueResearch(techId);
                  }}
                  aria-label={`Enqueue research: ${tech.name}`}
                  style={{
                    display: "block",
                    width: "100%",
                    margin: 0,
                    padding: 0,
                    border: "none",
                    background: "transparent",
                    color: "inherit",
                    font: "inherit",
                    textAlign: "left",
                    cursor: canEnqueue ? "pointer" : "default",
                  }}
                >
                  <div className="hud-research-list__row">
                    <span className="hud-research-list__id">{tech.id}</span>
                    <span className="hud-research-list__name">{tech.name}</span>
                    <span className="hud-research-list__cost">{tech.cost_rp} RP</span>
                  </div>
                  <div className="hud-research-list__meta">
                    {tech.era} · {tech.category}
                    {tech.prerequisites.length > 0
                      ? ` · req ${tech.prerequisites.join(", ")}`
                      : ""}
                  </div>
                  <p className="hud-research-list__desc">{tech.description}</p>
                </button>
              </li>
            );
          })}
        </ul>
      </div>
    </aside>
  );
}
