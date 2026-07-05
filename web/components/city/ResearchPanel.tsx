"use client";

import { useMemo } from "react";
import type { CSSProperties } from "react";
import {
  HUD_COLORS,
  hudActionButton,
  hudProgressFill,
  hudProgressTrack,
  hudSlidePanel,
} from "@/lib/hud-theme";
import { hudEraBadgeStyle } from "@/lib/era";
import {
  TECH_CATALOG_TOTAL,
  TECH_V1_CATALOG,
  classifyTechAvailability,
  formatUnlockLabel,
  techIdFromCatalogId,
  techNameFromIndex,
  type TechAvailability,
  type TechPreview,
} from "@/lib/tech-catalog";

const ERA_TAG_INDEX: Record<string, number> = {
  frontier: 0,
  industrial: 1,
  postwar: 2,
  modern: 3,
  future: 4,
};

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
  flexWrap: "wrap",
};

const activeResearchStyle: CSSProperties = {
  marginBottom: 16,
  padding: "12px 14px",
  borderRadius: 8,
  border: `1px solid ${HUD_COLORS.accentBorder}`,
  background: HUD_COLORS.accentSoft,
};

function formatRp(value: number | undefined): string {
  if (value === undefined) return "—";
  return value.toFixed(1);
}

function formatMonthsRemaining(value: number | undefined): string | null {
  if (value === undefined || value < 0) return null;
  if (value === 0) return "completing…";
  if (value < 1) return "<1 mo";
  return `~${value.toFixed(1)} mo`;
}

function availabilityClass(availability: TechAvailability): string {
  return `hud-research-list__item--${availability}`;
}

function eraTagStyle(era: string): CSSProperties {
  const palette = hudEraBadgeStyle(ERA_TAG_INDEX[era] ?? 0);
  return {
    color: palette.color,
    background: palette.background,
    border: palette.border,
  };
}

type ResearchPanelProps = {
  open: boolean;
  onClose: () => void;
  techCount?: number;
  researchPoints?: number;
  researchRate?: number;
  currentResearchId?: number;
  currentResearchProgress?: number;
  currentResearchMonthsRemaining?: number;
  unlockedTechIds?: number[];
  technologies?: TechPreview[];
  onEnqueueResearch?: (techId: number) => void;
};

export function ResearchPanel({
  open,
  onClose,
  techCount,
  researchPoints,
  researchRate,
  currentResearchId,
  currentResearchProgress,
  currentResearchMonthsRemaining,
  unlockedTechIds,
  technologies = TECH_V1_CATALOG,
  onEnqueueResearch,
}: ResearchPanelProps) {
  const unlockedSet = useMemo(
    () => new Set(unlockedTechIds ?? []),
    [unlockedTechIds],
  );

  if (!open) return null;

  const unlocked = techCount ?? unlockedSet.size;
  const catalogTotal = TECH_CATALOG_TOTAL;
  const activeTechName =
    currentResearchId !== undefined && currentResearchId >= 0
      ? techNameFromIndex(currentResearchId)
      : undefined;
  const progressPct =
    currentResearchProgress !== undefined
      ? Math.round(Math.min(1, Math.max(0, currentResearchProgress)) * 100)
      : 0;
  const etaLabel = formatMonthsRemaining(currentResearchMonthsRemaining);

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

        {activeTechName ? (
          <section style={activeResearchStyle} aria-label="Active research">
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                gap: 8,
                fontSize: 12,
                marginBottom: 8,
              }}
            >
              <span style={{ fontWeight: 600 }}>{activeTechName}</span>
              <span style={{ opacity: 0.75 }}>{progressPct}%</span>
            </div>
            <div
              style={hudProgressTrack()}
              role="progressbar"
              aria-valuenow={progressPct}
              aria-valuemin={0}
              aria-valuemax={100}
            >
              <div style={hudProgressFill(progressPct)} />
            </div>
            {etaLabel ? (
              <div className="hud-research-progress__eta">{etaLabel} remaining</div>
            ) : null}
          </section>
        ) : null}

        <div className="hud-research-list__heading">
          Technologies ({technologies.length})
        </div>

        <ul className="hud-research-list">
          {technologies.map((tech) => {
            const techId = techIdFromCatalogId(tech.id);
            const availability = classifyTechAvailability(
              tech,
              unlockedSet,
              currentResearchId,
            );
            const canEnqueue =
              onEnqueueResearch !== undefined &&
              techId >= 0 &&
              availability === "available";

            return (
              <li
                key={tech.id}
                className={`hud-research-list__item ${availabilityClass(availability)}`}
              >
                <button
                  type="button"
                  className="hud-research-list__enqueue"
                  disabled={!canEnqueue}
                  onClick={() => {
                    if (canEnqueue) onEnqueueResearch(techId);
                  }}
                  aria-label={
                    availability === "unlocked"
                      ? `${tech.name} — unlocked`
                      : availability === "researching"
                        ? `${tech.name} — researching`
                        : canEnqueue
                          ? `Enqueue research: ${tech.name}`
                          : `${tech.name} — locked`
                  }
                >
                  <div className="hud-research-list__row">
                    <span className="hud-research-list__id">{tech.id}</span>
                    <span className="hud-research-list__name">{tech.name}</span>
                    {availability === "unlocked" ? (
                      <span className="hud-research-list__badge hud-research-list__badge--done">
                        Unlocked
                      </span>
                    ) : availability === "researching" ? (
                      <span className="hud-research-list__badge hud-research-list__badge--active">
                        Active
                      </span>
                    ) : (
                      <span className="hud-research-list__cost">{tech.cost_rp} RP</span>
                    )}
                  </div>
                  <div className="hud-research-list__meta">
                    <span
                      className="hud-research-list__tag hud-research-list__tag--era"
                      style={eraTagStyle(tech.era)}
                    >
                      {tech.era}
                    </span>
                    <span className="hud-research-list__tag hud-research-list__tag--category">
                      {tech.category}
                    </span>
                    {tech.prerequisites.length > 0 ? (
                      <span className="hud-research-list__prereq">
                        Requires {tech.prerequisites.join(", ")}
                      </span>
                    ) : null}
                  </div>
                  {tech.unlocks && tech.unlocks.length > 0 ? (
                    <div className="hud-research-list__prereq" style={{ marginTop: 4 }}>
                      Unlocks {tech.unlocks.map(formatUnlockLabel).join(", ")}
                    </div>
                  ) : null}
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
