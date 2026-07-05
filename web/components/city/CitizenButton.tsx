"use client";

import { hudActionButton, hudBadge } from "@/lib/hud-theme";

type CitizenButtonProps = {
  badgeLabel: string;
  title: string;
  onClick: () => void;
  active?: boolean;
};

export function CitizenButton({
  badgeLabel,
  title,
  onClick,
  active,
}: CitizenButtonProps) {
  return (
    <button
      type="button"
      className="hud-citizen-button"
      data-onboarding-target="citizens"
      style={{
        ...hudActionButton(),
        display: "flex",
        alignItems: "center",
        gap: 8,
        border: active
          ? "1px solid rgba(255, 184, 77, 0.7)"
          : "1px solid rgba(120, 160, 220, 0.25)",
        background: active ? "rgba(255, 184, 77, 0.12)" : "rgba(16, 22, 38, 0.6)",
      }}
      title={title}
      onClick={onClick}
    >
      <span>Citizens</span>
      <span style={hudBadge()}>{badgeLabel}</span>
    </button>
  );
}
