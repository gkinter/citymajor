"use client";

import { useMemo, useState } from "react";
import type { CSSProperties } from "react";
import {
  BUILD_CATEGORIES,
  BUILD_CATALOG,
  isBuildUnlocked,
  type BuildCategory,
} from "@/lib/build-catalog";
import {
  HUD_COLORS,
  HUD_ZONE,
  hudActionButton,
  hudButton,
  hudPanel,
} from "@/lib/hud-theme";

type BuildToolbarProps = {
  activeBuildTypeId: number | null;
  onSelect: (typeId: number) => void;
  unlockedTechIds: number[];
  onClose: () => void;
};

const headerStyle: CSSProperties = {
  display: "flex",
  alignItems: "center",
  justifyContent: "space-between",
  padding: "8px 10px",
  borderBottom: `1px solid ${HUD_COLORS.borderSubtle}`,
};

const tabRowStyle: CSSProperties = {
  display: "flex",
  gap: 4,
  padding: "6px 8px",
  borderBottom: `1px solid ${HUD_COLORS.borderSubtle}`,
  flexWrap: "wrap",
};

const listStyle: CSSProperties = {
  listStyle: "none",
  margin: 0,
  padding: "6px 8px 8px",
  overflowY: "auto",
  maxHeight: 220,
  display: "flex",
  flexDirection: "column",
  gap: 4,
};

export function BuildToolbar({
  activeBuildTypeId,
  onSelect,
  unlockedTechIds,
  onClose,
}: BuildToolbarProps) {
  const [activeTab, setActiveTab] = useState<BuildCategory>("Civic");
  const unlockedSet = useMemo(
    () => new Set(unlockedTechIds),
    [unlockedTechIds],
  );
  const entries = useMemo(
    () => BUILD_CATALOG[activeTab],
    [activeTab],
  );

  return (
    <aside
      data-testid="build-toolbar"
      style={hudPanel({
        ...HUD_ZONE.bottomRight,
        width: "min(240px, 92vw)",
        display: "flex",
        flexDirection: "column",
        pointerEvents: "auto",
        zIndex: HUD_ZONE.bottomRight.zIndex as number | undefined,
      })}
      role="dialog"
      aria-label="Build catalog"
    >
      <header style={headerStyle}>
        <span style={{ fontWeight: 700, letterSpacing: "0.04em" }}>Build</span>
        <button
          type="button"
          style={{ ...hudActionButton(), padding: "2px 8px", fontSize: 12 }}
          onClick={onClose}
          aria-label="Close build toolbar"
        >
          ✕
        </button>
      </header>

      <div style={tabRowStyle} role="tablist" aria-label="Build categories">
        {BUILD_CATEGORIES.map((category) => (
          <button
            key={category}
            type="button"
            role="tab"
            aria-selected={activeTab === category}
            style={{ ...hudButton(activeTab === category), padding: "4px 8px", fontSize: 11 }}
            onClick={() => setActiveTab(category)}
          >
            {category}
          </button>
        ))}
      </div>

      <ul style={listStyle} role="listbox" aria-label={`${activeTab} buildings`}>
        {entries.map((entry) => {
          const unlocked = isBuildUnlocked(entry.contentKey, unlockedSet);
          const selected = activeBuildTypeId === entry.typeId;

          return (
            <li key={entry.contentKey}>
              <button
                type="button"
                role="option"
                aria-selected={selected}
                disabled={!unlocked}
                title={
                  unlocked
                    ? entry.label
                    : `${entry.label} — research required`
                }
                style={{
                  ...hudButton(selected && unlocked, !unlocked),
                  width: "100%",
                  textAlign: "left",
                  padding: "6px 10px",
                  display: "flex",
                  justifyContent: "space-between",
                  gap: 8,
                }}
                onClick={() => {
                  if (unlocked) onSelect(entry.typeId);
                }}
              >
                <span>{entry.label}</span>
                {!unlocked ? (
                  <span style={{ opacity: 0.55, fontSize: 10 }}>🔒</span>
                ) : null}
              </button>
            </li>
          );
        })}
      </ul>
    </aside>
  );
}
