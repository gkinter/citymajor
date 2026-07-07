"use client";

import {
  hudActionButton,
  hudBadge,
} from "@/lib/hud-theme";

type LawButtonProps = {
  title: string;
  onClick: () => void;
  active?: boolean;
  badgeLabel?: string;
};

export function LawButton({ title, onClick, active, badgeLabel }: LawButtonProps) {
  return (
    <button
      type="button"
      className="hud-law-button"
      style={{
        ...hudActionButton(),
        display: "flex",
        alignItems: "center",
        gap: 8,
        border: active
          ? "1px solid rgba(94, 200, 255, 0.7)"
          : "1px solid rgba(120, 160, 220, 0.25)",
        background: active ? "rgba(94, 200, 255, 0.15)" : "rgba(16, 22, 38, 0.6)",
      }}
      title={title}
      onClick={onClick}
    >
      <span>Laws</span>
      {badgeLabel ? <span style={hudBadge()}>{badgeLabel}</span> : null}
    </button>
  );
}
