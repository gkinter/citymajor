"use client";

import { useEffect } from "react";
import { HUD_COLORS, hudPanel } from "@/lib/hud-theme";
import type { CashCrisisSeverity } from "@/lib/sim-metrics";

type HudToastProps = {
  message: string | null;
  severity?: CashCrisisSeverity;
  /** Auto-dismiss after ms (default 4s). */
  durationMs?: number;
  onDismiss?: () => void;
};

const SEVERITY_COLORS: Record<
  CashCrisisSeverity,
  { text: string; border: string }
> = {
  critical: { text: "#ffb4b4", border: "rgba(136, 68, 68, 0.85)" },
  warn: { text: "#ffe0a0", border: "rgba(136, 119, 68, 0.85)" },
};

export function HudToast({
  message,
  severity,
  durationMs = 4_000,
  onDismiss,
}: HudToastProps) {
  useEffect(() => {
    if (!message || !onDismiss) return;
    const id = window.setTimeout(onDismiss, durationMs);
    return () => window.clearTimeout(id);
  }, [message, durationMs, onDismiss]);

  if (!message) return null;

  const severityStyle = severity ? SEVERITY_COLORS[severity] : null;

  return (
    <div
      role="status"
      aria-live="polite"
      data-testid="hud-toast"
      style={{
        ...hudPanel({
          padding: "8px 12px",
          fontSize: 12,
          color: severityStyle?.text ?? HUD_COLORS.toast,
          border: severityStyle
            ? `1px solid ${severityStyle.border}`
            : undefined,
          pointerEvents: "none",
          maxWidth: 320,
          lineHeight: 1.4,
        }),
      }}
    >
      {message}
    </div>
  );
}
