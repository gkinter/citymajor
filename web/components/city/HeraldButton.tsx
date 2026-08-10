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
  /** Live EventSystem count from WASM — shown beside quota when &gt; 0. */
  eventCountLabel?: string;
};

export function HeraldButton({
  quotaLabel,
  disabled,
  title,
  onClick,
  embedded = false,
  eventCountLabel,
}: HeraldButtonProps) {
  return (
    <button
      type="button"
      data-onboarding-target="herald"
      data-testid="herald-button"
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
      {eventCountLabel ? (
        <span
          data-testid="herald-active-event-count"
          style={hudBadge()}
        >
          {eventCountLabel}
        </span>
      ) : null}
      <span style={hudBadge()}>{quotaLabel}</span>
    </button>
  );
}
