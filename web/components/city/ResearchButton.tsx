"use client";

import {
  hudActionButton,
  hudBadge,
} from "@/lib/hud-theme";

type ResearchButtonProps = {
  badgeLabel: string;
  title: string;
  onClick: () => void;
};

export function ResearchButton({
  badgeLabel,
  title,
  onClick,
}: ResearchButtonProps) {
  return (
    <button
      type="button"
      className="hud-research-button"
      data-onboarding-target="research"
      style={{
        ...hudActionButton(),
        display: "flex",
        alignItems: "center",
        gap: 8,
      }}
      title={title}
      onClick={onClick}
    >
      <span>Research</span>
      <span style={hudBadge()}>{badgeLabel}</span>
    </button>
  );
}
