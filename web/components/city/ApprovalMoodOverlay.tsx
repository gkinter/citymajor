"use client";

import { LOW_APPROVAL_WARNING_THRESHOLD } from "@/lib/constants";
import { LOW_HAPPINESS_APPROVAL } from "@/lib/sim-metrics";

type ApprovalMoodOverlayProps = {
  approval: number | undefined;
};

/**
 * Screen vignette when mayor approval is critically low — mirrors crisis thresholds.
 */
export function ApprovalMoodOverlay({ approval }: ApprovalMoodOverlayProps) {
  if (approval === undefined) return null;

  let severity: "critical" | "strained" | null = null;
  if (approval < LOW_APPROVAL_WARNING_THRESHOLD) {
    severity = "critical";
  } else if (approval < LOW_HAPPINESS_APPROVAL) {
    severity = "strained";
  }

  if (!severity) return null;

  return (
    <div
      className={`hud-approval-vignette hud-approval-vignette--${severity}`}
      aria-hidden
    />
  );
}
