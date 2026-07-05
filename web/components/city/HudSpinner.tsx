"use client";

import { hudPanelStatusBox } from "@/lib/hud-theme";

export type HudSpinnerLineVariant = "short" | "medium" | "headline" | "full";

const DEFAULT_LINES: HudSpinnerLineVariant[] = [
  "short",
  "headline",
  "medium",
  "full",
];

type HudSpinnerProps = {
  /** Screen-reader label for the loading region. */
  ariaLabel: string;
  eyebrow?: string;
  message?: string;
  /** Shimmer skeleton line widths — defaults to a headline-style block. */
  lines?: HudSpinnerLineVariant[];
  className?: string;
};

function lineClassName(variant: HudSpinnerLineVariant): string {
  const base = "hud-spinner__line";
  if (variant === "full") return base;
  return `${base} hud-spinner__line--${variant}`;
}

/** Shared HUD loading shimmer — skeleton lines with cyan glass pulse. */
export function HudSpinner({
  ariaLabel,
  eyebrow,
  message,
  lines = DEFAULT_LINES,
  className,
}: HudSpinnerProps) {
  const rootClass = ["hud-spinner-status", "hud-spinner-status--loading", className]
    .filter(Boolean)
    .join(" ");

  return (
    <div
      className={rootClass}
      style={hudPanelStatusBox("loading")}
      aria-live="polite"
      aria-label={ariaLabel}
      aria-busy="true"
    >
      {eyebrow ? <p className="hud-spinner-status__eyebrow">{eyebrow}</p> : null}
      {message ? <p className="hud-spinner-status__message">{message}</p> : null}
      <div className="hud-spinner" aria-hidden>
        {lines.map((variant, index) => (
          <div key={`${variant}-${index}`} className={lineClassName(variant)} />
        ))}
      </div>
    </div>
  );
}
