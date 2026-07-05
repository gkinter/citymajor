"use client";

import { useMemo } from "react";
import {
  HUD_COLORS,
  HUD_ZONE,
  hudButton,
  hudToolbar,
} from "@/lib/hud-theme";
import {
  getPlayableTransitModesByKind,
  isTransitModeUnlocked,
  type TransitModeId,
  type TransitModeKind,
} from "@/lib/transit-modes";

type TransportToolbarProps = {
  activeTransitMode: TransitModeId | null;
  onSelect: (modeId: TransitModeId) => void;
  unlockedTechIds?: number[];
};

const KIND_LABELS: Record<TransitModeKind, string> = {
  bus: "Bus",
  rail: "Rail",
};

export function TransportToolbar({
  activeTransitMode,
  onSelect,
  unlockedTechIds,
}: TransportToolbarProps) {
  const modesByKind = useMemo(() => getPlayableTransitModesByKind(), []);

  return (
    <div
      data-testid="transport-toolbar"
      style={{
        ...hudToolbar(HUD_ZONE.bottomCenter),
        bottom: 156,
      }}
      role="toolbar"
      aria-label="Transit modes"
    >
      <span
        style={{
          opacity: 0.7,
          alignSelf: "center",
          marginRight: 4,
          color: HUD_COLORS.textMuted,
        }}
      >
        Transit
      </span>
      {(Object.keys(modesByKind) as TransitModeKind[]).map((kind) => {
        const modes = modesByKind[kind];
        if (modes.length === 0) return null;

        return modes.map(({ id, label, shortLabel, overlayColor }) => {
          const unlocked = isTransitModeUnlocked(id, unlockedTechIds);
          const active = activeTransitMode === id;

          return (
            <button
              key={id}
              type="button"
              style={{
                ...hudButton(active, !unlocked),
                minWidth: 72,
                display: "flex",
                alignItems: "center",
                gap: 6,
                opacity: unlocked ? 1 : 0.45,
              }}
              aria-pressed={active}
              disabled={!unlocked}
              title={
                unlocked
                  ? `Draw ${label.toLowerCase()} routes (${KIND_LABELS[kind]})`
                  : `Research ${label.toLowerCase()} to unlock`
              }
              onClick={() => {
                if (unlocked) onSelect(id);
              }}
            >
              <span
                style={{
                  width: 8,
                  height: 8,
                  borderRadius: kind === "rail" ? 0 : 2,
                  background: overlayColor,
                  flexShrink: 0,
                }}
                aria-hidden
              />
              {shortLabel}
              {!unlocked ? " 🔒" : null}
            </button>
          );
        });
      })}
    </div>
  );
}
