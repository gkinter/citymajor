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
};

export function HeraldButton({
  quotaLabel,
  disabled,
  title,
  onClick,
}: HeraldButtonProps) {
  return (
    <button
      type="button"
      style={{
        ...HUD_ZONE.topRight,
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
