"use client";

import {
  HUD_ZONE,
  hudActionButton,
  hudBadge,
} from "@/lib/hud-theme";

type HeraldButtonProps = {
  quotaLabel: string;
  disabled: boolean;
  title: string;
  onClick: () => void;
  /** When true, skip absolute top-right positioning (use inside a cluster). */
  embedded?: boolean;
};

export function HeraldButton({
  quotaLabel,
  disabled,
  title,
  onClick,
  embedded = false,
}: HeraldButtonProps) {
  return (
    <button
      type="button"
      data-onboarding-target="herald"
      style={{
        ...(embedded ? {} : HUD_ZONE.topRight),
        ...hudActionButton(disabled),
        display: "flex",
        alignItems: "center",
        gap: 8,
      }}
      disabled={disabled}
      title={title}
      onClick={onClick}
    >
      <span>Herald</span>
      <span style={hudBadge()}>{quotaLabel}</span>
    </button>
  );
}
