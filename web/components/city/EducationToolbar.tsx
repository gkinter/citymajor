"use client";

import {
  HUD_COLORS,
  HUD_ZONE,
  hudButton,
  hudToolbar,
} from "@/lib/hud-theme";
import {
  EDUCATION_BUILDINGS,
  type EducationBuildTypeId,
} from "@/lib/education-buildings";
import type { ServiceViewMode } from "@/lib/sim-bridge";

type EducationToolbarProps = {
  /** When `"education"`, the coverage overlay is active. */
  viewMode: ServiceViewMode;
  onViewModeChange: (mode: ServiceViewMode) => void;
  educationCoverage?: number;
  /** Active build type for tile placement; null = overlay-only mode. */
  selectedTypeId?: EducationBuildTypeId | null;
  onSelectTypeId?: (typeId: EducationBuildTypeId | null) => void;
};

function formatPct(value: number | undefined): string {
  if (value === undefined) return "—";
  return `${Math.round(value * 100)}%`;
}

export function EducationToolbar({
  viewMode,
  onViewModeChange,
  educationCoverage,
  selectedTypeId = null,
  onSelectTypeId,
}: EducationToolbarProps) {
  const overlayOn = viewMode === "education";

  return (
    <div
      data-testid="education-toolbar"
      style={{
        ...hudToolbar(HUD_ZONE.bottomLeft),
        bottom: 52,
      }}
      role="toolbar"
      aria-label="Education coverage and build tools"
    >
      <span
        style={{
          opacity: 0.7,
          alignSelf: "center",
          marginRight: 4,
          color: HUD_COLORS.textMuted,
        }}
      >
        Education
      </span>
      <button
        type="button"
        style={{ ...hudButton(overlayOn), minWidth: 44 }}
        aria-pressed={overlayOn}
        title="Show education coverage overlay"
        onClick={() => onViewModeChange(overlayOn ? "off" : "education")}
      >
        On
      </button>
      <button
        type="button"
        style={{ ...hudButton(!overlayOn), minWidth: 44 }}
        aria-pressed={!overlayOn}
        title="Hide education coverage overlay"
        onClick={() => onViewModeChange("off")}
      >
        Off
      </button>
      <span
        style={{
          marginLeft: 2,
          marginRight: 2,
          width: 1,
          height: 18,
          background: HUD_COLORS.borderSubtle,
          alignSelf: "center",
        }}
        aria-hidden
      />
      {EDUCATION_BUILDINGS.map(({ typeId, label }) => {
        const active = selectedTypeId === typeId;
        return (
          <button
            key={typeId}
            type="button"
            style={{ ...hudButton(active), minWidth: 72 }}
            aria-pressed={active}
            title={`Place ${label} (TypeId ${typeId})`}
            onClick={() => {
              if (!onSelectTypeId) return;
              onSelectTypeId(active ? null : typeId);
            }}
            disabled={!onSelectTypeId}
          >
            {label}
          </button>
        );
      })}
      <span
        style={{
          marginLeft: 6,
          fontSize: 11,
          color: HUD_COLORS.textDim,
          fontFamily: "ui-monospace, monospace",
          alignSelf: "center",
        }}
        title="City-wide mean education coverage over zoned tiles"
      >
        E {formatPct(educationCoverage)}
      </span>
    </div>
  );
}
